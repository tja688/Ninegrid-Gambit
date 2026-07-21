#if UNITY_EDITOR
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
    /// 按构型 0→6「第一次出现」烘焙大盘原初中心系位姿，并加（原初元素）后缀。
    /// </summary>
    public static class LivingUiPrimordialPoseInit
    {
        private const string LiveRootName = "大盘";

        private static readonly (LivingUiLayoutId Id, string RootName)[] LayoutOrder =
        {
            (LivingUiLayoutId.MainMenu, "大盘构型0-主菜单"),
            (LivingUiLayoutId.CharacterChoice, "大盘构型1-人物选择"),
            (LivingUiLayoutId.Battle, "大盘构型2-核心战斗面板"),
            (LivingUiLayoutId.RewardChoice, "大盘构型3-选择奖励"),
            (LivingUiLayoutId.DeckPreview, "大盘构型4-打开卡组视图"),
            (LivingUiLayoutId.Room, "大盘构型5-房间基础面板"),
            (LivingUiLayoutId.Route, "大盘构型6-预备待定"),
        };

        [MenuItem("LivingUI/Init Primordial Poses From First Blueprint Occurrence")]
        public static void Init()
        {
            var live = FindSceneRoot(LiveRootName);
            if (live == null)
            {
                Debug.LogError("[LivingUI] 未找到大盘");
                return;
            }

            var report = new StringBuilder();
            var initialized = 0;
            var skipped = 0;
            var missingLive = 0;
            var seen = new HashSet<string>();

            var prevActive = new Dictionary<Transform, bool>();
            for (var i = 0; i < LayoutOrder.Length; i++)
            {
                var root = FindSceneRoot(LayoutOrder[i].RootName);
                if (root == null) continue;
                prevActive[root] = root.gameObject.activeSelf;
                root.gameObject.SetActive(true);
            }

            prevActive[live] = live.gameObject.activeSelf;
            live.gameObject.SetActive(true);

            Undo.IncrementCurrentGroup();
            var group = Undo.GetCurrentGroup();

            for (var li = 0; li < LayoutOrder.Length; li++)
            {
                var layoutId = LayoutOrder[li].Id;
                var bpRoot = FindSceneRoot(LayoutOrder[li].RootName);
                if (bpRoot == null)
                {
                    report.AppendLine($"SKIP missing blueprint {LayoutOrder[li].RootName}");
                    continue;
                }

                for (var panelId = 1; panelId <= 12; panelId++)
                {
                    var bpCarrier = bpRoot.Find(panelId.ToString());
                    if (bpCarrier == null) continue;
                    var bpAttach = bpCarrier.Find("ContentAttach");
                    if (bpAttach == null) continue;

                    for (var c = 0; c < bpAttach.childCount; c++)
                    {
                        var bpContent = bpAttach.GetChild(c);
                        var baseName = LivingUiContentNames.BaseName(bpContent.name);
                        if (string.IsNullOrEmpty(baseName)) continue;
                        if (IsPanelCarrierName(baseName)) continue;

                        // 蓝图定性：确保有 Marker（锚点可后改）
                        EnsureBlueprintMarker(bpContent, panelId, layoutId);

                        if (!seen.Add(baseName))
                        {
                            skipped++;
                            continue;
                        }

                        var liveContent = FindLiveContent(live, panelId, baseName);
                        if (liveContent == null)
                        {
                            // 同基础名可能挂在其他面板（不应发生）；全盘搜一次
                            liveContent = FindLiveContentAnyPanel(live, baseName);
                        }

                        if (liveContent == null)
                        {
                            missingLive++;
                            report.AppendLine(
                                $"MISSING live for first={LayoutOrder[li].RootName} panel{panelId}/{baseName}");
                            continue;
                        }

                        var skin = bpCarrier.GetComponent<SpriteRenderer>();
                        var authoringSize = skin != null ? skin.size : Vector2.one;

                        Undo.RecordObject(liveContent.gameObject, "Init primordial pose");
                        Undo.RecordObject(liveContent, "Init primordial pose transform");

                        liveContent.localPosition = bpContent.localPosition;
                        liveContent.localRotation = bpContent.localRotation;
                        liveContent.localScale = bpContent.localScale == Vector3.zero
                            ? Vector3.one
                            : bpContent.localScale;

                        var primordialName = LivingUiContentNames.EnsurePrimordialName(baseName);
                        if (liveContent.name != primordialName)
                        {
                            liveContent.name = primordialName;
                        }

                        var marker = liveContent.GetComponent<LivingUiContentMarker>();
                        if (marker == null)
                        {
                            marker = Undo.AddComponent<LivingUiContentMarker>(liveContent.gameObject);
                        }

                        Undo.RecordObject(marker, "Init primordial marker");
                        marker.ApplyAuthored(
                            baseName,
                            panelId,
                            LivingUiContentAnchor.TopLeft,
                            layoutId,
                            restrictFace: false,
                            envelope: default,
                            authoring: authoringSize);
                        marker.CaptureAuthoredPoseFromTransform();
                        marker.EnsureAuthoringSize(authoringSize);
                        EditorUtility.SetDirty(marker);
                        EditorUtility.SetDirty(liveContent.gameObject);

                        initialized++;
                        report.AppendLine(
                            $"INIT {primordialName} <- {LayoutOrder[li].RootName}/panel{panelId} " +
                            $"lp={bpContent.localPosition} size={authoringSize}");
                    }
                }
            }

            // 恢复：仅大盘激活
            foreach (var kv in prevActive)
            {
                if (kv.Key == null) continue;
                kv.Key.gameObject.SetActive(kv.Key == live);
            }

            live.gameObject.SetActive(true);
            Undo.CollapseUndoOperations(group);

            var scene = live.gameObject.scene;
            if (scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log(
                $"[LivingUI] Primordial init done. initialized={initialized} " +
                $"laterOccurrencesSkipped={skipped} missingLive={missingLive}\n{report}");
        }

        private static void EnsureBlueprintMarker(Transform content, int panelId, LivingUiLayoutId face)
        {
            var marker = content.GetComponent<LivingUiContentMarker>();
            if (marker == null) marker = Undo.AddComponent<LivingUiContentMarker>(content.gameObject);
            var baseName = LivingUiContentNames.BaseName(content.name);
            marker.ApplyAuthored(
                baseName,
                panelId,
                marker.Anchor,
                face,
                restrictFace: true);
            // 蓝图位姿仅视觉参考，不作为 runtime 权威；仍捕获便于编辑器预览
            marker.CaptureAuthoredPoseFromTransform();
            var carrier = content.parent != null ? content.parent.parent : null;
            var skin = carrier != null ? carrier.GetComponent<SpriteRenderer>() : null;
            marker.EnsureAuthoringSize(skin != null ? skin.size : Vector2.one);
            EditorUtility.SetDirty(marker);
        }

        private static Transform FindLiveContent(Transform live, int panelId, string baseName)
        {
            var carrier = live.Find(panelId.ToString());
            if (carrier == null) return null;
            var attach = carrier.Find("ContentAttach");
            if (attach == null) return null;
            for (var i = 0; i < attach.childCount; i++)
            {
                var child = attach.GetChild(i);
                if (LivingUiContentNames.BaseNameEquals(child.name, baseName)) return child;
            }

            return null;
        }

        private static Transform FindLiveContentAnyPanel(Transform live, string baseName)
        {
            for (var panelId = 1; panelId <= 12; panelId++)
            {
                var found = FindLiveContent(live, panelId, baseName);
                if (found != null) return found;
            }

            return null;
        }

        private static bool IsPanelCarrierName(string name)
        {
            return name.Length <= 2 && int.TryParse(name, out var id) && id >= 1 && id <= 12;
        }

        private static Transform FindSceneRoot(string rootName)
        {
            var all = Resources.FindObjectsOfTypeAll<Transform>();
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (!t.gameObject.scene.IsValid()) continue;
                if (t.parent != null) continue;
                if (t.name != rootName) continue;
                return t;
            }

            return null;
        }
    }
}
#endif
