using System;
using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core
{
    public sealed class CoreCommandDispatchResult
    {
        public CoreCommandDispatchResult(CoreCommandResult commandResult, PresentationBatch batch, bool batchOpened)
        {
            CommandResult = commandResult;
            Batch = batch;
            BatchOpened = batchOpened;
        }

        public CoreCommandResult CommandResult { get; private set; }
        public PresentationBatch Batch { get; private set; }
        public bool BatchOpened { get; private set; }

        public bool Accepted
        {
            get { return CommandResult != null && CommandResult.Accepted; }
        }
    }

    public sealed class CoreCommandDispatcher
    {
        private readonly IArchitecture mArchitecture;
        private int mNextBatchId;

        public CoreCommandDispatcher(IArchitecture architecture)
            : this(architecture, 1)
        {
        }

        public CoreCommandDispatcher(IArchitecture architecture, int firstBatchId)
        {
            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            mArchitecture = architecture;
            mNextBatchId = Math.Max(1, firstBatchId);
        }

        public CoreCommandDispatchResult Send(ICommand<CoreCommandResult> command)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            var pipeline = mArchitecture.GetSystem<IActionPipelineSystem>();
            var sync = mArchitecture.GetSystem<IPresentationSyncSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var wasLocked = sync.IsInputLocked;
            var isPresentationFinished = command is PresentationFinishedCommand;

            var commandResult = mArchitecture.SendCommand(command);
            var batch = PresentationBatchFactory.FromEventLog(
                pipeline.EventLog,
                startIndex,
                mNextBatchId++,
                CoreViewSnapshotFactory.Capture(mArchitecture));

            var opened = false;
            // 拒收命令不得打开批次，否则导演门会因 ActiveBatchId>0 却 Accepted=false 而卡死。
            if (!wasLocked && !isPresentationFinished && commandResult != null && commandResult.Accepted)
            {
                sync.OpenBatch(batch);
                opened = true;
            }

            return new CoreCommandDispatchResult(commandResult, batch, opened);
        }
    }
}
