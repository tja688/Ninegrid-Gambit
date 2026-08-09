using System.Collections.Generic;
using System.Linq;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#187 工作台 Session 补丁、夹紧、快照恢复与空补丁/键冲突门禁。</summary>
    public sealed class AudioBindingEditorSessionTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":3,\"ticket\":\"#179\",\"bindings\":["
            + "{\"cueId\":\"ui.test.save\",\"note\":\"保存测试\",\"module\":\"UI\",\"enabled\":true,"
            + "\"clipKey\":\"audio/SFX/click\",\"volumeDb\":-3,\"startOffsetSeconds\":0,"
            + "\"bindingDelaySeconds\":0,\"minimumIntervalSeconds\":0,\"authoringStatus\":\"aiDraft\"},"
            + "{\"cueId\":\"ui.test.revert\",\"note\":\"回撤测试\",\"module\":\"UI\",\"enabled\":true,"
            + "\"clipKey\":\"audio/SFX/click\",\"volumeDb\":-6,\"startOffsetSeconds\":0,"
            + "\"bindingDelaySeconds\":0,\"minimumIntervalSeconds\":0,\"authoringStatus\":\"aiDraft\"}"
            + "]}";

        [Test]
        public void TrySave_MarksHumanConfirmedAndClearsDirty_WithoutDiskPath()
        {
            var session = CreateSession();
            var entry = session.Entries.First(e => e.CueId == "ui.test.save");
            entry.Dto.volumeDb = -12f;

            Assert.IsTrue(entry.IsDirty);
            Assert.AreEqual(1, session.DirtyCount);
            Assert.IsTrue(session.TrySave(entry, out var error), error);
            Assert.IsNull(error);
            Assert.IsFalse(entry.IsDirty);
            Assert.AreEqual(0, session.DirtyCount);
            Assert.AreEqual(AudioBindingAuthoringStatuses.HumanConfirmed, entry.Dto.authoringStatus);
            Assert.AreEqual(-12f, entry.Dto.volumeDb, 0.001f);
        }

        [Test]
        public void Revert_RestoresDiskSnapshotAndKeepsSiblingDirty()
        {
            var session = CreateSession();
            var save = session.Entries.First(e => e.CueId == "ui.test.save");
            var revert = session.Entries.First(e => e.CueId == "ui.test.revert");
            save.Dto.volumeDb = -20f;
            revert.Dto.volumeDb = -1f;

            session.Revert(revert);

            Assert.IsTrue(save.IsDirty);
            Assert.IsFalse(revert.IsDirty);
            Assert.AreEqual(-6f, revert.Dto.volumeDb, 0.001f);
            Assert.AreEqual(1, session.DirtyCount);
        }

        [Test]
        public void TrySaveAll_And_RevertAllDirty_RoundTrip()
        {
            var session = CreateSession();
            var first = session.Entries.First(e => e.CueId == "ui.test.save");
            var second = session.Entries.First(e => e.CueId == "ui.test.revert");
            first.Dto.volumeDb = -15f;
            second.Dto.bindingDelaySeconds = 0.8f;

            Assert.AreEqual(2, session.DirtyCount);
            Assert.IsTrue(session.TrySaveAll(out var error), error);
            Assert.AreEqual(0, session.DirtyCount);
            Assert.AreEqual(AudioBindingAuthoringStatuses.HumanConfirmed, first.Dto.authoringStatus);
            Assert.AreEqual(AudioBindingAuthoringStatuses.HumanConfirmed, second.Dto.authoringStatus);

            first.Dto.volumeDb = 0f;
            second.Dto.volumeDb = 0f;
            Assert.AreEqual(2, session.DirtyCount);
            session.RevertAllDirty();
            Assert.AreEqual(0, session.DirtyCount);
            Assert.AreEqual(-15f, first.Dto.volumeDb, 0.001f);
            Assert.AreEqual(-6f, second.Dto.volumeDb, 0.001f);
            Assert.AreEqual(0.8f, second.Dto.bindingDelaySeconds, 0.001f);
        }

        [Test]
        public void TryReplaceWorkingDto_ClampsNonNegative_KeepsAuthoritativeFields()
        {
            var session = CreateSession();
            var entry = session.Entries.First(e => e.CueId == "ui.test.save");
            var originalKey = entry.BindingKey;
            var replacement = new AudioBindingDto
            {
                cueId = "should-not-apply",
                note = "should-not-apply",
                module = "should-not-apply",
                authoringStatus = AudioBindingAuthoringStatuses.HumanConfirmed,
                enabled = false,
                clipKey = "audio/SFX/click",
                volumeDb = -9f,
                startOffsetSeconds = -1.5f,
                bindingDelaySeconds = -0.2f,
                minimumIntervalSeconds = -3f,
                variants = new[]
                {
                    new AudioVariantDto
                    {
                        variantId = "a",
                        clipKey = "audio/SFX/click",
                        weight = -2f,
                        startOffsetSeconds = -4f,
                    },
                },
            };

            Assert.IsTrue(session.TryReplaceWorkingDto(originalKey, replacement, out var updated, out var error), error);
            Assert.AreSame(entry, updated);
            Assert.AreEqual("ui.test.save", entry.Dto.cueId);
            Assert.AreEqual("保存测试", entry.Dto.note);
            Assert.AreEqual("UI", entry.Dto.module);
            Assert.AreEqual(AudioBindingAuthoringStatuses.AiDraft, entry.Dto.authoringStatus);
            Assert.IsFalse(entry.Dto.enabled);
            Assert.AreEqual(0f, entry.Dto.startOffsetSeconds, 0.001f);
            Assert.AreEqual(0f, entry.Dto.bindingDelaySeconds, 0.001f);
            Assert.AreEqual(0f, entry.Dto.minimumIntervalSeconds, 0.001f);
            Assert.AreEqual(0f, entry.Dto.variants[0].weight, 0.001f);
            Assert.AreEqual(0f, entry.Dto.variants[0].startOffsetSeconds, 0.001f);
        }

        [Test]
        public void TryReplaceWorkingDto_RejectsNullPatch_AndKeyCollision()
        {
            var session = CreateSession();
            var first = session.Entries.First(e => e.CueId == "ui.test.save");

            Assert.IsFalse(session.TryReplaceWorkingDto(first.BindingKey, null, out _, out var nullError));
            StringAssert.Contains("空补丁", nullError);

            var working = session.BuildWorkingCatalog();
            var rows = working.bindings.ToList();
            rows.Add(new AudioBindingDto
            {
                cueId = "ui.test.save",
                note = "dup",
                module = "UI",
                enabled = true,
                clipKey = "audio/SFX/click",
                authoringStatus = AudioBindingAuthoringStatuses.AiDraft,
                selectorCardDefId = "card.collide",
            });
            working.bindings = rows.ToArray();
            session.LoadFromSnapshots(
                CatalogJson,
                JsonUtility.ToJson(working),
                new[]
                {
                    new AudioBindingEditorDeclaration("ui.test.save", "保存测试", "UI", "Tests"),
                    new AudioBindingEditorDeclaration("ui.test.revert", "回撤测试", "UI", "Tests"),
                },
                new[] { new AudioBindingEditorClipOption("audio/SFX/click", "Assets/Resources/audio/SFX/click.wav") });

            var baseRow = session.Entries.First(e =>
                e.CueId == "ui.test.save" && string.IsNullOrEmpty(e.Dto.selectorCardDefId));
            var collidePatch = new AudioBindingDto
            {
                enabled = true,
                clipKey = "audio/SFX/click",
                selectorCardDefId = "card.collide",
            };
            Assert.IsFalse(
                session.TryReplaceWorkingDto(baseRow.BindingKey, collidePatch, out _, out var collideError));
            StringAssert.Contains("键冲突", collideError);
        }

        [Test]
        public void BuildWorkingJson_And_LoadFromSnapshots_RoundTripDirty()
        {
            var session = CreateSession();
            var entry = session.Entries.First(e => e.CueId == "ui.test.save");
            entry.Dto.volumeDb = -18f;
            var working = session.BuildWorkingJson();

            var restored = new AudioBindingEditorSession();
            restored.LoadFromSnapshots(
                CatalogJson,
                working,
                new[]
                {
                    new AudioBindingEditorDeclaration("ui.test.save", "保存测试", "UI", "Tests"),
                    new AudioBindingEditorDeclaration("ui.test.revert", "回撤测试", "UI", "Tests"),
                },
                new[] { new AudioBindingEditorClipOption("audio/SFX/click", "Assets/Resources/audio/SFX/click.wav") });

            var restoredEntry = restored.Entries.First(e => e.CueId == "ui.test.save");
            Assert.IsTrue(restoredEntry.IsDirty);
            Assert.AreEqual(-18f, restoredEntry.Dto.volumeDb, 0.001f);
            restored.Revert(restoredEntry);
            Assert.IsFalse(restoredEntry.IsDirty);
            Assert.AreEqual(-3f, restoredEntry.Dto.volumeDb, 0.001f);
        }

        [Test]
        public void MusicTryReplaceWorkingDto_ClampsAndRejectsEmpty()
        {
            var music = new MusicBindingEditorSession();
            music.LoadFromJson(
                "{\"schemaVersion\":1,\"ticket\":\"#172\",\"bindings\":["
                + "{\"state\":\"MainMenu\",\"enabled\":true,\"clipKey\":\"audio/BGM/menu\","
                + "\"volumeDb\":-6,\"startOffsetSeconds\":0,\"fadeInSeconds\":0.1,\"fadeOutSeconds\":0.2,\"loop\":true}]}");
            Assert.IsFalse(music.TryReplaceWorkingDto("MainMenu", null, out _, out var error));
            StringAssert.Contains("空补丁", error);

            Assert.IsTrue(music.TryReplaceWorkingDto(
                "MainMenu",
                new MusicBindingDto
                {
                    enabled = false,
                    clipKey = "audio/BGM/menu",
                    volumeDb = -3f,
                    startOffsetSeconds = -1f,
                    fadeInSeconds = -2f,
                    fadeOutSeconds = -3f,
                    loop = false,
                },
                out var entry,
                out var replaceError), replaceError);
            Assert.AreEqual(0f, entry.Dto.startOffsetSeconds, 0.001f);
            Assert.AreEqual(0f, entry.Dto.fadeInSeconds, 0.001f);
            Assert.AreEqual(0f, entry.Dto.fadeOutSeconds, 0.001f);
            Assert.IsFalse(entry.Dto.enabled);
            Assert.IsFalse(entry.Dto.loop);
            Assert.IsTrue(entry.IsDirty);
        }

        private static AudioBindingEditorSession CreateSession()
        {
            var session = new AudioBindingEditorSession();
            session.LoadFromJson(
                CatalogJson,
                new[]
                {
                    new AudioBindingEditorDeclaration("ui.test.save", "保存测试", "UI", "Tests"),
                    new AudioBindingEditorDeclaration("ui.test.revert", "回撤测试", "UI", "Tests"),
                },
                new[] { new AudioBindingEditorClipOption("audio/SFX/click", "Assets/Resources/audio/SFX/click.wav") });
            return session;
        }
    }
}
