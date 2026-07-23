using System;
using NineGrid.Cards;
using NineGrid.Presentation.Commands;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 空槽探索输入 Controller：经 <see cref="ExploreInputHook"/> 承接场地点击，发 QF Command。
    /// </summary>
    public sealed class ExploreInputController : PresentationController
    {
        private Func<int, bool> mSubmitHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            ExploreInputHook.WireController = WireToField;
        }

        protected override void OnBind()
        {
            InstallSubmitHandler();
        }

        protected override void OnUnbind()
        {
            ClearSubmitHandler();
        }

        /// <summary>空槽点击入口（Hook 与 EditMode 直驱共用）。</summary>
        public bool HandleEmptySlotClicked(int groundSlot)
        {
            return this.SendCommand(new SubmitExploreIntentCommand(groundSlot));
        }

        private void InstallSubmitHandler()
        {
            mSubmitHandler = HandleEmptySlotClicked;
            ExploreInputHook.TrySubmitExplore = mSubmitHandler;
        }

        private void ClearSubmitHandler()
        {
            if (mSubmitHandler != null && ExploreInputHook.TrySubmitExplore == mSubmitHandler)
            {
                ExploreInputHook.TrySubmitExplore = null;
            }

            mSubmitHandler = null;
        }

        private static void WireToField(GroundFieldView field)
        {
            if (field == null)
            {
                return;
            }

            var existing = field.GetComponent<ExploreInputController>();
            if (existing == null)
            {
                existing = UnityEngine.Object.FindObjectOfType<ExploreInputController>();
            }

            if (existing == null)
            {
                var host = new GameObject(nameof(ExploreInputController));
                host.transform.SetParent(field.transform, false);
                existing = host.AddComponent<ExploreInputController>();
            }

            existing.InstallSubmitHandler();
        }
    }
}
