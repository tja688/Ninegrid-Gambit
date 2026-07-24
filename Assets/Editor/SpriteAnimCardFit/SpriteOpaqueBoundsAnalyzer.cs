#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 非破坏性像素扫描：按帧求不透明 AABB，再按纹理中心叠到 clip 公共空间求联合包围盒。
/// </summary>
public static class SpriteOpaqueBoundsAnalyzer
{
    private static readonly Regex FrameIndexRegex = new(
        @"_(\d+)\.(png|PNG)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public struct IntRect
    {
        public float MinX;
        public float MinY;
        public float MaxX;
        public float MaxY;

        public bool IsEmpty => MaxX <= MinX || MaxY <= MinY;

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterY => (MinY + MaxY) * 0.5f;
        public float Area => Math.Max(0f, Width) * Math.Max(0f, Height);

        public static IntRect Empty => new IntRect
        {
            MinX = float.PositiveInfinity,
            MinY = float.PositiveInfinity,
            MaxX = float.NegativeInfinity,
            MaxY = float.NegativeInfinity,
        };

        public static IntRect FromMinMax(float minX, float minY, float maxX, float maxY)
        {
            return new IntRect { MinX = minX, MinY = minY, MaxX = maxX, MaxY = maxY };
        }

        public IntRect Union(IntRect other)
        {
            if (IsEmpty)
                return other;
            if (other.IsEmpty)
                return this;
            return FromMinMax(
                Math.Min(MinX, other.MinX),
                Math.Min(MinY, other.MinY),
                Math.Max(MaxX, other.MaxX),
                Math.Max(MaxY, other.MaxY));
        }

        public IntRect Translated(float dx, float dy)
        {
            return FromMinMax(MinX + dx, MinY + dy, MaxX + dx, MaxY + dy);
        }
    }

    public sealed class FrameOpaqueInfo
    {
        public string Path;
        public int CanvasW;
        public int CanvasH;
        public int OpaquePixelCount;
        /// <summary>纹理像素空间（原点左下，含右/上开区间）。</summary>
        public IntRect OpaqueInTexture;
        /// <summary>相对纹理中心的 clip 空间。</summary>
        public IntRect OpaqueInClipSpace;
    }

    public sealed class ClipOpaqueAnalysis
    {
        public string FolderName;
        public string FolderPath;
        public readonly List<FrameOpaqueInfo> Frames = new();
        public IntRect OpaqueUnion = IntRect.Empty;
        public int CanvasMaxW;
        public int CanvasMaxH;
        public string SourceHash;
        public string Error;
    }

    public static List<string> ListFramePathsSorted(string absoluteFolder)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(absoluteFolder) || !Directory.Exists(absoluteFolder))
            return result;

        foreach (string path in Directory.GetFiles(absoluteFolder, "*.png", SearchOption.TopDirectoryOnly))
            result.Add(path);

