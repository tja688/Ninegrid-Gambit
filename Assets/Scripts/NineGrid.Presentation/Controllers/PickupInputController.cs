using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 场地拾取输入 Controller：经 IntentIntake 门禁后发 Apply Command。
    /// </summary>
    public sealed class PickupInputController : PresentationController
    {
        private System.Func<int, PickupItemPresentationResult> mSubmitHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            PickupInputHook.WireController = WireToHand;
        }

        protected override void OnBind()
        {
            InstallSubmitHandler();
        }

        protected override void OnUnbind()
        {
            ClearSubmitHandler();
        }

        /// <summary>
        /// 场地格拾取入口（Hook 与 EditMode 直驱共用）。
        /// idle：IntentIntake Allow → ExternalHold → Core Apply（不得先改 Core 再抢锁）；
        /// busy：Reject，不缓冲。
        /// </summary>
        public PickupItemPresentationResult HandlePickupRequested(int groundSlot)
        {
            var intake = this.GetSystem<IIntentIntake>()
                ?? IntentIntakeSystem.EnsureRegistered();
            bool preview;
            var disposition = intake.Submit(
                new InputIntent(InputIntentKinds.Pickup, groundSlot),
                InputOwner.ProtectedField,
                out preview);

            if (disposition != IntentDisposition.Allow)
            {
                return default;
            }

            // Allow 之后、Apply 之前取租约：失败则 Core 未改。
            if (!PresentationInputGates.TryBeginExternalHold("Pickup"))
            {
                Debug.LogWarning(
                    "[PickupInputController] Pickup ExternalHold 失败，中止 Apply slot=" + groundSlot);
                return new PickupItemPresentationResult
                {
                    Accepted = false,
                    Reason = "lockFail",
                };
            }

            var summary = this.SendCommand(new ApplyPickupItemCommand(groundSlot));
            if (!summary.Accepted)
            {
                PresentationInputGates.EndExternalHold("Pickup-apply-reject");
            }

            return summary;
        }

        private void InstallSubmitHandler()
        {
            mSubmitHandler = HandlePickupRequested;
            PickupInputHook.TryApplyPickup = mSubmitHandler;
        }

        private void ClearSubmitHandler()
        {
            if (mSubmitHandler != null && PickupInputHook.TryApplyPickup == mSubmitHandler)
            {
                PickupInputHook.TryApplyPickup = null;
            }

            mSubmitHandler = null;
        }

        private static void WireToHand(CardHandManagerSingleton hand)
        {
            if (hand == null)
            {
                return;
            }

            var existing = hand.GetComponent<PickupInputController>();
            if (existing == null)
            {
                existing = UnityEngine.Object.FindObjectOfType<PickupInputController>();
            }

            if (existing == null)
            {
                var host = new GameObject(nameof(PickupInputController));
                host.transform.SetParent(hand.transform, false);
                existing = host.AddComponent<PickupInputController>();
            }

            existing.InstallSubmitHandler();
        }
    }
}
