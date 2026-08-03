using System;
using NineGrid.Cards;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 道具卡格回收输入 Controller：经 <see cref="RecycleItemInputHook"/> 承接拖放，发 QF Command。
    /// </summary>
    public sealed class RecycleItemInputController : PresentationController
    {
        private Func<int, bool> mSubmitHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            RecycleItemInputHook.WireController = WireToHand;
        }

        protected override void OnBind()
        {
            InstallSubmitHandler();
        }

        protected override void OnUnbind()
        {
            ClearSubmitHandler();
        }

        /// <summary>回收提交入口（Hook 与 EditMode 直驱共用）。</summary>
        public bool HandleRecycleItemRequested(int itemUid)
        {
            return this.SendCommand(new SubmitRecycleItemIntentCommand(itemUid));
        }

        private void InstallSubmitHandler()
        {
            mSubmitHandler = HandleRecycleItemRequested;
            RecycleItemInputHook.TrySubmitRecycleItem = mSubmitHandler;
        }

        private void ClearSubmitHandler()
        {
            if (mSubmitHandler != null && RecycleItemInputHook.TrySubmitRecycleItem == mSubmitHandler)
            {
                RecycleItemInputHook.TrySubmitRecycleItem = null;
            }

            mSubmitHandler = null;
        }

        private static void WireToHand(CardHandManagerSingleton hand)
        {
            if (hand == null)
            {
                return;
            }

            var existing = hand.GetComponent<RecycleItemInputController>();
            if (existing == null)
            {
                existing = UnityEngine.Object.FindObjectOfType<RecycleItemInputController>();
            }

            if (existing == null)
            {
                var host = new GameObject(nameof(RecycleItemInputController));
                host.transform.SetParent(hand.transform, false);
                existing = host.AddComponent<RecycleItemInputController>();
            }

            existing.InstallSubmitHandler();
        }
    }
}
