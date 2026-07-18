using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 内容层驱动：反应式投影 + 缩放进/退场（无 SetActive 瞬闪）。
    /// RigidTravel 仅写退场倍率；进场缩放请用 BoundaryReactive。
    /// </summary>
    [DefaultExecutionOrder(-180)]
    [DisallowMultipleComponent]
    public sealed class LivingUiContentDriver : MonoBehaviour
    {
        [Tooltip("灵动 UI 导演；留空时同物体 GetComponent，缺失则禁用。")]
        [SerializeField] private LivingUiDirector director;

        [Tooltip("构型样板来源；留空时同物体 GetComponent。反应式投影需读载体 live size。")]
        [SerializeField] private LivingUiSceneLayoutSource layoutSource;

        [Tooltip("离场缩至 0 的时长（秒）；进场仍随载体 size 自然展开。")]
        [Min(0.01f)]
        [SerializeField] private float exitDuration = LivingUiContentProjector.DefaultExitDuration;

        [Tooltip("可选显式内容标记列表；留空或空数组时运行时 FindObjectsByType 收集。")]
        [SerializeField] private LivingUiContentMarker[] markers;

        private readonly List<LivingUiContentMarker> _runtimeMarkers = new();
        private readonly Dictionary<int, Vector2> _baselineCache = new();

        private void Awake()
        {
            if (director == null) director = GetComponent<LivingUiDirector>();
            if (layoutSource == null) layoutSource = GetComponent<LivingUiSceneLayoutSource>();
            if (director == null)
            {
                Debug.LogError("[LivingUI] ContentDriver 未找到 LivingUiDirector，已禁用。");
                enabled = false;
            }
        }

        private void OnEnable()
        {
            RefreshMarkers();
        }

        private void Start()
        {
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
            var found = FindObjectsByType<LivingUiContentMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null) _runtimeMarkers.Add(found[i]);
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

            for (var i = 0; i < _runtimeMarkers.Count; i++)
            {
                var marker = _runtimeMarkers[i];
                if (marker == null) continue;

                var binding = marker.ToBinding();
                var phase = LivingUiContentPolicy.EvaluatePhase(
                    binding, source, target, effective, transitioning);

                if (phase == LivingUiContentPhase.Hidden)
                {
                    SetMarkerVisible(marker, false);
                    continue;
                }

                if (layoutSource == null
                    || !layoutSource.Carriers.TryGetValue(binding.CarrierId, out var skin)
                    || skin == null)
                {
                    SetMarkerVisible(marker, true);
                    continue;
                }

                var baseline = ResolveBaseline(binding);
                LivingUiContentProjection projection;
                if (phase == LivingUiContentPhase.Entering
                    && binding.FollowPolicy == LivingUiContentFollowPolicy.BoundaryReactive
                    && layoutSource.Snapshots.TryGetValue(source, out var sourceLayout)
                    && layoutSource.Snapshots.TryGetValue(target, out var targetLayout))
                {
                    var sourceSize = sourceLayout.GetTerminal(binding.CarrierId).Size;
                    var targetSize = targetLayout.GetTerminal(binding.CarrierId).Size;
                    projection = LivingUiContentProjector.ProjectBoundaryReactiveEnter(
                        binding.LocalPose,
                        sourceSize,
                        targetSize,
                        skin.size,
                        binding.StaggerSpan);
                }
                else
                {
                    projection = LivingUiContentProjector.Project(
                        binding.FollowPolicy,
                        binding.LocalPose,
                        baseline,
                        skin.size,
                        binding.FollowEdge,
                        binding.StaggerSpan);
                }

                var scale = projection.LocalScale;
                if (phase == LivingUiContentPhase.Exiting)
                {
                    var exitFactor = LivingUiContentProjector.ComputeExitScaleFactor(
                        transitionElapsed, exitDuration);
                    scale = new Vector3(
                        scale.x * exitFactor,
                        scale.y * exitFactor,
                        scale.z * exitFactor);
                }

                var visible = projection.Visible
                    && scale.x > LivingUiContentProjector.VisibleScaleEpsilon
                    && scale.y > LivingUiContentProjector.VisibleScaleEpsilon;

                SetMarkerVisible(marker, visible);
                if (!visible) continue;

                marker.transform.localPosition = projection.LocalPosition;
                marker.transform.localScale = scale;
            }
        }

        private static void SetMarkerVisible(LivingUiContentMarker marker, bool visible)
        {
            if (marker.gameObject.activeSelf != visible)
            {
                marker.gameObject.SetActive(visible);
            }
        }

        private Vector2 ResolveBaseline(LivingUiContentBinding binding)
        {
            if (binding.BaselineSize.x > 0f && binding.BaselineSize.y > 0f)
            {
                return binding.BaselineSize;
            }

            if (_baselineCache.TryGetValue(binding.CarrierId, out var cached))
            {
                return cached;
            }

            if (layoutSource != null
                && layoutSource.Snapshots.TryGetValue(LivingUiLayoutId.MainMenu, out var mainMenu))
            {
                var terminal = mainMenu.GetTerminal(binding.CarrierId);
                _baselineCache[binding.CarrierId] = terminal.Size;
                return terminal.Size;
            }

            return Vector2.one;
        }
    }
}
