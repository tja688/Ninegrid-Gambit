using System;
using NineGrid.Core;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// BoardWalk 剧本：仅把目标交给 <see cref="IAvatarWalkSystem"/>（连跳在 Runner 内完成）。
    /// </summary>
    public sealed class BoardWalkIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;

        public BoardWalkIntentScriptFactory(IArchitecture architecture)
        {
            mArchitecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            if (!string.Equals(intent.Kind, InputIntentKinds.BoardWalk, StringComparison.Ordinal))
            {
                return;
            }

            timeline.Enqueue(new BoardWalkSetDestinationStep(mArchitecture, intent.TargetId));
        }

        private sealed class BoardWalkSetDestinationStep : ITimelineStep
        {
            private readonly IArchitecture mArch;
            private readonly int mGroundSlot;
            private bool mDone;

            public BoardWalkSetDestinationStep(IArchitecture architecture, int groundSlot)
            {
                mArch = architecture;
                mGroundSlot = groundSlot;
            }

            public TimelineStepStatus Tick(float deltaTime)
            {
                if (mDone)
                {
                    return TimelineStepStatus.Finished;
                }

                mDone = true;
                var walk = mArch.GetSystem<IAvatarWalkSystem>()
                    ?? AvatarWalkSystem.EnsureRegistered(mArch);
                walk?.SetDestination(mGroundSlot);
                return TimelineStepStatus.Finished;
            }
        }
    }
}
