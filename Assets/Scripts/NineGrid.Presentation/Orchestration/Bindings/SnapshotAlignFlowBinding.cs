using System;
using NineGrid.Presentation.Flow.Core;

namespace NineGrid.Presentation.Orchestration.Bindings
{
    public sealed class SnapshotAlignFlowBinding : IFlowBinding
    {
        private readonly SnapshotAlignFlow mFlow;

        public SnapshotAlignFlowBinding(SnapshotAlignFlow flow)
        {
            mFlow = flow;
        }

        public FlowId Id => FlowId.SnapshotAlign;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            return new FlowHandle(mFlow, onMarker);
        }

        public void Stop()
        {
            mFlow?.StopAndRestore();
        }
    }
}
