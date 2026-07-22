#if UNITY_EDITOR || DEVELOPMENT_BUILD

using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.DevTest.Commands
{
    public sealed class BeginQuickTestRunCommand : AbstractCommand
    {
        private readonly QuickTestRunOptions mOptions;

        public BeginQuickTestRunCommand(QuickTestRunOptions options = null)
        {
            mOptions = options;
        }

        protected override void OnExecute()
        {
            GameFlowShellSystem.EnsureRegistered().BeginRun(new GameFlowRunOptions
            {
                TestMode = true,
                QuickTestMode = true,
                QuickTest = mOptions ?? new QuickTestRunOptions(),
            });
        }
    }

    public sealed class TryBeginQuickTestFromPickerCodeCommand : AbstractCommand<bool>
    {
        private readonly int mCode;

        public TryBeginQuickTestFromPickerCodeCommand(int code)
        {
            mCode = code;
        }

        protected override bool OnExecute()
        {
            return GameFlowShellSystem.EnsureRegistered().TryBeginQuickTestFromPickerCode(mCode);
        }
    }

    public sealed class ApplyInBattleTimeScaleX1Command : AbstractCommand
    {
        protected override void OnExecute()
        {
            GameFlowShellSystem.ApplyInBattleDebugQuickModeTimeScaleX1();
        }
    }

    public sealed class ApplyInBattleTimeScaleX2Command : AbstractCommand
    {
        protected override void OnExecute()
        {
            GameFlowShellSystem.ApplyInBattleDebugQuickModeTimeScaleX2();
        }
    }

    /// <summary>
    /// 只读：是否可接受主菜单 QuickTest / 局内调速入口。
    /// </summary>
    public static class GameFlowDevQueries
    {
        public static bool CanAcceptQuickTestEntry()
        {
            var shell = NineGridArchitecture.Interface?.GetSystem<IGameFlowShellSystem>()
                ?? NineGridArchitecture.Current?.GetSystem<IGameFlowShellSystem>();
            return shell != null && shell.CanAcceptQuickTestEntry;
        }

        public static bool CanAcceptInBattleDebugQuickMode()
        {
            var shell = NineGridArchitecture.Interface?.GetSystem<IGameFlowShellSystem>()
                ?? NineGridArchitecture.Current?.GetSystem<IGameFlowShellSystem>();
            return shell != null && shell.CanAcceptInBattleDebugQuickMode;
        }
    }
}

#endif