        result.Sort(CompareFramePaths);
        return result;
    }

    public static ClipOpaqueAnalysis AnalyzeClipFolder(
        string folderName,
        string folderAssetPath,
        string absoluteFolder,
        byte alphaThreshold)
    {
        var analysis = new ClipOpaqueAnalysis
        {
            FolderName = folderName,
            FolderPath = folderAssetPath?.Replace('\\', '/'),
        };

        List<string> absPaths = ListFramePathsSorted(absoluteFolder);
        if (absPaths.Count == 0)
        {
            analysis.Error = "No PNG frames.";
            analysis.SourceHash = ComputeSourceHash(absPaths);
            return analysis;
        }

        var union = IntRect.Empty;
        foreach (string abs in absPaths)
        {
            FrameOpaqueInfo frame = AnalyzePngFile(abs, alphaThreshold);
            if (frame == null)
                continue;

            analysis.Frames.Add(frame);
            analysis.CanvasMaxW = Math.Max(analysis.CanvasMaxW, frame.CanvasW);
            analysis.CanvasMaxH = Math.Max(analysis.CanvasMaxH, frame.CanvasH);
            union = union.Union(frame.OpaqueInClipSpace);
        }

        analysis.OpaqueUnion = union;
        analysis.SourceHash = ComputeSourceHash(absPaths);
        if (analysis.Frames.Count == 0)
            analysis.Error = "Failed to decode any frame.";

        return analysis;
    }

    public static FrameOpaqueInfo AnalyzePngFile(string absolutePngPath, byte alphaThreshold)
    {
        if (string.IsNullOrEmpty(absolutePngPath) || !File.Exists(absolutePngPath))
            return null;

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(absolutePngPath);
        }
        catch
        {
            return null;
        }

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
                return null;

            int w = tex.width;
            int h = tex.height;
            Color32[] pixels = tex.GetPixels32();

            int minX = w;
            int minY = h;
            int maxX = -1;
            int maxY = -1;
            int count = 0;

            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 0; x < w; x++)
                {
                    if (pixels[row + x].a < alphaThreshold)
                        continue;

                    count++;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            var info = new FrameOpaqueInfo
            {
                Path = absolutePngPath,
                CanvasW = w,
                CanvasH = h,
                OpaquePixelCount = count,
            };

            if (count == 0)
            {
                info.OpaqueInTexture = IntRect.Empty;
                info.OpaqueInClipSpace = IntRect.Empty;
                return info;
            }

            // Inclusive max pixel → exclusive edge.
            info.OpaqueInTexture = IntRect.FromMinMax(minX, minY, maxX + 1, maxY + 1);

            float cx = w * 0.5f;
            float cy = h * 0.5f;
            info.OpaqueInClipSpace = IntRect.FromMinMax(
                info.OpaqueInTexture.MinX - cx,
                info.OpaqueInTexture.MinY - cy,
                info.OpaqueInTexture.MaxX - cx,
                info.OpaqueInTexture.MaxY - cy);

            return info;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }

    public static void ComputeCardFit(
        IntRect opaqueUnionClipSpace,
        float slotW,
        float slotH,
        float slackPx,
        out Vector2 offsetPx,
        out Vector4 overflowPx,
        out float maxOverflow,
        out float fillRatio,
        out bool usable,
        out float suggestedUniformScale,
        out float score,
        List<string> reasons)
    {
        reasons?.Clear();
        offsetPx = Vector2.zero;
        overflowPx = Vector4.zero;
        maxOverflow = 0f;
        fillRatio = 0f;
        usable = false;
        suggestedUniformScale = 1f;
        score = 0f;

        if (opaqueUnionClipSpace.IsEmpty || slotW <= 0f || slotH <= 0f)
        {
            reasons?.Add("empty_opaque_or_invalid_slot");
            return;
        }

        // One clip transform: center opaque union on card slot center (0,0).
        offsetPx = new Vector2(-opaqueUnionClipSpace.CenterX, -opaqueUnionClipSpace.CenterY);
        IntRect centered = opaqueUnionClipSpace.Translated(offsetPx.x, offsetPx.y);

        float halfW = slotW * 0.5f;
        float halfH = slotH * 0.5f;

        float overflowL = Math.Max(0f, -centered.MinX - halfW);
        float overflowR = Math.Max(0f, centered.MaxX - halfW);
        float overflowB = Math.Max(0f, -centered.MinY - halfH);
        float overflowT = Math.Max(0f, centered.MaxY - halfH);
        overflowPx = new Vector4(overflowL, overflowR, overflowT, overflowB);
        maxOverflow = Math.Max(Math.Max(overflowL, overflowR), Math.Max(overflowB, overflowT));

        float slotArea = slotW * slotH;
        fillRatio = slotArea > 0f ? centered.Area / slotArea : 0f;

        usable = maxOverflow <= slackPx + 0.001f;
        if (!usable)
            reasons?.Add($"overflow_{maxOverflow:0.##}_gt_slack_{slackPx:0.##}");
        if (fillRatio < 0.08f)
            reasons?.Add("fill_too_small");
        if (fillRatio > 1.35f && usable)
            reasons?.Add("fill_very_large_but_within_slack");

        // Scale needed to fit inside slot with slack (uniform, read-only suggestion).
        float innerHalfW = Math.Max(0.5f, halfW - slackPx);
        float innerHalfH = Math.Max(0.5f, halfH - slackPx);
        float extX = Math.Max(Math.Abs(centered.MinX), Math.Abs(centered.MaxX));
        float extY = Math.Max(Math.Abs(centered.MinY), Math.Abs(centered.MaxY));
        float sx = extX > 0.001f ? innerHalfW / extX : 1f;
        float sy = extY > 0.001f ? innerHalfH / extY : 1f;
        suggestedUniformScale = Math.Min(1f, Math.Min(sx, sy));
        if (suggestedUniformScale < 0.999f)
            reasons?.Add($"suggest_scale_{suggestedUniformScale:0.###}");

        // Score: prefer low overflow, moderate fill (sweet spot ~0.35–0.85).
        float overflowPenalty = Mathf.Clamp01(maxOverflow / Math.Max(1f, Math.Max(slotW, slotH) * 0.5f));
        float fillScore;
        if (fillRatio < 0.35f)
            fillScore = fillRatio / 0.35f;
        else if (fillRatio <= 0.85f)
            fillScore = 1f;
        else
            fillScore = Mathf.Clamp01(1.3f - (fillRatio - 0.85f));

        score = Mathf.Clamp01(0.55f * fillScore + 0.45f * (1f - overflowPenalty));
        if (!usable)
            score *= 0.35f;
    }

    public static string ComputeSourceHash(IReadOnlyList<string> absolutePaths)
    {
        using var sha = SHA256.Create();
        var sb = new StringBuilder(absolutePaths.Count * 64);
        foreach (string path in absolutePaths)
        {
            sb.Append(Path.GetFileName(path));
            sb.Append('|');
            try
            {
                var fi = new FileInfo(path);
                sb.Append(fi.Length.ToString(CultureInfo.InvariantCulture));
                sb.Append('|');
                sb.Append(fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            catch
            {
                sb.Append("missing");
            }

            sb.Append(';');
        }

        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        var hex = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash)
            hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return hex.ToString();
    }

    public static int CompareFramePaths(string a, string b)
    {
        int ia = ExtractFrameIndex(a);
        int ib = ExtractFrameIndex(b);
        int cmp = ia.CompareTo(ib);
        if (cmp != 0)
            return cmp;
        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }

    public static int ExtractFrameIndex(string path)
    {
        Match m = FrameIndexRegex.Match(path ?? string.Empty);
        if (!m.Success)
            return int.MaxValue;
        return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
            ? v
            : int.MaxValue;
    }
}
#endif
