using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;

namespace NineGrid.Flow.Presentation
{
    /// <summary>飘字显示模式（内部切换，后续接线 UI；默认拆分血/甲双飘字）。</summary>
    public enum DamageFloaterDisplayMode
    {
        /// <summary>白色总伤害单飘字（旧逻辑，切回用）。</summary>
        TotalDamage,

        /// <summary>拆分：血量伤害红字 + 护甲伤害绿灰字，同一命中双飘字。</summary>
        SplitDamage
    }

    /// <summary>
    /// 伤害/治疗飘字装饰处理器：在 Impact 消费 ShowDamage（飘伤害数），
    /// 对 Healed 的 UpdateHp 只旁路飘绿色治疗数并返回 false，不占卡面/HUD 认领。
    /// 拆分模式下 ShowDamage 按 DamageDealt 的护甲/血量拆分量各飘一个数字。
    /// </summary>
    public sealed class DamageFloaterBeatHandler : IBattleBeatHandler
    {
        /// <summary>
        /// 显示模式切换（内部维护，后续可由 UI 接线）。默认拆分：血伤红字 + 甲伤绿灰字；
        /// 切回 <see cref="DamageFloaterDisplayMode.TotalDamage"/> 即恢复白色总伤害旧表现。
        /// </summary>
        public static DamageFloaterDisplayMode DisplayMode { get; set; } = DamageFloaterDisplayMode.SplitDamage;

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

                // 与飘字同 Impact 缝发声；不阻塞主线 ack，对齐可见命中事实。
                CombatOutcomeAudio.PulseShowDamage(gameEvent, "DamageFloaterBeatHandler.TryApply");

                var pos = PresentationOutputProjector.ResolveCardWorldPosition(gameEvent.TargetUid);
                if (!pos.HasValue)
                {
                    return true;
                }

                if (DisplayMode == DamageFloaterDisplayMode.SplitDamage)
                {
                    // 拆分：血伤红字 + 甲伤绿灰字（甲吸收部分含金甲代偿；IgnoreArmor 时甲伤为 0）。
                    var hpDamage = Math.Max(0, gameEvent.HpDamage);
                    var armorDamage = Math.Max(0, gameEvent.ArmorDamage);
                    if (hpDamage > 0)
                    {
                        DamageNumberHook.RequestSpawnHpDamage(pos.Value, hpDamage);
                    }

                    if (armorDamage > 0)
                    {
                        DamageNumberHook.RequestSpawnArmorDamage(pos.Value, armorDamage);
                    }
                }
                else
                {
                    DamageNumberHook.RequestSpawn(pos.Value, gameEvent.Amount);
                }

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

                CombatOutcomeAudio.PulseHeal(gameEvent, "DamageFloaterBeatHandler.TryApply");

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
