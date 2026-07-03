using System.Collections.Generic;
using System.Text;
using NineGrid.Presentation.Orchestration;
using UnityEngine;

namespace NineGrid.Presentation.Debugging.Slices
{
    /// <summary>
    /// Tier2 观察器：记录各 Flow Play / 结束 / Impact marker 的墙钟时序。
    /// </summary>
    public sealed class FlowTraceRecorder
    {
        private readonly List<string> mEntries = new List<string>();

        public void RecordPlay(FlowId flowId, float wallTime)
        {
            mEntries.Add(Format(flowId, "Play", wallTime));
        }

        public void RecordStopped(FlowId flowId, float wallTime)
        {
            mEntries.Add(Format(flowId, "Stopped", wallTime));
        }

        public void RecordMarker(FlowId flowId, string marker, float wallTime)
        {
            mEntries.Add(Format(flowId, marker ?? "Marker", wallTime));
        }

        public void FlushToLog()
        {
            if (mEntries.Count == 0)
            {
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine("[FlowTraceRecorder] " + mEntries.Count + " events");
            for (var i = 0; i < mEntries.Count; i++)
            {
                builder.AppendLine("  " + mEntries[i]);
            }

            Debug.Log(builder.ToString());
            mEntries.Clear();
        }

        private static string Format(FlowId flowId, string phase, float wallTime)
        {
            return wallTime.ToString("F3") + "s " + flowId + " " + phase;
        }
    }
}
