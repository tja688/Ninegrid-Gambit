using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class CardDeckEntryDurationTests
    {
        [Test]
        public void EstimateEntryDuration_MatchesEntrySlideAndRippleFormula()
        {
            var settings = new CardDeckLayoutSettings
            {
                entryDealInterval = 0.06f,
                rippleDelayPerSlot = 0.04f,
                moveDuration = 0.28f,
            };

            Assert.AreEqual(0f, settings.EstimateEntryDuration(0), 0.0001f);
            Assert.AreEqual(0.56f, settings.EstimateEntryDuration(1), 0.0001f);
            Assert.AreEqual(1.76f, settings.EstimateEntryDuration(13), 0.0001f);
        }

        [Test]
        public void DeckEntryAudioSession_PulseSlideBeat_EmitsDeckEntryCue()
        {
            TriggerPulseHub.ResetToNull();
            var sink = new CaptureSink();
            TriggerPulseHub.Configure(NullTriggerPulseSink.Instance, sink);

            var session = CardLifecycleAudioCues.BeginDeckEntryAudio("test");
            session.PulseSlideBeat();
            session.Complete();

            CollectionAssert.AreEqual(
                new[] { CardLifecycleAudioCues.DeckEntry },
                sink.CueIds);
        }

        private sealed class CaptureSink : ITriggerPulseSink, IAudioCuePulseSink
        {
            public readonly System.Collections.Generic.List<string> CueIds =
                new System.Collections.Generic.List<string>();

            public void Pulse(string triggerId)
            {
                CueIds.Add(triggerId);
            }

            public void Pulse(NineGrid.Content.Audio.AudioCueRequest request)
            {
                CueIds.Add(request.CueId);
            }
        }
    }
}
