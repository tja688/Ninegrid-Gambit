using System;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// ADR-0032：非战斗相位（RoomChoice / RewardItemChoice）使用道具卡的剧本——
    /// ApplyUseItem → 非锁步冲刷（BattleBeatFlush.PresentEventLogSlice，驱动 HUD 血/甲与金币）。
    /// 不锁步 Batch-ack、无交战通道、无击杀 Fill/Rotate/Fusion；卡离手表现由拖放路径
    /// （VanishCardAfterApplyAsync）承接。仅非战斗相位入队，战斗走 UseItemIntentScriptFactory。
    /// </summary>
    public sealed class NonCombatUseItemIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;

        public NonCombatUseItemIntentScriptFactory(IArchitecture architecture)
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

            if (!string.Equals(intent.Kind, InputIntentKinds.UseItem, StringComparison.Ordinal))
            {
                return;
            }

            if (intent.TargetId <= 0)
            {
                return;
            }

            var phase = mArchitecture.GetSystem<IPhaseSystem>();
            if (phase == null || phase.CurrentPhase == GamePhase.InteractionLoop)
            {
                return;
            }

            timeline.Enqueue(new NonCombatUseApplyStep(
                mArchitecture,
                intent.TargetId,
                intent.SelectedCardUids,
                intent.SelectedOption));
        }

        private sealed class NonCombatUseApplyStep : ITimelineStep
        {
            private readonly IArchitecture mArch;
            private readonly int mItemUid;
            private readonly int[] mSelected;
            private readonly string mOption;
            private bool mDone;

            public NonCombatUseApplyStep(IArchitecture architecture, int itemUid, int[] selected, string option)
            {
                mArch = architecture;
                mItemUid = itemUid;
                mSelected = selected;
                mOption = option;
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
                var summary = mArch.SendCommand(new ApplyUseItemCommand(mItemUid, mSelected, mOption));
                if (summary != null && summary.Accepted)
                {
                    // ADR-0032：非锁步冲刷；Healed→UpdateHp、GoldModified→UpdateGold 等经排期器驱动 HUD。
                    BattleBeatFlush.PresentEventLogSlice(mArch, logStart);
                }

                return TimelineStepStatus.Finished;
            }
        }
    }
}
