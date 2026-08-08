using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class MusicSystemBehaviorTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":1,\"ticket\":\"#170\",\"bindings\":["
            + "{\"state\":\"MainMenu\",\"enabled\":true,\"clipKey\":\"audio/BGM/menu\",\"volumeDb\":-6,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true},"
            + "{\"state\":\"RunExploration\",\"enabled\":true,\"clipKey\":\"audio/BGM/explore\",\"volumeDb\":-6,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true},"
            + "{\"state\":\"Battle\",\"enabled\":true,\"clipKey\":\"audio/BGM/battle\",\"volumeDb\":-6,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true},"
            + "{\"state\":\"BossBattle\",\"enabled\":true,\"clipKey\":\"audio/BGM/boss\",\"volumeDb\":-6,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true},"
            + "{\"state\":\"Victory\",\"enabled\":true,\"clipKey\":\"audio/BGM/result\",\"volumeDb\":-6,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true},"
            + "{\"state\":\"Defeat\",\"enabled\":true,\"clipKey\":\"audio/BGM/result\",\"volumeDb\":-6,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true}]}";

        [Test]
        public void FormalMusicJson_ResolvesAllRequiredStates()
        {
            var catalog = MusicBindingCatalog.FromJson(
                "{\"schemaVersion\":1,\"bindings\":["
                + "{\"state\":\"MainMenu\",\"clipKey\":\"audio/BGM/menu\"},"
                + "{\"state\":\"RunExploration\",\"clipKey\":\"audio/BGM/explore\"},"
                + "{\"state\":\"Battle\",\"clipKey\":\"audio/BGM/battle\"},"
                + "{\"state\":\"BossBattle\",\"clipKey\":\"audio/BGM/boss\"},"
                + "{\"state\":\"Victory\",\"clipKey\":\"audio/BGM/victory\"},"
                + "{\"state\":\"Defeat\",\"clipKey\":\"audio/BGM/defeat\"}]}" );

            foreach (DesiredMusicState state in Enum.GetValues(typeof(DesiredMusicState)))
            {
                Assert.IsTrue(catalog.TryResolve(state, out var binding), state.ToString());
                Assert.AreEqual(state, binding.State);
                Assert.IsNotEmpty(binding.ClipKey);
            }
        }

        [Test]
        public void FormalMusicJson_AllStatesResolveToRegisteredBgmKeys()
        {
            var catalog = MusicBindingCatalog.LoadFromResources();
            var manifest = AudioAssetManifestLoader.Load();
            var entries = manifest?.entries ?? System.Array.Empty<AudioAssetManifestEntry>();
            var keys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].kind, "BGM", System.StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(entries[i].resourcesKey);
                }
            }

            foreach (DesiredMusicState state in Enum.GetValues(typeof(DesiredMusicState)))
            {
                Assert.IsTrue(catalog.TryResolve(state, out var binding), state.ToString());
                Assert.IsTrue(keys.Contains(binding.ClipKey), state + " -> " + binding.ClipKey);
            }
        }
        [Test]
        public void MusicSystem_SameStateAndSameClip_AreNoOps_AndKeepSourceAttribution()
        {
            var adapter = new RecordingMusicAdapter();
            var system = CreateSystem(adapter);

            var first = system.RequestState(new MusicStateRequest(
                DesiredMusicState.MainMenu,
                "GameFlowShellSystem.Bind"));
            var sameState = system.RequestState(new MusicStateRequest(
                DesiredMusicState.MainMenu,
                "GameFlowShellSystem.Reenter"));
            var firstResult = system.RequestState(new MusicStateRequest(
                DesiredMusicState.Victory,
                "GameFlowOrchestrator.Victory"));
            var sameClip = system.RequestState(new MusicStateRequest(
                DesiredMusicState.Defeat,
                "GameFlowOrchestrator.Defeat"));

            Assert.AreEqual(MusicRequestOutcome.Played, first.Outcome);
            Assert.AreEqual(MusicRequestOutcome.NoOp, sameState.Outcome);
            Assert.AreEqual(MusicRequestOutcome.Played, firstResult.Outcome);
            Assert.AreEqual(MusicRequestOutcome.NoOp, sameClip.Outcome);
            Assert.AreEqual(2, adapter.Played.Count);
            Assert.AreEqual(DesiredMusicState.Defeat, system.DesiredState.Value);
            Assert.AreEqual("GameFlowOrchestrator.Defeat", system.CurrentStableSource);
            Assert.AreEqual(2L, system.CurrentMusicGeneration);
        }

        [Test]
        public void MusicSystem_ActualSwitches_UseMonotonicGenerations_AndStableSources()
        {
            var adapter = new RecordingMusicAdapter();
            var system = CreateSystem(adapter);

            var a = system.RequestState(new MusicStateRequest(DesiredMusicState.MainMenu, "main-menu"));
            var b = system.RequestState(new MusicStateRequest(DesiredMusicState.Battle, "battle-node-1"));

            Assert.AreEqual(1L, a.MusicGeneration);
            Assert.AreEqual(2L, b.MusicGeneration);
            Assert.AreEqual(2L, system.CurrentMusicGeneration);
            Assert.AreEqual(DesiredMusicState.Battle, system.CurrentState.Value);
            Assert.AreEqual("battle-node-1", system.CurrentStableSource);
            Assert.AreEqual(1, system.CurrentSourceCount);
            Assert.AreEqual(1, system.RetiringSourceCount);
        }

        [Test]
        public void MusicSystem_RapidAToBToC_ReleasesOlderRetiringSource_AndIgnoresStaleCallback()
        {
            var adapter = new RecordingMusicAdapter();
            var system = CreateSystem(adapter);

            system.RequestState(new MusicStateRequest(DesiredMusicState.MainMenu, "A"));
            system.RequestState(new MusicStateRequest(DesiredMusicState.Battle, "B"));
            system.RequestState(new MusicStateRequest(DesiredMusicState.BossBattle, "C"));

            Assert.AreEqual(3, adapter.Played.Count);
            Assert.AreEqual(1, system.CurrentSourceCount);
            Assert.AreEqual(1, system.RetiringSourceCount);
            Assert.AreEqual(1, adapter.Stopped.Count);
            Assert.AreEqual(DesiredMusicState.BossBattle, system.CurrentState.Value);
            Assert.AreEqual("C", system.CurrentStableSource);
            Assert.AreEqual(3L, system.CurrentMusicGeneration);

            adapter.InvokeFade(0);
            Assert.AreEqual(DesiredMusicState.BossBattle, system.CurrentState.Value);
            Assert.AreEqual("C", system.CurrentStableSource);
            Assert.AreEqual(1, system.RetiringSourceCount);

            adapter.InvokeFade(1);
            Assert.AreEqual(0, system.RetiringSourceCount);
            Assert.AreEqual(DesiredMusicState.BossBattle, system.CurrentState.Value);
            Assert.IsTrue(HasHistory(system, MusicHistoryOutcome.StaleCallback));
        }

        [Test]
        public void MusicSystem_RejectsMissingStableSource_WithoutPlayback()
        {
            var adapter = new RecordingMusicAdapter();
            var system = CreateSystem(adapter);

            var result = system.RequestState(new MusicStateRequest(DesiredMusicState.MainMenu, ""));

            Assert.AreEqual(MusicRequestOutcome.InvalidSource, result.Outcome);
            Assert.AreEqual(0, adapter.Played.Count);
            Assert.IsTrue(HasHistory(system, MusicHistoryOutcome.InvalidSource));
        }

        private static MusicSystem CreateSystem(RecordingMusicAdapter adapter)
        {
            return new MusicSystem(
                MusicBindingCatalog.FromJson(CatalogJson),
                adapter,
                new FixedAudioClock());
        }

        private static bool HasHistory(MusicSystem system, MusicHistoryOutcome outcome)
        {
            for (var i = 0; i < system.History.Count; i++)
            {
                if (system.History[i].Outcome == outcome)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class FixedAudioClock : IAudioClock
        {
            public double UnscaledTime => 1d;
        }

        private sealed class RecordingMusicAdapter : IMusicPlaybackAdapter
        {
            public readonly List<MusicPlaybackRequest> Played = new List<MusicPlaybackRequest>();
            public readonly List<MusicPlaybackHandle> Stopped = new List<MusicPlaybackHandle>();
            private readonly List<Action> mFadeCallbacks = new List<Action>();

            public MusicBackendResult Play(MusicPlaybackRequest request)
            {
                Played.Add(request);
                var handle = new MusicPlaybackHandle(request.Binding.ClipKey, request.Binding.State);
                return MusicBackendResult.Success(handle, request.Binding.ClipKey);
            }

            public void FadeOut(MusicPlaybackHandle handle, float durationSeconds, Action completed)
            {
                mFadeCallbacks.Add(completed);
            }

            public void Stop(MusicPlaybackHandle handle)
            {
                Stopped.Add(handle);
            }

            public void InvokeFade(int index)
            {
                mFadeCallbacks[index]?.Invoke();
            }
        }
    }
}
