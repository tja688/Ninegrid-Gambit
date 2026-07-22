using NineGrid.Core;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Commands
{
    /// <summary>
    /// Pickup / Drain / RewardDrain 经 Runtime 获取导演 external-hold 租约。
    /// </summary>
    public sealed class BeginDirectorExternalHoldCommand : AbstractCommand<bool>
    {
        private readonly string mReason;

        public BeginDirectorExternalHoldCommand(string reason = null)
        {
            mReason = reason;
        }

        protected override bool OnExecute()
        {
            var runtime = this.GetSystem<IPresentationRuntimeSystem>();
            if (runtime == null || !runtime.IsStarted)
            {
                return false;
            }

            return runtime.TryBeginExternalHold(mReason);
        }
    }

    public sealed class EndDirectorExternalHoldCommand : AbstractCommand
    {
        private readonly string mReason;

        public EndDirectorExternalHoldCommand(string reason = null)
        {
            mReason = reason;
        }

        protected override void OnExecute()
        {
            var runtime = this.GetSystem<IPresentationRuntimeSystem>();
            if (runtime != null && runtime.IsStarted)
            {
                runtime.EndExternalHold(mReason);
            }
        }
    }

    public sealed class ForceEndDirectorExternalHoldCommand : AbstractCommand
    {
        private readonly string mReason;

        public ForceEndDirectorExternalHoldCommand(string reason = null)
        {
            mReason = reason;
        }

        protected override void OnExecute()
        {
            var runtime = this.GetSystem<IPresentationRuntimeSystem>();
            if (runtime != null && runtime.IsStarted)
            {
                runtime.ForceEndExternalHold(mReason);
            }
        }
    }
}
