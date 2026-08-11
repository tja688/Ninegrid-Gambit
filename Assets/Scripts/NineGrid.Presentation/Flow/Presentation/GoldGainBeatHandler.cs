using NineGrid.Core;
using NineGrid.Flow;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 金币装饰处理器：在 Impact 消费 UpdateGold（尸体 Vacate 前保出生点），广播飞币/HUD，不占主线就位回执。
    /// </summary>
    public sealed class GoldGainBeatHandler : IBattleBeatHandler
    {
        public bool TryApply(PresentationInstruction instruction)
        {
            if (instruction == null || instruction.Kind != PresentationInstructionKind.UpdateGold)
            {
                return false;
            }

            var gameEvent = instruction.Event;
            if (gameEvent == null || gameEvent.Delta == 0)
            {
                return true;
            }

            if (string.Equals(
                    gameEvent.Message,
                    GoldGainPresentationScheduler.UnusedHelpCardsGoldReason,
                    System.StringComparison.Ordinal))
            {
                return true;
            }

            GoldGainPresentationBinder.EnsureInstalled();

            Vector3? origin = null;
            if (gameEvent.CardUid > 0)
            {
                origin = PresentationOutputProjector.ResolveCardWorldPosition(gameEvent.CardUid);
            }

            if (!origin.HasValue && gameEvent.TargetUid > 0)
            {
                origin = PresentationOutputProjector.ResolveCardWorldPosition(gameEvent.TargetUid);
            }

            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            arch?.SendEvent(new GoldGainPresentationRequested
            {
                Delta = gameEvent.Delta,
                AmountAfter = gameEvent.Amount,
                Reason = gameEvent.Message ?? string.Empty,
                SourceDefId = gameEvent.SourceDefId ?? string.Empty,
                ActionName = gameEvent.ActionName ?? string.Empty,
                OriginWorld = origin,
                IsSpend = gameEvent.Delta < 0
            });
            return true;
        }
    }
}
