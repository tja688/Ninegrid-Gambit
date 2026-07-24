#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 卡面适配 sidecar catalog：AI 可直接读 JSON，不改源 PNG。
/// </summary>
public static class SpriteClipCardFitCatalog
{
    public const string Version = "1";
    public const string DefaultRelativeOutputDir = "_card_fit";
    public const string CatalogFileName = "catalog.json";
    public const int DefaultSlotW = 45;
    public const int DefaultSlotH = 66;
    public const byte DefaultAlphaThreshold = 8;
    public const float DefaultSlackPx = 4f;

    [Serializable]
    public sealed class RectDto
    {
        public float minX;
        public float minY;
        public float maxX;
        public float maxY;
        public float width;
        public float height;
        public float centerX;
        public float centerY;

        public static RectDto From(SpriteOpaqueBoundsAnalyzer.IntRect r)
        {
            if (r.IsEmpty)
            {
                return new RectDto();
            }

            return new RectDto
            {
                minX = r.MinX,
                minY = r.MinY,
                maxX = r.MaxX,
                maxY = r.MaxY,
                width = r.Width,
                height = r.Height,
                centerX = r.CenterX,
                centerY = r.CenterY,
            };
        }
    }

    [Serializable]
    public sealed class OverflowDto
    {
        public float l;
        public float r;
        public float t;
        public float b;
    }

    [Serializable]
    public sealed class OffsetDto
    {
        public float x;
        public float y;
    }

    [Serializable]
    public sealed class FrameDto
    {
        public string path;
        public int canvasW;
        public int canvasH;
        public int opaquePixels;
        public RectDto opaqueInTexture;
        public RectDto opaqueInClipSpace;
    }

    [Serializable]
    public sealed class ClipEntry
    {
        public string folder;
        public string folderPath;
        public int frameCount;
        public string[] paths;
        public int canvasMaxW;
        public int canvasMaxH;
        public RectDto opaqueUnion;
        public FrameDto[] opaquePerFrame;
        public int slotW;
        public int slotH;
        public OffsetDto offsetPx;
        public OverflowDto overflowPx;
        public float maxOverflow;
        public float fillRatio;
        public bool usable;
        public float score;
        public string[] reasons;
        public float suggestedUniformScale = 1f;
        public string sourceHash;
        public string analyzedAt;
        public string error;
    }

    [Serializable]
    public sealed class CatalogRoot
    {
        public string version = Version;
        public int slotW = DefaultSlotW;
        public int slotH = DefaultSlotH;
        public int alphaThreshold = DefaultAlphaThreshold;
        public float slackPx = DefaultSlackPx;
        public string spritesRoot;
        public string analyzedAt;
        public int clipCount;
        public int usableCount;
        public ClipEntry[] clips = Array.Empty<ClipEntry>();
    }

    public sealed class ScanSettings
    {
        public string SpritesRootAssetPath;
        public int SlotW = DefaultSlotW;
        public int SlotH = DefaultSlotH;
        public byte AlphaThreshold = DefaultAlphaThreshold;
        public float SlackPx = DefaultSlackPx;
        public bool WritePerClipFiles;
        public bool Incremental = true;
    }

    public sealed class ScanProgress
    {
        public int Total;
        public int Done;
        public int Skipped;
        public int Failed;
        public string Current;
    }

    public static string FormatSampleReport()
    {
        string[] names =
        {
            "03slime_idle_01",
            "07bearBoss_idle_01",
            "002bat_idle_01",
            "LeadRole_Sword_run",
            "redfire",
            "22cyclops_idle_01",
        };

        var sb = new StringBuilder();
        CatalogRoot catalog = Load("Assets/Arts/Images/Png/像素怪物合集/sprites");
        if (catalog == null)
            return "catalog_missing";

        sb.Append("clips=").Append(catalog.clipCount)
            .Append(" usable=").Append(catalog.usableCount)
            .Append(" slot=").Append(catalog.slotW).Append('x').Append(catalog.slotH)
            .Append(" slack=").Append(catalog.slackPx.ToString(CultureInfo.InvariantCulture))
            .Append(" || ");

        foreach (string name in names)
        {
            sb.Append(FormatClipSummary(name)).Append(" || ");
        }

        return sb.ToString();
    }

    public static string FormatClipSummary(string folderName)
    {
        CatalogRoot catalog = Load("Assets/Arts/Images/Png/像素怪物合集/sprites");
        ClipEntry e = FindClip(catalog, folderName);
        if (e == null)
            return folderName + ":missing";

        return folderName
               + ": usable=" + e.usable
               + " ov=" + e.maxOverflow.ToString("0.##", CultureInfo.InvariantCulture)
               + " fill=" + e.fillRatio.ToString("0.##", CultureInfo.InvariantCulture)
               + " union=" + (e.opaqueUnion?.width ?? 0f).ToString("0.#", CultureInfo.InvariantCulture)
               + "x" + (e.opaqueUnion?.height ?? 0f).ToString("0.#", CultureInfo.InvariantCulture)
               + " off=(" + (e.offsetPx?.x ?? 0f).ToString("0.#", CultureInfo.InvariantCulture)
               + "," + (e.offsetPx?.y ?? 0f).ToString("0.#", CultureInfo.InvariantCulture) + ")";
    }

