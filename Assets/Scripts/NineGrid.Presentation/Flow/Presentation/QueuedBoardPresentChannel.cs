using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 将解算投影的盘面摘要排队，在 Present.Begin 时异步 drain；完成后 IsComplete。
    /// EditMode 可注入即时完成的 drain。
    /// </summary>
    public sealed class QueuedBoardPresentChannel : IPresentChannel
    {
        private readonly Func<PostKillBoardPresentationResult, CancellationToken, UniTask> mDrain;
        private readonly Func<CancellationToken> mTokenFactory;
        private PostKillBoardPresentationResult mPending;
        private bool mHasPending;
        private bool mComplete = true;
        private int mActiveBatchId;

        public QueuedBoardPresentChannel(
            Func<PostKillBoardPresentationResult, CancellationToken, UniTask> drain,
            Func<CancellationToken> tokenFactory = null)
        {
            if (drain == null)
            {
                throw new ArgumentNullException("drain");
            }

            mDrain = drain;
            mTokenFactory = tokenFactory;
        }

        public int ActiveBatchId
        {
            get { return mActiveBatchId; }
        }

        public bool IsComplete
        {
            get { return mComplete; }
        }

        /// <summary>解算回调投递本批表演摘要；Present.Begin 消费。</summary>
        public void Enqueue(PostKillBoardPresentationResult result)
        {
            mPending = result;
            mHasPending = true;
        }

        public void Begin(int batchId)
        {
            mActiveBatchId = batchId;
            mComplete = false;

            PostKillBoardPresentationResult result = default;
            var has = mHasPending;
            if (has)
            {
                result = mPending;
                mPending = default;
                mHasPending = false;
            }

            if (!has || !result.Accepted || IsEmptyPresentation(result))
            {
                mComplete = true;
                return;
            }

            var token = mTokenFactory != null ? mTokenFactory() : CancellationToken.None;
            RunDrainAsync(result, token).Forget();
        }

        public void Tick(float deltaTime)
        {
            // 完成态由异步 drain 回写；墙钟推进不在此通道。
        }

        private async UniTaskVoid RunDrainAsync(
            PostKillBoardPresentationResult result,
            CancellationToken token)
        {
            try
            {
                await mDrain(result, token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                mComplete = true;
            }
        }

        private static bool IsEmptyPresentation(PostKillBoardPresentationResult result)
        {
            var stepCount = result.Steps != null ? result.Steps.Length : 0;
            var moveCount = result.Moves != null ? result.Moves.Length : 0;
            var dealCount = result.Deals != null ? result.Deals.Length : 0;
            var removeCount = result.RemovedUids != null ? result.RemovedUids.Length : 0;
            return stepCount == 0 && moveCount == 0 && dealCount == 0 && removeCount == 0;
        }
    }
}
