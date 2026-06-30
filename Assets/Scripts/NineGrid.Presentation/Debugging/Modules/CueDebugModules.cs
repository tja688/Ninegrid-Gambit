using NineGrid.Presentation.Contracts;
using NineGrid.Presentation.Feedback;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Modules
{
    public sealed class HitFlashCueDebugModule : PerformanceDebugModuleBase<HitFlashCue>
    {
        public override string Id => "cue.hit-flash";
        public override string DisplayName => "受击闪白";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Cue;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("contextPreset", "Context", PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.BattlePair.ToString(),
                PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
            .Add("actorId", "Actor", PerformanceDebugParamKind.ActorId, "enemy");

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, HitFlashCue module, PerformanceDebugPayload payload)
        {
            Transform target = context.ResolveActor(payload.GetString("actorId", "enemy"));
            if (target == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing actor.");
            }

            HitFlashCue cue = target.GetComponent<HitFlashCue>();
            if (cue == null)
            {
                cue = target.gameObject.AddComponent<HitFlashCue>();
            }

            cue.Play(new CueInvocation(target));
            return PerformanceDebugPlayResult.Ok(0.5f);
        }
    }

    public sealed class CardShakeCueDebugModule : PerformanceDebugModuleBase<CardShakeCue>
    {
        public override string Id => "cue.card-shake";
        public override string DisplayName => "抖动";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Cue;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("contextPreset", "Context", PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.BattlePair.ToString(),
                PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
            .Add("actorId", "Actor", PerformanceDebugParamKind.ActorId, "player");

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CardShakeCue module, PerformanceDebugPayload payload)
        {
            Transform target = context.ResolveActor(payload.GetString("actorId", "player"));
            if (target == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing actor.");
            }

            module.Play(new CueInvocation(target));
            return PerformanceDebugPlayResult.Ok(0.6f);
        }
    }

    public sealed class AttackTrailCueDebugModule : PerformanceDebugModuleBase<AttackTrailCue>
    {
        public override string Id => "cue.attack-trail";
        public override string DisplayName => "攻击拖尾";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Cue;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("contextPreset", "Context", PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.BattlePair.ToString(),
                PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
            .Add("actorId", "Actor", PerformanceDebugParamKind.ActorId, "player");

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, AttackTrailCue module, PerformanceDebugPayload payload)
        {
            Transform target = context.ResolveActor(payload.GetString("actorId", "player"));
            if (target == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing actor.");
            }

            module.Play(new CueInvocation(target));
            context.Log.Warn("AttackTrailCue is a placeholder (no visuals yet).");
            return PerformanceDebugPlayResult.Ok(0f);
        }
    }
}
