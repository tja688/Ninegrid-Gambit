using System.Collections.Generic;
using System.Text;
using NineGrid.LivingUI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NineGrid.LivingUI.Editor
{
    /// <summary>
    /// 一次性：把各构型样板 Anchors 顶层内容按位置挂到活体载体（构型0）ContentAttach，
    /// 并装配 LivingUiContentMarker（Center 锚点，保持中心系局部位；模式由 ContentController 下发）。
    /// </summary>
    public static class LivingUiContentBindMigrator
    {
        private const string LiveRootName = "大盘";

        private static readonly (LivingUiLayoutId Face, string RootName, string IdPrefix)[] Sources =
        {
            (LivingUiLayoutId.CharacterChoice, "大盘构型1-人物选择", "char"),
            (LivingUiLayoutId.Battle, "大盘构型2-核心战斗面板", "battle"),
            (LivingUiLayoutId.RewardChoice, "大盘构型3-选择奖励", "reward"),
            (LivingUiLayoutId.DeckPreview, "大盘构型4-打开卡组视图", "deck"),
            (LivingUiLayoutId.Room, "大盘构型5-房间基础面板", "room"),
            (LivingUiLayoutId.Route, "大盘构型6-预备待定", "route"),
        };

        [MenuItem("LivingUI/Migrate Anchors → Live ContentAttach (1-4)")]
        public static void Migrate()
        {
            var liveRoot = FindSceneTransform(LiveRootName);
            if (liveRoot == null)
            {
                Debug.LogError($"[LivingUI] 未找到活体根 {LiveRootName}");
                return;
            }

            var liveCarriers = new Transform[13];
            var liveViews = new CarrierView[13];
            for (var c = 1; c <= 12; c++)
            {
                var child = liveRoot.Find(c.ToString());
                if (child == null)
                {
                    Debug.LogError($"[LivingUI] 活体缺少载体 {c}");
                    return;
                }

                var view = child.GetComponent<CarrierView>();
                if (view == null) view = Undo.AddComponent<CarrierView>(child.gameObject);
                view.EnsureWired();
                liveCarriers[c] = child;
                liveViews[c] = view;
            }

            var report = new StringBuilder();
            var moved = 0;
            var skipped = 0;

            foreach (var source in Sources)
            {
                var root = FindSceneTransform(source.RootName);
                if (root == null)
                {
                    report.AppendLine($"SKIP missing root {source.RootName}");
                    continue;
                }

                var anchors = root.Find("Anchors");
                if (anchors == null || anchors.childCount == 0)
                {
                    report.AppendLine($"{source.Face}: Anchors empty — ok");
                    continue;
                }

                var sourceCarriers = new Transform[13];
                var sourceSizes = new Vector2[13];
                for (var c = 1; c <= 12; c++)
                {
                    sourceCarriers[c] = root.Find(c.ToString());
                    if (sourceCarriers[c] == null) continue;
                    var sr = sourceCarriers[c].GetComponent<SpriteRenderer>();
                    sourceSizes[c] = sr != null ? sr.size : Vector2.one;
                }

                var children = new List<Transform>(anchors.childCount);
                for (var i = 0; i < anchors.childCount; i++)
                {
                    children.Add(anchors.GetChild(i));
                }

                foreach (var content in children)
                {
                    if (content.GetComponent<LivingUiContentMarker>() != null
                        && content.parent != null
                        && content.parent.name == CarrierView.ContentAttachName)
                    {
                        skipped++;
                        report.AppendLine($"  skip already-bound {content.name}");
                        continue;
                    }

                    if (!TryMatchCarrier(content.position, sourceCarriers, sourceSizes, out var carrierId, out var reason))
                    {
                        report.AppendLine($"  FAIL match {content.name}");
                        continue;
                    }

                    var sourceCarrier = sourceCarriers[carrierId];
                    var localPos = content.position - sourceCarrier.position;
                    var localScale = content.localScale;
                    if (localScale == Vector3.zero) localScale = Vector3.one;
                    var localRot = content.localRotation;

                    var attach = liveViews[carrierId].ContentAttach;
                    Undo.SetTransformParent(content, attach, "LivingUI Content Bind");
                    content.localPosition = localPos;
                    content.localRotation = localRot;
                    content.localScale = localScale;

                    var marker = content.GetComponent<LivingUiContentMarker>();
                    if (marker == null) marker = Undo.AddComponent<LivingUiContentMarker>(content.gameObject);

                    var contentId = $"{source.IdPrefix}.{SanitizeId(content.name)}";
                    // Center：局部位即相对中心，与旧 ContentAttach 装配一致，视觉不漂
                    marker.ApplyAuthored(
                        contentId,
                        carrierId,
                        LivingUiContentAnchor.Center,
                        source.Face,
                        restrictFace: true,
                        envelope: default,
                        authoring: sourceSizes[carrierId]);
                    marker.CaptureAuthoredPoseFromTransform();
                    EditorUtility.SetDirty(marker);
                    EditorUtility.SetDirty(content.gameObject);

                    moved++;
                    report.AppendLine(
                        $"  {source.Face}: {content.name} -> C{carrierId} ({reason}) " +
                        $"local=({localPos.x:F2},{localPos.y:F2}) id={contentId}");
                }
            }

            EditorSceneManager.MarkSceneDirty(liveRoot.gameObject.scene);
            Debug.Log($"[LivingUI] Content bind migrate done. moved={moved} skipped={skipped}\n{report}");
        }

        private static bool TryMatchCarrier(
            Vector3 worldPos,
            Transform[] carriers,
            Vector2[] sizes,
            out int bestId,
            out string reason)
        {
            bestId = -1;
            reason = "";
            var bestScore = float.PositiveInfinity;

            for (var c = 1; c <= 12; c++)
            {
                if (carriers[c] == null) continue;
                var cp = carriers[c].position;
                var half = sizes[c] * 0.5f;
                var dx = Mathf.Abs(worldPos.x - cp.x);
                var dy = Mathf.Abs(worldPos.y - cp.y);
                var inside = dx <= half.x + 0.05f && dy <= half.y + 0.05f;
                var normX = dx / Mathf.Max(half.x, 0.01f);
                var normY = dy / Mathf.Max(half.y, 0.01f);
                var cheb = Mathf.Max(normX, normY);
                var score = inside
                    ? cheb
                    : 100f + Vector2.Distance(new Vector2(worldPos.x, worldPos.y), new Vector2(cp.x, cp.y));
                if (score >= bestScore) continue;
                bestScore = score;
                bestId = c;
                reason = inside ? "inside" : "nearest";
            }

            return bestId > 0;
        }

        private static string SanitizeId(string name)
        {
            return name.Trim().Replace(' ', '_');
        }

        private static Transform FindSceneTransform(string objectName)
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i].gameObject.scene.IsValid() && all[i].name == objectName)
                {
                    return all[i];
                }
            }

            return null;
        }
    }
}
