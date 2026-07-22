using System;
using System.Collections.Generic;
using NineGrid.Flow.Presentation;

namespace NineGrid.Presentation.Tests.Fixtures
{
    /// <summary>
    /// 模拟 ADR-0002：在 Present.Begin 窗内触发 Commit，供基线断言时机，无需 Singleton 反射。
    /// </summary>
    public sealed class RecordingCommitProbe
    {
        public int CommitCount { get; private set; }
        public int LastBatchId { get; private set; }
        public int LastEventLogCountAtCommit { get; private set; }
        public readonly List<int> CommittedBatchIds = new List<int>();

        public void Record(int batchId, int eventLogCount)
        {
            CommitCount++;
            LastBatchId = batchId;
            LastEventLogCountAtCommit = eventLogCount;
            CommittedBatchIds.Add(batchId);
        }
    }

    public sealed class CommittingPresentChannel : IPresentChannel
    {
        private readonly int mTicksUntilComplete;
        private readonly RecordingCommitProbe mCommit;
        private readonly Func<int> mEventLogCount;
        private readonly Action<int> mOnBegin;
        private int mTicks;
        private bool mBegan;

        public CommittingPresentChannel(
            int ticksUntilComplete,
            RecordingCommitProbe commit,
            Func<int> eventLogCount = null,
            Action<int> onBegin = null)
        {
            mTicksUntilComplete = ticksUntilComplete;
            mCommit = commit;
            mEventLogCount = eventLogCount;
            mOnBegin = onBegin;
        }

        public int BeginCount { get; private set; }
        public int ActiveBatchId { get; private set; }

        public bool IsComplete
        {
            get { return mBegan && mTicks >= mTicksUntilComplete; }
        }

        public void Begin(int batchId)
        {
            mBegan = true;
            mTicks = 0;
            ActiveBatchId = batchId;
            BeginCount++;
            if (mOnBegin != null)
            {
                mOnBegin(batchId);
            }

            var logCount = mEventLogCount != null ? mEventLogCount() : 0;
            mCommit.Record(batchId, logCount);
        }

        public void Tick(float deltaTime)
        {
            if (mBegan)
            {
                mTicks++;
            }
        }
    }
}
