using NineGrid.Core;
using NineGrid.Core.Commands;
using QFramework;

namespace NineGrid.Presentation.Diagnostics
{
    public sealed class TracedCoreCommandDispatcher
    {
        private readonly CoreCommandDispatcher mInner;
        private readonly string mSource;

        public TracedCoreCommandDispatcher(IArchitecture architecture, string source)
            : this(new CoreCommandDispatcher(architecture), source)
        {
        }

        public TracedCoreCommandDispatcher(CoreCommandDispatcher inner, string source)
        {
            mInner = inner;
            mSource = source ?? "Unknown";
        }

        public CoreCommandDispatchResult Send(ICommand<CoreCommandResult> command)
        {
            var commandName = command != null ? command.GetType().Name : "null";
            var result = mInner.Send(command);
            var rejectReason = result.CommandResult != null && !result.CommandResult.Accepted
                ? result.CommandResult.Reason
                : string.Empty;

            PresentationTrace.Log(
                PresentationTraceChannel.Command,
                result.Accepted ? PresentationTraceLevel.Info : PresentationTraceLevel.Warn,
                "CMD_SEND",
                ("source", mSource),
                ("command", commandName),
                ("accepted", result.Accepted),
                ("batchOpened", result.BatchOpened),
                ("batch", result.Batch != null ? result.Batch.BatchId : 0),
                ("reject", rejectReason));

            return result;
        }
    }
}
