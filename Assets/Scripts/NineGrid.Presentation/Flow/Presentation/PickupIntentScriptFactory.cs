using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Commands;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// Pickup 缓冲 flush 后的表现通知；Hand 可注册以承接入手动画。
    /// </summary>
    public static class PickupIntentFlushHook
    {
        public static Action<int, PickupItemPresentationResult> Notify;
    }

    /// <summary>
    /// Pickup 剧本：ApplyPickupItemCommand；非 pickup kind 不入队。
    /// </summary>
    public sealed class PickupIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;

        public PickupIntentScriptFactory(IArchitecture architecture)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            mArchitecture = architecture;
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (!string.Equals(intent.Kind, InputIntentKinds.Pickup, StringComparison.Ordinal))
            {
                return;
            }

            timeline.Enqueue(new PickupApplyStep(mArchitecture, intent.TargetId));
        }

        private sealed class PickupApplyStep : ITimelineStep
        {
            private readonly IArchitecture mArch;
            private readonly int mGroundSlot;
            private bool mDone;

            public PickupApplyStep(IArchitecture architecture, int groundSlot)
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
                var summary = mArch.SendCommand(new ApplyPickupItemCommand(mGroundSlot));
                var notify = PickupIntentFlushHook.Notify;
                if (notify != null)
                {
                    notify(mGroundSlot, summary);
                }

                return TimelineStepStatus.Finished;
            }
        }
    }
}
