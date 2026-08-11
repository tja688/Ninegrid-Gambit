using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Presentation.Systems
{
    public readonly struct VfxPulseStartRequest
    {
        public VfxPulseStartRequest(
            VfxCueRequest cueRequest,
            VfxCueBinding binding,
            string variantId,
            string materialKey,
            float fps,
            float speed,
            float scale,
            float startOffsetSeconds,
            Color tint,
            bool useUnscaledTime,
            VfxSpatialContext acceptedSpatial)
        {
            CueRequest = cueRequest;
            Binding = binding;
            VariantId = variantId ?? string.Empty;
            MaterialKey = materialKey ?? string.Empty;
            Fps = fps;
            Speed = speed;
            Scale = scale;
            StartOffsetSeconds = startOffsetSeconds;
            Tint = tint;
            UseUnscaledTime = useUnscaledTime;
            AcceptedSpatial = acceptedSpatial;
        }

        public VfxCueRequest CueRequest { get; }
        public VfxCueBinding Binding { get; }
        public string VariantId { get; }
        public string MaterialKey { get; }
        public float Fps { get; }
        public float Speed { get; }
        public float Scale { get; }
        public float StartOffsetSeconds { get; }
        public Color Tint { get; }
        public bool UseUnscaledTime { get; }
        public VfxSpatialContext AcceptedSpatial { get; }
    }

    public readonly struct VfxPresentationPlan
    {
        public static readonly VfxPresentationPlan None = new VfxPresentationPlan(0f, 0f);

        public VfxPresentationPlan(float firstArrivalDelay, float lastArrivalDelay)
        {
            FirstArrivalDelay = firstArrivalDelay;
            LastArrivalDelay = lastArrivalDelay;
        }

        /// <summary>首枚金币抵达并消失的延迟（秒）；HUD 在此之后开始推进数字。</summary>
        public float FirstArrivalDelay { get; }

        /// <summary>末枚金币抵达并消失的延迟（秒）；HUD 在此精确收敛到 AmountAfter。</summary>
        public float LastArrivalDelay { get; }

        public bool IsValid => FirstArrivalDelay >= 0f && LastArrivalDelay >= FirstArrivalDelay;
    }

    public readonly struct VfxPlayerStartResult
    {
        private VfxPlayerStartResult(
            bool succeeded,
            string instanceId,
            string failureReason,
            VfxPresentationPlan plan)
        {
            Succeeded = succeeded;
            InstanceId = instanceId ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
            PresentationPlan = plan;
        }

        public bool Succeeded { get; }
        public string InstanceId { get; }
        public string FailureReason { get; }

        /// <summary>程序化播放器一次性给出的批次表现计划；仅金牌玩家提供。</summary>
        public VfxPresentationPlan PresentationPlan { get; }

        public bool HasPresentationPlan => PresentationPlan.IsValid;

        public static VfxPlayerStartResult Success(string instanceId, VfxPresentationPlan plan = default)
        {
            return new VfxPlayerStartResult(true, instanceId, string.Empty, plan);
        }

        public static VfxPlayerStartResult Failure(string reason)
        {
            return new VfxPlayerStartResult(false, string.Empty, reason, VfxPresentationPlan.None);
        }
    }

    public interface IVfxPulsePlayer
    {
        VfxPlayerStartResult StartPulse(VfxPulseStartRequest request);

        void Cancel();

        /// <summary>返回 true 表示实例已自然结束。</summary>
        bool Tick(float deltaTime);
    }

    public interface IVfxPulsePlayerFactory
    {
        bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason);
    }

    public interface IVfxDomainHostResolver
    {
        bool TryResolveHost(IVfxDomainHost hostToken, out IVfxDomainHost host);
    }
}
