using NineGrid.Content.Vfx;

namespace NineGrid.Presentation.Systems
{
    public enum VfxStateSlotOutcome
    {
        Applied,
        NoOp,
        Cleared,
        Unbound,
        Suppressed,
        InvalidBinding,
        PlayerUnavailable,
        DomainUnavailable,
        BackendFailure,
    }

    public sealed class VfxStateSlotResult
    {
        public VfxStateSlotOutcome Outcome { get; internal set; }
        public string StateId { get; internal set; }
        public string BindingKey { get; internal set; }
        public string PlayerId { get; internal set; }
        public string InstanceId { get; internal set; }
        public string FailureReason { get; internal set; }
    }

    /// <summary>持续视觉槽的运行时所有者；用于构建带稳定选择器的 State 请求。</summary>
    public interface IVfxSlotOwner
    {
        VfxStateRequest BuildStateRequest(string stateId);
    }

    public readonly struct VfxStateStartRequest
    {
        public VfxStateStartRequest(
            VfxStateRequest stateRequest,
            VfxStateBinding binding,
            string variantId,
            string materialKey,
            float fps,
            float speed,
            float scale,
            float startOffsetSeconds,
            UnityEngine.Color tint,
            bool useUnscaledTime,
            VfxSpatialContext acceptedSpatial)
        {
            StateRequest = stateRequest;
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

        public VfxStateRequest StateRequest { get; }
        public VfxStateBinding Binding { get; }
        public string VariantId { get; }
        public string MaterialKey { get; }
        public float Fps { get; }
        public float Speed { get; }
        public float Scale { get; }
        public float StartOffsetSeconds { get; }
        public UnityEngine.Color Tint { get; }
        public bool UseUnscaledTime { get; }
        public VfxSpatialContext AcceptedSpatial { get; }
    }

    public interface IVfxStatePlayer
    {
        VfxPlayerStartResult StartState(VfxStateStartRequest request);

        /// <summary>按绑定声明启动退出；immediate 时立刻拆除视觉。</summary>
        void BeginExit(bool immediate, int exitLoopLimit);

        void Cancel();

        /// <summary>返回 true 表示实例已完全结束（含退出段）。</summary>
        bool Tick(float deltaTime);
    }

    public interface IVfxStatePlayerFactory
    {
        bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason);
    }

    public interface IVfxPlayerFactory : IVfxPulsePlayerFactory, IVfxStatePlayerFactory
    {
    }

    public static class VfxStateExitMode
    {
        public const string Immediate = "immediate";
        public const string Segment = "segment";

        public static bool IsSegment(string exitMode)
        {
            return string.Equals(exitMode, Segment, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
