using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
namespace NineGrid.Presentation.Tests
{
    public sealed class GameFlowMusicSubmissionTests
    {
        [Test]
        public void SetStateCommand_SubmitsExpectedMusicState_WithStableSource()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var architecture = NineGridArchitecture.Interface;
                architecture.RegisterModel(new RunModel());
                architecture.RegisterSystem(new GameFlowShellSystem());
                architecture.RegisterSystem<IMusicSystem>(new MusicSystem(
                    MusicBindingCatalog.FromJson(
                        "{\"schemaVersion\":1,\"bindings\":["
                        + "{\"state\":\"MainMenu\",\"clipKey\":\"audio/BGM/menu\"},"
                        + "{\"state\":\"RunExploration\",\"clipKey\":\"audio/BGM/explore\"},"
                        + "{\"state\":\"Battle\",\"clipKey\":\"audio/BGM/battle\"},"
                        + "{\"state\":\"BossBattle\",\"clipKey\":\"audio/BGM/boss\"},"
                        + "{\"state\":\"Victory\",\"clipKey\":\"audio/BGM/victory\"},"
                        + "{\"state\":\"Defeat\",\"clipKey\":\"audio/BGM/defeat\"}]}"),
                    new RecordingMusicAdapter(),
                    new FixedAudioClock()));

                var shell = architecture.GetSystem<IGameFlowShellSystem>();
                var music = architecture.GetSystem<IMusicSystem>();

                architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.BattleStub));
                Assert.AreEqual(DesiredMusicState.Battle, music.DesiredState.Value);
                Assert.AreEqual(
                    "SetGameFlowShellStateCommand:" + GameFlowShellState.BattleStub,
                    music.CurrentStableSource);

                architecture.SendCommand(new SetGameFlowShellStateCommand(GameFlowShellState.VictoryNotice));
                Assert.AreEqual(DesiredMusicState.Victory, music.DesiredState.Value);
                Assert.AreEqual(
                    "SetGameFlowShellStateCommand:" + GameFlowShellState.VictoryNotice,
                    music.CurrentStableSource);
            }
        }

        private sealed class FixedAudioClock : IAudioClock
        {
            public double UnscaledTime => 1d;
        }

        private sealed class RecordingMusicAdapter : IMusicPlaybackAdapter
        {
            public readonly List<MusicPlaybackRequest> Played = new List<MusicPlaybackRequest>();

            public MusicBackendResult Play(MusicPlaybackRequest request)
            {
                Played.Add(request);
                return MusicBackendResult.Success(
                    new MusicPlaybackHandle(request.Binding.ClipKey, request.Binding.State),
                    request.Binding.ClipKey);
            }

            public void FadeOut(MusicPlaybackHandle handle, float durationSeconds, Action completed)
            {
                completed?.Invoke();
            }

            public void Stop(MusicPlaybackHandle handle)
            {
            }
        }
    }
}
