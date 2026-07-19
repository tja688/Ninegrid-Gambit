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
        private readonly ITimelineDiagnosticSink mDiagnostics;
        private readonly string mLane;
        private ITimelineStep mCurrent;
        private string mCurrentStepName;

        public BattleTimeline(
            ITimelineDiagnosticSink diagnostics = null,
            string lane = DirectorTimelineLane.Mainline)
        {
            mDiagnostics = diagnostics;
            mLane = string.IsNullOrEmpty(lane) ? DirectorTimelineLane.Mainline : lane;
        }

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
            if (mCurrent != null && !string.IsNullOrEmpty(mCurrentStepName))
            {
                EmitExit(mCurrentStepName);
            }

            mQueue.Clear();
            mCurrent = null;
            mCurrentStepName = null;
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

            if (string.IsNullOrEmpty(mCurrentStepName))
            {
                mCurrentStepName = ResolveStepName(mCurrent);
                EmitEnter(mCurrentStepName);
            }

            var status = mCurrent.Tick(deltaTime);
            if (status == TimelineStepStatus.Aborted)
            {
                // Clear 会再 EmitExit 一次当前步；先摘掉名称避免双写。
                var abortedName = mCurrentStepName;
                mCurrentStepName = null;
                EmitExit(abortedName);
                mQueue.Clear();
                mCurrent = null;
                return TimelineStepStatus.Finished;
            }

            if (status != TimelineStepStatus.Finished)
            {
                return TimelineStepStatus.Continue;
            }

            EmitExit(mCurrentStepName);
            mCurrent = null;
            mCurrentStepName = null;
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

        private void EmitEnter(string step)
        {
            if (mDiagnostics == null || string.IsNullOrEmpty(step))
            {
                return;
            }

            mDiagnostics.StepEnter(step, mLane);
        }

        private void EmitExit(string step)
        {
            if (mDiagnostics == null || string.IsNullOrEmpty(step))
            {
                return;
            }

            mDiagnostics.StepExit(step, mLane);
        }

        private static string ResolveStepName(ITimelineStep step)
        {
            return step != null ? step.GetType().Name : string.Empty;
        }
    }

    /// <summary>与 DirectorTrace lane 常量对齐，避免 Presentation→Diagnostics 循环引用时字符串漂移。</summary>
    public static class DirectorTimelineLane
    {
        public const string Mainline = "mainline";
        public const string Bypass = "bypass";
    }
}
