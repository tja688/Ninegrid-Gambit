using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Presentation;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 全局串行翻牌队列：同批多张与跨批入队不并行播，避免 Presenter._playing 丢翻；
    /// PresentStep 在通道 Begin / ack 前经 <see cref="IsIdle"/> 门控（ADR-0016）。
    /// </summary>
    public static class FlipPlaybackCoordinator
    {
        private struct Entry
        {
            public CardFaceFlipPresenter Presenter;
            public bool TargetFaceUp;
        }

        private static readonly Queue<Entry> Queue = new Queue<Entry>(8);
        private static bool PumpRunning;
        private static int Generation;

        public static bool IsIdle => !PumpRunning && Queue.Count == 0;

        public static int PendingCount => Queue.Count;

        /// <summary>测试 / 组合根拆卸：清空队列并作废在途泵。</summary>
        public static void Reset()
        {
            Queue.Clear();
            PumpRunning = false;
            Generation++;
        }

        public static void Enqueue(CardFaceFlipPresenter presenter, bool targetFaceUp)
        {
            if (presenter == null)
            {
                return;
            }

            Queue.Enqueue(new Entry
            {
                Presenter = presenter,
                TargetFaceUp = targetFaceUp,
            });
            EnsurePump();
        }

        public static async UniTask WaitIdleAsync(CancellationToken cancellationToken = default)
        {
            while (!IsIdle)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private static void EnsurePump()
        {
            if (PumpRunning)
            {
                return;
            }

            PumpRunning = true;
            var generation = Generation;
            RunPumpAsync(generation).Forget();
        }

        private static async UniTaskVoid RunPumpAsync(int generation)
        {
            try
            {
                while (generation == Generation && Queue.Count > 0)
                {
                    var entry = Queue.Dequeue();
                    var presenter = entry.Presenter;
                    if (presenter == null)
                    {
                        continue;
                    }

                    try
                    {
                        CardLifecycleAudioCues.Pulse(
                            CardLifecycleAudioCues.Flip,
                            "FlipPlaybackCoordinator.RunPumpAsync");
                        await presenter.PlayFlipAsync(entry.TargetFaceUp);
                    }
                    catch (OperationCanceledException)
                    {
                        // 卡销毁 / CancelPlay：继续下一条，不堵队列。
                    }
                }
            }
            finally
            {
                if (generation == Generation)
                {
                    PumpRunning = false;
                    if (Queue.Count > 0)
                    {
                        EnsurePump();
                    }
                }
            }
        }
    }
}
