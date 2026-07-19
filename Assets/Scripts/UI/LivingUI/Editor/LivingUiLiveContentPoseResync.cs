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
    /// 把大盘 live 内容的局部位姿按蓝图同面板同名对象重写，消除迁移时 worldPositionStays 造成的系统性偏差。
    /// </summary>
    public static class LivingUiLiveContentPoseResync
    {
        private const string LiveRootName = "大盘";

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

        // 同名多构型时优先取该构型的局部位姿（HUD 以战斗为准）
        private static readonly string[] PosePreferenceOrder =
        {
            "大盘构型2-核心战斗面板",
            "大盘构型5-房间基础面板",
            "大盘构型4-打开卡组视图",
            "大盘构型3-选择奖励",
            "大盘构型1-人物选择",
            "大盘构型0-主菜单",
            "大盘构型6-预备待定",
        };

        [MenuItem("TableNine/LivingUI/Resync Live Content Poses From Blueprints")]
        public static void Resync()
        {
            var live = FindSceneRoot(LiveRootName);
            if (live == null)
            {
                Debug.LogError("[LivingUI] 未找到大盘");
                return;
            }

            var report = new StringBuilder();
            var synced = 0;
            var missing = 0;

            // 临时激活以便取样
            var prevActive = new Dictionary<Transform, bool>();
            foreach (var name in BlueprintRoots)
            {
                var root = FindSceneRoot(name);
                if (root == null) continue;
                prevActive[root] = root.gameObject.activeSelf;
                root.gameObject.SetActive(true);
            }

            prevActive[live] = live.gameObject.activeSelf;
            live.gameObject.SetActive(true);

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            for (var panelId = 1; panelId <= 12; panelId++)
            {
                var liveCarrier = live.Find(panelId.ToString());
                if (liveCarrier == null) continue;
                var liveAttach = liveCarrier.Find("ContentAttach");
                if (liveAttach == null) continue;

                for (var i = 0; i < liveAttach.childCount; i++)
                {
                    var liveContent = liveAttach.GetChild(i);
                    if (!TryFindBlueprintSource(panelId, liveContent.name, out var bpContent, out var bpCarrier, out var bpRootName))
                    {
                        missing++;
                        report.AppendLine($"MISSING blueprint for panel{panelId}/{liveContent.name}");
                        continue;
                    }

                    Undo.RecordObject(liveContent, "Resync live content pose");
                    liveContent.localPosition = bpContent.localPosition;
                    liveContent.localRotation = bpContent.localRotation;
                    liveContent.localScale = bpContent.localScale;

                    var marker = liveContent.GetComponent<LivingUiContentMarker>();
                    if (marker == null)
                    {
                        marker = Undo.AddComponent<LivingUiContentMarker>(liveContent.gameObject);
                    }

                    var skin = bpCarrier.GetComponent<SpriteRenderer>();
                    var authoringSize = skin != null ? skin.size : Vector2.one;
                    marker.ApplyAuthored(
                        liveContent.name,
                        panelId,
                        LivingUiContentAnchor.TopLeft,
                        GuessFace(bpRootName),
                        restrictFace: false,
                        envelope: default,
                        authoring: authoringSize);
                    marker.CaptureAuthoredPoseFromTransform();
                    marker.ConvertCenterLocalToAnchorOffset(authoringSize);
                    EditorUtility.SetDirty(marker);
                    EditorUtility.SetDirty(liveContent.gameObject);

                    synced++;
                    report.AppendLine(
                        $"OK panel{panelId}/{liveContent.name} <- {bpRootName} lp={bpContent.localPosition}");
                }
            }

            // 恢复激活：仅大盘开
            foreach (var kv in prevActive)
            {
                if (kv.Key == null) continue;
                kv.Key.gameObject.SetActive(kv.Key == live);
            }

            live.gameObject.SetActive(true);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[LivingUI] Resync poses done synced={synced} missing={missing}\n{report}");
        }

        private static bool TryFindBlueprintSource(
            int panelId,
            string contentName,
            out Transform bpContent,
            out Transform bpCarrier,
            out string bpRootName)
        {
            bpContent = null;
            bpCarrier = null;
            bpRootName = null;

            // 先按偏好序
            for (var p = 0; p < PosePreferenceOrder.Length; p++)
            {
                if (TryGetInRoot(PosePreferenceOrder[p], panelId, contentName, out bpContent, out bpCarrier))
                {
                    bpRootName = PosePreferenceOrder[p];
                    return true;
                }
            }

            for (var p = 0; p < BlueprintRoots.Length; p++)
            {
                if (TryGetInRoot(BlueprintRoots[p], panelId, contentName, out bpContent, out bpCarrier))
                {
                    bpRootName = BlueprintRoots[p];
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetInRoot(
            string rootName,
            int panelId,
            string contentName,
            out Transform bpContent,
            out Transform bpCarrier)
        {
            bpContent = null;
            bpCarrier = null;
            var root = FindSceneRoot(rootName);
            if (root == null) return false;
            bpCarrier = root.Find(panelId.ToString());
            if (bpCarrier == null) return false;
            var attach = bpCarrier.Find("ContentAttach");
            if (attach == null) return false;
            for (var i = 0; i < attach.childCount; i++)
            {
                var ch = attach.GetChild(i);
                if (ch.name != contentName) continue;
                bpContent = ch;
                return true;
            }

            return false;
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
