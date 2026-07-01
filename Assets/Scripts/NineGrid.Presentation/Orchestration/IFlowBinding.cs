using System;
using NineGrid.Presentation.Contracts;

namespace NineGrid.Presentation.Orchestration
{
    public sealed class FlowHandle
    {
        public FlowHandle(IDirectedFlow directedFlow, Action<string> onMarker = null)
        {
            DirectedFlow = directedFlow;
            OnMarker = onMarker;
        }

        public IDirectedFlow DirectedFlow { get; }
        public Action<string> OnMarker { get; }

        public bool IsPlaying => DirectedFlow != null && DirectedFlow.IsPlaying;
        public float ExpectedDuration => DirectedFlow != null ? DirectedFlow.ExpectedDuration : 0f;

        public void NotifyMarker(string marker)
        {
            OnMarker?.Invoke(marker);
        }
    }

    public interface IFlowBinding
    {
        FlowId Id { get; }
        FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null);
        void Stop();
    }
}
