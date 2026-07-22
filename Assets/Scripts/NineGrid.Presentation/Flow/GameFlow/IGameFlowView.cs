using System;

namespace NineGrid.Flow
{
    /// <summary>
    /// 流程场景视图：Notice / Panel / 房间选择 UI / 时序参数 / 退出。
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

        void ShowRoomChoiceOverlay();

        void ShowRoomEventOverlay();

        void HideAllOverlays();

        bool IsRoomChoiceActive { get; }

        void BeginRoomChoice(
            string leftLabel,
            string rightLabel,
            Action<int, string> onPicked,
            Action onFinished,
            bool hoverOnNotice);

        void HideRoomChoice();

        void QuitGame();
    }
}
