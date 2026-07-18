using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    /// <summary>
    /// 内容层驱动：显隐 + 反应式投影（BoundaryReactive / PartialFollow）。
    /// RigidTravel 由父子挂接完成，不写 Transform。
    /// </summary>
    [DefaultExecutionOrder(-180)]
    [DisallowMultipleComponent]
    public sealed class LivingUiContentDriver : MonoBehaviour
    {
        [Tooltip("灵动 UI 导演；留空时同物体 GetComponent，缺失则禁用。")]
        [SerializeField] private LivingUiDirector director;

        [Tooltip("构型样板来源；留空时同物体 GetComponent。反应式投影需读载体 live size。")]
        [SerializeField] private LivingUiSceneLayoutSource layoutSource;

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
            var committed = director.CommittedLayout;
            var transitioning = director.IsTransitioning;
            for (var i = 0; i < _runtimeMarkers.Count; i++)
            {
                var marker = _runtimeMarkers[i];
                if (marker == null) continue;

                var binding = marker.ToBinding();
                var policyVisible = LivingUiContentPolicy.EvaluateVisible(
                    binding, effective, committed, transitioning);

                if (!policyVisible)
                {
                    if (marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                    continue;
                }

                if (binding.FollowPolicy == LivingUiContentFollowPolicy.RigidTravel)
                {
                    if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
                    continue;
                }

                if (layoutSource == null
                    || !layoutSource.Carriers.TryGetValue(binding.CarrierId, out var skin)
                    || skin == null)
                {
                    if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
                    continue;
                }

                var baseline = ResolveBaseline(binding);
                var projection = LivingUiContentProjector.Project(
                    binding.FollowPolicy,
                    binding.LocalPose,
                    baseline,
                    skin.size,
                    binding.FollowEdge,
                    binding.StaggerSpan);

                var visible = policyVisible && projection.Visible;
                if (marker.gameObject.activeSelf != visible)
                {
                    marker.gameObject.SetActive(visible);
                }

                if (!visible) continue;

                marker.transform.localPosition = projection.LocalPosition;
                marker.transform.localScale = projection.LocalScale;
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
