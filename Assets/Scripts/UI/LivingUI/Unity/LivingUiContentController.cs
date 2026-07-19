using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 内容控制器：持有构型→元素映射；按核心规则下发 Invariant/Scale。
    /// 同名（基础名）元素在大盘中唯一；按蓝图构型中是否出现同基础名登记多构型 → Invariant。
    /// 蓝图另提供每构型锚点（定性），供 Driver 投影；不提供位姿。
    /// </summary>
    [DefaultExecutionOrder(-190)]
    [DisallowMultipleComponent]
    public sealed class LivingUiContentController : MonoBehaviour
    {
        [Serializable]
        private struct LayoutContentEntry
        {
            [Tooltip("该构型下登记的内容标记；可跨构型重复引用同一实例。")]
            public LivingUiLayoutId LayoutId;

            [Tooltip("本构型出现的内容；留空则运行时按同名蓝图出现登记。")]
            public LivingUiContentMarker[] Markers;
        }

        [Serializable]
        private struct BlueprintRootRef
        {
            public LivingUiLayoutId LayoutId;
            public Transform Root;
        }

        [Tooltip("构型→元素映射；数组留空时按大盘 Marker 基础名与蓝图同名出现自动构建。")]
        [SerializeField] private LayoutContentEntry[] layoutContents;

        [Tooltip("运行时权威大盘根；留空时由 SceneLayoutSource.Capture 注入。")]
        [SerializeField] private Transform liveRoot;

        [Tooltip("蓝图构型根；留空时由 SceneLayoutSource.Capture 注入。")]
        [SerializeField] private BlueprintRootRef[] blueprintRoots;

        private readonly Dictionary<LivingUiLayoutId, HashSet<LivingUiContentMarker>> _map = new();
        private readonly Dictionary<LivingUiContentMarker, HashSet<LivingUiLayoutId>> _markerLayouts = new();
        private readonly Dictionary<(LivingUiLayoutId Layout, string BaseId), LivingUiContentAnchor> _anchors = new();
        private bool _built;

        private void Awake()
        {
            Rebuild();
        }

        private void OnEnable()
        {
            Rebuild();
        }

        /// <summary>
        /// 由 SceneLayoutSource.Capture 调用：仅驱动大盘下 Marker，按各蓝图是否含同名内容登记构型。
        /// </summary>
        public void RebuildByNamePresence(Transform live, IReadOnlyList<(LivingUiLayoutId LayoutId, Transform Root)> blueprints)
        {
            liveRoot = live;
            if (blueprints == null || blueprints.Count == 0)
            {
                blueprintRoots = Array.Empty<BlueprintRootRef>();
            }
            else
            {
                blueprintRoots = new BlueprintRootRef[blueprints.Count];
                for (var i = 0; i < blueprints.Count; i++)
                {
                    blueprintRoots[i] = new BlueprintRootRef
                    {
                        LayoutId = blueprints[i].LayoutId,
                        Root = blueprints[i].Root,
                    };
                }
            }

            Rebuild();
        }

        public void Rebuild()
        {
            _map.Clear();
            _markerLayouts.Clear();
            _anchors.Clear();
            _built = false;

            if (layoutContents != null && layoutContents.Length > 0)
            {
                for (var i = 0; i < layoutContents.Length; i++)
                {
                    var entry = layoutContents[i];
                    if (entry.Markers == null) continue;
                    for (var m = 0; m < entry.Markers.Length; m++)
                    {
                        Register(entry.LayoutId, entry.Markers[m]);
                    }
                }
            }

            if (_markerLayouts.Count == 0 && liveRoot != null && blueprintRoots != null && blueprintRoots.Length > 0)
            {
                BuildFromNamePresence();
            }

            if (_markerLayouts.Count == 0)
            {
                AutoRegisterFromMarkers();
            }

            _built = true;
        }

        private void BuildFromNamePresence()
        {
            var markers = liveRoot.GetComponentsInChildren<LivingUiContentMarker>(true);
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null) continue;
                var baseName = LivingUiContentNames.BaseName(
                    string.IsNullOrEmpty(marker.ContentId) ? marker.name : marker.ContentId);
                for (var b = 0; b < blueprintRoots.Length; b++)
                {
                    var bp = blueprintRoots[b];
                    if (bp.Root == null) continue;
                    var found = FindNamedContent(bp.Root, baseName);
                    if (found == null) continue;

                    Register(bp.LayoutId, marker);
                    var bpMarker = found.GetComponent<LivingUiContentMarker>();
                    var anchor = bpMarker != null ? bpMarker.Anchor : LivingUiContentAnchor.TopLeft;
                    _anchors[(bp.LayoutId, baseName)] = anchor;
                }
            }
        }

        /// <summary>取某构型下该内容的蓝图锚点；无登记时回退 marker.Anchor。</summary>
        public bool TryGetLayoutAnchor(
            LivingUiContentMarker marker,
            LivingUiLayoutId layout,
            out LivingUiContentAnchor anchor)
        {
            EnsureBuilt();
            anchor = marker != null ? marker.Anchor : LivingUiContentAnchor.TopLeft;
            if (marker == null) return false;

            var baseName = LivingUiContentNames.BaseName(
                string.IsNullOrEmpty(marker.ContentId) ? marker.name : marker.ContentId);
            if (_anchors.TryGetValue((layout, baseName), out var fromBp))
            {
                anchor = fromBp;
                return true;
            }

            // 懒采样：显式 layoutContents 路径或缓存未命中时从蓝图补一次
            if (LivingUiBlueprintPoseSampler.TrySampleQualitative(
                    layout, marker.CarrierId, baseName, out fromBp))
            {
                _anchors[(layout, baseName)] = fromBp;
                anchor = fromBp;
                return true;
            }

            return false;
        }

        public LivingUiContentAnchor ResolveLayoutAnchor(
            LivingUiContentMarker marker,
            LivingUiLayoutId layout)
        {
            TryGetLayoutAnchor(marker, layout, out var anchor);
            return anchor;
        }

        private static Transform FindNamedContent(Transform root, string contentName)
        {
            if (string.IsNullOrEmpty(contentName) || root == null) return null;
            var want = LivingUiContentNames.BaseName(contentName);
            var all = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == root) continue;
                if (!LivingUiContentNames.BaseNameEquals(t.name, want)) continue;
                if (IsPanelCarrierName(LivingUiContentNames.BaseName(t.name))) continue;
                // 只认挂在面板 ContentAttach（或历史 Anchors）下的内容
                if (t.parent == null) continue;
                var parentName = t.parent.name;
                if (parentName != "ContentAttach" && parentName != "Anchors") continue;
                return t;
            }

            return null;
        }

        private static bool IsPanelCarrierName(string name)
        {
            return name.Length <= 2 && int.TryParse(name, out var id) && id >= 1 && id <= 12;
        }

        private void AutoRegisterFromMarkers()
        {
            var found = liveRoot != null
                ? liveRoot.GetComponentsInChildren<LivingUiContentMarker>(true)
                : FindObjectsByType<LivingUiContentMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                var marker = found[i];
                if (marker == null) continue;
                if (!marker.RestrictToFace)
                {
                    foreach (LivingUiLayoutId layout in Enum.GetValues(typeof(LivingUiLayoutId)))
                    {
                        Register(layout, marker);
                    }
                }
                else
                {
                    Register(marker.FaceLayout, marker);
                }
            }
        }

        private void Register(LivingUiLayoutId layout, LivingUiContentMarker marker)
        {
            if (marker == null) return;
            if (!_map.TryGetValue(layout, out var set))
            {
                set = new HashSet<LivingUiContentMarker>();
                _map[layout] = set;
            }

            set.Add(marker);

            if (!_markerLayouts.TryGetValue(marker, out var layouts))
            {
                layouts = new HashSet<LivingUiLayoutId>();
                _markerLayouts[marker] = layouts;
            }

            layouts.Add(layout);
        }

        public bool IsPresent(LivingUiContentMarker marker, LivingUiLayoutId layout)
        {
            EnsureBuilt();
            return _map.TryGetValue(layout, out var set) && set.Contains(marker);
        }

        public LivingUiContentMotionMode ResolveMotionMode(
            LivingUiContentMarker marker,
            LivingUiLayoutId source,
            LivingUiLayoutId target,
            bool isTransitioning)
        {
            EnsureBuilt();
            return LivingUiContentPolicy.ResolveMotionMode(
                IsPresent(marker, source),
                IsPresent(marker, target),
                isTransitioning);
        }

        public LivingUiContentPhase ResolvePhase(
            LivingUiContentMarker marker,
            LivingUiLayoutId source,
            LivingUiLayoutId target,
            LivingUiLayoutId effective,
            bool isTransitioning)
        {
            EnsureBuilt();
            return LivingUiContentPolicy.EvaluatePhase(
                IsPresent(marker, source),
                IsPresent(marker, target),
                IsPresent(marker, effective),
                isTransitioning);
        }

        private void EnsureBuilt()
        {
            if (!_built) Rebuild();
        }
    }
}
