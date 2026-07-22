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
}
