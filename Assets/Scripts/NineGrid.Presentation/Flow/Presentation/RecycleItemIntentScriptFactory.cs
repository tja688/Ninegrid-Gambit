using System;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 回收缓冲 flush 后的表现通知；Hand 可注册以承接离手动画。
    /// </summary>
    public static class RecycleItemIntentFlushHook
    {
        public static Action<int, CoreCommandResult> Notify;
    }

    /// <summary>
    /// 回收剧本：ApplyRecycleItemSlotCommand；非 recycleItem kind 不入队。
    /// Apply 后经 <see cref="BattleBeatFlush"/> 只冲刷 UpdateGold（ADR-0007 / #61），
    /// 卡离手碎裂仍由 Hand 的 Notify 承接，避免 Remove 指令与碎裂双通道。
    /// </summary>
    public sealed class RecycleItemIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;

        public RecycleItemIntentScriptFactory(IArchitecture architecture)
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

            if (!string.Equals(intent.Kind, InputIntentKinds.RecycleItem, StringComparison.Ordinal))
            {
                return;
            }

            timeline.Enqueue(new RecycleApplyStep(mArchitecture, intent.TargetId));
        }

        private sealed class RecycleApplyStep : ITimelineStep
        {
            private readonly IArchitecture mArch;
            private readonly int mItemUid;
            private bool mDone;

            public RecycleApplyStep(IArchitecture architecture, int itemUid)
            {
                mArch = architecture;
                mItemUid = itemUid;
            }

            public TimelineStepStatus Tick(float deltaTime)
            {
                if (mDone)
                {
                    return TimelineStepStatus.Finished;
                }

                mDone = true;
                var pipeline = mArch.GetSystem<IActionPipelineSystem>();
                var logStart = pipeline?.EventLog?.Entries != null
                    ? pipeline.EventLog.Entries.Count
                    : 0;
                var summary = mArch.SendCommand(new ApplyRecycleItemSlotCommand(mItemUid));
                if (summary != null && summary.Accepted)
                {
                    // ADR-0007：GoldModified → Settled / GoldGainBeatHandler；只冲金币，不扫 Remove。
                    BattleBeatFlush.PresentEventLogSliceOnly(
                        mArch,
                        logStart,
                        PresentationInstructionKind.UpdateGold);
                }

                var notify = RecycleItemIntentFlushHook.Notify;
                if (notify != null)
                {
                    notify(mItemUid, summary);
                }

                return TimelineStepStatus.Finished;
            }
        }
    }
}
