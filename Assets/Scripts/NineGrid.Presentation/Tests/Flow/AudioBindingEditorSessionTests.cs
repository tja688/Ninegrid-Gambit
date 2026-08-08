#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NineGrid.Content.Audio;
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
        public void SaveAllAndSingleRevert_PersistTuningFields_AndRestoreWorkingCopy()
        {
            var entry = _session.Entries.Single(item =>
                item.CueId == "ui.click" && item.Dto != null && item.Dto.selectorContentId == "chest.open");
            entry.Dto.volumeDb = -9f;
            entry.Dto.startOffsetSeconds = 0.3f;
            entry.Dto.bindingDelaySeconds = 0.25f;
            entry.Dto.minimumIntervalSeconds = 0.75f;
            entry.Dto.enabled = false;

            Assert.IsTrue(_session.TrySaveAll(out var error), error);
            var saved = JsonUtility.FromJson<AudioBindingCatalogDto>(_session.SavedJson);
            var savedEntry = saved.bindings.Single(item => item.selectorContentId == "chest.open");
            Assert.AreEqual(-9f, savedEntry.volumeDb, 0.0001f);
            Assert.AreEqual(0.3f, savedEntry.startOffsetSeconds, 0.0001f);
            Assert.AreEqual(0.25f, savedEntry.bindingDelaySeconds, 0.0001f);
            Assert.AreEqual(0.75f, savedEntry.minimumIntervalSeconds, 0.0001f);
            Assert.IsFalse(savedEntry.enabled);
            Assert.AreEqual(0, _session.DirtyCount);

            entry.Dto.volumeDb = -1f;
            _session.Revert(entry);
            Assert.AreEqual(-9f, entry.Dto.volumeDb, 0.0001f);
            Assert.AreEqual(0, _session.DirtyCount);
        }

        [Test]
        public void SaveRejectsDuplicateSelectorKeys()
        {
            var baseEntry = _session.Entries.Single(item =>
                item.CueId == "ui.click" && item.Dto != null && string.IsNullOrEmpty(item.Dto.selectorContentId));
            baseEntry.Dto.selectorContentId = "chest.open";

            Assert.IsFalse(_session.TrySave(baseEntry, out var error));
            StringAssert.Contains("相同", error);
            Assert.Greater(_session.DirtyCount, 0);
        }

        [Test]
        public void RequestedHistory_LocatesResolvedContentBinding()
        {
            var contentEntry = _session.Entries.Single(item =>
                item.Dto != null && item.Dto.selectorContentId == "chest.open");
            var audio = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                new RecordingAudioPlaybackAdapter(),
                new FixedAudioClock());
            audio.RequestCue(new AudioCueRequest(
                "ui.click",
                "test.requested",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "chest.open"));

            var requested = audio.History[0];
            Assert.AreEqual(AudioHistoryOutcome.Requested, requested.Outcome);
            Assert.AreEqual(contentEntry.BindingKey, requested.BindingKey);
            Assert.AreSame(contentEntry, _session.FindEntryForHistory(requested));
        }

        [Test]
        public void ChainedSelectorRetarget_SaveAllKeepsDistinctRows()
        {
            var baseEntry = _session.Entries.Single(item =>
                item.Dto != null && string.IsNullOrEmpty(item.Dto.selectorContentId));
            var contentEntry = _session.Entries.Single(item =>
                item.Dto != null && item.Dto.selectorContentId == "chest.open");

            baseEntry.Dto.selectorContentId = "first.target";
            Assert.IsTrue(_session.TrySaveAll(out var error), error);
            contentEntry.Dto.selectorContentId = "second.target";
            Assert.IsTrue(_session.TrySaveAll(out error), error);
            baseEntry.Dto.selectorContentId = "second.target";

            Assert.IsTrue(_session.TrySaveAll(out error), error);
            var saved = JsonUtility.FromJson<AudioBindingCatalogDto>(_session.SavedJson);
            Assert.AreEqual(2, saved.bindings.Length);
            CollectionAssert.AreEquivalent(
                new[] { "first.target", "second.target" },
                saved.bindings.Select(item => item.selectorContentId).ToArray());
        }

        [Test]
        public void FailedFilter_UsesBackendFailureHistory()
        {
            var audio = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                new FailingAudioPlaybackAdapter(),
                new FixedAudioClock());
            audio.RequestCue(AudioCueRequest.Simple("ui.click", "test.failure"));
            _session.SetPlaybackHistory(audio.History);
            _session.FilterFailedOnly = true;

            var filtered = _session.GetFilteredEntries().ToList();
            Assert.AreEqual(1, filtered.Count);
            Assert.IsTrue(filtered[0].Dto != null && string.IsNullOrEmpty(filtered[0].Dto.selectorContentId));
        }

        [Test]
        public void HistoryLocate_UsesSavedBindingKeyAfterSelectorEdit()
        {
            var entry = _session.Entries.Single(item =>
                item.CueId == "ui.click" && item.Dto != null && string.IsNullOrEmpty(item.Dto.selectorContentId));
            var audio = new AudioSystem(
                AudioBindingCatalog.FromJson(CatalogJson),
                new RecordingAudioPlaybackAdapter(),
                new FixedAudioClock());
            audio.RequestCue(AudioCueRequest.Simple("ui.click", "test.locate"));
            entry.Dto.selectorContentId = "edited.content";

            Assert.AreSame(entry, _session.FindEntryForHistory(audio.History[1]));
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

        private sealed class FailingAudioPlaybackAdapter : IAudioPlaybackAdapter
        {
            public AudioBackendResult Play(AudioPlaybackRequest request)
            {
                return AudioBackendResult.Failure("test failure");
            }
        }
    }
}
#endif
