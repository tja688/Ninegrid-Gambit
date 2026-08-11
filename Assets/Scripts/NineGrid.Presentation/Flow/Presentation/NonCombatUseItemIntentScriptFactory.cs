using System;
using Cysharp.Threading.Tasks;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NineGrid.Flow.RoomIcons;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// ADR-0032：非战斗相位（RoomChoice / RewardItemChoice）使用道具卡的剧本——
    /// ApplyUseItem → 非锁步冲刷（BattleBeatFlush.PresentEventLogSlice，驱动 HUD 血/甲与金币）。
    /// 不锁步 Batch-ack、无交战通道、无击杀 Fill/Rotate/Fusion；卡离手表现由拖放路径
    /// （VanishCardAfterApplyAsync）承接。仅非战斗相位入队，战斗走 UseItemIntentScriptFactory。
    /// 宝箱卡等 `.use` OfferRewardChoice(relic.*) 会把 PendingChoice 换成遗物三选一：
    /// 战斗由 UseItem Present 通道当场 Bounce，本剧本在非战斗相位补开同一三选一面板。
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
                    TryPresentRelicRewardChoiceFromCore(mArch);
                }

                return TimelineStepStatus.Finished;
            }
        }

        /// <summary>
        /// 非战斗相位使用宝箱卡等：`.use` 已把 PendingChoice 换成遗物三选一（relic.* 池），
        /// 当场补开 Bounce 供玩家点选（战斗路径由 UseItem Present 通道承担）。
        /// </summary>
        private static void TryPresentRelicRewardChoiceFromCore(IArchitecture arch)
        {
            if (arch == null || PresentationInputGates.ChoiceOverlayActive)
            {
                return;
            }

            var pending = arch.GetModel<PendingChoiceModel>();
            if (pending == null
                || pending.Kind.Value != PendingChoiceKind.Reward
                || pending.RewardOptions == null
                || pending.RewardOptions.Count == 0
                || !IsRelicRewardPool(pending.PoolId.Value))
            {
                return;
            }

            var choices = arch.GetSystem<IChoicePresentationSystem>()
                ?? ChoicePresentationSystem.EnsureRegistered(arch);
            if (choices == null)
            {
                return;
            }

            PresentRelicRewardChoiceAsync(arch, choices).Forget();
        }

        private static async UniTaskVoid PresentRelicRewardChoiceAsync(
            IArchitecture arch,
            IChoicePresentationSystem choices)
        {
            try
            {
                await choices.PresentRewardChoiceFromCoreAsync(hoverOnNotice: false);
                // 选完若仍在选房面（非战斗节点 / 清关后 RoomChoice），Core 已按节点调度重发 Offer；
                // 场地图标按新 Pending 重刷，避免旧图标与新选项错位。
                var phase = arch.GetSystem<IPhaseSystem>();
                if (phase != null && phase.CurrentPhase == GamePhase.RoomChoice)
                {
                    var pending = arch.GetModel<PendingChoiceModel>();
                    if (pending != null
                        && (pending.Kind.Value == PendingChoiceKind.Room
                            || pending.Kind.Value == PendingChoiceKind.Navigation))
                    {
                        RoomIconBoardPresenter.Current.TrySpawnFromPending(arch);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        private static bool IsRelicRewardPool(string poolId)
        {
            return !string.IsNullOrEmpty(poolId)
                && poolId.StartsWith("relic.", StringComparison.Ordinal);
        }
    }
}
