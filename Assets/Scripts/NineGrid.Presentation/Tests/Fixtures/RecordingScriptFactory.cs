using System.Collections.Generic;
using NineGrid.Flow.Presentation;

namespace NineGrid.Presentation.Tests.Fixtures
{
    public sealed class RecordingScriptFactory : IIntentScriptFactory
    {
        private readonly int mContinueTicks;
        public readonly List<InputIntent> Built = new List<InputIntent>();

        public RecordingScriptFactory(int continueTicks = 0)
        {
            mContinueTicks = continueTicks;
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            Built.Add(intent);
            timeline.Enqueue(new ScriptedTimelineStep(continueTicks: mContinueTicks));
        }
    }

    /// <summary>为 batchCount 次 Resolve→Present 锁步剧本。</summary>
    public sealed class LockstepScriptFactory : IIntentScriptFactory
    {
        private readonly IPresentationBatchGate mGate;
        private readonly IPresentChannel mPresent;
        private readonly int mBatchCount;

        public LockstepScriptFactory(IPresentationBatchGate gate, IPresentChannel present, int batchCount)
        {
            mGate = gate;
            mPresent = present;
            mBatchCount = batchCount;
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            for (var i = 0; i < mBatchCount; i++)
            {
                timeline.Enqueue(new ResolveBatchStep(mGate));
                timeline.Enqueue(new PresentStep(mGate, mPresent));
            }
        }
    }
}
