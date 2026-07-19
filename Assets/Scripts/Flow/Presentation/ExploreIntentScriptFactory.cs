using System;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 空格 explore 剧本：ClickEmpty → Present → Fill → Present → Rotate → Present。
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
            var fillGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new ResolvePostKillFillCommand())));
            var rotateGate = PresentationSyncBatchGate.FromSync(
                sync,
                () => ResolveAndProject(slotIndex, () => mDispatcher.Send(new ResolvePostKillRotateCommand())));

            timeline.Enqueue(new ResolveBatchStep(clickGate));
            timeline.Enqueue(new PresentStep(clickGate, mPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(fillGate));
            timeline.Enqueue(new PresentStep(fillGate, mPresentChannel));
            timeline.Enqueue(new ResolveBatchStep(rotateGate));
            timeline.Enqueue(new PresentStep(rotateGate, mPresentChannel));
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
                mOnBatchProjected(startIndex, boardSlot, IntentBatchProjection.Build(mArchitecture, pipeline, startIndex));
            }

            return dispatch;
        }
    }
}
