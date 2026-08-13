#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 调色板映射工具：把指定文件夹（含子目录）下所有 PNG 的像素颜色就地映射到
/// GPL 调色板（Oklab 最近色，明度权重 1.15 / 色度权重 1.30，仅处理 alpha&gt;0 像素）。
/// 与 scripts/palette-map-apollo.py 同源算法；映射幂等，可对新导入素材重复执行。
/// 默认调色板：Assets/Arts/配色文件/apollo.gpl。
/// </summary>
public sealed class PaletteMapFolderWindow : EditorWindow
{
    private const string DefaultPalettePath = "Assets/Arts/配色文件/apollo.gpl";
    private const float LightnessWeight = 1.15f;
    private const float ChromaWeight = 1.30f;

    [SerializeField] private DefaultAsset targetFolder;
    [SerializeField] private DefaultAsset paletteAsset;
    [SerializeField] private Vector2 logScroll;

    private readonly List<string> logs = new();

    [MenuItem("NineGrid/Tools/Palette Map Folder（调色板映射）")]
    private static void Open()
    {
        var window = GetWindow<PaletteMapFolderWindow>("调色板映射");
        window.minSize = new Vector2(420f, 320f);
    }

    private void OnEnable()
    {
        if (paletteAsset == null)
        {
            paletteAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(DefaultPalettePath);
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("目标文件夹（含子目录的全部 PNG，就地覆写）", EditorStyles.boldLabel);
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "图片文件夹", targetFolder, typeof(DefaultAsset), false);

        if (GUILayout.Button("使用 Project 当前选中的文件夹"))
        {
            TryAdoptSelectionFolder();
        }

        EditorGUILayout.Space(6f);
        paletteAsset = (DefaultAsset)EditorGUILayout.ObjectField(
            "调色板 (.gpl)", paletteAsset, typeof(DefaultAsset), false);

        EditorGUILayout.Space(10f);
        var folderPath = ResolveFolderPath();
        using (new EditorGUI.DisabledScope(folderPath == null))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("干跑统计（不写文件）", GUILayout.Height(28f)))
                {
                    Run(folderPath, dryRun: true);
                }

