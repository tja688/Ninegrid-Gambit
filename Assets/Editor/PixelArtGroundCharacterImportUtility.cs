#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 站立角色（人/怪）帧动画智能导入：识别图集 vs 文件夹序列，统一脚底（Bottom Center）Pivot。
/// </summary>
public static class PixelArtGroundCharacterImportUtility
{
    public const string GroundPivotMarkSuffix = "|groundPivot=bottom";

    private const int AlignmentBottomCenter = (int)SpriteAlignment.BottomCenter;

    public enum GroundAssetKind
    {
        /// <summary>单张图集 / 横条 Sheet（Multiple + 子 Sprite 脚底锚点）。</summary>
        SpriteSheet = 0,

        /// <summary>文件夹内多枚单帧 PNG（每张 Single + 脚底锚点）。</summary>
        FrameSequence = 1,
    }

    public readonly struct SmartImportJob
    {
        public readonly string AssetPath;
        public readonly GroundAssetKind Kind;
        public readonly string SourceFolder;

        public SmartImportJob(string assetPath, GroundAssetKind kind, string sourceFolder)
        {
            AssetPath = assetPath;
            Kind = kind;
            SourceFolder = sourceFolder;
        }
    }

    private static readonly Regex FrameIndexRegex = new Regex(
        @"_(\d+)\.png$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CellSizeInPathRegex = new Regex(
        @"\((\d+)x(\d+)\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// 递归扫描根目录下每个含 PNG 的文件夹，按「图集 / 序列帧」分类。
    /// </summary>
    public static List<SmartImportJob> CollectJobs(string rootAssetFolder, string projectRoot)
    {
        var jobs = new List<SmartImportJob>();
        if (string.IsNullOrWhiteSpace(rootAssetFolder))
        {
            return jobs;
        }

        string normalizedRoot = rootAssetFolder.Replace('\\', '/').TrimEnd('/');
        string absRoot = Path.GetFullPath(Path.Combine(projectRoot, normalizedRoot));
        if (!Directory.Exists(absRoot))
        {
            return jobs;
        }

        WalkDirectory(absRoot, normalizedRoot, projectRoot, jobs);
        jobs.Sort((a, b) => string.Compare(a.AssetPath, b.AssetPath, StringComparison.OrdinalIgnoreCase));
        return jobs;
    }

    private static void WalkDirectory(
        string absDir,
        string assetDir,
        string projectRoot,
        List<SmartImportJob> jobs)
    {
        string[] pngFiles;
        try
        {
            pngFiles = Directory.GetFiles(absDir, "*.png", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            pngFiles = Array.Empty<string>();
        }

        if (pngFiles.Length > 0)
        {
            AppendJobsForFolder(pngFiles, assetDir, projectRoot, jobs);
        }

        string[] subDirs;
        try
        {
            subDirs = Directory.GetDirectories(absDir, "*", SearchOption.TopDirectoryOnly);
        }
        catch
        {
            subDirs = Array.Empty<string>();
        }

        for (var i = 0; i < subDirs.Length; i++)
        {
            string subAbs = subDirs[i];
            string subName = Path.GetFileName(subAbs);
            if (string.IsNullOrEmpty(subName) || subName.StartsWith(".", StringComparison.Ordinal))
            {
                continue;
            }

            string subAsset = assetDir + "/" + subName;
            WalkDirectory(subAbs, subAsset, projectRoot, jobs);
        }
    }

    private static void AppendJobsForFolder(
        string[] absPngFiles,
        string assetFolder,
        string projectRoot,
        List<SmartImportJob> jobs)
    {
        var entries = new List<(string abs, string asset, string fileName)>(absPngFiles.Length);
        for (var i = 0; i < absPngFiles.Length; i++)
        {
            string abs = absPngFiles[i];
            string asset = AbsoluteFileToAssetPath(abs, projectRoot);
            if (string.IsNullOrEmpty(asset))
            {
                continue;
            }

            entries.Add((abs, asset, Path.GetFileName(abs)));
        }

        if (entries.Count == 0)
        {
            return;
        }

        int sheetLike = entries.Count(e => IsSheetLikeFileName(e.fileName));
        GroundAssetKind folderKind;

        if (entries.Count == 1 || sheetLike == entries.Count)
        {
            folderKind = GroundAssetKind.SpriteSheet;
        }
        else if (sheetLike > 0)
        {
            // 混合目录：Sheet 走图集，其余走单帧。
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var kind = IsSheetLikeFileName(e.fileName)
                    ? GroundAssetKind.SpriteSheet
                    : GroundAssetKind.FrameSequence;
                jobs.Add(new SmartImportJob(e.asset, kind, assetFolder));
            }

            return;
        }
        else if (entries.Count >= 2 && LooksLikeNumberedFrameSet(entries.Select(e => e.fileName)))
        {
            folderKind = GroundAssetKind.FrameSequence;
        }
        else if (entries.Count >= 2)
        {
            // 多 PNG 但命名不像 Sheet → 视为序列帧文件夹。
            folderKind = GroundAssetKind.FrameSequence;
        }
        else
        {
            folderKind = GroundAssetKind.SpriteSheet;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            jobs.Add(new SmartImportJob(entries[i].asset, folderKind, assetFolder));
        }
    }

    public static bool IsSheetLikeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        if (fileName.IndexOf("sheet", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (fileName.IndexOf("-strip", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    private static bool LooksLikeNumberedFrameSet(IEnumerable<string> fileNames)
    {
        int matched = 0;
        int total = 0;
        foreach (string name in fileNames)
        {
            total++;
            if (FrameIndexRegex.IsMatch(name))
            {
                matched++;
            }
        }

        return total >= 2 && matched >= Mathf.Max(2, total / 2);
    }

    public struct ImportSettingsSnapshot
    {
        public float PixelsPerUnit;
        public int MaxTextureSize;
        public FilterMode FilterMode;
        public TextureImporterCompression TextureCompression;
        public TextureImporterFormat TextureFormat;
        public bool MipmapEnabled;
        public bool AlphaIsTransparency;
        public TextureWrapMode WrapMode;
    }

    public static bool TryApplyImport(
        string assetPath,
        GroundAssetKind kind,
        ImportSettingsSnapshot settings,
        string processMark,
        bool forceReprocess,
        out bool changed,
        out string logLine)
    {
        changed = false;
        logLine = null;

        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            logLine = "[错误] 非 TextureImporter：" + assetPath;
            return false;
        }

        string fullMark = processMark + GroundPivotMarkSuffix;
        bool markOk = !forceReprocess && importer.userData == fullMark;

        switch (kind)
        {
            case GroundAssetKind.SpriteSheet:
                return TryApplySpriteSheet(importer, assetPath, settings, fullMark, forceReprocess, markOk, out changed, out logLine);
            case GroundAssetKind.FrameSequence:
                return TryApplyFrameSequence(importer, settings, fullMark, forceReprocess, markOk, out changed, out logLine);
            default:
                logLine = "[错误] 未知 GroundAssetKind：" + kind;
                return false;
        }
    }

    private static bool TryApplySpriteSheet(
        TextureImporter importer,
        string assetPath,
        ImportSettingsSnapshot settings,
        string fullMark,
        bool forceReprocess,
        bool markOk,
        out bool changed,
        out string logLine)
    {
        changed = false;
        logLine = null;

        ApplyCommonTextureSettings(importer, settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;

        bool pivotOk = HasBottomCenterSprites(importer);
        bool needsBase = forceReprocess
                         || !markOk
                         || !pivotOk
                         || NeedsCommonSettingsChange(importer, settings, SpriteImportMode.Multiple);

        if (!needsBase && markOk && pivotOk)
        {
            return true;
        }

        if (!EnsureSpriteSheetSlices(importer, assetPath))
        {
            logLine = "[告警] 图集未能切片（需手动 Sprite Editor 或检查尺寸）：" + assetPath;
        }

        bool pivotChanged = ApplyBottomCenterToAllSprites(importer);
        importer.userData = fullMark;
        changed = needsBase || pivotChanged;
        logLine = $"[处理·图集] {assetPath}";
        return true;
    }

    private static bool TryApplyFrameSequence(
        TextureImporter importer,
        ImportSettingsSnapshot settings,
        string fullMark,
        bool forceReprocess,
        bool markOk,
        out bool changed,
        out string logLine)
    {
        changed = false;
        logLine = null;

        ApplyCommonTextureSettings(importer, settings);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        SetSingleSpriteBottomCenter(importer);

        bool needs = forceReprocess
                     || !markOk
                     || NeedsCommonSettingsChange(importer, settings, SpriteImportMode.Single)
                     || !IsSingleSpriteBottomCenter(importer);

        if (!needs)
        {
            return true;
        }

        importer.userData = fullMark;
        changed = true;
        logLine = $"[处理·序列帧] {importer.assetPath}";
        return true;
    }

    private static void ApplyCommonTextureSettings(
        TextureImporter importer,
        ImportSettingsSnapshot settings)
    {
        importer.spritePixelsPerUnit = settings.PixelsPerUnit;
        importer.mipmapEnabled = settings.MipmapEnabled;
        importer.filterMode = settings.FilterMode;
        importer.textureCompression = settings.TextureCompression;
        importer.alphaIsTransparency = settings.AlphaIsTransparency;
        importer.wrapMode = settings.WrapMode;

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        platform.maxTextureSize = settings.MaxTextureSize;
        platform.textureCompression = settings.TextureCompression;
        platform.format = settings.TextureFormat;
        importer.SetPlatformTextureSettings(platform);
    }

    private static bool NeedsCommonSettingsChange(
        TextureImporter importer,
        ImportSettingsSnapshot settings,
        SpriteImportMode expectedMode)
    {
        if (importer.textureType != TextureImporterType.Sprite)
        {
            return true;
        }

        if (importer.spriteImportMode != expectedMode)
        {
            return true;
        }

        if (!Mathf.Approximately(importer.spritePixelsPerUnit, settings.PixelsPerUnit))
        {
            return true;
        }

        if (importer.mipmapEnabled != settings.MipmapEnabled)
        {
            return true;
        }

        if (importer.filterMode != settings.FilterMode)
        {
            return true;
        }

        if (importer.textureCompression != settings.TextureCompression)
        {
            return true;
        }

        if (importer.alphaIsTransparency != settings.AlphaIsTransparency)
        {
            return true;
        }

        if (importer.wrapMode != settings.WrapMode)
        {
            return true;
        }

        TextureImporterPlatformSettings platform = importer.GetDefaultPlatformTextureSettings();
        return platform.maxTextureSize != settings.MaxTextureSize
               || platform.textureCompression != settings.TextureCompression
               || platform.format != settings.TextureFormat;
    }

    private static bool HasBottomCenterSprites(TextureImporter importer)
    {
        SpriteMetaData[] sheet = importer.spritesheet;
        if (sheet == null || sheet.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < sheet.Length; i++)
        {
            if (sheet[i].alignment != AlignmentBottomCenter)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ApplyBottomCenterToAllSprites(TextureImporter importer)
    {
        SpriteMetaData[] sheet = importer.spritesheet;
        if (sheet == null || sheet.Length == 0)
        {
            return false;
        }

        bool changed = false;
        for (var i = 0; i < sheet.Length; i++)
        {
            SpriteMetaData meta = sheet[i];
            if (meta.alignment == AlignmentBottomCenter
                && Mathf.Approximately(meta.pivot.x, 0.5f)
                && Mathf.Approximately(meta.pivot.y, 0f))
            {
                continue;
            }

            meta.alignment = AlignmentBottomCenter;
            meta.pivot = new Vector2(0.5f, 0f);
            sheet[i] = meta;
            changed = true;
        }

        if (changed)
        {
            importer.spritesheet = sheet;
        }

        return changed;
    }

    /// <summary>
    /// 无现有切片时，按路径 (96x96) 或均匀横条尝试自动切片。
    /// </summary>
    private static bool EnsureSpriteSheetSlices(TextureImporter importer, string assetPath)
    {
        SpriteMetaData[] existing = importer.spritesheet;
        if (existing != null && existing.Length > 0)
        {
            return true;
        }

        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        int? cell = InferCellSizeFromAssetPath(assetPath);
        if (!cell.HasValue)
        {
            if (height >= 16 && width >= height * 2 && width % height == 0)
            {
                cell = height;
            }
            else
            {
                return false;
            }
        }

        int cellSize = cell.Value;
        if (cellSize <= 0 || width < cellSize)
        {
            return false;
        }

        int columns = width / cellSize;
        if (columns <= 0)
        {
            return false;
        }

        // 横条图集：单行多列（与常见 Sprite Sheet 包一致）。
        int rows = height / cellSize;
        if (rows <= 0)
        {
            rows = 1;
        }

        var metas = new List<SpriteMetaData>(columns * rows);
        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        int index = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < columns; col++)
            {
                int x = col * cellSize;
                int y = height - (row + 1) * cellSize;
                if (x + cellSize > width || y < 0)
                {
                    continue;
                }

                metas.Add(new SpriteMetaData
                {
                    name = baseName + "_" + index,
                    rect = new Rect(x, y, cellSize, cellSize),
                    alignment = AlignmentBottomCenter,
                    pivot = new Vector2(0.5f, 0f),
                    border = Vector4.zero,
                });
                index++;
            }
        }

        if (metas.Count == 0)
        {
            return false;
        }

        importer.spritesheet = metas.ToArray();
        return true;
    }

    private static int? InferCellSizeFromAssetPath(string assetPath)
    {
        Match match = CellSizeInPathRegex.Match(assetPath ?? string.Empty);
        if (!match.Success)
        {
            return null;
        }

        if (!int.TryParse(match.Groups[1].Value, out int w)
            || !int.TryParse(match.Groups[2].Value, out int h))
        {
            return null;
        }

        return w == h ? w : (int?)null;
    }

    private static string AbsoluteFileToAssetPath(string absolutePath, string projectRoot)
    {
        string dataPath = Path.Combine(projectRoot, "Assets").Replace('\\', '/');
        string full = Path.GetFullPath(absolutePath).Replace('\\', '/');
        if (!full.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ("Assets" + full.Substring(dataPath.Length)).Replace('\\', '/');
    }

    private static void SetSingleSpriteBottomCenter(TextureImporter importer)
    {
        importer.spritePivot = new Vector2(0.5f, 0f);
        var serialized = new SerializedObject(importer);
        SerializedProperty alignment = serialized.FindProperty("m_SpriteAlignment");
        if (alignment != null)
        {
            alignment.intValue = AlignmentBottomCenter;
        }

        SerializedProperty pivot = serialized.FindProperty("m_SpritePivot");
        if (pivot != null)
        {
            pivot.vector2Value = new Vector2(0.5f, 0f);
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool IsSingleSpriteBottomCenter(TextureImporter importer)
    {
        if (!Mathf.Approximately(importer.spritePivot.x, 0.5f)
            || !Mathf.Approximately(importer.spritePivot.y, 0f))
        {
            return false;
        }

        var serialized = new SerializedObject(importer);
        SerializedProperty alignment = serialized.FindProperty("m_SpriteAlignment");
        if (alignment == null)
        {
            return true;
        }

        return alignment.intValue == AlignmentBottomCenter;
    }
}
#endif