    public static CatalogRoot RunDefaultScan(bool incremental, bool writePerClipFiles = false)
    {
        const string root = "Assets/Arts/Images/Png/像素怪物合集/sprites";
        var settings = new ScanSettings
        {
            SpritesRootAssetPath = root,
            SlotW = DefaultSlotW,
            SlotH = DefaultSlotH,
            AlphaThreshold = DefaultAlphaThreshold,
            SlackPx = DefaultSlackPx,
            WritePerClipFiles = writePerClipFiles,
            Incremental = incremental,
        };

        CatalogRoot previous = incremental ? Load(root) : null;
        CatalogRoot catalog = Scan(settings, previous, null);
        Save(catalog, root, writePerClipFiles);
        return catalog;
    }

    public static string GetOutputDirAssetPath(string spritesRootAssetPath)
    {
        string root = (spritesRootAssetPath ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        return root + "/" + DefaultRelativeOutputDir;
    }

    public static string GetCatalogAssetPath(string spritesRootAssetPath)
    {
        return GetOutputDirAssetPath(spritesRootAssetPath) + "/" + CatalogFileName;
    }

    public static CatalogRoot Load(string spritesRootAssetPath)
    {
        string catalogPath = GetCatalogAssetPath(spritesRootAssetPath);
        string abs = AbsoluteFromAssetPath(catalogPath);
        if (string.IsNullOrEmpty(abs) || !File.Exists(abs))
            return null;

        try
        {
            string json = File.ReadAllText(abs, Encoding.UTF8);
            return JsonUtility.FromJson<CatalogRoot>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SpriteClipCardFitCatalog] Failed to load: " + ex.Message);
            return null;
        }
    }

