using System;
using NineGrid.Content.Vfx;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>gold-flight 程序化播放器工厂（#203）；只支持 Pulse，不支持 State。</summary>
    public sealed class VfxGoldFlightPlayerFactory : IVfxPlayerFactory
    {
        public bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason)
        {
            player = null;
            failureReason = string.Empty;
            if (!string.Equals(playerId, VfxPlayerRegistry.GoldFlight, StringComparison.Ordinal))
            {
                failureReason = "非 gold-flight 播放器。";
                return false;
            }

            player = new VfxGoldFlightPlayer();
            return true;
        }

        public bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason)
        {
            player = null;
            failureReason = "gold-flight 不支持 State。";
            return false;
        }
    }
}
