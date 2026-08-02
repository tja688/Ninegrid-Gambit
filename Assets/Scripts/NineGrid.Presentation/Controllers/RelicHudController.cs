using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 遗物栏 HUD Controller：消费 Sync Event / Hook，驱动 RelicManager 表现同步；
    /// 右键丢弃经 IntentIntake → <see cref="SubmitDiscardRelicCommand"/>（#98）。
    /// </summary>
    public sealed class RelicHudController : PresentationController
    {
        private System.Action mSyncHandler;
        private System.Action mClearHandler;
        private System.Func<string, bool> mDiscardHandler;
        private RelicManagerSingleton mRelicManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            RelicHudHook.WireController = Wire;
            RelicHudHook.SyncFromCore = null;
            RelicHudHook.Clear = null;
            RelicHudHook.TryDiscardRelic = null;
        }

        protected override void OnBind()
        {
            InstallHandlers();
            this.RegisterEvent<RelicHudSyncRequestedEvent>(OnSyncRequested).AddToUnregisterList(this);
        }

        protected override void OnUnbind()
        {
            ClearHandlers();
        }

        public void BindRelicManager(RelicManagerSingleton relicManager)
        {
            mRelicManager = relicManager;
        }

        public void HandleSyncFromCore()
        {
            ResolveRelicManager()?.SyncFromCore();
        }

        public void HandleClear()
        {
            ResolveRelicManager()?.Clear();
        }

        public void RequestSyncViaCommand(bool clear = false)
        {
            this.SendCommand(new SyncRelicHudCommand(clear));
        }

        /// <summary>右键丢弃入口（Hook / EditMode 直驱）。</summary>
        public bool HandleDiscardRelic(string relicDefId)
        {
            if (string.IsNullOrEmpty(relicDefId)
                || !relicDefId.StartsWith("relic.", System.StringComparison.Ordinal))
            {
                return false;
            }

            var owner = PresentationInputGates.ChoiceOverlayActive
                ? InputOwner.ChoiceOverlay
                : InputOwner.ProtectedField;
            var intake = this.GetSystem<IIntentIntake>()
                ?? IntentIntakeSystem.EnsureRegistered();
            bool preview;
            if (intake.Submit(
                    new InputIntent(InputIntentKinds.DiscardRelic, 0, null, relicDefId),
                    owner,
                    out preview) != IntentDisposition.Allow)
            {
                return false;
            }

            var result = this.SendCommand(new SubmitDiscardRelicCommand(relicDefId));
            if (result == null || !result.Accepted)
            {
                if (result != null && !string.IsNullOrEmpty(result.Reason))
                {
                    BoardBriefTipPresenter.EnsureExists().ShowNotice(result.Reason);
                }

                return false;
            }

            return true;
        }

        private void OnSyncRequested(RelicHudSyncRequestedEvent e)
        {
            if (e.Clear)
            {
                HandleClear();
            }
            else
            {
                HandleSyncFromCore();
            }
        }

        private void InstallHandlers()
        {
            mSyncHandler = HandleSyncFromCore;
            mClearHandler = HandleClear;
            mDiscardHandler = HandleDiscardRelic;
            RelicHudHook.SyncFromCore = mSyncHandler;
            RelicHudHook.Clear = mClearHandler;
            RelicHudHook.TryDiscardRelic = mDiscardHandler;
        }

        private void ClearHandlers()
        {
            if (mSyncHandler != null && RelicHudHook.SyncFromCore == mSyncHandler)
            {
                RelicHudHook.SyncFromCore = null;
            }

            if (mClearHandler != null && RelicHudHook.Clear == mClearHandler)
            {
                RelicHudHook.Clear = null;
            }

            if (mDiscardHandler != null && RelicHudHook.TryDiscardRelic == mDiscardHandler)
            {
                RelicHudHook.TryDiscardRelic = null;
            }

            mSyncHandler = null;
            mClearHandler = null;
            mDiscardHandler = null;
        }

        private RelicManagerSingleton ResolveRelicManager()
        {
            if (mRelicManager != null)
            {
                return mRelicManager;
            }

            mRelicManager = UnityEngine.Object.FindFirstObjectByType<RelicManagerSingleton>();
            return mRelicManager;
        }

        private static void Wire()
        {
            var existing = Object.FindObjectOfType<RelicHudController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(RelicHudController));
                existing = host.AddComponent<RelicHudController>();
            }

            existing.InstallHandlers();
        }
    }
}
