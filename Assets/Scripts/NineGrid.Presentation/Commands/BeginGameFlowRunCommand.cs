using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 开局：取消胜负 Delay → Orchestrator.Start。
    /// </summary>
    public sealed class BeginGameFlowRunCommand : AbstractCommand
    {
        private readonly GameFlowRunOptions mOptions;

        public BeginGameFlowRunCommand(GameFlowRunOptions options = null)
        {
            mOptions = options ?? new GameFlowRunOptions();
        }

        public BeginGameFlowRunCommand(bool testMode, bool quickTestMode = false, QuickTestRunOptions quickTest = null)
            : this(new GameFlowRunOptions
            {
                TestMode = testMode,
                QuickTestMode = quickTestMode,
                QuickTest = quickTest,
            })
        {
        }

        protected override void OnExecute()
        {
            GameFlowShellSystem.EnsureRegistered().BeginRun(mOptions);
        }
    }
}
