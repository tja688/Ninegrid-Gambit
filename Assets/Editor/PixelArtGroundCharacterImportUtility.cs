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

    /// <summary>常见像素角色格尺寸；用于总览网格图推断。</summary>
    private static readonly int[] CommonCellSizes =
    {
        16, 24, 32, 48, 64, 72, 80, 96, 100, 128,
    };

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
        var entries = new List<(string abs, string asset, string fileName, int width, int height)>(
            absPngFiles.Length);
        for (var i = 0; i < absPngFiles.Length; i++)
        {
            string abs = absPngFiles[i];
            string asset = AbsoluteFileToAssetPath(abs, projectRoot);
            if (string.IsNullOrEmpty(asset))
            {
                continue;
            }

            TryReadPngSize(abs, out int width, out int height);
            entries.Add((abs, asset, Path.GetFileName(abs), width, height));
        }

        if (entries.Count == 0)
        {
            return;
        }

        // 同目录横条图集的格边长：用于把同角色总览拼图（非横条）也判成图集。
        int? folderStripCell = InferDominantStripCell(entries);

        bool numberedFrames = entries.Count >= 2
                              && LooksLikeNumberedFrameSet(entries.Select(e => e.fileName));

        for (var i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            GroundAssetKind kind = ClassifyAsset(
                e.fileName,
                e.asset,
                e.width,
                e.height,
                folderStripCell,
                numberedFrames);
            jobs.Add(new SmartImportJob(e.asset, kind, assetFolder));
        }
    }

    /// <summary>
    /// 按「像素几何优先、文件名次之」区分图集与单帧序列。
    /// 典型误判源：Soldier_Idle.png（600×100 横条）文件名无 sheet，旧逻辑整夹判成序列帧 → Single。
    /// </summary>
    private static GroundAssetKind ClassifyAsset(
        string fileName,
        string assetPath,
        int width,
        int height,
        int? folderStripCell,
        bool folderLooksLikeNumberedFrames)
    {
        if (LooksLikeSpriteSheet(fileName, assetPath, width, height, folderStripCell))
        {
            return GroundAssetKind.SpriteSheet;
        }

        // 编号帧文件夹里偶发「看起来像格」的单图仍跟序列走，避免把单帧肖像切碎。
        if (folderLooksLikeNumberedFrames && width > 0 && height > 0 && !IsStripGeometry(width, height))
        {
            return GroundAssetKind.FrameSequence;
        }

        return GroundAssetKind.FrameSequence;
    }

    public static bool LooksLikeSpriteSheet(
        string fileName,
        string assetPath,
        int width,
        int height,
        int? folderStripCell = null)
    {
        if (IsSheetLikeFileName(fileName))
        {
            return true;
        }

        if (width > 0 && height > 0 && IsStripGeometry(width, height))
        {
            return true;
        }

        int? pathCell = InferCellSizeFromAssetPath(assetPath);
        if (pathCell.HasValue
            && width > 0
            && height > 0
            && width % pathCell.Value == 0
            && height % pathCell.Value == 0
            && (width / pathCell.Value) * (height / pathCell.Value) >= 2)
        {
            return true;
        }

        if (folderStripCell.HasValue
            && width > 0
            && height > 0
            && width % folderStripCell.Value == 0
            && height % folderStripCell.Value == 0
            && (width / folderStripCell.Value) * (height / folderStripCell.Value) >= 2)
        {
            return true;
        }

        return false;
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

        if (fileName.IndexOf("_strip", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return false;
    }

    /// <summary>横条（宽≥2×高且整除）或竖条（高≥2×宽且整除）。</summary>
    public static bool IsStripGeometry(int width, int height)
    {
        if (height >= 16 && width >= height * 2 && width % height == 0)
        {
            return true;
        }

        if (width >= 16 && height >= width * 2 && height % width == 0)
        {
            return true;
        }

        return false;
    }

    public static int? InferStripCellSize(int width, int height)
    {
        if (height >= 16 && width >= height * 2 && width % height == 0)
        {
            return height;
        }

        if (width >= 16 && height >= width * 2 && height % width == 0)
        {
            return width;
        }

        return null;
    }

    private static int? InferDominantStripCell(
        List<(string abs, string asset, string fileName, int width, int height)> entries)
    {
        var counts = new Dictionary<int, int>();
        for (var i = 0; i < entries.Count; i++)
        {
            int? cell = InferStripCellSize(entries[i].width, entries[i].height);
            if (!cell.HasValue)
            {
                continue;
            }

            counts.TryGetValue(cell.Value, out int n);
            counts[cell.Value] = n + 1;
        }

        if (counts.Count == 0)
        {
            return null;
        }

        int bestCell = 0;
        int bestCount = 0;
        foreach (KeyValuePair<int, int> pair in counts)
        {
            if (pair.Value > bestCount || (pair.Value == bestCount && pair.Key > bestCell))
            {
                bestCount = pair.Value;
                bestCell = pair.Key;
            }
        }

        return bestCell > 0 ? bestCell : (int?)null;
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

    /// <summary>读 PNG IHDR，不依赖 Unity AssetDatabase / System.Drawing。</summary>
    public static bool TryReadPngSize(string absolutePath, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
        {
            return false;
        }

        try
        {
            using (var stream = File.OpenRead(absolutePath))
            {
                // signature(8) + length(4) + type(4) + width(4) + height(4)
                if (stream.Length < 24)
                {
                    return false;
                }

                var header = new byte[24];
                if (stream.Read(header, 0, 24) < 24)
                {
                    return false;
                }

                if (header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
                {
                    return false;
                }

                if (header[12] != (byte)'I' || header[13] != (byte)'H'
                    || header[14] != (byte)'D' || header[15] != (byte)'R')
                {
                    return false;
                }

                width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return width > 0 && height > 0;
            }
        }
        catch
        {
            width = 0;
            height = 0;
            return false;
        }
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
    /// 无现有均匀切片时，按路径 (96x96) / 横竖条 / 常见格尺寸尝试自动切片。
    /// 已有 alpha 紧裁切片但几何上是标准横条时，改为均匀格，保证脚底 Pivot 跨帧一致。
    /// </summary>
    private static bool EnsureSpriteSheetSlices(TextureImporter importer, string assetPath)
    {
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        int? cell = InferCellSizeForSlicing(width, height, assetPath);
        SpriteMetaData[] existing = importer.spritesheet;
        bool hasExisting = existing != null && existing.Length > 0;

        if (hasExisting)
        {
            if (!cell.HasValue)
            {
                return true;
            }

            if (IsUniformCellLayout(existing, cell.Value, width, height))
            {
                return true;
            }

            // 有可推断的均匀格，但现有切片不是该格 → 重切。
        }
        else if (!cell.HasValue)
        {
            return false;
        }

        int cellSize = cell.Value;
        if (cellSize <= 0 || width < cellSize || height < cellSize)
        {
            return false;
        }

        int columns = width / cellSize;
        int rows = height / cellSize;
        if (columns <= 0 || rows <= 0)
        {
            return false;
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

    private static int? InferCellSizeForSlicing(int width, int height, string assetPath)
    {
        int? pathCell = InferCellSizeFromAssetPath(assetPath);
        if (pathCell.HasValue
            && width % pathCell.Value == 0
            && height % pathCell.Value == 0
            && (width / pathCell.Value) * (height / pathCell.Value) >= 2)
        {
            return pathCell;
        }

        int? stripCell = InferStripCellSize(width, height);
        if (stripCell.HasValue)
        {
            return stripCell;
        }

        return InferCommonGridCell(width, height);
    }

    private static int? InferCommonGridCell(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        int g = GreatestCommonDivisor(width, height);
        int best = 0;
        for (var i = 0; i < CommonCellSizes.Length; i++)
        {
            int c = CommonCellSizes[i];
            if (c > g || g % c != 0)
            {
                continue;
            }

            int cells = (width / c) * (height / c);
            if (cells >= 2 && c > best)
            {
                best = c;
            }
        }

        return best > 0 ? best : (int?)null;
    }

    private static bool IsUniformCellLayout(
        SpriteMetaData[] sheet,
        int cellSize,
        int textureWidth,
        int textureHeight)
    {
        if (sheet == null || sheet.Length == 0 || cellSize <= 0)
        {
            return false;
        }

        int expected = (textureWidth / cellSize) * (textureHeight / cellSize);
        if (expected <= 0 || sheet.Length != expected)
        {
            return false;
        }

        for (var i = 0; i < sheet.Length; i++)
        {
            Rect r = sheet[i].rect;
            if (!Mathf.Approximately(r.width, cellSize) || !Mathf.Approximately(r.height, cellSize))
            {
                return false;
            }

            if (Mathf.Abs(r.x % cellSize) > 0.01f || Mathf.Abs(r.y % cellSize) > 0.01f)
            {
                return false;
            }
        }

        return true;
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        a = Mathf.Abs(a);
        b = Mathf.Abs(b);
        while (b != 0)
        {
            int t = a % b;
            a = b;
            b = t;
        }

        return a;
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
