using System.Collections.Generic;
using NineGrid.Flow.Presentation;

namespace NineGrid.Presentation.Tests.Fixtures
{
    public sealed class RecordingUiPickSink : IUiPickPreviewSink
    {
        public readonly List<InputIntent> Previews = new List<InputIntent>();

        public void Preview(InputIntent intent)
        {
            Previews.Add(intent);
        }
    }

    public sealed class RecordingTriggerSink : ITriggerPulseSink
    {
        public readonly List<string> Pulses = new List<string>();

        public void Pulse(string triggerId)
        {
            Pulses.Add(triggerId);
        }
    }

    /// <summary>#50 impatience-tap 计数假体。</summary>
    public sealed class RecordingAccelerationSink : IAccelerationSink
    {
        public readonly List<InputIntent> Taps = new List<InputIntent>();

        public void Tap(InputIntent intent)
        {
            Taps.Add(intent);
        }
    }
}
