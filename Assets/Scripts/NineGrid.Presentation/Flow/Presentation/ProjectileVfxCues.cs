using NineGrid.Content.Vfx;
using NineGrid.Presentation.Systems;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 「谁打了谁」弹道视觉特效稳定声明：源→靶双坐标经 VfxSpatialContext
    /// （PositionSnapshot=源、TargetPositionSnapshot=靶）传入 projectile 播放器，
    /// 独立型一次性脉冲、不占主线 ack（ADR-0040）。
    /// 命中时刻经返回的 VfxCueResult.PresentationPlan（首达/末达秒数）回传，
    /// 表演编排可据此把飘字 / 扣血反馈对齐到弹道命中帧。
    /// 按伤害来源四类分诊 + 攻击独立 cue；内容定向差异（火球/骨刺/毒液等）
    /// 用绑定 selector（cardDefId / skillId）覆盖行实现，不新增 cue。
    /// </summary>
    public static class ProjectileVfxCues
    {
        [VfxCue(
            "vfx.projectile.attack",
            "攻击弹道：攻击者飞向受击者的伤害轨迹",
            "Battle",
            "FieldBattlePresentationExecutor",
            VfxCueContexts.CardDefId | VfxCueContexts.SkillId)]
        public const string Attack = "vfx.projectile.attack";

        [VfxCue(
            "vfx.projectile.effect",
            "效果伤害弹道：效果源卡飞向目标",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            VfxCueContexts.CardDefId | VfxCueContexts.SkillId)]
        public const string Effect = "vfx.projectile.effect";

        [VfxCue(
            "vfx.projectile.skill",
            "怪物技能弹道：技能宿主飞向目标",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            VfxCueContexts.CardDefId | VfxCueContexts.SkillId)]
        public const string Skill = "vfx.projectile.skill";

        [VfxCue(
            "vfx.projectile.trap",
            "机关弹道：机关飞向目标",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            VfxCueContexts.CardDefId | VfxCueContexts.SkillId)]
        public const string Trap = "vfx.projectile.trap";

        [VfxCue(
            "vfx.projectile.relic",
            "遗物弹道：遗物触发飞向目标",
            "Battle",
            "EffectTriggerPulseBeatHandler.TryApply",
            VfxCueContexts.CardDefId | VfxCueContexts.SkillId)]
        public const string Relic = "vfx.projectile.relic";

        private const string SemanticRole = "projectile-flight";

        /// <summary>按伤害来源前缀分诊弹道 cue（与 SkillEffectTrapRelicVfxCues 同规则）。</summary>
        public static string ResolveCueId(string sourceDefId, string cause)
        {
            if (StartsWithToken(sourceDefId, "trap.") || StartsWithToken(cause, "trap."))
            {
                return Trap;
            }

            if (StartsWithToken(sourceDefId, "relic.") || StartsWithToken(cause, "relic."))
            {
                return Relic;
            }

            if (StartsWithToken(cause, "skill."))
            {
                return Skill;
            }

            return Effect;
        }

        /// <summary>
        /// 发射一条源→靶弹道。返回结果的 PresentationPlan.FirstArrivalDelay / LastArrivalDelay
        /// 为命中延迟（秒），编排侧可用它对齐伤害反馈；未播成功时 Plan 无效。
        /// </summary>
        public static VfxCueResult PulseFromTo(
            string cueId,
            string diagnosticSource,
            string cardDefId,
            string skillId,
            Vector3 sourcePosition,
            Vector3 targetPosition,
            int diagnosticUid = 0)
        {
            return TriggerPulseHub.PulseVfx(
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
                    SemanticRole,
                    null,
                    sourcePosition,
                    diagnosticUid,
                    0,
                    targetPosition));
        }

        private static bool StartsWithToken(string value, string prefix)
        {
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(prefix, System.StringComparison.Ordinal);
        }
    }
}
