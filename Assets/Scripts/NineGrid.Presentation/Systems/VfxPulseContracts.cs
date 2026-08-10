using NineGrid.Content.Vfx;

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
            float scale,
            float startOffsetSeconds,
            VfxSpatialContext acceptedSpatial)
        {
            CueRequest = cueRequest;
            Binding = binding;
            VariantId = variantId ?? string.Empty;
            MaterialKey = materialKey ?? string.Empty;
            Fps = fps;
            Scale = scale;
            StartOffsetSeconds = startOffsetSeconds;
            AcceptedSpatial = acceptedSpatial;
        }

        public VfxCueRequest CueRequest { get; }
        public VfxCueBinding Binding { get; }
        public string VariantId { get; }
        public string MaterialKey { get; }
        public float Fps { get; }
        public float Scale { get; }
        public float StartOffsetSeconds { get; }
        public VfxSpatialContext AcceptedSpatial { get; }
    }

    public readonly struct VfxPlayerStartResult
    {
        private VfxPlayerStartResult(bool succeeded, string instanceId, string failureReason)
        {
            Succeeded = succeeded;
            InstanceId = instanceId ?? string.Empty;
            FailureReason = failureReason ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string InstanceId { get; }
        public string FailureReason { get; }

        public static VfxPlayerStartResult Success(string instanceId)
        {
            return new VfxPlayerStartResult(true, instanceId, string.Empty);
        }

        public static VfxPlayerStartResult Failure(string reason)
        {
            return new VfxPlayerStartResult(false, string.Empty, reason);
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
