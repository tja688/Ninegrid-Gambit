using System.Collections;
using UnityEngine;

namespace NineGrid.Presentation.Shell
{
    /// <summary>
    /// Harness 模式假推进：不订阅 Core PhaseChanged。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainFlowHarnessDriver : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float rewardAutoAdvanceDelay = 0f;
        [SerializeField, Min(0)] private int autoVictoryAfterNodeCycles = 0;

        private MainFlowFsm flowFsm;
        private MainFlowDirector director;
        private int nodeCycleCount;
        private Coroutine rewardCoroutine;

        public void Bind(MainFlowDirector owner, MainFlowFsm fsm)
        {
            director = owner;
            flowFsm = fsm;
            if (flowFsm != null)
            {
                flowFsm.IsHarnessMode = true;
                flowFsm.ScreenChanged += OnScreenChanged;
            }
        }

        private void OnDestroy()
        {
            if (flowFsm != null)
            {
                flowFsm.ScreenChanged -= OnScreenChanged;
            }
        }

        public void StartHarness()
        {
            nodeCycleCount = 0;
            director?.InitializeToMainMenu();
        }

        public void NotifyNodeComplete()
        {
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
            if (current == MainFlowScreen.RunSession)
            {
                flowFsm?.RequestTransition(MainFlowTransition.BeginNode);
                return;
            }

            if (current == MainFlowScreen.RewardScreen)
            {
                ScheduleRewardAdvance();
                return;
            }

            if (current == MainFlowScreen.NodePlaying && previous == MainFlowScreen.NodeAdvance)
            {
                nodeCycleCount++;
                if (autoVictoryAfterNodeCycles > 0 && nodeCycleCount >= autoVictoryAfterNodeCycles)
                {
                    TriggerVictory();
                }
            }
        }

        private void ScheduleRewardAdvance()
        {
            if (rewardCoroutine != null)
            {
                StopCoroutine(rewardCoroutine);
                rewardCoroutine = null;
            }

            if (rewardAutoAdvanceDelay <= 0f)
            {
                return;
            }

            rewardCoroutine = StartCoroutine(AutoConfirmReward());
        }

        private IEnumerator AutoConfirmReward()
        {
            yield return new WaitForSeconds(rewardAutoAdvanceDelay);
            rewardCoroutine = null;
            if (flowFsm != null && flowFsm.CurrentScreen == MainFlowScreen.RewardScreen)
            {
                ConfirmReward();
            }
        }
    }
}
