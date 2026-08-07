using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 伤害/治疗飘字装饰处理器：在 Impact 消费 ShowDamage（飘伤害数），
    /// 对 Healed 的 UpdateHp 只旁路飘绿色治疗数并返回 false，不占卡面/HUD 认领。
    /// </summary>
    public sealed class DamageFloaterBeatHandler : IBattleBeatHandler
    {
        public bool TryApply(PresentationInstruction instruction)
        {
            if (instruction == null)
            {
                return false;
            }

            var gameEvent = instruction.Event;
            if (gameEvent == null || gameEvent.TargetUid <= 0)
            {
                return false;
            }

            if (instruction.Kind == PresentationInstructionKind.ShowDamage)
            {
                if (gameEvent.Amount <= 0)
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

            if (instruction.Kind == PresentationInstructionKind.UpdateHp
                && gameEvent.Type == CoreEventType.Healed)
            {
                // 旁路装饰：飘实际治疗量（Delta=clamp 后生效值），不认领指令。
                if (gameEvent.Delta <= 0)
                {
                    return false;
                }

                var pos = PresentationOutputProjector.ResolveCardWorldPosition(gameEvent.TargetUid);
                if (!pos.HasValue)
                {
                    return false;
                }

                DamageNumberHook.RequestSpawnHeal(pos.Value, gameEvent.Delta);
                return false;
            }

            return false;
        }
    }
}
