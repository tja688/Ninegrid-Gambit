using System;
using NineGrid.Presentation.Orchestration;

namespace NineGrid.Presentation.Debugging.Trace
{
    internal sealed class BattleTraceFlowBinding : IFlowBinding
    {
        private readonly IFlowBinding mInner;

        private BattleTraceFlowBinding(IFlowBinding inner)
        {
            mInner = inner;
        }

        public FlowId Id => mInner.Id;

        public static IFlowBinding Wrap(IFlowBinding inner)
        {
            if (inner == null || inner is BattleTraceFlowBinding)
            {
                return inner;
            }

            return new BattleTraceFlowBinding(inner);
        }

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            BattleTraceHooks.RecordFlowLifecycle(Id, "Play");
            Action<string> wrappedMarker = marker =>
            {
                BattleTraceHooks.RecordFlowLifecycle(Id, marker ?? "Marker");
                onMarker?.Invoke(marker);
            };

            FlowHandle handle = mInner.Play(registry, payload, wrappedMarker);
            if (handle != null && !handle.IsPlaying)
            {
                BattleTraceHooks.RecordFlowLifecycle(Id, "Stopped");
            }

            return handle;
        }

        public void Stop()
        {
            mInner.Stop();
            BattleTraceHooks.RecordFlowLifecycle(Id, "Stopped");
        }
    }
}
