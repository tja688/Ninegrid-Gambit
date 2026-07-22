using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 洗回牌库 Present Flush 统一入口：由 NineGrid.Presentation Controller 接线，避免 CombatHitSink 业务委托。
    /// </summary>
    public static class BoardPresentShuffleHook
    {
        public static Action<Func<CancellationToken, UniTask>> WireFlush;

        /// <summary>Flush 导演 sink 中的待播洗回；由 InBattle FlushPendingShuffle 经 Controller 注册。</summary>
        public static Func<CancellationToken, UniTask> Flush;

        public static void RequestWire(Func<CancellationToken, UniTask> flush)
        {
            Flush = flush;
            WireFlush?.Invoke(flush);
        }

        public static UniTask RequestFlush(CancellationToken cancellationToken = default)
        {
            if (Flush == null)
            {
                Debug.LogWarning("[BoardPresentShuffleHook] Flush 未注册。");
                return UniTask.CompletedTask;
            }

            return Flush(cancellationToken);
        }
    }
}
