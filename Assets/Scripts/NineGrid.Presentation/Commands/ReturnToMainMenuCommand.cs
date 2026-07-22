using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 强制停循环并回主菜单。
    /// </summary>
    public sealed class ReturnToMainMenuCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            GameFlowShellSystem.EnsureRegistered().ReturnToMainMenu();
        }
    }
}
