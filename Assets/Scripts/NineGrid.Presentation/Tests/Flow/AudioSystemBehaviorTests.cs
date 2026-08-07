using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class AudioSystemBehaviorTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":1,\"ticket\":\"#168\",\"bindings\":["
            + "{\"cueId\":\"ui.main_menu.start\",\"note\":\"主菜单开始游戏点击\","
            + "\"module\":\"MainMenu\",\"enabled\":true,\"clipKey\":\"audio/SFX/按钮点击\","
            + "\"volumeDb\":-2,\"startOffsetSeconds\":0,\"bindingDelaySeconds\":0,"
            + "\"minimumIntervalSeconds\":0,\"selectorCardDefId\":\"\",\"selectorSkillId\":\"\","
            + "\"selectorRoomId\":\"\",\"selectorItemDefId\":\"\",\"selectorContentId\":\"\"}]}";

        [Test]
        public void AudioSystem_RequestCue_RecordsRequestedAndPlayed_AndUsesBinding()
        {
            var adapter = new RecordingAudioPlaybackAdapter();
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                adapter,
                new FakeAudioClock());

            var result = system.RequestCue(AudioCueRequest.Simple(
                "ui.main_menu.start",
                "GameFlowController.BeginFormalRun"));

            Assert.AreEqual(AudioCueOutcome.Played, result.Outcome);
            Assert.AreEqual("audio/SFX/按钮点击", adapter.PlayedClipKeys[0]);
            Assert.AreEqual(2, system.History.Count);
            Assert.AreEqual(AudioHistoryOutcome.Requested, system.History[0].Outcome);
            Assert.AreEqual(AudioHistoryOutcome.Played, system.History[1].Outcome);
            Assert.AreEqual("主菜单开始游戏点击", system.History[1].CueNote);
            Assert.AreEqual("audio/SFX/按钮点击", system.History[1].ActualClipKey);
        }

        [Test]
        public void AudioSystem_UnboundCue_IsSilentAndObservable()
        {
            var adapter = new RecordingAudioPlaybackAdapter();
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                adapter,
                new FakeAudioClock());

            var result = system.RequestCue(AudioCueRequest.Simple(
                "ui.missing",
                "test.unbound"));

            Assert.AreEqual(AudioCueOutcome.Unbound, result.Outcome);
            Assert.IsEmpty(adapter.PlayedClipKeys);
            Assert.AreEqual(AudioHistoryOutcome.Unbound, system.History[1].Outcome);
            Assert.AreEqual("ui.missing", system.History[1].CueId);
        }

        [Test]
        public void AudioSystem_BackendFailure_IsObservableAndDoesNotThrow()
        {
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                new FailingAudioPlaybackAdapter("backend unavailable"),
                new FakeAudioClock());

            AudioCueResult result = null;
            Assert.DoesNotThrow(() => result = system.RequestCue(AudioCueRequest.Simple(
                "ui.main_menu.start",
                "test.backend")));

            Assert.AreEqual(AudioCueOutcome.BackendFailure, result.Outcome);
            Assert.AreEqual(AudioHistoryOutcome.BackendFailure, system.History[1].Outcome);
            Assert.AreEqual("backend unavailable", system.History[1].FailureReason);
        }

        [Test]
        public void TriggerPulseHub_TypedRequest_ReachesIAudioSystem_AndSimpleEntryRemainsAvailable()
        {
            var adapter = new RecordingAudioPlaybackAdapter();
            var system = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                adapter,
                new FakeAudioClock());
            TriggerPulseHub.Configure(
                NullTriggerPulseSink.Instance,
                new AudioTriggerPulseSink(system));

            try
            {
                TriggerPulseHub.PulseAudio(new AudioCueRequest(
                    "ui.main_menu.start",
                    "GameFlowController",
                    cardDefId: "",
                    skillId: "",
                    roomId: "",
                    itemDefId: "",
                    contentId: ""));
                TriggerPulseHub.PulseAudio("ui.main_menu.start");

                Assert.AreEqual(2, CountPlayed(system));
                Assert.AreEqual(2, adapter.PlayedClipKeys.Count);
            }
            finally
            {
                TriggerPulseHub.ResetToNull();
            }
        }

        [Test]
        public void AudioBindingCatalog_UsesStableContentOverrideBeforeBaseBinding()
        {
            const string json =
                "{\"schemaVersion\":1,\"bindings\":["
                + "{\"cueId\":\"skill.cast\",\"note\":\"基础\",\"enabled\":true,"
                + "\"clipKey\":\"audio/SFX/界面点击\",\"selectorContentId\":\"\"},"
                + "{\"cueId\":\"skill.cast\",\"note\":\"内容专属\",\"enabled\":true,"
                + "\"clipKey\":\"audio/SFX/施法增强\",\"selectorSkillId\":\"skill.fire\"}]}";

            var catalog = AudioBindingCatalog.FromJson(json);
            Assert.IsTrue(catalog.TryResolve(
                new AudioCueRequest("skill.cast", "test", "", "skill.fire", "", "", ""),
                out var resolved));

            Assert.AreEqual("内容专属", resolved.Note);
            Assert.AreEqual("audio/SFX/施法增强", resolved.ClipKey);
        }

        [Test]
        public void FormalBindingsJson_ResolvesMainMenuCue_ToRegisteredFormalClip()
        {
            var catalog = AudioBindingCatalog.LoadFromResources();
            Assert.IsTrue(catalog.TryResolve(
                AudioCueRequest.Simple("ui.main_menu.start", "test.formal-json"),
                out var binding));

            Assert.AreEqual("主菜单开始游戏点击", binding.Note);
            Assert.AreEqual("MainMenu", binding.Module);
            Assert.AreEqual("audio/SFX/按钮点击", binding.ClipKey);
            Assert.IsTrue(AudioAssetManifestLoader.IsKnownFormalKey(binding.ClipKey));
        }

        private static int CountPlayed(IAudioSystem system)
        {
            var count = 0;
            for (var i = 0; i < system.History.Count; i++)
            {
                if (system.History[i].Outcome == AudioHistoryOutcome.Played)
                {
                    count++;
                }
            }

            return count;
        }

        private sealed class FakeAudioClock : IAudioClock
        {
            public double UnscaledTime => 1;
        }

        private sealed class RecordingAudioPlaybackAdapter : IAudioPlaybackAdapter
        {
            public readonly List<string> PlayedClipKeys = new List<string>();

            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                PlayedClipKeys.Add(request.ClipKey);
                return AudioBackendResult.Success(request.ClipKey);
            }
        }

        private sealed class FailingAudioPlaybackAdapter : IAudioPlaybackAdapter
        {
            private readonly string mReason;

            public FailingAudioPlaybackAdapter(string reason)
            {
                mReason = reason;
            }

            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                return AudioBackendResult.Failure(mReason);
            }
        }
    }
}
