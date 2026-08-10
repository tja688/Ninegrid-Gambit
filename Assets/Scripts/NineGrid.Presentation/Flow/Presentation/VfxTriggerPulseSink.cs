using NineGrid.Content.Vfx;

namespace NineGrid.Flow.Presentation
{
    public interface IVfxCuePulseSink
    {
        void Pulse(VfxCueRequest request, VfxSpatialContext spatialContext = default);
    }

    public sealed class NullVfxCuePulseSink : IVfxCuePulseSink
    {
        public static readonly NullVfxCuePulseSink Instance = new NullVfxCuePulseSink();

        private NullVfxCuePulseSink()
        {
        }

        public void Pulse(VfxCueRequest request, VfxSpatialContext spatialContext = default)
        {
        }
    }

    public sealed class VfxTriggerPulseSink : IVfxCuePulseSink
    {
        private readonly NineGrid.Presentation.Systems.IVfxSystem mVfx;

        public VfxTriggerPulseSink(NineGrid.Presentation.Systems.IVfxSystem vfx = null)
        {
            mVfx = vfx;
        }

        public void Pulse(VfxCueRequest request, VfxSpatialContext spatialContext = default)
        {
            var vfx = mVfx ?? NineGrid.Presentation.Systems.VfxSystem.EnsureRegistered();
            vfx.RequestCue(request, spatialContext);
        }
    }
}
