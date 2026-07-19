using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.LivingUI;
using NineGrid.LivingUI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NineGrid.LivingUI.Editor
{
    /// <summary>
    /// 抢救：大盘权威 + 构型蓝图父子化 + 同名合并 + 跨面板改名。
    /// </summary>
    public static class LivingUiAuthorityStageMigrator
    {
        private const string LiveRootName = "大盘";
        private const string MenuRootName = "大盘构型0-主菜单";

        private static readonly string[] BlueprintRoots =
        {
            "大盘构型0-主菜单",
            "大盘构型1-人物选择",
            "大盘构型2-核心战斗面板",
            "大盘构型3-选择奖励",
            "大盘构型4-打开卡组视图",
            "大盘构型5-房间基础面板",
            "大盘构型6-预备待定",
        };

        // layoutRootName -> panelId -> element names (pre-rename Excel names)
        private static readonly Dictionary<string, Dictionary<int, string[]>> LayoutPanelElements =
            new Dictionary<string, Dictionary<int, string[]>>
            {
                [MenuRootName] = new Dictionary<int, string[]>
                {
                    [2] = new[] { "主菜单-结束游戏" },
                    [4] = new[] { "版本信息", "主菜单标题", "主菜单-Mainlogo" },
                    [11] = new[] { "主菜单-设置" },
                    [12] = new[] { "主菜单-开始" },
                },
                ["大盘构型1-人物选择"] = new Dictionary<int, string[]>
                {
                    [3] = new[] { "人物选项1", "人物选项2", "人物选项3", "人物选项4" },
                    [4] = new[] { "人物选择-核心头像" },
                    [5] = new[] { "血量图标", "基础生命" },
                    [6] = new[] { "攻击图标", "基础攻击" },
                    [7] = new[] { "防御图标", "基础护甲" },
                    [11] = new[] { "人物选择-介绍（限42字）" },
                },
                ["大盘构型2-核心战斗面板"] = new Dictionary<int, string[]>
                {
                    [1] = new[] { "Card Info Text" },
                    [2] = new[] { "CardHandAnchors" },
                    [3] = new[] { "GroundAnchors" },
                    [4] = new[] { "玩家头像", "设置按钮", "PlayerNameText", "回到主菜单按钮" },
                    [5] = new[] { "血量图标", "5-HpText" },
                    [6] = new[] { "攻击图标", "6-AttackText" },
                    [7] = new[] { "防御图标", "7-ArmorText" },
                    [8] = new[] { "金币图标", "8-GoldText" },
                    [9] = new[] { "RelicPanelAnchors" },
                    [10] = new[] { "Notice Text", "Leve Info Text" },
                    [12] = new[] { "CardDeckAnchors" },
                },
                ["大盘构型3-选择奖励"] = new Dictionary<int, string[]>
                {
                    [2] = new[] { "跳过/继续" },
                    [3] = new[] { "Notice Text（限 62 ）", "3-ArmorText" },
                    [5] = new[] { "血量图标", "5-HpText" },
                    [6] = new[] { "攻击图标", "6-AttackText" },
                    [7] = new[] { "防御图标" },
                    [8] = new[] { "金币图标", "8-GoldText" },
                    [12] = new[] { "DeckIcon" },
                },
                ["大盘构型4-打开卡组视图"] = new Dictionary<int, string[]>
                {
                    [2] = new[] { "Card Info Text(限76)" },
                    [3] = new[] { "show" },
                    [4] = new[] { "玩家头像", "设置按钮", "PlayerNameText", "回到主菜单按钮" },
                    [5] = new[] { "血量图标", "5-GoldText" },
                    [6] = new[] { "攻击图标" },
                    [7] = new[] { "防御图标", "7-ArmorText" },
                    [8] = new[] { "金币图标" },
                    [9] = new[] { "DeckRelicPanelAnchors" },
                    [12] = new[] { "DeckCardDeckAnchors", "12-HpText", "12-AttackText" },
                },
                ["大盘构型5-房间基础面板"] = new Dictionary<int, string[]>
                {
                    [3] = new[] { "Card Info Text（限60字）" },
                    [4] = new[] { "玩家头像", "设置按钮", "回到主菜单按钮", "PlayerNameText" },
                    [5] = new[] { "血量图标", "5-HpText" },
                    [6] = new[] { "攻击图标", "6-AttackText" },
                    [7] = new[] { "防御图标", "7-ArmorText" },
                    [8] = new[] { "金币图标", "8-GoldText" },
                    [9] = new[] { "RelicPanelAnchors" },
                    [10] = new[] { "Leve Info Text（限18字）" },
                    [12] = new[] { "CardDeckAnchors" },
                },
            };

        private static readonly LivingUiLayoutId[] LayoutIds =
        {
            LivingUiLayoutId.MainMenu,
            LivingUiLayoutId.CharacterChoice,
            LivingUiLayoutId.Battle,
            LivingUiLayoutId.RewardChoice,
            LivingUiLayoutId.DeckPreview,
            LivingUiLayoutId.Room,
            LivingUiLayoutId.Route,
        };

        [MenuItem("TableNine/LivingUI/Migrate Authority Stage")]
        public static void Migrate()
        {
            var report = new StringBuilder();
            try
            {
                Undo.IncrementCurrentGroup();
                var group = Undo.GetCurrentGroup();

                var live = FindSceneRoot(LiveRootName);
                var menu = FindSceneRoot(MenuRootName);
                if (live == null || menu == null)
                {
                    Debug.LogError("[LivingUI] 缺少「大盘」或「大盘构型0-主菜单」");
                    return;
                }

                // 激活全部构型以便查找
                foreach (var name in BlueprintRoots)
                {
                    var root = FindSceneRoot(name);
                    if (root != null) root.gameObject.SetActive(true);
                }

                report.AppendLine(PromoteLiveRoot(live, menu));
                report.AppendLine(ParentBlueprintContents());
                report.AppendLine(MergeUniqueContentIntoLive(live));
                report.AppendLine(EnsureMarkers(live));
                report.AppendLine(WireDirectorComponents());

                // 蓝图关闭，大盘开启
                foreach (var name in BlueprintRoots)
                {
                    var root = FindSceneRoot(name);
                    if (root != null) root.gameObject.SetActive(false);
                }

                live.gameObject.SetActive(true);

                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                Debug.Log("[LivingUI] Authority Stage migrate done:\n" + report);
            }
            catch (Exception ex)
            {
                Debug.LogError("[LivingUI] Authority Stage migrate failed: " + ex);
            }
        }

        private static string PromoteLiveRoot(Transform live, Transform menu)
        {
            var sb = new StringBuilder("PromoteLiveRoot: ");
            if (live.childCount >= 12 && live.Find("1") != null)
            {
                sb.Append("大盘已有载体，跳过迁移。");
                EnsureCarrierViews(live);
                return sb.ToString();
            }

            for (var id = 1; id <= 12; id++)
            {
                var orig = menu.Find(id.ToString());
                if (orig == null) throw new InvalidOperationException("主菜单缺少载体 " + id);

                // 蓝图克隆留在主菜单
                var cloneGo = UnityEngine.Object.Instantiate(orig.gameObject, menu);
                Undo.RegisterCreatedObjectUndo(cloneGo, "Blueprint carrier");
                cloneGo.name = id + "__bp";
                StripRuntimeExtras(cloneGo.transform);

                Undo.SetTransformParent(orig, live, "Move carrier to 大盘");
                orig.name = id.ToString();

                cloneGo.name = id.ToString();
                EnsureContentAttach(cloneGo.transform);
            }

            // 主菜单去掉空 Anchors
            var anchors = menu.Find("Anchors");
            if (anchors != null && anchors.childCount == 0)
            {
                Undo.DestroyObjectImmediate(anchors.gameObject);
            }

            EnsureCarrierViews(live);
            sb.Append("已将 1-12 迁入大盘，并在主菜单留下蓝图克隆。");
            return sb.ToString();
        }

        private static void StripRuntimeExtras(Transform carrier)
        {
            // 蓝图不需要 CarrierView；保留 SpriteRenderer + ContentAttach
            var view = carrier.GetComponent<CarrierView>();
            if (view != null) Undo.DestroyObjectImmediate(view);
        }

        private static void EnsureCarrierViews(Transform live)
        {
            for (var id = 1; id <= 12; id++)
            {
                var child = live.Find(id.ToString());
                if (child == null) continue;
                var view = child.GetComponent<CarrierView>();
                if (view == null) view = Undo.AddComponent<CarrierView>(child.gameObject);
                view.EnsureWired();
            }
        }

        private static string ParentBlueprintContents()
        {
            var sb = new StringBuilder("ParentBlueprintContents:\n");
            foreach (var kv in LayoutPanelElements)
            {
                var root = FindSceneRoot(kv.Key);
                if (root == null)
                {
                    sb.AppendLine("  missing " + kv.Key);
                    continue;
                }

                EnsureAllContentAttaches(root);
                var moved = 0;
                var missing = 0;
                foreach (var panelKv in kv.Value)
                {
                    var panelId = panelKv.Key;
                    var attach = GetContentAttach(root, panelId);
                    if (attach == null)
                    {
                        sb.AppendLine($"  {kv.Key} panel {panelId} no ContentAttach");
                        continue;
                    }

                    foreach (var rawName in panelKv.Value)
                    {
                        var finalName = ApplyCrossPanelRename(rawName, panelId);
                        var src = FindContentInLayout(root, rawName) ?? FindContentInLayout(root, finalName);
                        if (src == null)
                        {
                            missing++;
                            sb.AppendLine($"  MISSING {kv.Key}/{rawName} -> panel {panelId}");
                            continue;
                        }

                        if (src.name != finalName)
                        {
                            Undo.RecordObject(src.gameObject, "Rename content");
                            src.name = finalName;
                        }

                        if (src.parent != attach)
                        {
                            Undo.SetTransformParent(src, attach, "Parent to ContentAttach");
                            moved++;
                        }

                        EnsureBlueprintMarker(src, panelId, GuessFace(kv.Key));
                    }
                }

                var anchors = root.Find("Anchors");
                if (anchors != null)
                {
                    // 残留未映射子物体报出来
                    for (var i = 0; i < anchors.childCount; i++)
                    {
                        sb.AppendLine($"  LEFTOVER Anchors/{anchors.GetChild(i).name} under {kv.Key}");
                    }

                    if (anchors.childCount == 0)
                    {
                        Undo.DestroyObjectImmediate(anchors.gameObject);
                    }
                }

                sb.AppendLine($"  {kv.Key}: moved={moved} missing={missing}");
            }

            return sb.ToString();
        }

        private static string MergeUniqueContentIntoLive(Transform live)
        {
            var sb = new StringBuilder("MergeUniqueContentIntoLive:\n");
            EnsureAllContentAttaches(live);
            var seen = new HashSet<string>();

            // 先登记大盘已有
            for (var id = 1; id <= 12; id++)
            {
                var attach = GetContentAttach(live, id);
                if (attach == null) continue;
                for (var i = 0; i < attach.childCount; i++)
                {
                    seen.Add(attach.GetChild(i).name);
                }
            }

            var added = 0;
            foreach (var rootName in BlueprintRoots)
            {
                var root = FindSceneRoot(rootName);
                if (root == null) continue;
                var face = GuessFace(rootName);

                for (var panelId = 1; panelId <= 12; panelId++)
                {
                    var srcAttach = GetContentAttach(root, panelId);
                    if (srcAttach == null) continue;
                    var dstAttach = GetContentAttach(live, panelId);
                    if (dstAttach == null) continue;

                    for (var i = 0; i < srcAttach.childCount; i++)
                    {
                        var src = srcAttach.GetChild(i);
                        if (seen.Contains(src.name)) continue;

                        var clone = UnityEngine.Object.Instantiate(src.gameObject, dstAttach);
                        Undo.RegisterCreatedObjectUndo(clone, "Live content");
                        clone.name = src.name;
                        // 保持世界位姿
                        clone.transform.position = src.position;
                        clone.transform.rotation = src.rotation;
                        clone.transform.localScale = src.localScale;

                        EnsureLiveMarker(clone.transform, panelId, face);
                        seen.Add(src.name);
                        added++;
                    }
                }
            }

            // 确保大盘已有内容也有 Marker（菜单迁入的）
            for (var panelId = 1; panelId <= 12; panelId++)
            {
                var attach = GetContentAttach(live, panelId);
                if (attach == null) continue;
                for (var i = 0; i < attach.childCount; i++)
                {
                    var child = attach.GetChild(i);
                    EnsureLiveMarker(child, panelId, LivingUiLayoutId.MainMenu);
                }
            }

            sb.AppendLine("  added unique=" + added + " totalUnique=" + seen.Count);
            return sb.ToString();
        }

        private static string EnsureMarkers(Transform live)
        {
            var count = live.GetComponentsInChildren<LivingUiContentMarker>(true).Length;
            return "Live markers=" + count;
        }

        private static string WireDirectorComponents()
        {
            var directors = FindSceneRoot("Directors");
            if (directors == null) return "WireDirector: no Directors";
            var living = directors.Find("LivingUI");
            if (living == null)
            {
                // 可能直接挂在 Directors 子级
                var all = directors.GetComponentsInChildren<LivingUiDirector>(true);
                if (all.Length == 0) return "WireDirector: no LivingUiDirector";
                living = all[0].transform;
            }

            var ctrl = living.GetComponent<LivingUiContentController>();
            if (ctrl == null)
            {
                ctrl = Undo.AddComponent<LivingUiContentController>(living.gameObject);
            }

            var driver = living.GetComponent<LivingUiContentDriver>();
            if (driver == null)
            {
                Undo.AddComponent<LivingUiContentDriver>(living.gameObject);
            }

            return "WireDirector: ContentController+Driver on " + living.name;
        }

        private static string ApplyCrossPanelRename(string rawName, int panelId)
        {
            switch (rawName)
            {
                case "ArmorText":
                    return panelId + "-ArmorText";
                case "AttackText":
                    return panelId + "-AttackText";
                case "HpText":
                    return panelId + "-HpText";
                case "GoldText":
                    return panelId + "-GoldText";
                default:
                    return rawName;
            }
        }

        private static LivingUiLayoutId GuessFace(string rootName)
        {
            for (var i = 0; i < BlueprintRoots.Length && i < LayoutIds.Length; i++)
            {
                if (BlueprintRoots[i] == rootName) return LayoutIds[i];
            }

            return LivingUiLayoutId.MainMenu;
        }

        private static void EnsureAllContentAttaches(Transform root)
        {
            for (var id = 1; id <= 12; id++)
            {
                var carrier = root.Find(id.ToString());
                if (carrier == null) continue;
                EnsureContentAttach(carrier);
            }
        }

        private static Transform EnsureContentAttach(Transform carrier)
        {
            var attach = carrier.Find("ContentAttach");
            if (attach != null) return attach;
            var go = new GameObject("ContentAttach");
            Undo.RegisterCreatedObjectUndo(go, "ContentAttach");
            go.transform.SetParent(carrier, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static Transform GetContentAttach(Transform root, int panelId)
        {
            var carrier = root.Find(panelId.ToString());
            if (carrier == null) return null;
            return EnsureContentAttach(carrier);
        }

        private static Transform FindContentInLayout(Transform root, string name)
        {
            // Prefer Anchors, then any descendant except carrier roots themselves
            var anchors = root.Find("Anchors");
            if (anchors != null)
            {
                for (var i = 0; i < anchors.childCount; i++)
                {
                    if (anchors.GetChild(i).name == name) return anchors.GetChild(i);
                }
            }

            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t.name != name) continue;
                if (t == root) continue;
                if (t.parent == root && int.TryParse(t.name, out _)) continue;
                return t;
            }

            return null;
        }

        private static void EnsureBlueprintMarker(Transform content, int panelId, LivingUiLayoutId face)
        {
            var marker = content.GetComponent<LivingUiContentMarker>();
            if (marker == null) marker = Undo.AddComponent<LivingUiContentMarker>(content.gameObject);
            marker.ApplyAuthored(
                LivingUiContentNames.BaseName(content.name),
                panelId,
                LivingUiContentAnchor.TopLeft,
                face,
                restrictFace: true);
            marker.CaptureAuthoredPoseFromTransform();
            ConvertPoseToTopLeft(marker, content);
            EditorUtility.SetDirty(marker);
        }

        private static void EnsureLiveMarker(Transform content, int panelId, LivingUiLayoutId homeFace)
        {
            var marker = content.GetComponent<LivingUiContentMarker>();
            if (marker == null) marker = Undo.AddComponent<LivingUiContentMarker>(content.gameObject);
            // restrictToFace=false：由 ContentController 按蓝图同名出现登记多构型
            marker.ApplyAuthored(
                LivingUiContentNames.BaseName(content.name),
                panelId,
                LivingUiContentAnchor.TopLeft,
                homeFace,
                restrictFace: false);
            marker.CaptureAuthoredPoseFromTransform();
            ConvertPoseToTopLeft(marker, content);
            EditorUtility.SetDirty(marker);
        }

        private static void ConvertPoseToTopLeft(LivingUiContentMarker marker, Transform content)
        {
            var carrier = content.parent != null ? content.parent.parent : null;
            var skin = carrier != null ? carrier.GetComponent<SpriteRenderer>() : null;
            var size = skin != null ? skin.size : Vector2.one;
            // 原初权威：AuthoredPose 保持中心系；仅钉死 authoringSize。
            marker.EnsureAuthoringSize(size);
        }

        private static Transform FindSceneRoot(string rootName)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!t.gameObject.scene.IsValid()) continue;
                if (t.parent != null) continue;
                if (t.name == rootName) return t;
            }

            return null;
        }
    }
}
