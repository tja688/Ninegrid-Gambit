using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.Presentation.Controllers
{
    /// <summary>
    /// 洗回牌库 Present Flush Controller：经 <see cref="BoardPresentShuffleHook"/> 承接统一 Flush 入口。
    /// </summary>
    public sealed class BoardPresentShuffleController : PresentationController
    {
        private Func<CancellationToken, UniTask> mFlushHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterInstallHook()
        {
            BoardPresentShuffleHook.WireFlush = WireFlush;
        }

        protected override void OnBind()
        {
            if (mFlushHandler != null)
            {
                BoardPresentShuffleHook.Flush = mFlushHandler;
            }
        }

        protected override void OnUnbind()
        {
            ClearFlushHandler();
        }

        /// <summary>绑定 InBattle 洗回 Flush（Hook 与 EditMode 直驱共用）。</summary>
        public void BindFlush(Func<CancellationToken, UniTask> flush)
        {
            mFlushHandler = flush;
            BoardPresentShuffleHook.Flush = mFlushHandler;
        }

        private void ClearFlushHandler()
        {
            if (mFlushHandler != null && BoardPresentShuffleHook.Flush == mFlushHandler)
            {
                BoardPresentShuffleHook.Flush = null;
            }

            mFlushHandler = null;
        }

        private static void WireFlush(Func<CancellationToken, UniTask> flush)
        {
            if (flush == null)
            {
                return;
            }

            var existing = UnityEngine.Object.FindObjectOfType<BoardPresentShuffleController>();
            if (existing == null)
            {
                var host = new GameObject(nameof(BoardPresentShuffleController));
                existing = host.AddComponent<BoardPresentShuffleController>();
            }

            existing.BindFlush(flush);
        }
    }
}
