using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.LivingUI.Unity
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class LivingUiDirector : MonoBehaviour
    {
        [Tooltip("构型样板来源；留空时运行时通过同物体 GetComponent 自动装配，缺失则报错。")]
        [SerializeField] private LivingUiSceneLayoutSource layoutSource;

        [Tooltip("舞台相机；留空时运行时自动取 Camera.main，缺失则报错。")]
        [SerializeField] private Camera stageCamera;

        [Tooltip("全局播放速度倍率；运行中可调整，所有卡农偏移和曲线时长按同一倍率推进。")]
        [Min(0.05f)]
        [SerializeField] private float playbackSpeed = 1f;

        [Tooltip("转场风格参数；控制基础时长、距离增量、卡农跨度、出入画时差与尺寸边界。")]
        [SerializeField] private LivingUiTransitionStyle transitionStyle = new();

        private readonly TransitionPlanner _planner = new();
        private readonly LivingUiTransitionPlayer _player = new();
        private LivingUiLayoutId _committedLayout = LivingUiLayoutId.MainMenu;
        private LivingUiLayoutId? _previewLayout;
        private Rect _stageBounds;

        public LivingUiLayoutId CommittedLayout => _committedLayout;
        public LivingUiLayoutId EffectiveLayout => _previewLayout ?? _committedLayout;
        public LivingUiLayoutId? PreviewLayout => _previewLayout;
        public float PlaybackSpeed { get => playbackSpeed; set => playbackSpeed = Mathf.Max(0.05f, value); }
        public Rect StageBounds => _stageBounds;
        public int ActiveGeneration => _player.ActivePlan?.Generation ?? 0;
        public bool IsTransitioning => _player.IsPlaying;

        private void Awake()
        {
            if (layoutSource == null) layoutSource = GetComponent<LivingUiSceneLayoutSource>();
            if (layoutSource == null) throw new InvalidOperationException("LivingUiDirector 缺少 LivingUiSceneLayoutSource。");
            if (stageCamera == null) stageCamera = Camera.main;
            if (stageCamera == null) throw new InvalidOperationException("LivingUiDirector 未找到舞台相机。");
        }

        private void Start()
        {
            _stageBounds = CalculateStageBounds(stageCamera);
            ApplyLayoutImmediate(LivingUiLayoutId.MainMenu);
        }

        private void Update()
        {
            if (_player.ActivePlan == null) return;
            _player.Advance(Time.unscaledDeltaTime * playbackSpeed);
            ApplyCurrentPlanSample();
        }

        public void Commit(LivingUiLayoutId layoutId)
        {
            _previewLayout = null;
            _committedLayout = layoutId;
            BeginTransition(layoutId);
        }

        public void Preview(LivingUiLayoutId layoutId)
        {
            if (_previewLayout == layoutId) return;
            _previewLayout = layoutId;
            BeginTransition(layoutId);
        }

        public void ClearPreview()
        {
            if (!_previewLayout.HasValue) return;
            _previewLayout = null;
            BeginTransition(_committedLayout);
        }

        public Rect GetTerminalRect(LivingUiLayoutId layoutId, int carrierId)
        {
            var terminal = layoutSource.GetLayout(layoutId).GetTerminal(carrierId);
            return new Rect(terminal.Position - terminal.Size * 0.5f, terminal.Size);
        }

        private void BeginTransition(LivingUiLayoutId layoutId)
        {
            var liveStates = CaptureLiveStates();
            var target = layoutSource.GetLayout(layoutId);
            var envelopeFloors = LivingUiContentProjector.AggregateEnvelopeFloors(
                layoutSource.ContentBindings);
            var plan = _planner.Plan(liveStates, target, _stageBounds, transitionStyle, envelopeFloors);
            _player.TryBegin(plan);

            foreach (var entry in plan.Programs)
            {
                var renderer = layoutSource.Carriers[entry.Key];
                renderer.sortingLayerID = entry.Value.Target.SortingLayerId;
                renderer.sortingOrder = entry.Value.Target.SortingOrder;
            }

            Debug.Log($"[LivingUI] generation={plan.Generation} target={layoutId} makespan={plan.Makespan:F3}s");
        }

        private List<LivingUiCarrierState> CaptureLiveStates()
        {
            var states = new List<LivingUiCarrierState>(layoutSource.Carriers.Count);
            foreach (var entry in layoutSource.Carriers)
            {
                var velocity = Vector2.zero;
                var sizeVelocity = Vector2.zero;
                if (_player.ActivePlan != null)
                {
                    var sampled = _player.Sample(entry.Key);
                    velocity = sampled.Velocity;
                    sizeVelocity = sampled.SizeVelocity;
                }

                states.Add(new LivingUiCarrierState(
                    entry.Key,
                    entry.Value.transform.position,
                    velocity,
                    entry.Value.size,
                    sizeVelocity));
            }

            return states;
        }

        private void ApplyCurrentPlanSample()
        {
            foreach (var entry in layoutSource.Carriers)
            {
                var state = _player.Sample(entry.Key);
                var transform = entry.Value.transform;
                transform.position = new Vector3(state.Position.x, state.Position.y, transform.position.z);
                entry.Value.size = state.Size;
            }
        }

        private void ApplyLayoutImmediate(LivingUiLayoutId layoutId)
        {
            var layout = layoutSource.GetLayout(layoutId);
            foreach (var entry in layoutSource.Carriers)
            {
                var terminal = layout.GetTerminal(entry.Key);
                var transform = entry.Value.transform;
                transform.position = new Vector3(terminal.Position.x, terminal.Position.y, transform.position.z);
                entry.Value.size = terminal.Size;
                entry.Value.sortingLayerID = terminal.SortingLayerId;
                entry.Value.sortingOrder = terminal.SortingOrder;
            }
        }

        private static Rect CalculateStageBounds(Camera camera)
        {
            var bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, Mathf.Abs(camera.transform.position.z)));
            var topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, Mathf.Abs(camera.transform.position.z)));
            return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
        }
    }
}
