using System.Text;
using NineGrid.LivingUI;
using NineGrid.LivingUI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NineGrid.LivingUI.Editor
{
    /// <summary>
    /// 修补错误面板映射 + 构型4被同名丢弃的变体。
    /// </summary>
    public static class LivingUiMappingFixBatch
    {
        private const string LiveRootName = "大盘";

        [MenuItem("TableNine/LivingUI/Fix Mapping Batch (panel moves + deck variants)")]
        public static void Fix()
        {
            var report = new StringBuilder();
            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            // 激活全部便于操作
            foreach (var root in FindAllStageRoots())
            {
                root.gameObject.SetActive(true);
            }

            report.AppendLine(MoveRenameAll("7-AttackText", 6, "6-AttackText"));
            report.AppendLine(MoveRenameAll("6-HpText", 5, "5-HpText"));
            report.AppendLine(MoveRenameAll("12-ArmorText", 7, "7-ArmorText"));

            report.AppendLine(FixLayout4DeckVariants());

            // 仅大盘保持激活
            foreach (var root in FindAllStageRoots())
            {
                root.gameObject.SetActive(root.name == LiveRootName);
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            LivingUiBlueprintPoseSampler.InvalidateCache();
            Debug.Log("[LivingUI] Mapping fix batch:\n" + report);
        }

        private static string MoveRenameAll(string fromName, int toPanel, string toName)
        {
            var sb = new StringBuilder($"MoveRename {fromName} -> P{toPanel}/{toName}:");
            var roots = FindAllStageRoots();
            for (var r = 0; r < roots.Length; r++)
            {
                var root = roots[r];
                var src = FindNamedUnderRoot(root, fromName);
                if (src == null)
                {
                    // 可能已是目标名但还在错误面板
                    src = FindNamedUnderRoot(root, toName);
                    if (src == null) continue;
                }

                var attach = EnsureContentAttach(root, toPanel);
                if (attach == null)
                {
                    sb.Append($" [{root.name} no panel{toPanel}]");
                    continue;
                }

                // 目标面板若已有同名，保留蓝图位姿更完整的一份
                var existing = FindChildNamed(attach, toName);
                if (existing != null && existing != src)
                {
                    if (root.name == LiveRootName)
                    {
                        // live：丢弃多余，保留已在正确面板的
                        if (src.parent != attach)
                        {
                            Undo.DestroyObjectImmediate(src.gameObject);
                            sb.Append($" [{root.name} drop dup keep existing]");
                            continue;
                        }
                    }
                    else
                    {
                        // 蓝图：若 src 要从别处迁入且已有 toName，删旧 existing 用 src
                        Undo.DestroyObjectImmediate(existing.gameObject);
                    }
                }

                Undo.RecordObject(src.gameObject, "Rename mapping");
                src.name = toName;
                if (src.parent != attach)
                {
                    Undo.SetTransformParent(src, attach, "Move to panel");
                }

                var marker = src.GetComponent<LivingUiContentMarker>();
                if (marker != null)
                {
                    marker.ApplyAuthored(
                        toName,
                        toPanel,
                        LivingUiContentAnchor.TopLeft,
                        GuessFace(root.name),
                        restrictFace: root.name != LiveRootName,
                        envelope: default,
                        authoring: GetCarrierSize(root, toPanel));
                    marker.CaptureAuthoredPoseFromTransform();
                    marker.EnsureAuthoringSize(GetCarrierSize(root, toPanel));
                    EditorUtility.SetDirty(marker);
                }

                sb.Append($" [{root.name} ok]");
            }

            return sb.ToString();
        }

        private static string FixLayout4DeckVariants()
        {
            var sb = new StringBuilder("Layout4 variants:");
            var layout4 = FindRoot("大盘构型4-打开卡组视图");
            var live = FindRoot(LiveRootName);
            if (layout4 == null || live == null) return "Layout4/大盘 missing";

            // 4a RelicPanelAnchors -> DeckRelicPanelAnchors（仍挂面板9）
            var relic = FindChildNamed(GetContentAttach(layout4, 9), "RelicPanelAnchors")
                        ?? FindChildNamed(GetContentAttach(layout4, 9), "DeckRelicPanelAnchors");
            if (relic != null)
            {
                Undo.RecordObject(relic.gameObject, "Rename deck relic");
                relic.name = "DeckRelicPanelAnchors";
                EnsureMarker(relic, 9, LivingUiLayoutId.DeckPreview, layout4.name != LiveRootName);
                EnsureLiveClone(live, 9, relic, "DeckRelicPanelAnchors");
                sb.Append(" [DeckRelicPanelAnchors]");
            }
            else sb.Append(" [Relic MISSING]");

            // 4b show 从 CardDeck* 提到面板3
            var deckAttach = GetContentAttach(layout4, 12);
            Transform show = null;
            Transform deckRoot = FindChildNamed(deckAttach, "CardDeckAnchors")
                                 ?? FindChildNamed(deckAttach, "DeckCardDeckAnchors");
            if (deckRoot != null)
            {
                show = FindChildNamed(deckRoot, "show");
            }

            if (show != null)
            {
                var p3 = EnsureContentAttach(layout4, 3);
                Undo.SetTransformParent(show, p3, "Extract show to panel3");
                Undo.RecordObject(show.gameObject, "Rename show");
                // 保持名 show
                EnsureMarker(show, 3, LivingUiLayoutId.DeckPreview, true);
                EnsureLiveClone(live, 3, show, "show");
                sb.Append(" [show->P3]");
            }
            else
            {
                // 可能已在 P3
                show = FindChildNamed(GetContentAttach(layout4, 3), "show");
                if (show != null)
                {
                    EnsureLiveClone(live, 3, show, "show");
                    sb.Append(" [show already P3]");
                }
                else sb.Append(" [show MISSING]");
            }

            // 4c CardDeckAnchors -> DeckCardDeckAnchors
            deckRoot = FindChildNamed(GetContentAttach(layout4, 12), "CardDeckAnchors")
                       ?? FindChildNamed(GetContentAttach(layout4, 12), "DeckCardDeckAnchors");
            if (deckRoot != null)
            {
                Undo.RecordObject(deckRoot.gameObject, "Rename deck carddeck");
                deckRoot.name = "DeckCardDeckAnchors";
                EnsureMarker(deckRoot, 12, LivingUiLayoutId.DeckPreview, true);
                EnsureLiveClone(live, 12, deckRoot, "DeckCardDeckAnchors");
                sb.Append(" [DeckCardDeckAnchors]");
            }
            else sb.Append(" [CardDeck MISSING]");

            return sb.ToString();
        }

        private static void EnsureLiveClone(Transform live, int panelId, Transform blueprintSrc, string name)
        {
            var attach = EnsureContentAttach(live, panelId);
            var existing = FindChildNamed(attach, name);
            if (existing != null)
            {
                // 已存在：同步位姿
                existing.localPosition = blueprintSrc.localPosition;
                existing.localRotation = blueprintSrc.localRotation;
                existing.localScale = blueprintSrc.localScale;
                EnsureMarker(existing, panelId, LivingUiLayoutId.DeckPreview, false);
                return;
            }

            var clone = Object.Instantiate(blueprintSrc.gameObject, attach);
            Undo.RegisterCreatedObjectUndo(clone, "Live variant clone");
            clone.name = name;
            clone.transform.localPosition = blueprintSrc.localPosition;
            clone.transform.localRotation = blueprintSrc.localRotation;
            clone.transform.localScale = blueprintSrc.localScale;
            EnsureMarker(clone.transform, panelId, LivingUiLayoutId.DeckPreview, false);
        }

        private static void EnsureMarker(Transform content, int panelId, LivingUiLayoutId face, bool restrict)
        {
            var marker = content.GetComponent<LivingUiContentMarker>();
            if (marker == null) marker = Undo.AddComponent<LivingUiContentMarker>(content.gameObject);
            var size = Vector2.one;
            var carrier = content.parent != null ? content.parent.parent : null;
            var skin = carrier != null ? carrier.GetComponent<SpriteRenderer>() : null;
            if (skin != null) size = skin.size;
            marker.ApplyAuthored(content.name, panelId, LivingUiContentAnchor.TopLeft, face, restrict, default, size);
            marker.CaptureAuthoredPoseFromTransform();
            marker.EnsureAuthoringSize(size);
            EditorUtility.SetDirty(marker);
        }

        private static Vector2 GetCarrierSize(Transform root, int panelId)
        {
            var c = root.Find(panelId.ToString());
            var skin = c != null ? c.GetComponent<SpriteRenderer>() : null;
            return skin != null ? skin.size : Vector2.one;
        }

        private static Transform EnsureContentAttach(Transform root, int panelId)
        {
            var carrier = root.Find(panelId.ToString());
            if (carrier == null) return null;
            var attach = carrier.Find("ContentAttach");
            if (attach != null) return attach;
            var go = new GameObject("ContentAttach");
            Undo.RegisterCreatedObjectUndo(go, "ContentAttach");
            go.transform.SetParent(carrier, false);
            return go.transform;
        }

        private static Transform GetContentAttach(Transform root, int panelId)
        {
            return EnsureContentAttach(root, panelId);
        }

        private static Transform FindChildNamed(Transform parent, string name)
        {
            if (parent == null) return null;
            for (var i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            }

            return null;
        }

        private static Transform FindNamedUnderRoot(Transform root, string name)
        {
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] == root) continue;
                if (all[i].name != name) continue;
                // 跳过纯载体名
                if (all[i].parent == root) continue;
                return all[i];
            }

            return null;
        }

        private static LivingUiLayoutId GuessFace(string rootName)
        {
            switch (rootName)
            {
                case "大盘构型0-主菜单": return LivingUiLayoutId.MainMenu;
                case "大盘构型1-人物选择": return LivingUiLayoutId.CharacterChoice;
                case "大盘构型2-核心战斗面板": return LivingUiLayoutId.Battle;
                case "大盘构型3-选择奖励": return LivingUiLayoutId.RewardChoice;
                case "大盘构型4-打开卡组视图": return LivingUiLayoutId.DeckPreview;
                case "大盘构型5-房间基础面板": return LivingUiLayoutId.Room;
                case "大盘构型6-预备待定": return LivingUiLayoutId.Route;
                default: return LivingUiLayoutId.MainMenu;
            }
        }

        private static Transform FindRoot(string name)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!t.gameObject.scene.IsValid() || t.parent != null) continue;
                if (t.name == name) return t;
            }

            return null;
        }

        private static Transform[] FindAllStageRoots()
        {
            var names = new[]
            {
                LiveRootName,
                "大盘构型0-主菜单",
                "大盘构型1-人物选择",
                "大盘构型2-核心战斗面板",
                "大盘构型3-选择奖励",
                "大盘构型4-打开卡组视图",
                "大盘构型5-房间基础面板",
                "大盘构型6-预备待定",
            };
            var list = new System.Collections.Generic.List<Transform>();
            for (var i = 0; i < names.Length; i++)
            {
                var r = FindRoot(names[i]);
                if (r != null) list.Add(r);
            }

            return list.ToArray();
        }
    }
}
