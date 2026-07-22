using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 用牌解算批表演通道：Begin 时播放飘字/卸尸/用牌盘面 delta；完成后 IsComplete。
    /// </summary>
    public sealed class UseItemPresentChannel : IPresentChannel
    {
        private readonly Func<PostKillBoardPresentationResult, CancellationToken, UniTask> mPlayUse;
        private readonly Func<CancellationToken> mTokenFactory;
        private PostKillBoardPresentationResult mPending;
        private bool mHasPending;
        private bool mComplete = true;
        private int mActiveBatchId;

        public UseItemPresentChannel(
            Func<PostKillBoardPresentationResult, CancellationToken, UniTask> playUse,
            Func<CancellationToken> tokenFactory = null)
        {
            if (playUse == null)
            {
                throw new ArgumentNullException("playUse");
            }

            mPlayUse = playUse;
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

            if (!has || !result.Accepted)
            {
                mComplete = true;
                return;
            }

            var token = mTokenFactory != null ? mTokenFactory() : CancellationToken.None;
            RunPlayAsync(result, token).Forget();
        }

        public void Tick(float deltaTime)
        {
        }

        private async UniTaskVoid RunPlayAsync(
            PostKillBoardPresentationResult result,
            CancellationToken token)
        {
            try
            {
                await mPlayUse(result, token);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                mComplete = true;
            }
        }
    }
}
