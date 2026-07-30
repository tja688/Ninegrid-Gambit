#if UNITY_EDITOR
using System.Linq;
using System.Text;
using NineGrid.Cards.Anim;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-shot / menu helper: unify 主视图Mask (SpriteMask + anchor) on four face templates.
/// </summary>
public static class CardMainVisualMaskSetup
{
    private static readonly string[] PrefabPaths =
    {
        "Assets/Prefabs/玩家卡标准模板.prefab",
        "Assets/Prefabs/怪物卡标准模板.prefab",
        "Assets/Prefabs/道具卡标准模版.prefab",
        "Assets/Prefabs/遗物卡标准模版.prefab",
    };

    private static readonly string[] MainIconNames =
    {
        "核心图标", "主图标", "遗物主图标", "MainIcon", "Main Icon"
    };

    [MenuItem("NineGrid/Tools/Setup Card Main Visual Masks")]
    public static void SetupAll()
    {
        var sb = new StringBuilder();
        foreach (var path in PrefabPaths)
        {
            sb.AppendLine(SetupOne(path));
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[CardMainVisualMaskSetup]\n" + sb);
    }

    [MenuItem("NineGrid/Tools/Inspect Card Main Visual Masks")]
    public static void InspectAll()
    {
        var sb = new StringBuilder();
        foreach (var path in PrefabPaths)
        {
            if (!System.IO.File.Exists(ToAbsolute(path)))
            {
                sb.AppendLine(path + " MISSING");
                continue;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var mask = FindDeep(root.transform, CardMainVisualMaskAnchor.NodeName);
                var main = FindMainIcon(root.transform);
                var sms = root.GetComponentsInChildren<SpriteMask>(true);
                sb.AppendLine(path);
                sb.AppendLine("  mask=" + (mask == null ? "NULL" : mask.name)
                    + " SM=" + (mask != null && mask.GetComponent<SpriteMask>() != null)
                    + " SR=" + (mask != null && mask.GetComponent<SpriteRenderer>() != null)
                    + " Anchor=" + (mask != null && mask.GetComponent<CardMainVisualMaskAnchor>() != null));
                if (mask != null)
                {
                    var sr = mask.GetComponent<SpriteRenderer>();
                    sb.AppendLine("  maskSprite=" + (sr != null && sr.sprite != null ? sr.sprite.name : "null")
                        + " pos=" + mask.localPosition + " scale=" + mask.localScale);
                }

                sb.AppendLine("  main=" + (main == null ? "NULL" : main.name)
                    + " maskInt=" + (main != null ? main.maskInteraction.ToString() : "-"));
                sb.AppendLine("  spriteMaskCount=" + sms.Length);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log("[CardMainVisualMaskInspect]\n" + sb);
    }

    private static string SetupOne(string path)
    {
        if (!System.IO.File.Exists(ToAbsolute(path)))
        {
            return path + " MISSING";
        }

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var maskTf = FindDeep(root.transform, CardMainVisualMaskAnchor.NodeName);
            if (maskTf == null)
            {
                var go = new GameObject(CardMainVisualMaskAnchor.NodeName);
                go.transform.SetParent(root.transform, false);
                maskTf = go.transform;
            }

            var maskSr = maskTf.GetComponent<SpriteRenderer>();
            if (maskSr == null)
            {
                maskSr = maskTf.gameObject.AddComponent<SpriteRenderer>();
            }

            // Prefer existing mask sprite; else copy from main icon sprite as placeholder shape.
            var main = FindMainIcon(root.transform);
            if (maskSr.sprite == null && main != null && main.sprite != null)
            {
                maskSr.sprite = main.sprite;
            }

            // Hide mask graphic in game view — SpriteMask uses the sprite shape.
            maskSr.color = new Color(1f, 1f, 1f, 0f);
            maskSr.enabled = false;

            var spriteMask = maskTf.GetComponent<SpriteMask>();
            if (spriteMask == null)
            {
                spriteMask = maskTf.gameObject.AddComponent<SpriteMask>();
            }

            if (maskSr.sprite != null)
            {
                spriteMask.sprite = maskSr.sprite;
            }

            spriteMask.alphaCutoff = 0.1f;

            var anchor = maskTf.GetComponent<CardMainVisualMaskAnchor>();
            if (anchor == null)
            {
                anchor = maskTf.gameObject.AddComponent<CardMainVisualMaskAnchor>();
            }

            // Monster face root previously had a loose SpriteMask — disable extras not on 主视图Mask
            // or 背景图（遗物六边背景裁切专用 Mask_hexagon）。
            foreach (var sm in root.GetComponentsInChildren<SpriteMask>(true))
            {
                if (sm == null || sm.transform == maskTf)
                {
                    continue;
                }

                if (sm.gameObject.name == "背景图" || sm.gameObject.name == "背景")
                {
                    continue;
                }

                sm.enabled = false;
            }

            // 只有主视觉吃主视图 Mask；其它层保持 None。背景六边 Mask 由 EnsureFaceBackgroundHexMask 单独处理。
            foreach (var sr in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null || sr == maskSr)
                {
                    continue;
                }

                if (main != null && sr == main)
                {
                    continue;
                }

                if (sr.GetComponent<SpriteMask>() != null
                    && (sr.gameObject.name == "背景图" || sr.gameObject.name == "背景"))
                {
                    continue;
                }

                sr.maskInteraction = SpriteMaskInteraction.None;
            }

            if (main != null)
            {
                anchor.SyncMaskSortingTo(main);
                main.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
            else
            {
                spriteMask.isCustomRangeActive = false;
            }

            CardMainVisualMaskAnchor.EnsureFaceBackgroundHexMask(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return path + " OK maskSprite=" + (spriteMask.sprite != null ? spriteMask.sprite.name : "null")
                + " main=" + (main != null ? main.name : "null");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static SpriteRenderer FindMainIcon(Transform root)
    {
        foreach (var name in MainIconNames)
        {
            var t = FindDeep(root, name);
            if (t == null)
            {
                continue;
            }

            var sr = t.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                return sr;
            }
        }

        return null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == name)
        {
            return root;
        }

        for (var i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static string ToAbsolute(string assetPath)
    {
        var normalized = assetPath.Replace('\\', '/');
        if (normalized.StartsWith("Assets/"))
        {
            normalized = normalized.Substring("Assets/".Length);
        }

        return System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, normalized.Replace('/', System.IO.Path.DirectorySeparatorChar)));
    }
}
#endif
