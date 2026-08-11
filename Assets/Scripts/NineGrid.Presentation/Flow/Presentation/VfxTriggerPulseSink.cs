using NineGrid.Content.Vfx;
using NineGrid.Presentation.Systems;

namespace NineGrid.Flow.Presentation
{
    public interface IVfxCuePulseSink
    {
        VfxCueResult Pulse(VfxCueRequest request, VfxSpatialContext spatialContext = default);
    }

    public sealed class NullVfxCuePulseSink : IVfxCuePulseSink
    {
        public static readonly NullVfxCuePulseSink Instance = new NullVfxCuePulseSink();

        private NullVfxCuePulseSink()
        {
        }

        public VfxCueResult Pulse(VfxCueRequest request, VfxSpatialContext spatialContext = default)
        {
            return new VfxCueResult
            {
                Outcome = VfxCueOutcome.BackendFailure,
                CueId = request.CueId,
                FailureReason = "vfx sink unavailable",
                PresentationPlan = VfxPresentationPlan.None,
            };
        }
    }

    public sealed class VfxTriggerPulseSink : IVfxCuePulseSink
    {
        private readonly IVfxSystem mVfx;

        public VfxTriggerPulseSink(IVfxSystem vfx = null)
        {
            mVfx = vfx;
        }

        public VfxCueResult Pulse(VfxCueRequest request, VfxSpatialContext spatialContext = default)
        {
            var vfx = mVfx ?? VfxSystem.EnsureRegistered();
            return vfx.RequestCue(request, spatialContext);
        }
    }
}
