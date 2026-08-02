using System;

namespace NineGrid.Flow
{
    /// <summary>
    /// 流程场景视图：Notice / Panel / 时序参数 / 退出。
    /// 业务编排由 <see cref="GameFlowOrchestrator"/> 持有。
    /// </summary>
    public interface IGameFlowView
    {
        float RoomEventStubSeconds { get; }

        float VictoryNoticeSeconds { get; }

        float DefeatNoticeSeconds { get; }

        string VictoryMessage { get; }

        string DefeatMessage { get; }

        void EnsureViewBindings();

        void ShowNotice(string message);

        void HideNotice();

        void ShowMainMenuPanels();

        void ShowInRunShell(bool inBattle = true);

        void ShowRewardOverlay();

        void ShowRoomEventOverlay();

        void HideAllOverlays();

        void QuitGame();
    }
}
