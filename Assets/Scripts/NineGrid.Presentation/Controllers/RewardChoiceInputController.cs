using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 奖励点选 Controller：经 Hook 承接 InBattle Bounce，发 QF Command。
    /// </summary>
    public sealed class RewardChoiceInputController : PresentationController
    {
        private System.Func<int, CoreCommandResult> mSelectHandler;
        private System.Func<CoreCommandResult> mSkipHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            RewardChoiceCoreHook.WireController = Wire;
            RewardChoiceCoreHook.SelectReward = null;
            RewardChoiceCoreHook.SkipHelpChoice = null;
        }

        protected override void OnBind()
        {
            InstallHandlers();
        }

        protected override void OnUnbind()
        {
            ClearHandlers();
        }

        public CoreCommandResult HandleSelectReward(int optionIndex)
        {
            return this.SendCommand(new SubmitSelectRewardCommand(optionIndex));
        }

        public CoreCommandResult HandleSkipHelpChoice()
        {
            return this.SendCommand(new SubmitSkipHelpChoiceCommand());
        }

        private void InstallHandlers()
        {
            mSelectHandler = HandleSelectReward;
            mSkipHandler = HandleSkipHelpChoice;
            RewardChoiceCoreHook.SelectReward = mSelectHandler;
            RewardChoiceCoreHook.SkipHelpChoice = mSkipHandler;
        }

        private void ClearHandlers()
        {
            if (mSelectHandler != null && RewardChoiceCoreHook.SelectReward == mSelectHandler)
            {
                RewardChoiceCoreHook.SelectReward = null;
            }

            if (mSkipHandler != null && RewardChoiceCoreHook.SkipHelpChoice == mSkipHandler)
            {
                RewardChoiceCoreHook.SkipHelpChoice = null;
            }

            mSelectHandler = null;
            mSkipHandler = null;
        }

        private static void Wire()
        {
            var existing = Object.FindObjectOfType<RewardChoiceInputController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(RewardChoiceInputController));
                existing = host.AddComponent<RewardChoiceInputController>();
            }

            existing.InstallHandlers();
        }
    }
}
