using System.Collections.Generic;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Shell;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Modules
{
    public sealed class BoardCardHoverDebugModule : PerformanceDebugModuleBase<BoardCardHoverPresenter>
    {
        public override string Id => "interaction.board-hover";
        public override string DisplayName => "场地 Hover";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Interaction;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.BattlePair);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, BoardCardHoverPresenter module, PerformanceDebugPayload payload)
        {
            Transform actor = context.ResolveActor("player");
            if (actor == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing player actor.");
            }

            module.Play(actor);
            return PerformanceDebugPlayResult.Ok(0.5f);
        }
    }

    public sealed class HandLayoutDebugModule : PerformanceDebugModuleBase<HandLayoutPresenter>
    {
        public override string Id => "interaction.hand-layout";
        public override string DisplayName => "手牌布局";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Interaction;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Hand7);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, HandLayoutPresenter module, PerformanceDebugPayload payload)
        {
            var actors = new List<Transform>();
            var targets = new List<HandCardLayoutTarget>();
            var solver = new HandCardLayoutSolver();
            for (var i = 1; i <= 7; i++)
            {
                Transform actor = context.ResolveActor($"hand{i}");
                if (actor != null)
                {
                    actors.Add(actor);
                    module.RegisterActor(actor, actor.localPosition, 10 + i);
                }
            }

            solver.BuildLayout(actors.Count, targets);
            module.Relayout(actors, targets);
            return PerformanceDebugPlayResult.Ok(0.35f);
        }
    }

    public sealed class HandCardDragDebugModule : PerformanceDebugModuleBase<HandCardDragPresenter>
    {
        public override string Id => "interaction.hand-drag";
        public override string DisplayName => "手牌拖拽";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Interaction;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add(PerformanceDebugPayloadKeys.ContextPreset, "Context", PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.Hand7.ToString(),
                PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
            .Add(PerformanceDebugPayloadKeys.HandFocusIndex, "Focus Hand Index", PerformanceDebugParamKind.Int, "3");

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, HandCardDragPresenter module, PerformanceDebugPayload payload)
        {
            int focusIndex = Mathf.Clamp(payload.GetInt(PerformanceDebugPayloadKeys.HandFocusIndex, 3), 0, 6);
            var others = new List<Transform>();
            Transform focused = context.ResolveActor($"hand{focusIndex + 1}");
            for (var i = 1; i <= 7; i++)
            {
                Transform actor = context.ResolveActor($"hand{i}");
                if (actor != null && actor != focused)
                {
                    others.Add(actor);
                }
            }

            if (focused == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing focused hand actor.");
            }

            module.PlayFocus(focused, others);
            Vector3 pointer = focused.position + new Vector3(0.5f, 0.5f, 0f);
            module.BeginDrag(focused, pointer);
            module.UpdateDrag(focused, pointer + new Vector3(0.3f, 0.2f, 0f), inZone: true);
            return PerformanceDebugPlayResult.Ok(0.4f);
        }
    }

    public sealed class HandCardReturnDebugModule : PerformanceDebugModuleBase<HandCardReturnPresenter>
    {
        public override string Id => "interaction.hand-return";
        public override string DisplayName => "回手";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Interaction;
        public override PerformanceDebugSchema Schema => PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.Hand7);

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, HandCardReturnPresenter module, PerformanceDebugPayload payload)
        {
            Transform actor = context.ResolveActor("hand4");
            Transform home = context.ResolveActor("hand1");
            if (actor == null || home == null)
            {
                return PerformanceDebugPlayResult.Fail("Missing hand actors.");
            }

            module.PlayReturn(actor, home.localPosition, 12, null);
            return PerformanceDebugPlayResult.Ok(module.ReturnDuration);
        }

        protected override float TryGetCustomExpectedDuration(HandCardReturnPresenter module) => module.ReturnDuration;
    }

    public sealed class SelectionOptionHoverDebugModule : PerformanceDebugModuleBase<SelectionPresentation>
    {
        public override string Id => "interaction.selection-hover";
        public override string DisplayName => "选择项 Hover";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Interaction;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("contextPreset", "Context", PerformanceDebugParamKind.ContextPreset,
                PerformanceDebugContextPreset.Selection3.ToString(),
                PerformanceDebugSchemaFactory.EnumNames<PerformanceDebugContextPreset>())
            .Add("hoverIndex", "Hover Index", PerformanceDebugParamKind.Int, "1");

        protected override PerformanceDebugPlayResult PlayModule(PerformanceDebugContext context, SelectionPresentation module, PerformanceDebugPayload payload)
        {
            var optionActors = new List<SelectionPresentation.GeneralOption>();
            for (var i = 1; i <= 3; i++)
            {
                Transform actor = context.ResolveActor($"option{i}");
                if (actor == null)
                {
                    continue;
                }

                optionActors.Add(new SelectionPresentation.GeneralOption(
                    actor,
                    actor.localPosition,
                    actor.localEulerAngles.z,
                    10 + i));
            }

            module.SetGeneralOptions(optionActors);
            module.PlayGeneralHover(payload.GetInt("hoverIndex", 1));
            return PerformanceDebugPlayResult.Ok(0.5f);
        }
    }
}
