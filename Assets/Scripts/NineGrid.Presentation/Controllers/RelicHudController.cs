using NineGrid.Cards;
using NineGrid.Cards.Presentation;
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
    /// 拖入回收区丢弃经 IntentIntake → <see cref="SubmitDiscardRelicCommand"/>（ADR-0027 / #98）。
    /// </summary>
    public sealed class RelicHudController : PresentationController
    {
        private System.Action mSyncHandler;
        private System.Action mClearHandler;
        private System.Func<string, bool> mDiscardHandler;
        private System.Func<Camera, Vector2, bool> mBeginDragHandler;
        private System.Func<Camera, Vector2, bool> mInspectHandler;
        private RelicManagerSingleton mRelicManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            RelicHudHook.WireController = Wire;
            RelicHudHook.SyncFromCore = null;
            RelicHudHook.Clear = null;
            RelicHudHook.TryDiscardRelic = null;
            RelicHudHook.TryBeginDragRelic = null;
            RelicHudHook.TryInspectRelic = null;
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

        /// <summary>拖入回收区丢弃入口（Hook / EditMode 直驱）。</summary>
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

        public bool HandleBeginDragRelic(Camera camera, Vector2 screen)
        {
            return ResolveRelicManager()?.TryBeginDragUnderPointer(camera, screen) == true;
        }

        public bool HandleInspectRelic(Camera camera, Vector2 screen)
        {
            if (!TryFindRelicDefIdUnderPointer(camera, screen, out var defId))
            {
                return false;
            }

            return CardInspectOverlayPresenter.TryOpenByDefId(defId, CardPresentationKind.Relic);
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
            mBeginDragHandler = HandleBeginDragRelic;
            mInspectHandler = HandleInspectRelic;
            RelicHudHook.SyncFromCore = mSyncHandler;
            RelicHudHook.Clear = mClearHandler;
            RelicHudHook.TryDiscardRelic = mDiscardHandler;
            RelicHudHook.TryBeginDragRelic = mBeginDragHandler;
            RelicHudHook.TryInspectRelic = mInspectHandler;
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

            if (mBeginDragHandler != null && RelicHudHook.TryBeginDragRelic == mBeginDragHandler)
            {
                RelicHudHook.TryBeginDragRelic = null;
            }

            if (mInspectHandler != null && RelicHudHook.TryInspectRelic == mInspectHandler)
            {
                RelicHudHook.TryInspectRelic = null;
            }

            mSyncHandler = null;
            mClearHandler = null;
            mDiscardHandler = null;
            mBeginDragHandler = null;
            mInspectHandler = null;
        }

        private RelicManagerSingleton ResolveRelicManager()
        {
            if (mRelicManager != null)
            {
                return mRelicManager;
            }

            mRelicManager = Object.FindFirstObjectByType<RelicManagerSingleton>();
            return mRelicManager;
        }

        private static bool TryFindRelicDefIdUnderPointer(Camera camera, Vector2 screen, out string defId)
        {
            defId = null;
            ContentIconSlotHitProxy bestRelic = null;
            var bestSort = int.MinValue;
            var targets = PointerHitRegistry.All;
            for (var i = 0; i < targets.Count; i++)
            {
                var proxy = targets[i] as ContentIconSlotHitProxy;
                if (proxy == null
                    || string.IsNullOrEmpty(proxy.DefId)
                    || !proxy.DefId.StartsWith("relic.", System.StringComparison.Ordinal))
                {
                    continue;
                }

                var collider = proxy.HitCollider;
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var planeZ = collider.transform.position.z;
                var depth = planeZ - camera.transform.position.z;
                var world = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
                world.z = planeZ;
                if (!collider.OverlapPoint(world))
                {
                    continue;
                }

                var sort = proxy.HitSortOrder;
                if (bestRelic == null || sort > bestSort)
                {
                    bestRelic = proxy;
                    bestSort = sort;
                }
            }

            if (bestRelic == null)
            {
                return false;
            }

            defId = bestRelic.DefId;
            return true;
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
