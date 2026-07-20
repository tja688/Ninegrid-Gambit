using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 反击命中批表演通道：Begin 时播放已解算反击的 Rig/受击观感；完成后 IsComplete。
    /// </summary>
    public sealed class CombatCounterPresentChannel : IPresentChannel
    {
        private readonly Func<int, int, PostKillBoardPresentationResult, CancellationToken, UniTask> mPlayCounter;
        private readonly Func<CancellationToken> mTokenFactory;
        private PostKillBoardPresentationResult mPending;
        private int mPendingAttackerSlot;
        private int mPendingAttackerUid;
        private bool mHasPending;
        private bool mComplete = true;
        private int mActiveBatchId;

        public CombatCounterPresentChannel(
            Func<int, int, PostKillBoardPresentationResult, CancellationToken, UniTask> playCounter,
            Func<CancellationToken> tokenFactory = null)
        {
            if (playCounter == null)
            {
                throw new ArgumentNullException("playCounter");
            }

            mPlayCounter = playCounter;
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

        /// <param name="attackerSlot">反击怪物所在格。</param>
        /// <param name="attackerUid">Resolve 批已确定的反击方 UID。</param>
        public void Enqueue(int attackerSlot, int attackerUid, PostKillBoardPresentationResult result)
        {
            mPendingAttackerSlot = attackerSlot;
            mPendingAttackerUid = attackerUid;
            mPending = result;
            mHasPending = true;
        }

        public void Begin(int batchId)
        {
            mActiveBatchId = batchId;
            mComplete = false;

            var slot = mPendingAttackerSlot;
            var attackerUid = mPendingAttackerUid;
            PostKillBoardPresentationResult result = default;
            var has = mHasPending;
            if (has)
            {
                result = mPending;
                mPending = default;
                mHasPending = false;
                mPendingAttackerSlot = 0;
                mPendingAttackerUid = 0;
            }

            if (!has || !result.Accepted)
            {
                mComplete = true;
                return;
            }

            var token = mTokenFactory != null ? mTokenFactory() : CancellationToken.None;
            RunPlayAsync(slot, attackerUid, result, token).Forget();
        }

        public void Tick(float deltaTime)
        {
        }

        private async UniTaskVoid RunPlayAsync(
            int attackerSlot,
            int attackerUid,
            PostKillBoardPresentationResult result,
            CancellationToken token)
        {
            try
            {
                await mPlayCounter(attackerSlot, attackerUid, result, token);
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
