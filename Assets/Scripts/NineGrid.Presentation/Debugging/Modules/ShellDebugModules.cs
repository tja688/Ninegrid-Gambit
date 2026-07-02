using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Shell;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Modules
{
    public sealed class MainFlowShellDebugModule : PerformanceDebugModuleBase<MainFlowDirector>
    {
        public override string Id => "shell.main-flow";
        public override string DisplayName => "主流程 Harness";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Shell;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("action", "Action", PerformanceDebugParamKind.Enum, "start-harness",
                "start-harness", "jump-screen", "node-complete", "confirm-reward", "victory", "defeat")
            .Add("screen", "Screen", PerformanceDebugParamKind.Enum, MainFlowScreen.MainMenu.ToString(),
                System.Enum.GetNames(typeof(MainFlowScreen)));

        protected override PerformanceDebugPlayResult PlayModule(
            PerformanceDebugContext context,
            MainFlowDirector director,
            PerformanceDebugPayload payload)
        {
            director = director ?? MainFlowDirector.Current;
            if (director == null)
            {
                return PerformanceDebugPlayResult.Fail("MainFlowDirector not found in scene.");
            }

            string action = payload.GetString("action", "start-harness");
            switch (action)
            {
                case "start-harness":
                    director.HarnessDriver?.StartHarness();
                    return PerformanceDebugPlayResult.Ok(0f);
                case "jump-screen":
                    if (System.Enum.TryParse(payload.GetString("screen", MainFlowScreen.MainMenu.ToString()), out MainFlowScreen screen))
                    {
                        director.HarnessDriver?.JumpToScreen(screen);
                    }

                    return PerformanceDebugPlayResult.Ok(0f);
                case "node-complete":
                    director.HarnessDriver?.NotifyNodeComplete();
                    return PerformanceDebugPlayResult.Ok(0f);
                case "confirm-reward":
                    director.HarnessDriver?.ConfirmReward();
                    return PerformanceDebugPlayResult.Ok(0f);
                case "victory":
                    director.HarnessDriver?.TriggerVictory();
                    return PerformanceDebugPlayResult.Ok(director.GetComponent<RunOutcomeInfoPresenter>() != null ? 3f : 0f);
                case "defeat":
                    director.HarnessDriver?.TriggerDefeat();
                    return PerformanceDebugPlayResult.Ok(director.GetComponent<RunOutcomeInfoPresenter>() != null ? 3f : 0f);
                default:
                    return PerformanceDebugPlayResult.Fail($"Unknown action: {action}");
            }
        }
    }

    public sealed class SelectionShellDebugModule : PerformanceDebugModuleBase<SelectionFsmOwner>
    {
        public override string Id => "shell.selection";
        public override string DisplayName => "选择 FSM";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Shell;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("channel", "Channel", PerformanceDebugParamKind.Enum, SelectionChannel.General.ToString(),
                System.Enum.GetNames(typeof(SelectionChannel)))
            .Add("hoverIndex", "Hover Index", PerformanceDebugParamKind.Int, "0")
            .Add("confirmIndex", "Confirm Index", PerformanceDebugParamKind.Int, "-1");

        protected override PerformanceDebugPlayResult PlayModule(
            PerformanceDebugContext context,
            SelectionFsmOwner module,
            PerformanceDebugPayload payload)
        {
            MainFlowDirector director = MainFlowDirector.Current;
            SelectionFsm fsm = director?.SelectionFsm;
            if (fsm == null)
            {
                return PerformanceDebugPlayResult.Fail("SelectionFsm not bound.");
            }

            string channelName = payload.GetString("channel", SelectionChannel.General.ToString());
            if (System.Enum.TryParse(channelName, out SelectionChannel channel))
            {
                if (channel == SelectionChannel.None)
                {
                    fsm.Deactivate();
                }
                else
                {
                    fsm.ActivateChannel(channel);
                }
            }

            int confirmIndex = payload.GetInt("confirmIndex", -1);
            if (confirmIndex >= 0)
            {
                fsm.NotifyConfirm(confirmIndex);
                return PerformanceDebugPlayResult.Ok(0f);
            }

            fsm.NotifyHover(payload.GetInt("hoverIndex", 0));
            return PerformanceDebugPlayResult.Ok(0.35f);
        }
    }

    public sealed class RoomChoiceInShellDebugModule : PerformanceDebugModuleBase<SelectionPresentation>
    {
        public override string Id => "flow.room-choice-in";
        public override string DisplayName => "房间入场";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Shell;
        public override PerformanceDebugSchema Schema =>
            PerformanceDebugSchemaFactory.ContextOnlySchema(PerformanceDebugContextPreset.None);

        protected override PerformanceDebugPlayResult PlayModule(
            PerformanceDebugContext context,
            SelectionPresentation module,
            PerformanceDebugPayload payload)
        {
            MainFlowDirector director = MainFlowDirector.Current;
            RoomChoiceScreenPresenter presenter = director?.RoomChoicePresenter;
            if (module == null || presenter == null)
            {
                return PerformanceDebugPlayResult.Fail("Room choice presentation/presenter missing.");
            }

            presenter.OnScreenEntered();
            return PerformanceDebugPlayResult.Ok(module.ExpectedDuration);
        }
    }

    public sealed class RoomChoiceOutShellDebugModule : PerformanceDebugModuleBase<SelectionPresentation>
    {
        public override string Id => "flow.room-choice-out";
        public override string DisplayName => "房间离场";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Shell;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("selectedIndex", "Selected Index", PerformanceDebugParamKind.Int, "0");

        protected override PerformanceDebugPlayResult PlayModule(
            PerformanceDebugContext context,
            SelectionPresentation module,
            PerformanceDebugPayload payload)
        {
            MainFlowDirector director = MainFlowDirector.Current;
            if (director?.RoomChoicePresenter == null || module == null)
            {
                return PerformanceDebugPlayResult.Fail("Room choice presentation missing.");
            }

            director.RoomChoicePresenter.HandleOptionConfirmed(payload.GetInt("selectedIndex", 0));
            return PerformanceDebugPlayResult.Ok(module.ExpectedDuration);
        }
    }

    public sealed class RoomChoiceHoverShellDebugModule : PerformanceDebugModuleBase<SelectionPresentation>
    {
        public override string Id => "interaction.room-choice-hover";
        public override string DisplayName => "房间 Hover";
        public override PerformanceDebugCategory Category => PerformanceDebugCategory.Shell;
        public override PerformanceDebugSchema Schema => CreateSchema()
            .Add("hoverIndex", "Hover Index", PerformanceDebugParamKind.Int, "0");

        protected override PerformanceDebugPlayResult PlayModule(
            PerformanceDebugContext context,
            SelectionPresentation module,
            PerformanceDebugPayload payload)
        {
            MainFlowDirector director = MainFlowDirector.Current;
            director?.RoomChoicePresenter?.HandleOptionHovered(payload.GetInt("hoverIndex", 0));
            return PerformanceDebugPlayResult.Ok(0.35f);
        }
    }
}
