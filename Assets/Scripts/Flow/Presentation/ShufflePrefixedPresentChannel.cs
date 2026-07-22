using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Present 前缀（EditMode / 假时钟）：先消耗洗回 sink，再播内层通道。
    /// 运行时用牌路径在 PlayDirectorUseItemPresentAsync 内直接 Flush，不走本通道的异步形态。
    /// </summary>
    public sealed class ShufflePrefixedPresentChannel : IPresentChannel
    {
        private enum Phase
        {
            Idle,
            Shuffling,
            Inner,
            Done,
        }

        private readonly ShuffleIntoDeckPresentSink mSink;
        private readonly IPresentChannel mInner;
        private readonly ShuffleIntoDeckScheduler mScheduler;
        private readonly int mFakeShuffleTicks;
        private Phase mPhase = Phase.Idle;
        private int mShuffleTicksLeft;
        private int mActiveBatchId;
        private int mShuffleBeginCount;
        private int mLastShuffleCount;

        public ShufflePrefixedPresentChannel(
            ShuffleIntoDeckPresentSink sink,
            IPresentChannel inner,
            int fakeShuffleTicksUntilComplete = 1,
            ShuffleIntoDeckScheduler scheduler = null)
        {
            if (sink == null)
            {
                throw new ArgumentNullException("sink");
            }

            if (inner == null)
            {
                throw new ArgumentNullException("inner");
            }

            mSink = sink;
            mInner = inner;
            mScheduler = scheduler ?? new ShuffleIntoDeckScheduler();
            mFakeShuffleTicks = fakeShuffleTicksUntilComplete < 1 ? 1 : fakeShuffleTicksUntilComplete;
        }

        public int ActiveBatchId
        {
            get { return mActiveBatchId; }
        }

        public int ShuffleBeginCount
        {
            get { return mShuffleBeginCount; }
        }

        public int LastShuffleCount
        {
            get { return mLastShuffleCount; }
        }

        public IPresentChannel Inner
        {
            get { return mInner; }
        }

        public bool IsComplete
        {
            get { return mPhase == Phase.Done; }
        }

        public void Begin(int batchId)
        {
            mActiveBatchId = batchId;
            mLastShuffleCount = mSink.PendingCount;
            if (mSink.HasPending)
            {
                mPhase = Phase.Shuffling;
                mShuffleBeginCount++;
                mScheduler.RecordPresentBegin(mLastShuffleCount);
                mShuffleTicksLeft = mFakeShuffleTicks;
                return;
            }

            StartInner(batchId);
        }

        public void Tick(float deltaTime)
        {
            if (mPhase == Phase.Shuffling)
            {
                mShuffleTicksLeft--;
                if (mShuffleTicksLeft <= 0)
                {
                    mSink.Clear();
                    mScheduler.RecordPresentEnd(mLastShuffleCount);
                    StartInner(mActiveBatchId);
                }

                return;
            }

            if (mPhase == Phase.Inner)
            {
                mInner.Tick(deltaTime);
                if (mInner.IsComplete)
                {
                    mPhase = Phase.Done;
                }
            }
        }

        private void StartInner(int batchId)
        {
            mPhase = Phase.Inner;
            mInner.Begin(batchId);
            if (mInner.IsComplete)
            {
                mPhase = Phase.Done;
            }
        }
    }
}
