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

    /// <summary>横条（宽≥2×高）或竖条（高≥2×宽）；方格优先，也认接近方格的矩形帧。</summary>
    public static bool IsStripGeometry(int width, int height)
    {
        return InferStripCell(width, height).HasValue;
    }

    public static int? InferStripCellSize(int width, int height)
    {
        // 对外仍返回「方格边长」；矩形横条见 InferStripCell。
        CellSize? cell = InferStripCell(width, height);
        if (!cell.HasValue)
        {
            return null;
        }

        return cell.Value.Width == cell.Value.Height ? cell.Value.Width : (int?)null;
    }

    /// <summary>推断横/竖条单帧尺寸（可为非正方形，如 70×50）。</summary>
    public static CellSize? InferStripCell(int width, int height)
    {
        if (height >= 16 && width >= height * 2 && width % height == 0)
        {
            return new CellSize(height, height);
        }

        if (width >= 16 && height >= width * 2 && height % width == 0)
        {
            return new CellSize(width, width);
        }

        // 非方格横条：帧高=图高，找最接近正方形且整除宽度的帧宽（如 980×50 → 70×50）。
        if (height >= 16 && width >= height * 2)
        {
            int? cellW = InferRectangularStripWidth(width, height);
            if (cellW.HasValue)
            {
                return new CellSize(cellW.Value, height);
            }
        }

        // 非方格竖条：帧宽=图宽。
        if (width >= 16 && height >= width * 2)
        {
            int? cellH = InferRectangularStripWidth(height, width);
            if (cellH.HasValue)
            {
                return new CellSize(width, cellH.Value);
            }
        }

        return null;
    }

    public readonly struct CellSize
    {
        public readonly int Width;
        public readonly int Height;

        public CellSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public bool IsSquare => Width == Height;
    }

    private static int? InferDominantStripCell(
        List<(string abs, string asset, string fileName, int width, int height)> entries)
    {
        var counts = new Dictionary<int, int>();
        for (var i = 0; i < entries.Count; i++)
        {
            CellSize? cell = InferStripCell(entries[i].width, entries[i].height);
            if (!cell.HasValue || !cell.Value.IsSquare)
            {
                continue;
            }

            counts.TryGetValue(cell.Value.Width, out int n);
            counts[cell.Value.Width] = n + 1;
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

    /// <summary>
    /// 横条非方格：在 [frameH/2, frameH*3] 内找整除 totalLength 且最接近正方形的帧宽。
    /// </summary>
    private static int? InferRectangularStripWidth(int totalLength, int frameThickness)
    {
        if (frameThickness <= 0 || totalLength < frameThickness * 2)
        {
            return null;
        }

        int minW = Mathf.Max(8, frameThickness / 2);
        int maxW = Mathf.Max(minW + 1, frameThickness * 3);
        int bestW = 0;
        int bestScore = int.MaxValue;
        for (int cellW = minW; cellW <= maxW; cellW++)
        {
            if (totalLength % cellW != 0)
            {
                continue;
            }

            int frames = totalLength / cellW;
            if (frames < 2)
            {
                continue;
            }

            int score = Mathf.Abs(cellW - frameThickness);
            // 同接近度时偏好帧数更合理（避免切成巨宽 2 帧）。
            if (score < bestScore || (score == bestScore && (bestW == 0 || frames > totalLength / bestW)))
            {
                bestScore = score;
                bestW = cellW;
            }
        }

        return bestW > 0 ? bestW : (int?)null;
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

        // 优先 spritesheet.txt 帧表；否则把碎切（Automatic 紧裁）收成均匀格，再判 pivot / 指纹。
        bool slicesTouched = EnsureSpriteSheetSlices(importer, assetPath);
        bool pivotOk = HasBottomCenterSprites(importer);
        bool needsBase = forceReprocess
                         || !markOk
                         || !pivotOk
                         || slicesTouched
                         || NeedsCommonSettingsChange(importer, settings, SpriteImportMode.Multiple);

        if (!needsBase && markOk && pivotOk)
        {
            return true;
        }

        if (!slicesTouched && importer.spritesheet != null && importer.spritesheet.Length == 0)
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
    /// 优先按旁路 <c>spritesheet.txt</c>（资源包自带帧表，左上原点）切片；
    /// 否则按路径 (96x96) / 横竖条 / 常见格尺寸推断均匀格。
    /// 返回 true 表示写入了新的 spritesheet。
    /// </summary>
    private static bool EnsureSpriteSheetSlices(TextureImporter importer, string assetPath)
    {
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (TryLoadSpritesheetTxtFrames(assetPath, out List<TxtFrame> txtFrames))
        {
            return ApplyTxtFramesIfNeeded(importer, assetPath, width, height, txtFrames);
        }

        CellSize? cell = InferCellSizeForSlicing(width, height, assetPath);
        SpriteMetaData[] existing = importer.spritesheet;
        bool hasExisting = existing != null && existing.Length > 0;

        if (hasExisting)
        {
            if (!cell.HasValue)
            {
                return false;
            }

            if (IsUniformCellLayout(existing, cell.Value, width, height))
            {
                return false;
            }

            // 有可推断的均匀格，但现有切片不是该格 → 重切。
        }
        else if (!cell.HasValue)
        {
            return false;
        }

        CellSize cellSize = cell.Value;
        if (cellSize.Width <= 0 || cellSize.Height <= 0
            || width < cellSize.Width || height < cellSize.Height)
        {
            return false;
        }

        int columns = width / cellSize.Width;
        int rows = height / cellSize.Height;
        if (columns <= 0 || rows <= 0
            || width % cellSize.Width != 0
            || height % cellSize.Height != 0)
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
                int x = col * cellSize.Width;
                int y = height - (row + 1) * cellSize.Height;
                if (x + cellSize.Width > width || y < 0)
                {
                    continue;
                }

                metas.Add(new SpriteMetaData
                {
                    name = baseName + "_" + index,
                    rect = new Rect(x, y, cellSize.Width, cellSize.Height),
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

    private readonly struct TxtFrame
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Width;
        public readonly int Height;

        public TxtFrame(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }

    private static readonly Regex SpritesheetTxtFrameRegex = new Regex(
        @"=\s*(\d+)\s+(\d+)\s+(\d+)\s+(\d+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>
    /// 读取同目录 <c>spritesheet.txt</c>：每行 <c>…/frameNNNN.png = x y w h</c>（x/y 为纹理左上原点）。
    /// </summary>
    private static bool TryLoadSpritesheetTxtFrames(string assetPath, out List<TxtFrame> frames)
    {
        frames = null;
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string dir = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(dir))
        {
            return false;
        }

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string absTxt = Path.GetFullPath(Path.Combine(projectRoot, dir, "spritesheet.txt"));
        if (!File.Exists(absTxt))
        {
            return false;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(absTxt);
        }
        catch
        {
            return false;
        }

        var parsed = new List<TxtFrame>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            Match match = SpritesheetTxtFrameRegex.Match(line.TrimEnd());
            if (!match.Success)
            {
                continue;
            }

            if (!int.TryParse(match.Groups[1].Value, out int x)
                || !int.TryParse(match.Groups[2].Value, out int y)
                || !int.TryParse(match.Groups[3].Value, out int w)
                || !int.TryParse(match.Groups[4].Value, out int h)
                || w <= 0
                || h <= 0)
            {
                continue;
            }

            parsed.Add(new TxtFrame(x, y, w, h));
        }

        if (parsed.Count == 0)
        {
            return false;
        }

        frames = parsed;
        return true;
    }

    private static bool ApplyTxtFramesIfNeeded(
        TextureImporter importer,
        string assetPath,
        int textureWidth,
        int textureHeight,
        List<TxtFrame> frames)
    {
        string baseName = Path.GetFileNameWithoutExtension(assetPath);
        var metas = new List<SpriteMetaData>(frames.Count);
        for (var i = 0; i < frames.Count; i++)
        {
            TxtFrame frame = frames[i];
            if (frame.X < 0
                || frame.Y < 0
                || frame.X + frame.Width > textureWidth
                || frame.Y + frame.Height > textureHeight)
            {
                Debug.LogWarning(
                    $"[PixelArtGround] spritesheet.txt 帧越界，跳过该图集：{assetPath} "
                    + $"frame#{i}={frame.X},{frame.Y},{frame.Width},{frame.Height} tex={textureWidth}x{textureHeight}");
                return false;
            }

            // txt 为左上原点；Unity Sprite rect 为左下原点。
            int unityY = textureHeight - frame.Y - frame.Height;
            metas.Add(new SpriteMetaData
            {
                name = baseName + "_" + i,
                rect = new Rect(frame.X, unityY, frame.Width, frame.Height),
                alignment = AlignmentBottomCenter,
                pivot = new Vector2(0.5f, 0f),
                border = Vector4.zero,
            });
        }

        if (SpritesMatchTxtLayout(importer.spritesheet, metas))
        {
            return false;
        }

        importer.spritesheet = metas.ToArray();
        return true;
    }

    private static bool SpritesMatchTxtLayout(SpriteMetaData[] existing, List<SpriteMetaData> expected)
    {
        if (existing == null || existing.Length != expected.Count)
        {
            return false;
        }

        for (var i = 0; i < expected.Count; i++)
        {
            SpriteMetaData a = existing[i];
            SpriteMetaData b = expected[i];
            if (!string.Equals(a.name, b.name, StringComparison.Ordinal)
                || !Mathf.Approximately(a.rect.x, b.rect.x)
                || !Mathf.Approximately(a.rect.y, b.rect.y)
                || !Mathf.Approximately(a.rect.width, b.rect.width)
                || !Mathf.Approximately(a.rect.height, b.rect.height))
            {
                return false;
            }
        }

        return true;
    }

    private static CellSize? InferCellSizeForSlicing(int width, int height, string assetPath)
    {
        int? pathCell = InferCellSizeFromAssetPath(assetPath);
        if (pathCell.HasValue
            && width % pathCell.Value == 0
            && height % pathCell.Value == 0
            && (width / pathCell.Value) * (height / pathCell.Value) >= 2)
        {
            return new CellSize(pathCell.Value, pathCell.Value);
        }

        CellSize? stripCell = InferStripCell(width, height);
        if (stripCell.HasValue)
        {
            return stripCell;
        }

        int? common = InferCommonGridCell(width, height);
        return common.HasValue ? new CellSize(common.Value, common.Value) : (CellSize?)null;
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
        CellSize cellSize,
        int textureWidth,
        int textureHeight)
    {
        if (sheet == null || sheet.Length == 0 || cellSize.Width <= 0 || cellSize.Height <= 0)
        {
            return false;
        }

        if (textureWidth % cellSize.Width != 0 || textureHeight % cellSize.Height != 0)
        {
            return false;
        }

        int expected = (textureWidth / cellSize.Width) * (textureHeight / cellSize.Height);
        if (expected <= 0 || sheet.Length != expected)
        {
            return false;
        }

        for (var i = 0; i < sheet.Length; i++)
        {
            Rect r = sheet[i].rect;
            if (!Mathf.Approximately(r.width, cellSize.Width)
                || !Mathf.Approximately(r.height, cellSize.Height))
            {
                return false;
            }

            if (Mathf.Abs(r.x % cellSize.Width) > 0.01f
                || Mathf.Abs(r.y % cellSize.Height) > 0.01f)
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
