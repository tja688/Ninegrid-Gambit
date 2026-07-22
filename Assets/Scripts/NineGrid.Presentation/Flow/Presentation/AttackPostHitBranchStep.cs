using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 命中批 Present 之后：若击杀则向主线追加 Fill/Rotate 锁步剧本；否则追加反击锁步剧本。
    /// </summary>
    public sealed class AttackPostHitBranchStep : ITimelineStep
    {
        private readonly BattleTimeline mTimeline;
        private readonly Func<bool> mWasTargetKilled;
        private readonly Action<BattleTimeline> mEnqueueKillAftermath;
        private readonly Action<BattleTimeline> mEnqueueCounterAftermath;
        private bool mDone;

        public AttackPostHitBranchStep(
            BattleTimeline timeline,
            Func<bool> wasTargetKilled,
            Action<BattleTimeline> enqueueKillAftermath,
            Action<BattleTimeline> enqueueCounterAftermath = null)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (wasTargetKilled == null)
            {
                throw new ArgumentNullException("wasTargetKilled");
            }

            if (enqueueKillAftermath == null)
            {
                throw new ArgumentNullException("enqueueKillAftermath");
            }

            mTimeline = timeline;
            mWasTargetKilled = wasTargetKilled;
            mEnqueueKillAftermath = enqueueKillAftermath;
            mEnqueueCounterAftermath = enqueueCounterAftermath;
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            if (mDone)
            {
                return TimelineStepStatus.Finished;
            }

            if (mWasTargetKilled())
            {
                mEnqueueKillAftermath(mTimeline);
            }
            else if (mEnqueueCounterAftermath != null)
            {
                mEnqueueCounterAftermath(mTimeline);
            }

            mDone = true;
            return TimelineStepStatus.Finished;
        }
    }
}
