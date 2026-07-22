using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 盘面 Present drain Controller：经 <see cref="BoardPresentDrainHook"/> 承接统一 drain 入口。
    /// </summary>
    public sealed class BoardPresentDrainController : PresentationController
    {
        private Func<PostKillBoardPresentationResult, CancellationToken, UniTask> mDrainHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            BoardPresentDrainHook.WireDrain = WireDrain;
        }

        protected override void OnBind()
        {
            // Drain 由 InBattle RequestWire 注入；Bind 时仅保证 Hook 指向本实例持有的委托。
            if (mDrainHandler != null)
            {
                BoardPresentDrainHook.Drain = mDrainHandler;
            }
        }

        protected override void OnUnbind()
        {
            ClearDrainHandler();
        }

        /// <summary>绑定 InBattle Present drain（Hook 与 EditMode 直驱共用）。</summary>
        public void BindDrain(Func<PostKillBoardPresentationResult, CancellationToken, UniTask> drain)
        {
            mDrainHandler = drain;
            BoardPresentDrainHook.Drain = mDrainHandler;
        }

        private void ClearDrainHandler()
        {
            if (mDrainHandler != null && BoardPresentDrainHook.Drain == mDrainHandler)
            {
                BoardPresentDrainHook.Drain = null;
            }

            mDrainHandler = null;
        }

        private static void WireDrain(
            Func<PostKillBoardPresentationResult, CancellationToken, UniTask> drain)
        {
            if (drain == null)
            {
                return;
            }

            var existing = UnityEngine.Object.FindObjectOfType<BoardPresentDrainController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(BoardPresentDrainController));
                existing = host.AddComponent<BoardPresentDrainController>();
            }

            existing.BindDrain(drain);
        }
    }
}
