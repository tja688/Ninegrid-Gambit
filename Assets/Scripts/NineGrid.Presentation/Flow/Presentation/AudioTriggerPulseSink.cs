using NineGrid.Content.Audio;

namespace NineGrid.Flow.Presentation
{
    public interface IAudioCuePulseSink
    {
        void Pulse(AudioCueRequest request);
    }

    public sealed class AudioTriggerPulseSink : ITriggerPulseSink, IAudioCuePulseSink
    {
        private readonly NineGrid.Presentation.Systems.IAudioSystem mAudio;

        public AudioTriggerPulseSink(NineGrid.Presentation.Systems.IAudioSystem audio = null)
        {
            mAudio = audio;
        }

        public void Pulse(string triggerId)
        {
            Pulse(AudioCueRequest.Simple(triggerId, "TriggerPulseHub.PulseAudio"));
        }

        public void Pulse(AudioCueRequest request)
        {
            var audio = mAudio
                ?? NineGrid.Presentation.Systems.AudioSystem.EnsureRegistered();
            audio.RequestCue(request);
        }
    }
}
