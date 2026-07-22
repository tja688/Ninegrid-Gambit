using System.Collections.Generic;
using NineGrid.Flow.Presentation;

namespace NineGrid.Presentation.Tests.Fixtures
{
    /// <summary>
    /// 可配置 tick 完成的 Present 通道；记录 Begin 次数与 batchId，供迁移票复用。
    /// </summary>
    public sealed class RecordingPresentChannel : IPresentChannel
    {
        private readonly int mTicksUntilComplete;
        private int mTicks;
        private bool mBegan;

        public RecordingPresentChannel(int ticksUntilComplete)
        {
            mTicksUntilComplete = ticksUntilComplete;
        }

        public int BeginCount { get; private set; }
        public readonly List<int> PresentedBatchIds = new List<int>();
        public int ActiveBatchId { get; private set; }
        public bool Began { get { return mBegan; } }

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
            PresentedBatchIds.Add(batchId);
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