    public static void Save(CatalogRoot root, string spritesRootAssetPath, bool writePerClipFiles)
    {
        if (root == null)
            return;

        string outDirAsset = GetOutputDirAssetPath(spritesRootAssetPath);
        EnsureAssetFolder(outDirAsset);

        string catalogAsset = GetCatalogAssetPath(spritesRootAssetPath);
        string absCatalog = AbsoluteFromAssetPath(catalogAsset);
        string json = JsonUtility.ToJson(root, prettyPrint: true);
        File.WriteAllText(absCatalog, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (writePerClipFiles && root.clips != null)
        {
            foreach (ClipEntry clip in root.clips)
            {
                if (clip == null || string.IsNullOrEmpty(clip.folder))
                    continue;
                string safe = SanitizeFileName(clip.folder);
                string perAsset = outDirAsset + "/" + safe + ".json";
                string perAbs = AbsoluteFromAssetPath(perAsset);
                File.WriteAllText(perAbs, JsonUtility.ToJson(clip, prettyPrint: true), new UTF8Encoding(false));
            }
        }

        AssetDatabase.ImportAsset(outDirAsset);
        AssetDatabase.Refresh();
    }

    public static CatalogRoot Scan(
        ScanSettings settings,
        CatalogRoot previous,
        Action<ScanProgress> onProgress = null)
    {
        settings ??= new ScanSettings();
        string root = (settings.SpritesRootAssetPath ?? string.Empty).Replace('\\', '/').TrimEnd('/');
        string absRoot = AbsoluteFromAssetPath(root);
        if (string.IsNullOrEmpty(absRoot) || !Directory.Exists(absRoot))
            throw new DirectoryNotFoundException("Sprites root not found: " + root);

        var previousByFolder = new Dictionary<string, ClipEntry>(StringComparer.OrdinalIgnoreCase);
        if (previous?.clips != null)
        {
            foreach (ClipEntry c in previous.clips)
            {
                if (c != null && !string.IsNullOrEmpty(c.folder))
                    previousByFolder[c.folder] = c;
            }
        }

        string[] subDirs = Directory.GetDirectories(absRoot);
        Array.Sort(subDirs, StringComparer.OrdinalIgnoreCase);

        var clips = new List<ClipEntry>(subDirs.Length);
        var progress = new ScanProgress { Total = subDirs.Length };
        string stamp = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        for (int i = 0; i < subDirs.Length; i++)
        {
            string absFolder = subDirs[i];
            string folderName = Path.GetFileName(absFolder);
            progress.Done = i;
            progress.Current = folderName;
            onProgress?.Invoke(progress);

            // Skip our own output directory.
            if (string.Equals(folderName, DefaultRelativeOutputDir, StringComparison.OrdinalIgnoreCase))
            {
                progress.Skipped++;
                continue;
            }

            List<string> frameAbs = SpriteOpaqueBoundsAnalyzer.ListFramePathsSorted(absFolder);
            if (frameAbs.Count == 0)
            {
                progress.Skipped++;
                continue;
            }

            string hash = SpriteOpaqueBoundsAnalyzer.ComputeSourceHash(frameAbs);
            bool settingsUnchanged = previous != null
                && previous.slotW == settings.SlotW
                && previous.slotH == settings.SlotH
                && previous.alphaThreshold == settings.AlphaThreshold
                && Math.Abs(previous.slackPx - settings.SlackPx) < 0.001f;

            if (settings.Incremental &&
                settingsUnchanged &&
                previousByFolder.TryGetValue(folderName, out ClipEntry prev) &&
                prev != null &&
                string.Equals(prev.sourceHash, hash, StringComparison.Ordinal))
            {
                clips.Add(prev);
                progress.Skipped++;
                continue;
            }

            string folderAsset = root + "/" + folderName;
            var analysis = SpriteOpaqueBoundsAnalyzer.AnalyzeClipFolder(
                folderName,
                folderAsset,
                absFolder,
                settings.AlphaThreshold);

            ClipEntry entry = BuildEntry(analysis, settings, stamp, frameAbs, root);
            if (!string.IsNullOrEmpty(entry.error))
                progress.Failed++;

            clips.Add(entry);
        }

        progress.Done = subDirs.Length;
        progress.Current = "done";
        onProgress?.Invoke(progress);

        int usable = 0;
        foreach (ClipEntry c in clips)
        {
            if (c != null && c.usable)
                usable++;
        }

        return new CatalogRoot
        {
            version = Version,
            slotW = settings.SlotW,
            slotH = settings.SlotH,
            alphaThreshold = settings.AlphaThreshold,
            slackPx = settings.SlackPx,
            spritesRoot = root,
            analyzedAt = stamp,
            clipCount = clips.Count,
            usableCount = usable,
            clips = clips.ToArray(),
        };
    }

    public static ClipEntry FindClip(CatalogRoot catalog, string folderName)
    {
        if (catalog?.clips == null || string.IsNullOrEmpty(folderName))
            return null;

        foreach (ClipEntry c in catalog.clips)
        {
            if (c != null && string.Equals(c.folder, folderName, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    private static ClipEntry BuildEntry(
        SpriteOpaqueBoundsAnalyzer.ClipOpaqueAnalysis analysis,
        ScanSettings settings,
        string stamp,
        List<string> frameAbs,
        string spritesRoot)
    {
        var reasons = new List<string>();
        SpriteOpaqueBoundsAnalyzer.ComputeCardFit(
            analysis.OpaqueUnion,
            settings.SlotW,
            settings.SlotH,
            settings.SlackPx,
            out Vector2 offset,
            out Vector4 overflow,
            out float maxOverflow,
            out float fillRatio,
            out bool usable,
            out float suggestedScale,
            out float score,
            reasons);

        if (!string.IsNullOrEmpty(analysis.Error))
            reasons.Add(analysis.Error);

        var pathAssets = new string[frameAbs.Count];
        for (int i = 0; i < frameAbs.Count; i++)
            pathAssets[i] = AbsoluteToAssetPath(frameAbs[i]) ?? frameAbs[i].Replace('\\', '/');

        var frames = new FrameDto[analysis.Frames.Count];
        for (int i = 0; i < analysis.Frames.Count; i++)
        {
            var f = analysis.Frames[i];
            frames[i] = new FrameDto
            {
                path = AbsoluteToAssetPath(f.Path) ?? f.Path.Replace('\\', '/'),
                canvasW = f.CanvasW,
                canvasH = f.CanvasH,
                opaquePixels = f.OpaquePixelCount,
                opaqueInTexture = RectDto.From(f.OpaqueInTexture),
                opaqueInClipSpace = RectDto.From(f.OpaqueInClipSpace),
            };
        }

        return new ClipEntry
        {
            folder = analysis.FolderName,
            folderPath = analysis.FolderPath,
            frameCount = analysis.Frames.Count,
            paths = pathAssets,
            canvasMaxW = analysis.CanvasMaxW,
            canvasMaxH = analysis.CanvasMaxH,
            opaqueUnion = RectDto.From(analysis.OpaqueUnion),
            opaquePerFrame = frames,
            slotW = settings.SlotW,
            slotH = settings.SlotH,
            offsetPx = new OffsetDto { x = offset.x, y = offset.y },
            overflowPx = new OverflowDto
            {
                l = overflow.x,
                r = overflow.y,
                t = overflow.z,
                b = overflow.w,
            },
            maxOverflow = maxOverflow,
            fillRatio = fillRatio,
            usable = usable && string.IsNullOrEmpty(analysis.Error),
            score = score,
            reasons = reasons.ToArray(),
            suggestedUniformScale = suggestedScale,
            sourceHash = analysis.SourceHash,
            analyzedAt = stamp,
            error = analysis.Error,
        };
    }

    public static string AbsoluteFromAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;
        string normalized = assetPath.Replace('\\', '/');
        if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase))
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(project ?? string.Empty, normalized));
        }

        return Path.GetFullPath(assetPath);
    }

    public static string AbsoluteToAssetPath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return null;

        string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
        string dataPath = Application.dataPath.Replace('\\', '/');
        if (!full.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            return null;

        return "Assets" + full.Substring(dataPath.Length);
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        string normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized))
            return;

        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string SanitizeFileName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "clip";
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
#endif
