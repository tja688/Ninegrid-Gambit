using System;
using NineGrid.Content.Vfx;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>particle 程序化粒子播放器工厂：Pulse 与 State 双能力。</summary>
    public sealed class VfxParticlePlayerFactory : IVfxPlayerFactory
    {
        public bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason)
        {
            player = null;
            failureReason = string.Empty;
            if (!string.Equals(playerId, VfxPlayerRegistry.Particle, StringComparison.Ordinal))
            {
                failureReason = "非 particle 播放器。";
                return false;
            }

            player = new VfxParticlePulsePlayer();
            return true;
        }

        public bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason)
        {
            player = null;
            failureReason = string.Empty;
            if (!string.Equals(playerId, VfxPlayerRegistry.Particle, StringComparison.Ordinal))
            {
                failureReason = "非 particle 播放器。";
                return false;
            }

            player = new VfxParticleStatePlayer();
            return true;
        }
    }
}
