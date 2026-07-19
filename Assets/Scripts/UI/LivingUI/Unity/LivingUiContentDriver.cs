using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 内容层驱动：锚点解算 + 控制器下发的不变/缩放进退场。
    /// </summary>
    [DefaultExecutionOrder(-180)]
    [DisallowMultipleComponent]
    public sealed class LivingUiContentDriver : MonoBehaviour
    {
        [Tooltip("灵动 UI 导演；留空时同物体 GetComponent，缺失则禁用。")]
        [SerializeField] private LivingUiDirector director;

        [Tooltip("构型样板来源；留空时同物体 GetComponent。")]
        [SerializeField] private LivingUiSceneLayoutSource layoutSource;

        [Tooltip("内容控制器；留空时同物体 GetComponent，缺失则按 Marker.faceLayout 退化为单 Face 规则。")]
        [SerializeField] private LivingUiContentController contentController;

        [Tooltip("离场缩至 0 的时长（秒）。")]
        [Min(0.01f)]
        [SerializeField] private float exitDuration = LivingUiContentProjector.DefaultExitDuration;

        [Tooltip("可选显式内容标记列表；留空或空数组时运行时 FindObjectsByType 收集。")]
        [SerializeField] private LivingUiContentMarker[] markers;

        private readonly List<LivingUiContentMarker> _runtimeMarkers = new();
        private LivingUiLayoutId? _syncedPoseLayout;

        private void Awake()
        {
            if (director == null) director = GetComponent<LivingUiDirector>();
            if (layoutSource == null) layoutSource = GetComponent<LivingUiSceneLayoutSource>();
            if (contentController == null) contentController = GetComponent<LivingUiContentController>();
            if (director == null)
            {
                Debug.LogError("[LivingUI] ContentDriver 未找到 LivingUiDirector，已禁用。");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            LivingUiBlueprintPoseSampler.InvalidateCache();
            _syncedPoseLayout = null;
            RefreshMarkers();
        }

        private void Start()
        {
            if (contentController != null) contentController.Rebuild();
            RefreshMarkers();
            ApplyAll();
        }

        private void LateUpdate()
        {
            ApplyAll();
        }

        public void RefreshMarkers()
        {
            _runtimeMarkers.Clear();

            // 只驱动权威大盘下的内容；蓝图构型上的 Marker 仅作摆布参考。
            Transform liveRoot = null;
            if (layoutSource != null)
            {
                try { liveRoot = layoutSource.LiveRoot; }
                catch { /* Capture 尚未完成时忽略 */ }
            }

            if (liveRoot == null)
            {
                var stage = GameObject.Find(LivingUiSceneLayoutSource.LiveRootName);
                if (stage != null) liveRoot = stage.transform;
            }

            if (liveRoot != null)
            {
                var found = liveRoot.GetComponentsInChildren<LivingUiContentMarker>(true);
                for (var i = 0; i < found.Length; i++)
                {
                    if (found[i] != null) _runtimeMarkers.Add(found[i]);
                }
            }

            if (markers == null) return;
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker != null && !_runtimeMarkers.Contains(marker))
                {
                    _runtimeMarkers.Add(marker);
                }
            }
        }

        private void ApplyAll()
        {
            if (director == null) return;
            if (_runtimeMarkers.Count == 0) RefreshMarkers();

            var effective = director.EffectiveLayout;
            var source = director.TransitionSourceLayout;
            var target = director.TransitionTargetLayout;
            var transitioning = director.IsTransitioning;
            var transitionElapsed = director.TransitionElapsed;

            // 停稳或进场：按目标/有效构型蓝图刷新作者位姿，消除迁移偏差。
            // 转场中的 Invariant 不刷新，避免同名跨构型时跳动。
            if (!transitioning)
            {
                SyncAuthoredPosesFromBlueprint(effective);
            }

            for (var i = 0; i < _runtimeMarkers.Count; i++)
            {
                var marker = _runtimeMarkers[i];
                if (marker == null) continue;

                var binding = marker.ToBinding();
                var phase = ResolvePhase(marker, binding, source, target, effective, transitioning);
                if (phase == LivingUiContentPhase.Hidden)
                {
                    SetMarkerVisible(marker, false);
                    continue;
                }

                if (transitioning && phase == LivingUiContentPhase.Entering)
                {
                    TryApplyBlueprintPose(marker, target);
                    binding = marker.ToBinding();
                }
                else if (transitioning && phase == LivingUiContentPhase.Exiting)
                {
                    TryApplyBlueprintPose(marker, source);
                    binding = marker.ToBinding();
                }

                if (layoutSource == null
                    || !layoutSource.Carriers.TryGetValue(binding.CarrierId, out var skin)
                    || skin == null)
                {
                    SetMarkerVisible(marker, true);
                    continue;
                }

                var mode = ResolveMode(marker, binding, source, target, transitioning);
                var authoringSize = ResolveAuthoringSize(binding, target);
                LivingUiContentProjection projection;

                if (mode == LivingUiContentMotionMode.Invariant
                    || phase == LivingUiContentPhase.Stable)
                {
                    projection = LivingUiContentProjector.ProjectInvariant(
                        binding.Anchor, binding.LocalPose, skin.size);
                }
                else if (phase == LivingUiContentPhase.Entering
                         && layoutSource.Snapshots.TryGetValue(source, out var sourceLayout)
                         && layoutSource.Snapshots.TryGetValue(target, out var targetLayout))
                {
                    var sourceTerminal = sourceLayout.GetTerminal(binding.CarrierId);
                    var targetTerminal = targetLayout.GetTerminal(binding.CarrierId);
                    var makespan = director.TransitionMakespan;
                    var timeProgress = makespan > 0.0001f
                        ? Mathf.Clamp01(transitionElapsed / makespan)
                        : 1f;
                    var currentPos = new Vector2(skin.transform.position.x, skin.transform.position.y);
                    var enter = LivingUiContentProjector.ComputeEnterProgress(
                        sourceTerminal.Size,
                        targetTerminal.Size,
                        skin.size,
                        sourceTerminal.Position,
                        targetTerminal.Position,
                        currentPos,
                        timeProgress);
                    projection = LivingUiContentProjector.ProjectScale(
                        binding.Anchor, binding.LocalPose, targetTerminal.Size, enter);
                }
                else
                {
                    var refSize = authoringSize;
                    if (layoutSource.Snapshots.TryGetValue(source, out var exitSource))
                    {
                        refSize = exitSource.GetTerminal(binding.CarrierId).Size;
                    }

                    var exitFactor = LivingUiContentProjector.ComputeExitScaleFactor(
                        transitionElapsed, exitDuration);
                    projection = LivingUiContentProjector.ProjectScale(
                        binding.Anchor, binding.LocalPose, refSize, exitFactor);
                }

                var scale = projection.LocalScale;
                var visible = projection.Visible
                    && scale.x > LivingUiContentProjector.VisibleScaleEpsilon
                    && scale.y > LivingUiContentProjector.VisibleScaleEpsilon;

                SetMarkerVisible(marker, visible);
                if (!visible) continue;

                marker.transform.localPosition = projection.LocalPosition;
                marker.transform.localScale = scale;
            }
        }

        private void SyncAuthoredPosesFromBlueprint(LivingUiLayoutId layout)
        {
            if (_syncedPoseLayout == layout) return;
            _syncedPoseLayout = layout;
            for (var i = 0; i < _runtimeMarkers.Count; i++)
            {
                var marker = _runtimeMarkers[i];
                if (marker == null) continue;
                TryApplyBlueprintPose(marker, layout);
            }
        }

        private static void TryApplyBlueprintPose(LivingUiContentMarker marker, LivingUiLayoutId layout)
        {
            var contentName = string.IsNullOrEmpty(marker.ContentId) ? marker.name : marker.ContentId;
            if (!LivingUiBlueprintPoseSampler.TrySample(
                    layout,
                    marker.CarrierId,
                    contentName,
                    out var localPosition,
                    out var localScale,
                    out var carrierSize)
                && contentName != marker.name
                && !LivingUiBlueprintPoseSampler.TrySample(
                    layout,
                    marker.CarrierId,
                    marker.name,
                    out localPosition,
                    out localScale,
                    out carrierSize))
            {
                return;
            }

            marker.transform.localPosition = localPosition;
            marker.transform.localScale = localScale;
            marker.ApplyAuthored(
                contentName,
                marker.CarrierId,
                LivingUiContentAnchor.TopLeft,
                layout,
                restrictFace: false,
                envelope: default,
                authoring: carrierSize);
            marker.CaptureAuthoredPoseFromTransform();
            marker.ConvertCenterLocalToAnchorOffset(carrierSize);
        }

        private LivingUiContentPhase ResolvePhase(
            LivingUiContentMarker marker,
            LivingUiContentBinding binding,
            LivingUiLayoutId source,
            LivingUiLayoutId target,
            LivingUiLayoutId effective,
            bool transitioning)
        {
            if (contentController != null)
            {
                return contentController.ResolvePhase(marker, source, target, effective, transitioning);
            }

            return LivingUiContentPolicy.EvaluatePhase(binding, source, target, effective, transitioning);
        }

        private LivingUiContentMotionMode ResolveMode(
            LivingUiContentMarker marker,
            LivingUiContentBinding binding,
            LivingUiLayoutId source,
            LivingUiLayoutId target,
            bool transitioning)
        {
            if (contentController != null)
            {
                return contentController.ResolveMotionMode(marker, source, target, transitioning);
            }

            if (!binding.FaceLayout.HasValue)
            {
                return LivingUiContentPolicy.ResolveMotionMode(true, true, transitioning);
            }

            var face = binding.FaceLayout.Value;
            return LivingUiContentPolicy.ResolveMotionMode(
                face == source, face == target, transitioning);
        }

        private Vector2 ResolveAuthoringSize(LivingUiContentBinding binding, LivingUiLayoutId fallbackLayout)
        {
            if (binding.AuthoringSize.x > 0f && binding.AuthoringSize.y > 0f)
            {
                return binding.AuthoringSize;
            }

            if (layoutSource != null
                && binding.FaceLayout.HasValue
                && layoutSource.Snapshots.TryGetValue(binding.FaceLayout.Value, out var faceLayout))
            {
                return faceLayout.GetTerminal(binding.CarrierId).Size;
            }

            if (layoutSource != null
                && layoutSource.Snapshots.TryGetValue(fallbackLayout, out var fallback))
            {
                return fallback.GetTerminal(binding.CarrierId).Size;
            }

            return Vector2.one;
        }

        private static void SetMarkerVisible(LivingUiContentMarker marker, bool visible)
        {
            if (marker.gameObject.activeSelf != visible)
            {
                marker.gameObject.SetActive(visible);
            }
        }
    }
}
