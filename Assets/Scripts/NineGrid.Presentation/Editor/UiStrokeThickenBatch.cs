using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// 将 Fantasy UI 包素材按「localScale×2 + Sprite size÷2」加粗 9-slice 描边，
    /// 保持世界尺寸与布局，并同步 BoxCollider / 子节点位姿与文字缩放。
    /// </summary>
    public static class UiStrokeThickenBatch
    {
        private const string PackRoot =
            "Assets/Arts/Images/Png/2D Pixel Quest Vol3_ The UI-GUI";

        private const float AlreadyProcessedLossyScale = 1.75f;

        private static readonly string[] PrefabPaths =
        {
            "Assets/Prefabs/横幅带框.prefab",
            "Assets/Prefabs/一个精妙的条装UI架构.prefab",
            "Assets/Prefabs/StandUISelection.prefab",
            "Assets/Prefabs/StandardUIButton.prefab",
            "Assets/Prefabs/楼层面板UI.prefab",
        };

        [MenuItem("NineGrid/UI/Thicken Fantasy UI Strokes (MainScene + Prefabs)")]
        public static void RunFromMenu()
        {
            Debug.Log(RunAll(dryRun: false));
        }

        [MenuItem("NineGrid/UI/Thicken Fantasy UI Strokes (Dry Run)")]
        public static void DryRunFromMenu()
        {
            Debug.Log(RunAll(dryRun: true));
        }

        /// <summary>供 unity command eval 调用。</summary>
        public static string RunAll(bool dryRun)
        {
            var packGuids = BuildPackGuidSet();
            var report = new StringBuilder();
            report.AppendLine(dryRun ? "[UiStrokeThicken] DRY RUN" : "[UiStrokeThicken] APPLY");
            report.AppendLine($"packGuids={packGuids.Count}");

            var scenePath = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scenePath))
                scenePath = "Assets/Scenes/MainScene.unity";

            if (EditorSceneManager.GetActiveScene().path != scenePath)
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var sceneCount = ProcessOpenSceneRoots(packGuids, dryRun, report);
            report.AppendLine($"MainScene processed={sceneCount}");

            if (!dryRun && sceneCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            }

            foreach (var prefabPath in PrefabPaths)
            {
                if (!File.Exists(prefabPath))
                {
                    report.AppendLine($"skip missing prefab: {prefabPath}");
                    continue;
                }

                var prefabCount = ProcessPrefabAsset(prefabPath, packGuids, dryRun, report);
                report.AppendLine($"prefab {Path.GetFileName(prefabPath)} processed={prefabCount}");
            }

            return report.ToString();
        }

        private static HashSet<string> BuildPackGuidSet()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PackRoot });
            foreach (var guid in guids)
                set.Add(guid);
            return set;
        }

        private static int ProcessOpenSceneRoots(
            HashSet<string> packGuids,
            bool dryRun,
            StringBuilder report)
        {
            var renderers = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            return ProcessRenderers(renderers, packGuids, dryRun, report, "scene");
        }

        private static int ProcessPrefabAsset(
            string prefabPath,
            HashSet<string> packGuids,
            bool dryRun,
            StringBuilder report)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
                var count = ProcessRenderers(renderers, packGuids, dryRun, report, prefabPath);
                if (!dryRun && count > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                return count;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int ProcessRenderers(
            IEnumerable<SpriteRenderer> renderers,
            HashSet<string> packGuids,
            bool dryRun,
            StringBuilder report,
            string context)
        {
            var targets = renderers
                .Where(sr => sr != null && sr.sprite != null && IsPackSprite(sr.sprite, packGuids))
                .Select(sr => sr.transform)
                .Distinct()
                .OrderByDescending(t => GetDepth(t))
                .ToList();

            var processed = 0;
            foreach (var t in targets)
            {
                var sr = t.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null)
                    continue;

                // 父级已 ×2 时 lossyScale 已加粗，避免重复。
                if (t.lossyScale.x >= AlreadyProcessedLossyScale ||
                    t.lossyScale.y >= AlreadyProcessedLossyScale)
                {
                    continue;
                }

                var path = BuildTransformPath(t);
                var beforeScale = t.localScale;
                var beforeSize = sr.size;
                var beforeMode = sr.drawMode;

                report.AppendLine(
                    $"  {context} | {path} | scale {Fmt(beforeScale)} size {Fmt(beforeSize)} mode={beforeMode}");

                if (dryRun)
                {
                    processed++;
                    continue;
                }

                Undo.RecordObject(t, "UI Stroke Thicken Scale");
                Undo.RecordObject(sr, "UI Stroke Thicken Sprite");

                // Simple → Sliced，否则 size 不生效，无法在 ×2 scale 后收回尺寸。
                if (sr.drawMode == SpriteDrawMode.Simple)
                {
                    var natural = sr.sprite != null
                        ? (Vector2)sr.sprite.bounds.size
                        : sr.size;
                    sr.drawMode = SpriteDrawMode.Sliced;
                    sr.size = natural;
                }

                sr.size = new Vector2(sr.size.x * 0.5f, sr.size.y * 0.5f);
                t.localScale = new Vector3(
                    beforeScale.x * 2f,
                    beforeScale.y * 2f,
                    beforeScale.z * 2f);

                // 子节点：收回因父 scale×2 带来的位移与文字放大。
                for (var i = 0; i < t.childCount; i++)
                {
                    var child = t.GetChild(i);
                    Undo.RecordObject(child, "UI Stroke Thicken Child");
                    child.localPosition = child.localPosition * 0.5f;
                    child.localScale = child.localScale * 0.5f;
                }

                foreach (var col2 in t.GetComponents<BoxCollider2D>())
                {
                    Undo.RecordObject(col2, "UI Stroke Thicken Collider2D");
                    col2.size = col2.size * 0.5f;
                    col2.offset = col2.offset * 0.5f;
                    EditorUtility.SetDirty(col2);
                }

                foreach (var col in t.GetComponents<BoxCollider>())
                {
                    Undo.RecordObject(col, "UI Stroke Thicken Collider");
                    col.size = col.size * 0.5f;
                    col.center = col.center * 0.5f;
                    EditorUtility.SetDirty(col);
                }

                // 同物体上的 TMP 不随 mesh 变「糊」，但父 scale×2 会放大字，需 ÷2。
                foreach (var tmp in t.GetComponents<TMP_Text>())
                {
                    // TMP 在同一节点时没有独立 transform；靠子节点补偿。此处仅标记 dirty。
                    EditorUtility.SetDirty(tmp);
                }

                EditorUtility.SetDirty(t);
                EditorUtility.SetDirty(sr);
                processed++;
            }

            return processed;
        }

        private static bool IsPackSprite(Sprite sprite, HashSet<string> packGuids)
        {
            var path = AssetDatabase.GetAssetPath(sprite);
            if (string.IsNullOrEmpty(path))
                return false;
            if (path.StartsWith(PackRoot, StringComparison.OrdinalIgnoreCase))
                return true;
            var guid = AssetDatabase.AssetPathToGUID(path);
            return !string.IsNullOrEmpty(guid) && packGuids.Contains(guid);
        }

        private static int GetDepth(Transform t)
        {
            var d = 0;
            while (t.parent != null)
            {
                d++;
                t = t.parent;
            }

            return d;
        }

        private static string BuildTransformPath(Transform t)
        {
            var stack = new Stack<string>();
            while (t != null)
            {
                stack.Push(t.name);
                t = t.parent;
            }

            return string.Join("/", stack);
        }

        private static string Fmt(Vector3 v) =>
            $"({v.x:0.###},{v.y:0.###},{v.z:0.###})";

        private static string Fmt(Vector2 v) =>
            $"({v.x:0.###},{v.y:0.###})";
    }
}
