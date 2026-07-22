using System;
using NineGrid.Cards;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 手牌用牌输入 Controller：经 <see cref="UseItemInputHook"/> 承接拖放/多选提交，发 QF Command。
    /// </summary>
    public sealed class UseItemInputController : PresentationController
    {
        private Func<int, int[], string, bool> mSubmitHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            UseItemInputHook.WireController = WireToHand;
        }

        protected override void OnBind()
        {
            InstallSubmitHandler();
        }

        protected override void OnUnbind()
        {
            ClearSubmitHandler();
        }

        /// <summary>用牌提交入口（Hook 与 EditMode 直驱共用）。</summary>
        public bool HandleUseItemRequested(
            int itemUid,
            int[] selectedCardUids,
            string selectedOption)
        {
            return this.SendCommand(
                new SubmitUseItemIntentCommand(itemUid, selectedCardUids, selectedOption));
        }

        private void InstallSubmitHandler()
        {
            mSubmitHandler = HandleUseItemRequested;
            UseItemInputHook.TrySubmitUseItem = mSubmitHandler;
        }

        private void ClearSubmitHandler()
        {
            if (mSubmitHandler != null && UseItemInputHook.TrySubmitUseItem == mSubmitHandler)
            {
                UseItemInputHook.TrySubmitUseItem = null;
            }

            mSubmitHandler = null;
        }

        private static void WireToHand(CardHandManagerSingleton hand)
        {
            if (hand == null)
            {
                return;
            }

            var existing = hand.GetComponent<UseItemInputController>();
            if (existing == null)
            {
                existing = UnityEngine.Object.FindObjectOfType<UseItemInputController>();
            }

            if (existing == null)
            {
                var host = new GameObject(nameof(UseItemInputController));
                host.transform.SetParent(hand.transform, false);
                existing = host.AddComponent<UseItemInputController>();
            }

            existing.InstallSubmitHandler();
        }
    }
}
