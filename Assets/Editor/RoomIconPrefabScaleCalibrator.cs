using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// #100：按 ADR-0024 目标观感校准房间图标 / 选项模板根 localScale（所见即所得）。
/// 目标盒对齐「房间选项标准模板」外框约 2.55×3.7（底板 3.8×4.9 留白）。
/// </summary>
public static class RoomIconPrefabScaleCalibrator
{
    public const float TargetWidth = 2.55f;
    public const float TargetHeight = 3.7f;

    private const string PrefabsFolder = "Assets/Prefabs";
    private const string OptionTemplateName = "房间选项标准模板";

    public static string Measure()
    {
        return Run(apply: false);
    }

    public static string Calibrate()
    {
        return Run(apply: true);
    }

    private static string Run(bool apply)
    {
        var target = new Vector2(TargetWidth, TargetHeight);
        var sb = new StringBuilder();
        sb.AppendLine(apply ? "CALIBRATE" : "MEASURE");
        sb.AppendLine($"target={TargetWidth:F2}x{TargetHeight:F2}");

        var changed = 0;
        var skipped = 0;
        foreach (var path in EnumerateTargetPrefabPaths())
        {
                var name = Path.GetFileNameWithoutExtension(path);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    // 选项模板：ADR-0024 已确认根缩放 1、外框约 2.55×3.7 为终值；勿按合并包围盒再压。
                    if (name == OptionTemplateName)
                    {
                        var optionScale = root.transform.localScale;
                        var optionOk = Approximately(optionScale.x, 1f)
                                       && Approximately(optionScale.y, 1f)
                                       && Approximately(optionScale.z, 1f);
                        sb.AppendLine(
                            $"{name}|was={FormatScale(optionScale)}|policy=keep-authored-root-1" +
                            $"|ok={(optionOk ? "yes" : "NO")}|write=no");
                        if (!optionOk)
                        {
                            skipped++;
                        }

                        continue;
                    }

                    var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                    if (renderers == null || renderers.Length == 0 || !HasAnySprite(renderers))
                    {
                        skipped++;
                        sb.AppendLine($"{name}|skip=no-sprite|rootScale={FormatScale(root.transform.localScale)}");
                        continue;
                    }

                    var prefabScale = root.transform.localScale;
                    // 临时单位根缩放，读 Sprite 合并世界包围盒后压进目标盒。
                    // （运行时 RoomIconVisualFit 仍只取首个 SpriteRenderer；本校准器面向删 Fit 后的所见即所得。）
                    root.transform.localScale = Vector3.one;
                    var unitSize = MeasureCombinedSpriteSize(renderers);
                var fit = ComputeUniformScale(unitSize, target);
                var currentWorld = new Vector2(unitSize.x * prefabScale.x, unitSize.y * prefabScale.y);
                var fittedWorld = new Vector2(unitSize.x * fit, unitSize.y * fit);
                var primary = FindLargestSprite(renderers);

                var needsWrite = !Approximately(prefabScale.x, fit)
                                 || !Approximately(prefabScale.y, fit)
                                 || !Approximately(prefabScale.z, fit);

                sb.AppendLine(
                    $"{name}|was={FormatScale(prefabScale)}|unit={unitSize.x:F3}x{unitSize.y:F3}" +
                    $"|worldWas={currentWorld.x:F3}x{currentWorld.y:F3}" +
                    $"|fit={fit:F4}|worldFit={fittedWorld.x:F3}x{fittedWorld.y:F3}" +
                    $"|sprite={(primary != null && primary.sprite != null ? primary.sprite.name : "?")}" +
                    $"|write={(needsWrite ? "yes" : "no")}");
                if (!apply)
                {
                    root.transform.localScale = prefabScale;
                    continue;
                }

                if (!needsWrite)
                {
                    root.transform.localScale = prefabScale;
                    skipped++;
                    continue;
                }

                root.transform.localScale = new Vector3(fit, fit, fit);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        if (apply)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        sb.AppendLine($"changed={changed}|skippedOrUnchanged={skipped}");
        return sb.ToString();
    }

    private static IEnumerable<string> EnumerateTargetPrefabPaths()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabsFolder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var name = Path.GetFileNameWithoutExtension(path);
            if (name.EndsWith("图标") || name == OptionTemplateName)
            {
                yield return path;
            }
        }
    }

    private static float ComputeUniformScale(Vector2 spriteWorldSizeAtUnitScale, Vector2 targetMaxSize)
    {
        if (spriteWorldSizeAtUnitScale.x <= 0.0001f || spriteWorldSizeAtUnitScale.y <= 0.0001f)
        {
            return 1f;
        }

        if (targetMaxSize.x <= 0.0001f || targetMaxSize.y <= 0.0001f)
        {
            return 1f;
        }

        var sx = targetMaxSize.x / spriteWorldSizeAtUnitScale.x;
        var sy = targetMaxSize.y / spriteWorldSizeAtUnitScale.y;
        return Mathf.Min(sx, sy);
    }

    private static bool HasAnySprite(SpriteRenderer[] renderers)
    {
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].sprite != null)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector2 MeasureCombinedSpriteSize(SpriteRenderer[] renderers)
    {
        var has = false;
        var bounds = new Bounds();
        for (var i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || r.sprite == null || !r.enabled)
            {
                continue;
            }

            if (!has)
            {
                bounds = r.bounds;
                has = true;
            }
            else
            {
                bounds.Encapsulate(r.bounds);
            }
        }

        return has ? new Vector2(bounds.size.x, bounds.size.y) : Vector2.zero;
    }

    private static SpriteRenderer FindLargestSprite(SpriteRenderer[] renderers)
    {
        SpriteRenderer best = null;
        var bestArea = -1f;
        for (var i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || r.sprite == null)
            {
                continue;
            }

            var s = r.bounds.size;
            var area = s.x * s.y;
            if (area > bestArea)
            {
                bestArea = area;
                best = r;
            }
        }

        return best;
    }

    private static bool Approximately(float a, float b) => Mathf.Abs(a - b) <= 0.0005f;

    private static string FormatScale(Vector3 s) => $"{s.x:F4},{s.y:F4},{s.z:F4}";
}
