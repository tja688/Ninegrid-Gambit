using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 唯一串行主线。玩家输入互斥只认它是否在跑。
    /// </summary>
    public sealed class BattleTimeline
    {
        private readonly Queue<ITimelineStep> mQueue = new Queue<ITimelineStep>();
        private ITimelineStep mCurrent;

        public bool IsBusy
        {
            get { return mCurrent != null || mQueue.Count > 0; }
        }

        public int PendingCount
        {
            get { return mQueue.Count + (mCurrent != null ? 1 : 0); }
        }

        public void Enqueue(ITimelineStep step)
        {
            if (step == null)
            {
                throw new ArgumentNullException("step");
            }

            mQueue.Enqueue(step);
        }

        public void Clear()
        {
            mQueue.Clear();
            mCurrent = null;
        }

        /// <summary>
        /// 推进当前 Step。未完成返回 Continue；当前 Finished 且仍有后续返回 Continue；全空返回 Finished。
        /// </summary>
        public TimelineStepStatus Tick(float deltaTime)
        {
            if (mCurrent == null && !TryDequeue(out mCurrent))
            {
                return TimelineStepStatus.Finished;
            }

            var status = mCurrent.Tick(deltaTime);
            if (status != TimelineStepStatus.Finished)
            {
                return TimelineStepStatus.Continue;
            }

            mCurrent = null;
            return IsBusy ? TimelineStepStatus.Continue : TimelineStepStatus.Finished;
        }

        private bool TryDequeue(out ITimelineStep step)
        {
            if (mQueue.Count == 0)
            {
                step = null;
                return false;
            }

            step = mQueue.Dequeue();
            return true;
        }
    }
}
