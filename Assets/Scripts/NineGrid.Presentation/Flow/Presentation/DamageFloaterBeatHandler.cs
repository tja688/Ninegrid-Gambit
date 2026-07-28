using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 伤害飘字装饰处理器：在 Impact 消费 ShowDamage，不占主线就位回执。
    /// </summary>
    public sealed class DamageFloaterBeatHandler : IBattleBeatHandler
    {
        public bool TryApply(PresentationInstruction instruction)
        {
            if (instruction == null || instruction.Kind != PresentationInstructionKind.ShowDamage)
            {
                return false;
            }

            var gameEvent = instruction.Event;
            if (gameEvent == null || gameEvent.Amount <= 0 || gameEvent.TargetUid <= 0)
            {
                return true;
            }

            var pos = PresentationOutputProjector.ResolveCardWorldPosition(gameEvent.TargetUid);
            if (!pos.HasValue)
            {
                return true;
            }

            DamageNumberHook.RequestSpawn(pos.Value, gameEvent.Amount);
            return true;
        }
    }
}
