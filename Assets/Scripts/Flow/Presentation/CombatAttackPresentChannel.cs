using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 攻击命中批表演通道：Begin 时播放已解算命中的 lunge/受击观感；完成后 IsComplete。
    /// </summary>
    public sealed class CombatAttackPresentChannel : IPresentChannel
    {
        private readonly Func<int, PostKillBoardPresentationResult, CancellationToken, UniTask> mPlayHit;
        private readonly Func<CancellationToken> mTokenFactory;
        private PostKillBoardPresentationResult mPending;
        private int mPendingSlot;
        private bool mHasPending;
        private bool mComplete = true;
        private int mActiveBatchId;

        public CombatAttackPresentChannel(
            Func<int, PostKillBoardPresentationResult, CancellationToken, UniTask> playHit,
            Func<CancellationToken> tokenFactory = null)
        {
            if (playHit == null)
            {
                throw new ArgumentNullException("playHit");
            }

            mPlayHit = playHit;
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

        public void Enqueue(int boardSlot, PostKillBoardPresentationResult result)
        {
            mPendingSlot = boardSlot;
            mPending = result;
            mHasPending = true;
        }

        public void Begin(int batchId)
        {
            mActiveBatchId = batchId;
            mComplete = false;

            var slot = mPendingSlot;
            PostKillBoardPresentationResult result = default;
            var has = mHasPending;
            if (has)
            {
                result = mPending;
                mPending = default;
                mHasPending = false;
                mPendingSlot = 0;
            }

            if (!has || !result.Accepted)
            {
                mComplete = true;
                return;
            }

            var token = mTokenFactory != null ? mTokenFactory() : CancellationToken.None;
            RunPlayAsync(slot, result, token).Forget();
        }

        public void Tick(float deltaTime)
        {
        }

        private async UniTaskVoid RunPlayAsync(
            int boardSlot,
            PostKillBoardPresentationResult result,
            CancellationToken token)
        {
            try
            {
                await mPlayHit(boardSlot, result, token);
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
