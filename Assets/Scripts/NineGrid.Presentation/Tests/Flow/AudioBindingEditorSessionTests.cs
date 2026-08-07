#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NineGrid.Content.Audio;
using System.Linq;
using NineGrid.Content.Editor;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class AudioBindingEditorSessionTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":1,\"ticket\":\"#169-test\",\"bindings\":["
            + "{\"cueId\":\"ui.click\",\"note\":\"界面点击\",\"module\":\"UI\",\"enabled\":true,"
            + "\"clipKey\":\"audio/SFX/click\",\"volumeDb\":-2,\"startOffsetSeconds\":0,"
            + "\"bindingDelaySeconds\":0,\"minimumIntervalSeconds\":0.05,\"selectorContentId\":\"\"},"
            + "{\"cueId\":\"ui.click\",\"note\":\"界面点击·宝箱\",\"module\":\"UI\",\"enabled\":true,"
            + "\"clipKey\":\"audio/SFX/chest\",\"volumeDb\":-4,\"startOffsetSeconds\":0.1,"
            + "\"bindingDelaySeconds\":0.02,\"minimumIntervalSeconds\":0.1,\"selectorContentId\":\"chest.open\"}]}";

        private AudioBindingEditorSession _session;

        [SetUp]
        public void SetUp()
        {
            _session = new AudioBindingEditorSession();
            _session.LoadFromJson(
                CatalogJson,
                new[]
                {
                    new AudioBindingEditorDeclaration("ui.click", "界面点击", "UI", "TestEmitter"),
                    new AudioBindingEditorDeclaration("ui.unbound", "未绑定提示", "UI", "TestEmitter"),
                },
                new[]
                {
                    new AudioBindingEditorClipOption("audio/SFX/click", "Assets/Resources/audio/SFX/click.wav"),
                    new AudioBindingEditorClipOption("audio/SFX/chest", "Assets/Resources/audio/SFX/chest.wav"),
                });
        }

        [Test]
        public void SaveSingle_WritesOnlySelectedBinding_AndLeavesOtherDraftDirty()
        {
            var baseEntry = _session.Entries.Single(entry =>
                entry.CueId == "ui.click" && string.IsNullOrEmpty(entry.Dto.selectorContentId));
            var contentEntry = _session.Entries.Single(entry =>
                entry.Dto != null && entry.Dto.selectorContentId == "chest.open");

            baseEntry.Dto.volumeDb = -8f;
            contentEntry.Dto.minimumIntervalSeconds = 0.9f;

            Assert.AreEqual(2, _session.DirtyCount);
            Assert.IsTrue(_session.TrySave(baseEntry, out var error), error);
            Assert.AreEqual(1, _session.DirtyCount);
            Assert.IsFalse(baseEntry.IsDirty);
            Assert.IsTrue(contentEntry.IsDirty);

            var saved = JsonUtility.FromJson<AudioBindingCatalogDto>(_session.SavedJson);
            Assert.AreEqual(-8f, saved.bindings[0].volumeDb, 0.0001f);
            Assert.AreEqual(0.1f, saved.bindings[1].minimumIntervalSeconds, 0.0001f);
        }

        [Test]
        public void RevertAllDirty_RestoresSavedSnapshot_AndPreservesPlaybackHistory()
        {
            var entry = _session.Entries.Single(item =>
                item.CueId == "ui.click" && string.IsNullOrEmpty(item.Dto.selectorContentId));
            entry.Dto.clipKey = "audio/SFX/chest";
            var adapter = new RecordingAudioPlaybackAdapter();
            var audio = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                adapter,
                new FixedAudioClock());
            audio.RequestCue(AudioCueRequest.Simple("ui.click", "test.editor-history"));
            _session.SetPlaybackHistory(audio.History);

            Assert.AreEqual(1, _session.PlaybackHistory.Count(item => item.Outcome == AudioHistoryOutcome.Played));
            Assert.Greater(_session.DirtyCount, 0);

            _session.RevertAllDirty();
            var reverted = _session.Entries.Single(item =>
                item.CueId == "ui.click" && string.IsNullOrEmpty(item.Dto.selectorContentId));
            Assert.AreEqual(0, _session.DirtyCount);
            Assert.AreEqual("audio/SFX/click", reverted.Dto.clipKey);
            Assert.IsNotNull(_session.FindEntryForHistory(_session.PlaybackHistory[1]));
        }

        [Test]
        public void Filters_SearchSemanticAndTechnicalFields_AndExposeUnboundBrokenAndFailedRows()
        {
            var unbound = _session.Entries.Single(item => item.CueId == "ui.unbound");
            Assert.IsTrue(unbound.IsUnbound);

            var broken = _session.Entries.Single(item => item.CueId == "ui.click" && item.Dto.selectorContentId == "chest.open");
            broken.Dto.clipKey = "audio/SFX/missing";
            Assert.IsTrue(_session.IsBroken(broken));

            _session.SearchText = "chest.open";
            Assert.AreEqual(1, _session.GetFilteredEntries().Count());
            Assert.AreSame(broken, _session.GetFilteredEntries().Single());

            _session.SearchText = string.Empty;
            _session.FilterUnboundOnly = true;
            Assert.AreEqual(1, _session.GetFilteredEntries().Count());
            Assert.AreSame(unbound, _session.GetFilteredEntries().Single());
            _session.FilterUnboundOnly = false;
            _session.FilterBrokenOnly = true;
            Assert.AreEqual(1, _session.GetFilteredEntries().Count());
            Assert.AreSame(broken, _session.GetFilteredEntries().Single());

            _session.FilterBrokenOnly = false;
            _session.SearchText = "未绑定";
            Assert.AreEqual(1, _session.GetFilteredEntries().Count());
            Assert.AreSame(unbound, _session.GetFilteredEntries().Single());
        }

        private sealed class FixedAudioClock : IAudioClock
        {
            public double UnscaledTime => 1;
        }

        private sealed class RecordingAudioPlaybackAdapter : IAudioPlaybackAdapter
        {
            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                return AudioBackendResult.Success(request.ClipKey);
            }
        }
    }
}
#endif
