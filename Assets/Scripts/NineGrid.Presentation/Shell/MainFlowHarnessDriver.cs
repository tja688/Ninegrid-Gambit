using System.Collections;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// 主流程测试劫持：运行时开启后自动推进节点；右房走事件+一轮节点后跳过奖励直接结局。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainFlowHarnessDriver : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float nodeAutoCompleteDelay = 1f;
        [SerializeField] private RunOutcome testOutcomeOnRightRoom = RunOutcome.Victory;

        public bool IsActive { get; private set; }
        public float NodeAutoCompleteDelay => nodeAutoCompleteDelay;
        public RunOutcome TestOutcomeOnRightRoom => testOutcomeOnRightRoom;

        private MainFlowFsm flowFsm;
        private MainFlowDirector director;
        private RoomChoiceScreenPresenter roomChoicePresenter;
        private Coroutine nodeCompleteCoroutine;
        private bool pendingOutcomeAfterNode;

        public void Bind(MainFlowDirector owner, MainFlowFsm fsm)
        {
            director = owner;
            flowFsm = fsm;
            roomChoicePresenter = owner?.RoomChoicePresenter;

            if (flowFsm != null)
            {
                flowFsm.ScreenChanged -= OnScreenChanged;
                flowFsm.ScreenChanged += OnScreenChanged;
            }
        }

        private void OnDestroy()
        {
            if (flowFsm != null)
            {
                flowFsm.ScreenChanged -= OnScreenChanged;
            }

            UnhookRoomChoice();
            CancelNodeComplete();
        }

        public void EnableTestFlow()
        {
            if (IsActive)
            {
                return;
            }

            IsActive = true;
            pendingOutcomeAfterNode = false;
            if (flowFsm != null)
            {
                flowFsm.IsHarnessMode = true;
            }

            HookRoomChoice();
            director?.InitializeToMainMenu();
        }

        public void DisableTestFlow()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            pendingOutcomeAfterNode = false;
            if (flowFsm != null)
            {
                flowFsm.IsHarnessMode = false;
            }

            UnhookRoomChoice();
            CancelNodeComplete();
            ResetSelectionInputIfOnMainMenu();
        }

        /// <summary>兼容表演调试模块的旧入口。</summary>
        public void StartHarness()
        {
            EnableTestFlow();
        }

        public void NotifyNodeComplete()
        {
            if (pendingOutcomeAfterNode)
            {
                pendingOutcomeAfterNode = false;
                flowFsm?.ShowOutcome(testOutcomeOnRightRoom);
                return;
            }

            flowFsm?.RequestTransition(MainFlowTransition.NodeComplete);
        }

        public void ConfirmReward()
        {
            flowFsm?.RequestTransition(MainFlowTransition.ConfirmReward);
        }

        public void TriggerVictory()
        {
            flowFsm?.ShowOutcome(RunOutcome.Victory);
        }

        public void TriggerDefeat()
        {
            flowFsm?.ShowOutcome(RunOutcome.Defeat);
        }

        public void JumpToScreen(MainFlowScreen screen)
        {
            director?.JumpToScreen(screen);
        }

        private void OnScreenChanged(MainFlowScreen previous, MainFlowScreen current)
        {
            if (!IsActive)
            {
                return;
            }

            if (current == MainFlowScreen.RunSession)
            {
                flowFsm?.RequestTransition(MainFlowTransition.BeginNode);
                return;
            }

            if (current == MainFlowScreen.NodePlaying)
            {
                ScheduleNodeComplete();
                return;
            }

            if (current != MainFlowScreen.NodePlaying)
            {
                CancelNodeComplete();
            }
        }

        private void HookRoomChoice()
        {
            if (roomChoicePresenter == null)
            {
                return;
            }

            roomChoicePresenter.RoomChosen -= OnRoomChosen;
            roomChoicePresenter.RoomChosen += OnRoomChosen;
        }

        private void UnhookRoomChoice()
        {
            if (roomChoicePresenter != null)
            {
                roomChoicePresenter.RoomChosen -= OnRoomChosen;
            }

            pendingOutcomeAfterNode = false;
        }

        private void OnRoomChosen(int index, RoomKind kind)
        {
            if (!IsActive)
            {
                return;
            }

            pendingOutcomeAfterNode = index == 1;
        }

        private void ScheduleNodeComplete()
        {
            CancelNodeComplete();
            nodeCompleteCoroutine = StartCoroutine(AutoNodeComplete());
        }

        private void CancelNodeComplete()
        {
            if (nodeCompleteCoroutine != null)
            {
                StopCoroutine(nodeCompleteCoroutine);
                nodeCompleteCoroutine = null;
            }
        }

        private IEnumerator AutoNodeComplete()
        {
            if (nodeAutoCompleteDelay > 0f)
            {
                yield return new WaitForSeconds(nodeAutoCompleteDelay);
            }

            nodeCompleteCoroutine = null;
            if (IsActive && flowFsm != null && flowFsm.CurrentScreen == MainFlowScreen.NodePlaying)
            {
                NotifyNodeComplete();
            }
        }

        private void ResetSelectionInputIfOnMainMenu()
        {
            if (director?.SelectionFsm == null)
            {
                return;
            }

            director.SelectionFsm.Deactivate();
            if (flowFsm != null && flowFsm.CurrentScreen == MainFlowScreen.MainMenu)
            {
                director.SelectionFsm.InputLocked = false;
                director.SelectionFsm.ActivateChannel(SelectionChannel.General);
            }
        }
    }
}
