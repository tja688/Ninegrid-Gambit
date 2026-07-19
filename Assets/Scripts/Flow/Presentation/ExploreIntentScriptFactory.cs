using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 空格 explore 剧本：ClickEmpty 一批 → Present → ResolvePostKillBoard 一批 → Present。
    /// 未识别 kind 不入队（留给旧路径 / 后续切片）。
    /// </summary>
    public sealed class ExploreIntentScriptFactory : IIntentScriptFactory
    {
        private readonly IArchitecture mArchitecture;
        private readonly CoreCommandDispatcher mDispatcher;
        private readonly IPresentChannel mPresentChannel;
        private readonly Action<int, int, PostKillBoardPresentationResult> mOnBatchProjected;

        public ExploreIntentScriptFactory(
            IArchitecture architecture,
            CoreCommandDispatcher dispatcher,
            IPresentChannel presentChannel,
            Action<int, int, PostKillBoardPresentationResult> onBatchProjected = null)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            if (dispatcher == null)
            {
                throw new ArgumentNullException("dispatcher");
            }

            if (presentChannel == null)
            {
                throw new ArgumentNullException("presentChannel");
            }

            mArchitecture = architecture;
            mDispatcher = dispatcher;
            mPresentChannel = presentChannel;
            mOnBatchProjected = onBatchProjected;
        }

        public void BuildScript(InputIntent intent, BattleTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException("timeline");
            }

            if (!string.Equals(intent.Kind, InputIntentKinds.Explore, StringComparison.Ordinal))
            {
                return;
            }

            var slotIndex = intent.TargetId;
            var slot = SlotId.Board(slotIndex);
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();

            var clickGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new ClickEmptyCommand(slot))));
            var postKillGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new ResolvePostKillBoardCommand())));

            timeline.Enqueue(new ResolveBatchStep(clickGate));
            timeline.Enqueue(new PresentStep(clickGate, mPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(postKillGate));
            timeline.Enqueue(new PresentStep(postKillGate, mPresentChannel));
        }

        private CoreCommandDispatchResult ResolveAndProject(
            int boardSlot,
            Func<CoreCommandDispatchResult> resolve)
        {
            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var dispatch = resolve();
            if (dispatch == null || !dispatch.Accepted)
            {
                return dispatch;
            }

            if (mOnBatchProjected != null)
            {
                mOnBatchProjected(startIndex, boardSlot, BuildPresentationResult(pipeline, startIndex));
            }

            return dispatch;
        }

        private static PostKillBoardPresentationResult BuildPresentationResult(
            IActionPipelineSystem pipeline,
            int startIndex)
        {
            var phase = NineGridArchitecture.Current.GetSystem<IPhaseSystem>();
            var summary = new PostKillBoardPresentationResult
            {
                Accepted = true,
                AvatarDefeated = phase.CurrentPhase == GamePhase.Defeat,
                NodeClearedOrRewardPhase =
                    phase.CurrentPhase == GamePhase.RewardItemChoice
                    || phase.CurrentPhase == GamePhase.ClearCheck
                    || phase.CurrentPhase == GamePhase.NodeCompleted
                    || NineGridArchitecture.Current.GetSystem<IDeckSystem>().IsNodeCleared(),
            };

            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var projection = BoardPresentationStepProjector.Project(
                pipeline.EventLog.Entries,
                startIndex,
                registry);
            summary.Steps = projection.Steps ?? Array.Empty<BoardPresentationStep>();
            summary.Moves = projection.LegacyMoves ?? Array.Empty<PostKillCardMove>();
            summary.Deals = projection.LegacyDeals ?? Array.Empty<PostKillCardDeal>();
            summary.RemovedUids = projection.LegacyRemovedUids ?? Array.Empty<int>();
            summary.DamagePopups = Array.Empty<CombatDamagePopup>();
            return summary;
        }
    }
}
