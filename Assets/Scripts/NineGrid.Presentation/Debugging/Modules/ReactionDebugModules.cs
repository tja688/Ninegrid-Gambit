using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Reactions;
using NineGrid.Presentation.Shell;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Modules
{
    public sealed class DamageNumbersDebugModule : PerformanceDebugModuleBase<DamageNumbersReaction>
    {
        public override string Id => "reaction.damage-numbers";
        public override string DisplayName => "伤害数字";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Reaction;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add(PerformanceDebugPayloadKeys.ContextPreset, "Context", PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.BattlePair.ToString(),
                PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
            .Add(PerformanceDebugPayloadKeys.TargetActor, "Target Actor", PerformanceDebugParamKind.ActorId, "enemy")
            .Add("kind", "Kind", PerformanceDebugParamKind.Enum, DamagePopupKind.Damage.ToString(),
                "Damage", "Heal", "Gold")
            .Add(PerformanceDebugPayloadKeys.Amount, "Amount", PerformanceDebugParamKind.Float, "12");

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, DamageNumbersReaction module, PerformanceDebugPayload payload)
        {
            string targetActorId = payload.GetString(PerformanceDebugPayloadKeys.TargetActor, "enemy");
            Transform target = context.ResolveActor(targetActorId) ?? context.ResolveActor("enemy") ?? context.ResolveActor("player");
            if (target == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing target actor.");
            }

            var kind = System.Enum.TryParse(payload.GetString("kind"), true, out DamagePopupKind parsed)
                ? parsed
                : DamagePopupKind.Damage;
            module.Play(target, payload.GetFloat(PerformanceDebugPayloadKeys.Amount, 12f), kind);
            return PerformanceDebugPlayResult.Ok(1.5f);
        }
    }

    public sealed class CardAcquisitionDebugModule : PerformanceDebugModuleBase<CardAcquisitionFlow>
    {
        public override string Id => "reaction.card-acquisition";
        public override string DisplayName => "卡牌获得";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Reaction;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Hand7);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, CardAcquisitionFlow module, PerformanceDebugPayload payload)
        {
            module.PlayPreview();
            return PerformanceDebugPlayResult.Ok(module.TotalDuration);
        }

        protected override float TryGetCustomExpectedDuration(CardAcquisitionFlow module) => module.TotalDuration;

        protected override bool TryGetCustomIsPlaying(CardAcquisitionFlow module, out bool isPlaying)
        {
            isPlaying = module.IsPlaying;
            return true;
        }
    }

    public sealed class StatusTickDebugModule : PerformanceDebugModuleBase<DamageNumbersReaction>
    {
        public override string Id => "reaction.status-tick";
        public override string DisplayName => "状态跳变";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Reaction;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("contextPreset", "Context", PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.StatusPanel.ToString(),
                PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
            .Add("stat", "Stat", PerformanceDebugParamKind.Enum, "Attack", "Attack", "Life", "Armor")
            .Add("from", "From", PerformanceDebugParamKind.Int, "1")
            .Add("to", "To", PerformanceDebugParamKind.Int, "7");

        protected override PerformanceDebugPlayResult PlayModule(
            PerformanceDebugContext context,
            DamageNumbersReaction module,
            PerformanceDebugPayload payload)
        {
            Transform actor = context.ResolveActor("statusCard") ?? context.ResolveActor("player");
            if (actor == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing status card actor.");
            }

            int from = payload.GetInt("from", 1);
            int to = payload.GetInt("to", 7);
            string stat = payload.GetString("stat", "Attack");
            var slot = new BoardSlotView(SlotId.Board(1), 1, "debug", CardKind.Monster, 5, 5, 5, 5, 0, 0, from, from, false);

            TableNineCardStatusView view = actor.GetComponent<TableNineCardStatusView>();
            if (view == null)
            {
                view = actor.gameObject.AddComponent<TableNineCardStatusView>();
                view.EnsureBindings();
                view.ConfigureDigitSprites(TableNineDigitSpriteLibrary.LoadDefaultDigits());
            }

            view.SnapFromSlot(slot);

            switch (stat)
            {
                case "Life":
                    view.PlayLifeTo(to, animate: true);
                    break;
                case "Armor":
                    view.PlayArmorTo(to, animate: true);
                    break;
                default:
                    view.PlayAttackTo(to, animate: true);
                    break;
            }

            return PerformanceDebugPlayResult.Ok(1f);
        }
    }

    public sealed class SelectionFallOffDebugModule : PerformanceDebugModuleBase<SelectionPresentation>
    {
        public override string Id => "reaction.selection-falloff";
        public override string DisplayName => "选择落下";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Reaction;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("contextPreset", "Context", PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.Selection3.ToString(),
                PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
            .Add("selectedIndex", "Selected Index", PerformanceDebugParamKind.Int, "1");

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, SelectionPresentation module, PerformanceDebugPayload payload)
        {
            int selectedIndex = payload.GetInt("selectedIndex", 1);
            var options = new List<Transform>
            {
                context.ResolveActor("option1"),
                context.ResolveActor("option2"),
                context.ResolveActor("option3"),
            };

            for (var i = 0; i < options.Count; i++)
            {
                if (i == selectedIndex || options[i] == null)
                {
                    continue;
                }

                module.PlayGeneralFallOff(options[i], i, selectedIndex, options[i].localEulerAngles.z);
            }

            return PerformanceDebugPlayResult.Ok(1.2f);
        }

        protected override bool TryGetCustomIsPlaying(SelectionPresentation module, out bool isPlaying)
        {
            isPlaying = module.IsPlaying;
            return true;
        }
    }
}
