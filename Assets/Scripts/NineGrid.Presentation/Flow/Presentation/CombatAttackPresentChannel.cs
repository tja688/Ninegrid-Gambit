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
        private readonly Func<int, int, PostKillBoardPresentationResult, CancellationToken, UniTask> mPlayHit;
        private readonly Func<CancellationToken> mTokenFactory;
        private PostKillBoardPresentationResult mPending;
        private int mPendingSlot;
        private int mPendingResolvedCombatUid;
        private bool mHasPending;
        private bool mComplete = true;
        private int mActiveBatchId;

        public CombatAttackPresentChannel(
            Func<int, int, PostKillBoardPresentationResult, CancellationToken, UniTask> playHit,
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

        /// <param name="boardSlot">玩家点击格（蓄力朝向）。</param>
        /// <param name="resolvedCombatUid">Resolve 批已确定的战斗目标 UID（嘲讽重定向后可能≠点击格卡）。</param>
        public void Enqueue(int boardSlot, int resolvedCombatUid, PostKillBoardPresentationResult result)
        {
            mPendingSlot = boardSlot;
            mPendingResolvedCombatUid = resolvedCombatUid;
            mPending = result;
            mHasPending = true;
        }

        public void Begin(int batchId)
        {
            mActiveBatchId = batchId;
            mComplete = false;

            var slot = mPendingSlot;
            var resolvedCombatUid = mPendingResolvedCombatUid;
            PostKillBoardPresentationResult result = default;
            var has = mHasPending;
            if (has)
            {
                result = mPending;
                mPending = default;
                mHasPending = false;
                mPendingSlot = 0;
                mPendingResolvedCombatUid = 0;
            }

            if (!has || !result.Accepted)
            {
                mComplete = true;
                return;
            }

            var token = mTokenFactory != null ? mTokenFactory() : CancellationToken.None;
            RunPlayAsync(slot, resolvedCombatUid, result, token).Forget();
        }

        public void Tick(float deltaTime)
        {
        }

        private async UniTaskVoid RunPlayAsync(
            int boardSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult result,
            CancellationToken token)
        {
            try
            {
                await mPlayHit(boardSlot, resolvedCombatUid, result, token);
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
