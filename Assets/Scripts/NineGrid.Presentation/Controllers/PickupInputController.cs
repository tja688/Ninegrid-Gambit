using System;
using NineGrid.Cards;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 场地拾取输入 Controller：经 <see cref="PickupInputHook"/> 承接点击入手，发 QF Command。
    /// </summary>
    public sealed class PickupInputController : PresentationController
    {
        private Func<int, PickupItemPresentationResult> mSubmitHandler;

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

        /// <summary>场地格拾取入口（Hook 与 EditMode 直驱共用）。</summary>
        public PickupItemPresentationResult HandlePickupRequested(int groundSlot)
        {
            return this.SendCommand(new ApplyPickupItemCommand(groundSlot));
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
