using System;
using NineGrid.Content.Vfx;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>projectile 程序化弹道播放器工厂：仅 Pulse 能力（弹道天然一次性，无 State 形态）。</summary>
    public sealed class VfxProjectilePlayerFactory : IVfxPlayerFactory
    {
        public bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason)
        {
            player = null;
            failureReason = string.Empty;
            if (!string.Equals(playerId, VfxPlayerRegistry.Projectile, StringComparison.Ordinal))
            {
                failureReason = "非 projectile 播放器。";
                return false;
            }

            player = new VfxProjectilePulsePlayer();
            return true;
        }

        public bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason)
        {
            player = null;
            failureReason = "projectile 播放器仅支持 Pulse。";
            return false;
        }
    }
}
