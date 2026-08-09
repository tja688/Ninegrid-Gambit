using System.Linq;
using NineGrid.Content.Audio;
using NineGrid.Content.Editor;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#179 调音工作台：单条保存/回撤、Save All Dirty、Revert All Dirty（不写正式磁盘路径）。</summary>
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
