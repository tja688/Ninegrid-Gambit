using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 遗物栏 HUD Controller：消费 Sync Event / Hook，驱动 RelicManager 表现同步。
    /// </summary>
    public sealed class RelicHudController : PresentationController
    {
        private System.Action mSyncHandler;
        private System.Action mClearHandler;
        private RelicManagerSingleton mRelicManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            RelicHudHook.WireController = Wire;
            RelicHudHook.SyncFromCore = null;
            RelicHudHook.Clear = null;
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
            RelicHudHook.SyncFromCore = mSyncHandler;
            RelicHudHook.Clear = mClearHandler;
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

            mSyncHandler = null;
            mClearHandler = null;
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