                if (GUILayout.Button("执行映射（就地覆写）", GUILayout.Height(28f)))
                {
                    Run(folderPath, dryRun: false);
                }
            }
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("日志", EditorStyles.boldLabel);
        logScroll = EditorGUILayout.BeginScrollView(logScroll, GUILayout.ExpandHeight(true));
        foreach (var line in logs)
        {
            EditorGUILayout.LabelField(line, EditorStyles.miniLabel);
        }

        EditorGUILayout.EndScrollView();
    }

    private void TryAdoptSelectionFolder()
    {
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path))
            {
                targetFolder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
                return;
            }
        }

        ShowNotification(new GUIContent("请在 Project 窗口选中一个文件夹"));
    }

    private string ResolveFolderPath()
    {
        if (targetFolder == null)
        {
            return null;
        }

        var path = AssetDatabase.GetAssetPath(targetFolder);
        return AssetDatabase.IsValidFolder(path) ? path : null;
    }

    private void Run(string folderAssetPath, bool dryRun)
    {
        logs.Clear();
        var palettePath = paletteAsset != null
            ? AssetDatabase.GetAssetPath(paletteAsset)
            : DefaultPalettePath;
        Color32[] palette;
        try
        {
            palette = LoadGplPalette(AssetPathToAbsolute(palettePath));
        }
        catch (Exception e)
        {
            Log($"调色板加载失败 {palettePath}: {e.Message}");
            return;
        }

        var folderAbs = AssetPathToAbsolute(folderAssetPath);
        var files = Directory.GetFiles(folderAbs, "*.png", SearchOption.AllDirectories);
        if (files.Length == 0)
        {
            Log("文件夹下没有 PNG。");
            return;
        }

        if (!dryRun && !EditorUtility.DisplayDialog(
                "调色板映射",
                $"将就地覆写 {files.Length} 张 PNG（{folderAssetPath}），映射到 {Path.GetFileName(palettePath)}"
                + $"（{palette.Length} 色）。\n映射幂等，已映射过的图不会再变。\n\n继续？",
                "执行", "取消"))
        {
            return;
        }

        var mapper = new OklabMapper(palette);
        int okCount = 0, changedFiles = 0, errorCount = 0;
        try
        {
            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "调色板映射",
                        $"{i + 1}/{files.Length}  {Path.GetFileName(file)}",
                        (i + 1f) / files.Length))
                {
                    Log($"已取消（完成 {i}/{files.Length}）。");
                    break;
                }

                try
                {
                    var changed = ProcessFile(file, mapper, dryRun);
                    okCount++;
                    if (changed > 0)
                    {
                        changedFiles++;
                    }
                }
                catch (Exception e)
                {
                    errorCount++;
                    Log($"失败 {ToAssetPath(file)}: {e.Message}");
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Log($"{(dryRun ? "干跑" : "映射")}完成：处理 {okCount}/{files.Length}，"
            + $"有改动 {changedFiles}，失败 {errorCount}。");
        if (!dryRun)
        {
            AssetDatabase.Refresh();
            if (errorCount > 0)
            {
                Log("失败文件多为被占用（导入中），稍后重按「执行映射」补跑即可（幂等）。");
            }
        }

        Repaint();
    }

    /// <summary>返回被改动的像素颜色种数；0 表示本图已在调色板内。</summary>
    private static int ProcessFile(string absolutePath, OklabMapper mapper, bool dryRun)
    {
        var bytes = File.ReadAllBytes(absolutePath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        try
        {
            if (!texture.LoadImage(bytes, false))
            {
                throw new InvalidDataException("PNG 解码失败");
            }

            var pixels = texture.GetPixels32();
            var changed = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                if (p.a == 0)
                {
                    continue;
                }

                var mapped = mapper.Map(p);
                if (mapped.r != p.r || mapped.g != p.g || mapped.b != p.b)
                {
                    changed++;
                    pixels[i] = new Color32(mapped.r, mapped.g, mapped.b, p.a);
                }
            }

            if (changed > 0 && !dryRun)
            {
                texture.SetPixels32(pixels);
                File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            }

            return changed;
        }
        finally
        {
            DestroyImmediate(texture);
        }
    }

    private static Color32[] LoadGplPalette(string absolutePath)
    {
        var colors = new List<Color32>();
        foreach (var line in File.ReadAllLines(absolutePath))
        {
            var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 4
                && byte.TryParse(parts[0], out var r)
                && byte.TryParse(parts[1], out var g)
                && byte.TryParse(parts[2], out var b))
            {
                colors.Add(new Color32(r, g, b, 255));
            }
        }

        if (colors.Count == 0)
        {
            throw new InvalidDataException("GPL 中没有颜色行");
        }

        return colors.ToArray();
    }

    private static string AssetPathToAbsolute(string assetPath)
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath);
        return Path.Combine(projectRoot ?? string.Empty, assetPath);
    }

    private static string ToAssetPath(string absolutePath)
    {
        var projectRoot = Path.GetDirectoryName(Application.dataPath) ?? string.Empty;
        return absolutePath.Replace('\\', '/').Replace(projectRoot.Replace('\\', '/') + "/", string.Empty);
    }

    private void Log(string message)
    {
        logs.Add(message);
        Debug.Log($"[PaletteMap] {message}");
    }

    /// <summary>Oklab 加权最近色映射器（带按颜色缓存，跨文件复用）。</summary>
    private sealed class OklabMapper
    {
        private readonly Color32[] palette;
        private readonly Vector3[] paletteLab;
        private readonly Dictionary<int, Color32> cache = new();

        public OklabMapper(Color32[] palette)
        {
            this.palette = palette;
            paletteLab = new Vector3[palette.Length];
            for (var i = 0; i < palette.Length; i++)
            {
                paletteLab[i] = WeightedOklab(palette[i]);
            }
        }

        public Color32 Map(Color32 color)
        {
            var key = (color.r << 16) | (color.g << 8) | color.b;
            if (cache.TryGetValue(key, out var hit))
            {
                return hit;
            }

            var lab = WeightedOklab(color);
            var best = 0;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < paletteLab.Length; i++)
            {
                var distance = (lab - paletteLab[i]).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            var mapped = palette[best];
            cache[key] = mapped;
            return mapped;
        }

        private static Vector3 WeightedOklab(Color32 c)
        {
            var r = Linearize(c.r / 255f);
            var g = Linearize(c.g / 255f);
            var b = Linearize(c.b / 255f);
            var l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b;
            var m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b;
            var s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b;
            var l3 = Cbrt(l);
            var m3 = Cbrt(m);
            var s3 = Cbrt(s);
            return new Vector3(
                (0.2104542553f * l3 + 0.7936177850f * m3 - 0.0040720468f * s3) * LightnessWeight,
                (1.9779984951f * l3 - 2.4285922050f * m3 + 0.4505937099f * s3) * ChromaWeight,
                (0.0259040371f * l3 + 0.7827717662f * m3 - 0.8086757660f * s3) * ChromaWeight);
        }

        private static float Cbrt(float v)
        {
            return v <= 0f ? 0f : Mathf.Pow(v, 1f / 3f);
        }

        private static float Linearize(float c)
        {
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }
    }
}
#endif
