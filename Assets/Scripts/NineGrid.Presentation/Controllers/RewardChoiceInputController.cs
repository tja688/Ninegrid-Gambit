using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 奖励点选 Controller：经 IntentIntake 门禁后发 QF Command。
    /// </summary>
    public sealed class RewardChoiceInputController : PresentationController
    {
        private System.Func<int, CoreCommandResult> mSelectHandler;
        private System.Func<CoreCommandResult> mSkipHandler;
        private System.Func<CoreCommandResult> mRefreshHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            RewardChoiceCoreHook.WireController = Wire;
            RewardChoiceCoreHook.SelectReward = null;
            RewardChoiceCoreHook.SkipHelpChoice = null;
            RewardChoiceCoreHook.RefreshShop = null;
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
            if (!TryIntakeModal(InputIntentKinds.SelectReward, optionIndex))
            {
                return CoreCommandResult.Reject("intentIntakeReject");
            }

            return this.SendCommand(new SubmitSelectRewardCommand(optionIndex));
        }

        public CoreCommandResult HandleSkipHelpChoice()
        {
            if (!TryIntakeModal(InputIntentKinds.SkipHelpChoice, 0))
            {
                return CoreCommandResult.Reject("intentIntakeReject");
            }

            return this.SendCommand(new SubmitSkipHelpChoiceCommand());
        }

        public CoreCommandResult HandleRefreshShop()
        {
            if (!TryIntakeModal(InputIntentKinds.RefreshShop, 0))
            {
                return CoreCommandResult.Reject("intentIntakeReject");
            }

            return this.SendCommand(new SubmitRefreshShopCommand());
        }

        private bool TryIntakeModal(string kind, int targetId)
        {
            var intake = this.GetSystem<IIntentIntake>()
                ?? IntentIntakeSystem.EnsureRegistered();
            bool preview;
            return intake.Submit(
                new InputIntent(kind, targetId),
                InputOwner.ChoiceOverlay,
                out preview) == IntentDisposition.Allow;
        }

        private void InstallHandlers()
        {
            mSelectHandler = HandleSelectReward;
            mSkipHandler = HandleSkipHelpChoice;
            mRefreshHandler = HandleRefreshShop;
            RewardChoiceCoreHook.SelectReward = mSelectHandler;
            RewardChoiceCoreHook.SkipHelpChoice = mSkipHandler;
            RewardChoiceCoreHook.RefreshShop = mRefreshHandler;
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

            if (mRefreshHandler != null && RewardChoiceCoreHook.RefreshShop == mRefreshHandler)
            {
                RewardChoiceCoreHook.RefreshShop = null;
            }

            mSelectHandler = null;
            mSkipHandler = null;
            mRefreshHandler = null;
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
