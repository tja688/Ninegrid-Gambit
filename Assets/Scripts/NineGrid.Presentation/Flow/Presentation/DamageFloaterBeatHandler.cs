using System;
using NineGrid.Cards;
using NineGrid.Cards.Vfx;
using NineGrid.Core;
using NineGrid.Flow;
using UnityEngine;

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
    /// 伤害/治疗/护甲飘字装饰处理器：在 Impact 消费 ShowDamage（飘伤害数），
    /// 对 Healed 的 UpdateHp 旁路飘绿色治疗数；对 ArmorChanged 增甲旁路飘绿灰护甲数；
    /// 二者均 return false，不占卡面/HUD 认领。减甲飘字仍只走 ShowDamage 拆分甲伤，避免与
    /// UpdateArmor 负 Delta 双轨重复。
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

                // 与飘字同缝的受击视觉脉冲：格挡/护甲碎裂/血飞溅（独立型，不占主线）。
                CombatOutcomeVfx.PulseShowDamage(gameEvent, pos.Value, "DamageFloaterBeatHandler.TryApply");

                // 顺劈斧测试弹道仅 \0 快速测试可发；正式局 / 教程不得走这条表演。
                PulseCleaveAxeProjectileIfNeeded(gameEvent, pos.Value);

                // 同缝的物理反馈：抖屏 + 受击卡一颤（装饰，不占主线 ack）。
                ApplyHitFeedback(gameEvent);

                // 遗物对场地怪物/机关造成伤害时的遗物图标小演出
                RelicImpactBadgeHook.RequestSpawnForTarget(gameEvent.TargetUid, gameEvent.SourceDefId, gameEvent.Cause);

                if (DisplayMode == DamageFloaterDisplayMode.SplitDamage)
                {
                    // 拆分：血伤红字 + 净甲伤绿灰字 + 金币盔甲代偿金色字（代偿部分不再报成甲伤）。
                    // ArmorDamage 是毛甲伤（含金甲代偿，ADR-0028 口径）；卡面当前甲只按净额下降，
                    // 飘字必须与卡面同口径，否则全代偿命中会误读成「卡面漏扣甲」。
                    var hpDamage = Math.Max(0, gameEvent.HpDamage);
                    var goldAbsorbed = Math.Max(0, gameEvent.GoldAbsorbedArmor);
                    var armorDamage = Math.Max(0, gameEvent.ArmorDamage);
                    var netArmorDamage = Math.Max(0, armorDamage - goldAbsorbed);
                    if (hpDamage > 0)
                    {
                        DamageNumberHook.RequestSpawnHpDamage(pos.Value, hpDamage);
                    }

                    if (netArmorDamage > 0)
                    {
                        DamageNumberHook.RequestSpawnArmorDamage(pos.Value, netArmorDamage);
                    }

                    if (goldAbsorbed > 0)
                    {
                        DamageNumberHook.RequestSpawnGoldSpend(
                            pos.Value,
                            goldAbsorbed * DealDamageAction.GoldPerArmorAbsorbed);
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

                CombatOutcomeVfx.PulseHeal(gameEvent, pos.Value, "DamageFloaterBeatHandler.TryApply");
                DamageNumberHook.RequestSpawnHeal(pos.Value, gameEvent.Delta);
                return false;
            }

            if (instruction.Kind == PresentationInstructionKind.ModifyBaseStat
                && gameEvent.Type == CoreEventType.BaseStatModified
                && (StatId)gameEvent.Amount == StatId.Attack)
            {
                // 旁路装饰：攻击提升（含显式加攻与卡面对账提交）播 spell_attack_up 帧动画，
                // 怪物与玩家同缝；不认领指令，卡面数值仍由 CardFaceStatHandler 提交。
                if (gameEvent.Delta <= 0)
                {
                    return false;
                }

                var attackCardUid = gameEvent.CardUid > 0 ? gameEvent.CardUid : gameEvent.TargetUid;
                var attackPos = PresentationOutputProjector.ResolveCardWorldPosition(attackCardUid);
                if (!attackPos.HasValue)
                {
                    return false;
                }

                CombatOutcomeVfx.PulseAttackGain(gameEvent, attackPos.Value, "DamageFloaterBeatHandler.TryApply");
                return false;
            }

            if (instruction.Kind == PresentationInstructionKind.UpdateArmor
                && gameEvent.Type == CoreEventType.ArmorChanged)
            {
                // 旁路装饰：飘当前护甲增量（卡面 CurrentArmor）；减甲仍由 ShowDamage 甲伤承担。
                if (gameEvent.Delta <= 0)
                {
                    return false;
                }

                CombatOutcomeAudio.PulseArmorGain(gameEvent, "DamageFloaterBeatHandler.TryApply");

                var cardUid = gameEvent.CardUid > 0 ? gameEvent.CardUid : gameEvent.TargetUid;
                var pos = PresentationOutputProjector.ResolveCardWorldPosition(cardUid);
                if (!pos.HasValue)
                {
                    return false;
                }

                CombatOutcomeVfx.PulseArmorGain(gameEvent, pos.Value, "DamageFloaterBeatHandler.TryApply");
                DamageNumberHook.RequestSpawnArmorDamage(pos.Value, gameEvent.Delta);
                return false;
            }

            if (instruction.Kind == PresentationInstructionKind.RemoveCard)
            {
                // 旁路装饰：遗物直接破坏/移除卡牌（非普通致死击杀），在受影响格位弹出遗物图标
                var targetCardUid = gameEvent.CardUid > 0 ? gameEvent.CardUid : gameEvent.TargetUid;
                if (targetCardUid > 0 && !string.Equals(gameEvent.Message, "kill", StringComparison.OrdinalIgnoreCase))
                {
                    RelicImpactBadgeHook.RequestSpawnForTarget(targetCardUid, gameEvent.SourceDefId, gameEvent.Cause);
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// 命中物理反馈：按扣血 + 破甲量抖屏，并给受击卡一次颤动。
        /// 正被打击表演搬动的卡会在 <see cref="BoardCardLifeFx"/> 侧自动让位，不与击退叠加。
        /// </summary>
        private static void ApplyHitFeedback(CoreGameEvent gameEvent)
        {
            var magnitude = Math.Max(0, gameEvent.HpDamage) + Math.Max(0, gameEvent.ArmorDamage);
            if (magnitude <= 0)
            {
                magnitude = Math.Max(0, gameEvent.Amount);
            }

            if (magnitude <= 0)
            {
                return;
            }

            ScreenImpact.Hit(magnitude);
            BoardCardLifeFx.PlayJolt(gameEvent.TargetUid, Mathf.Clamp(magnitude / 12f, 0.3f, 1f));
        }

        private static void PulseCleaveAxeProjectileIfNeeded(CoreGameEvent gameEvent, Vector3 targetPosition)
        {
            if (gameEvent == null)
            {
                return;
            }

            // 门闩必须在 PulseFromTo 之前：非 QuickTest 连默认遗物弹道也不能发。
            if (!QuickTestProjectileEffectState.TryGetActivePresetForQuickTest(out var testPresetId))
            {
                return;
            }

            var isCleave = string.Equals(gameEvent.SourceDefId, "relic.rotten_cleave_axe", StringComparison.Ordinal)
                || string.Equals(gameEvent.Cause, "relic.rotten_cleave_axe.cleave", StringComparison.Ordinal);
            if (!isCleave)
            {
                return;
            }

            Vector3 sourcePos = targetPosition;
            var relicManager = UnityEngine.Object.FindFirstObjectByType<RelicManagerSingleton>();
            if (relicManager != null && relicManager.TryGetDealOrigin("relic.rotten_cleave_axe", out var anchor) && anchor != null)
            {
                sourcePos = anchor.position;
            }
            else if (relicManager != null)
            {
                sourcePos = relicManager.transform.position;
            }
            else
            {
                var avatarPos = PresentationOutputProjector.ResolveCardWorldPosition(gameEvent.ActorUid);
                if (avatarPos.HasValue)
                {
                    sourcePos = avatarPos.Value;
                }
            }

            ProjectileVfxCues.PulseFromTo(
                ProjectileVfxCues.Relic,
                "DamageFloaterBeatHandler.PulseCleaveAxeProjectile",
                "relic.rotten_cleave_axe",
                testPresetId,
                sourcePos,
                targetPosition,
                gameEvent.TargetUid);
        }
    }
}
