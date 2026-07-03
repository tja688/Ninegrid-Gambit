using System;
using NineGrid.Presentation.Orchestration;

namespace NineGrid.Presentation.Debugging.Slices
{
    /// <summary>
    /// 包装已有 <see cref="IFlowBinding"/>，向 <see cref="FlowTraceRecorder"/> 记录时序。
    /// </summary>
    public sealed class TracingFlowBinding : IFlowBinding
    {
        private readonly IFlowBinding mInner;
        private readonly FlowTraceRecorder mRecorder;

        public TracingFlowBinding(IFlowBinding inner, FlowTraceRecorder recorder)
        {
            mInner = inner;
            mRecorder = recorder;
        }

        public FlowId Id => mInner.Id;

        public FlowHandle Play(IViewRegistry registry, FlowPayload payload, Action<string> onMarker = null)
        {
            mRecorder?.RecordPlay(Id, UnityEngine.Time.realtimeSinceStartup);
            Action<string> wrappedMarker = marker =>
            {
                mRecorder?.RecordMarker(Id, marker, UnityEngine.Time.realtimeSinceStartup);
                onMarker?.Invoke(marker);
            };

            FlowHandle handle = mInner.Play(registry, payload, wrappedMarker);
            if (handle?.DirectedFlow != null && !handle.IsPlaying)
            {
                mRecorder?.RecordStopped(Id, UnityEngine.Time.realtimeSinceStartup);
            }

            return handle;
        }

        public void Stop()
        {
            mInner.Stop();
            mRecorder?.RecordStopped(Id, UnityEngine.Time.realtimeSinceStartup);
        }
    }
}
