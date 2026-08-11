using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 战斗结果视觉特效稳定声明：与 <see cref="CombatOutcomeAudio"/> 同缝发射（Impact 装饰），
    /// 位置快照经 VfxSpatialContext 传入，独立型一次性脉冲，不占主线 ack（ADR-0040）。
    /// </summary>
    public static class BattleCombatVfxCues
    {
        [VfxCue("vfx.combat.hp_damage", "血量受伤红色飞溅", "Battle", "DamageFloaterBeatHandler.TryApply", VfxCueContexts.CardDefId)]
        public const string HpDamage = "vfx.combat.hp_damage";

        [VfxCue("vfx.combat.armor_absorb", "护甲吸收蓝色碎裂", "Battle", "DamageFloaterBeatHandler.TryApply", VfxCueContexts.CardDefId)]
        public const string ArmorAbsorb = "vfx.combat.armor_absorb";

        [VfxCue("vfx.combat.block", "护甲完全格挡白闪", "Battle", "DamageFloaterBeatHandler.TryApply", VfxCueContexts.CardDefId)]
        public const string Block = "vfx.combat.block";

        [VfxCue("vfx.combat.heal", "治疗生效恢复光环", "Battle", "DamageFloaterBeatHandler.TryApply", VfxCueContexts.CardDefId)]
        public const string Heal = "vfx.combat.heal";

        [VfxCue("vfx.combat.armor_gain", "获得护甲蓝盾上升", "Battle", "DamageFloaterBeatHandler.TryApply", VfxCueContexts.CardDefId)]
        public const string ArmorGain = "vfx.combat.armor_gain";

        [VfxCue("vfx.combat.death", "单位战斗死亡骷髅烟雾", "Battle", "CardEffectManager.PlayDeathAsync", VfxCueContexts.CardDefId)]
        public const string Death = "vfx.combat.death";
    }

    /// <summary>卡牌生命周期视觉特效稳定声明；发射点对齐既有生命周期 Audio Cue。</summary>
    public static class CardLifecycleVfxCues
    {
        [VfxCue("vfx.card.exit", "卡牌非战斗退场烟雾", "Cards", "CardEffectManager.PlayDeathAsync", VfxCueContexts.CardDefId)]
        public const string Exit = "vfx.card.exit";

        [VfxCue("vfx.card.item_use", "道具卡使用金色光爆", "Cards", "CardEffectManager.PlayUseAsync", VfxCueContexts.CardDefId)]
        public const string ItemUse = "vfx.card.item_use";

        /// <summary>死亡/退场脉冲：战斗死亡（battle.combat.death）走骷髅烟雾，其余退场走轻烟。</summary>
        public static void PulseDeathOrExit(string audioCueId, Vector3 position, string cardDefId)
        {
            var cueId = string.Equals(audioCueId, BattleCombatAudioCues.Death, System.StringComparison.Ordinal)
                ? BattleCombatVfxCues.Death
                : Exit;
            VfxPulseEmit.PulseAt(cueId, "CardEffectManager.PlayDeathAsync", cardDefId, null, position);
        }

        public static void PulseItemUse(Vector3 position, string cardDefId)
        {
            VfxPulseEmit.PulseAt(ItemUse, "CardEffectManager.PlayUseAsync", cardDefId, null, position);
        }
    }

    /// <summary>
    /// Impact 交战结果 → 视觉特效映射。与 <see cref="CombatOutcomeAudio"/> 同一分诊逻辑：
    /// 甲伤无血伤=格挡；甲伤+血伤=吸收+飞溅（吸收略上移避免重叠）；纯血伤=飞溅。
    /// </summary>
    public static class CombatOutcomeVfx
    {
        private static readonly Vector3 ArmorImpactOffset = new Vector3(0f, 0.28f, 0f);

        public static void PulseShowDamage(NineGrid.Core.CoreGameEvent gameEvent, Vector3 targetPosition, string diagnosticSource)
        {
            if (gameEvent == null || gameEvent.Amount <= 0)
            {
                return;
            }

            var cardDefId = gameEvent.SourceDefId;
            var armorDamage = gameEvent.ArmorDamage;
            var hpDamage = gameEvent.HpDamage;
            if (armorDamage > 0 && hpDamage <= 0)
            {
                VfxPulseEmit.PulseAt(BattleCombatVfxCues.Block, diagnosticSource, cardDefId, null, targetPosition, gameEvent.TargetUid);
                return;
            }

            if (armorDamage > 0)
            {
                VfxPulseEmit.PulseAt(
                    BattleCombatVfxCues.ArmorAbsorb,
                    diagnosticSource,
                    cardDefId,
                    null,
                    targetPosition + ArmorImpactOffset,
                    gameEvent.TargetUid);
            }

            if (hpDamage > 0)
            {
                VfxPulseEmit.PulseAt(BattleCombatVfxCues.HpDamage, diagnosticSource, cardDefId, null, targetPosition, gameEvent.TargetUid);
            }
        }

        public static void PulseHeal(NineGrid.Core.CoreGameEvent gameEvent, Vector3 targetPosition, string diagnosticSource)
        {
            if (gameEvent == null || gameEvent.Delta <= 0)
            {
                return;
            }

            VfxPulseEmit.PulseAt(BattleCombatVfxCues.Heal, diagnosticSource, gameEvent.SourceDefId, null, targetPosition, gameEvent.TargetUid);
        }

        public static void PulseArmorGain(NineGrid.Core.CoreGameEvent gameEvent, Vector3 targetPosition, string diagnosticSource)
        {
            if (gameEvent == null || gameEvent.Delta <= 0)
            {
                return;
            }

            VfxPulseEmit.PulseAt(BattleCombatVfxCues.ArmorGain, diagnosticSource, gameEvent.SourceDefId, null, targetPosition, gameEvent.TargetUid);
        }
    }

    /// <summary>
    /// 技能 / 可见效果 / 机关 / 遗物触发视觉特效；与 <see cref="SkillEffectTrapRelicAudioCues"/>
    /// 同一分诊规则（SourceDefId/Cause 前缀），发射点同为 EffectTriggerPulseBeatHandler。
    /// </summary>
    public static class SkillEffectTrapRelicVfxCues
    {
        [VfxCue(
            "vfx.effect.trigger",
            "可见效果触发蓝色星爆",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            VfxCueContexts.CardDefId | VfxCueContexts.SkillId)]
        public const string EffectTrigger = "vfx.effect.trigger";

        [VfxCue(
            "vfx.skill.trigger",
            "怪物技能触发紫电爆发",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            VfxCueContexts.CardDefId | VfxCueContexts.SkillId)]
        public const string SkillTrigger = "vfx.skill.trigger";

        [VfxCue(
            "vfx.trap.trigger",
            "机关触发金色火花",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            VfxCueContexts.CardDefId | VfxCueContexts.SkillId)]
        public const string TrapTrigger = "vfx.trap.trigger";

        [VfxCue(
            "vfx.relic.trigger",
            "遗物触发星光闪烁",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            VfxCueContexts.CardDefId | VfxCueContexts.SkillId)]
        public const string RelicTrigger = "vfx.relic.trigger";

        public static string ResolveCueId(string sourceDefId, string cause)
        {
            if (StartsWithToken(sourceDefId, "trap.") || StartsWithToken(cause, "trap."))
            {
                return TrapTrigger;
            }

            if (StartsWithToken(sourceDefId, "relic.") || StartsWithToken(cause, "relic."))
            {
                return RelicTrigger;
            }

            if (StartsWithToken(cause, "skill."))
            {
                return SkillTrigger;
            }

            return EffectTrigger;
        }

        public static void PulseTrigger(
            string sourceDefId,
            string cause,
            Vector3 position,
            string diagnosticSource,
            int diagnosticCardUid = 0)
        {
            var cueId = ResolveCueId(sourceDefId, cause);
            VfxPulseEmit.PulseAt(cueId, diagnosticSource, sourceDefId, cause, position, diagnosticCardUid);
        }

        private static bool StartsWithToken(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(prefix, System.StringComparison.Ordinal);
        }
    }

    /// <summary>整局胜负提示视觉特效；棋盘中心（Avatar 保留格锚点）大字演出，unscaled 时间基。</summary>
    public static class FlowBattleEndVfxCues
    {
        [VfxCue("vfx.flow.victory", "整局胜利大字演出", "Flow", "GameFlowOrchestrator.ShowBattleEndAndReturnAsync", VfxCueContexts.None)]
        public const string Victory = "vfx.flow.victory";

        [VfxCue("vfx.flow.defeat", "战斗失败大字演出", "Flow", "GameFlowOrchestrator.ShowBattleEndAndReturnAsync", VfxCueContexts.None)]
        public const string Defeat = "vfx.flow.defeat";

        public static void PulseBattleEnd(bool victory, string diagnosticSource)
        {
            var center = NineGrid.Flow.PresentationOutputProjector.ResolveBoardSlotWorldPosition(
                NineGrid.Cards.GroundSlotTopology.AvatarReservedSlot);
            if (!center.HasValue)
            {
                return;
            }

            VfxPulseEmit.PulseAt(victory ? Victory : Defeat, diagnosticSource, null, null, center.Value);
        }
    }

    /// <summary>类型化 VFX 脉冲的统一发射小工具：稳定 cue 常量 + 位置快照，独立型无宿主。</summary>
    internal static class VfxPulseEmit
    {
        private const string DefaultSemanticRole = "board-burst";

        public static void PulseAt(
            string cueId,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            Vector3 position,
            int diagnosticUid = 0)
        {
            TriggerPulseHub.PulseVfx(
                new VfxCueRequest(
                    cueId,
                    diagnosticSource,
                    cardDefId ?? string.Empty,
                    skillId ?? string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    diagnosticUid),
                new VfxSpatialContext(
                    DefaultSemanticRole,
                    null,
                    position,
                    diagnosticUid));
        }
    }
}
