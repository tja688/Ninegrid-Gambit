using System;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// 洗回牌库：扫描 EventLog 切片并入导演 sink（不立刻 Present）。
    /// </summary>
    public sealed class EnqueueShuffleIntoDeckFromEventLogCommand : AbstractCommand<int>
    {
        private readonly ShuffleIntoDeckPresentSink mSink;
        private readonly int mStartIndex;
        private readonly Func<int, bool> mIsAlreadyInDeck;
        private readonly ShuffleIntoDeckScheduler mScheduler;

        public EnqueueShuffleIntoDeckFromEventLogCommand(
            ShuffleIntoDeckPresentSink sink,
            int startIndex,
            Func<int, bool> isAlreadyInDeck = null,
            ShuffleIntoDeckScheduler scheduler = null)
        {
            mSink = sink;
            mStartIndex = startIndex;
            mIsAlreadyInDeck = isAlreadyInDeck;
            mScheduler = scheduler ?? new ShuffleIntoDeckScheduler();
        }

        protected override int OnExecute()
        {
            if (mSink == null)
            {
                return 0;
            }

            return mScheduler.EnqueueFromEventLog(
                mSink,
                NineGridArchitecture.Interface,
                mStartIndex,
                mIsAlreadyInDeck);
        }
    }
}
