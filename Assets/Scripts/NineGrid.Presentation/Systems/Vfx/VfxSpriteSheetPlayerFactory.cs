using System;
using NineGrid.Content.Vfx;

namespace NineGrid.Presentation.Systems.Vfx
{
    public sealed class VfxSpriteSheetPlayerFactory : IVfxPlayerFactory
    {
        private readonly VfxSpriteSheetVisualPool mPool;
        private readonly IVfxMaterialFrameLoader mFrameLoader;

        public VfxSpriteSheetPlayerFactory(
            VfxSpriteSheetVisualPool pool = null,
            IVfxMaterialFrameLoader frameLoader = null)
        {
            mPool = pool ?? new VfxSpriteSheetVisualPool();
            mFrameLoader = frameLoader ?? new DefaultVfxMaterialFrameLoader();
        }

        public bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason)
        {
            player = null;
            failureReason = string.Empty;
            if (!string.Equals(playerId, VfxPlayerRegistry.SpriteSheet, StringComparison.Ordinal))
            {
                failureReason = "非 sprite-sheet 播放器。";
                return false;
            }

            player = new VfxSpriteSheetPulsePlayer(mPool, mFrameLoader);
            return true;
        }

        public bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason)
        {
            player = null;
            failureReason = string.Empty;
            if (!string.Equals(playerId, VfxPlayerRegistry.SpriteSheet, StringComparison.Ordinal))
            {
                failureReason = "非 sprite-sheet 播放器。";
                return false;
            }

            player = new VfxSpriteSheetStatePlayer(mPool, mFrameLoader);
            return true;
        }
    }
}
