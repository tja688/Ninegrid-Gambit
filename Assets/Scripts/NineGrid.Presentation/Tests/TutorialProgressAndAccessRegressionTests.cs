using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Tutorial;
using NineGrid.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// Issue #226 验收测试：
    /// 教程进度槽（四路标记）、入口显隐（主菜单教程/战斗壳规则书）、血色解锁与选人锁定、刺客解锁。
    /// </summary>
    [TestFixture]
    public class TutorialProgressAndAccessRegressionTests
    {
        private sealed class MemoryRunSaveStore : IRunSaveStore
        {
            private readonly Dictionary<string, string> mSlots = new Dictionary<string, string>();

            public bool Exists(string slotId) => mSlots.ContainsKey(slotId);
            public bool TryRead(string slotId, out string json) => mSlots.TryGetValue(slotId, out json);
            public void Write(string slotId, string json) => mSlots[slotId] = json;
            public void Delete(string slotId) => mSlots.Remove(slotId);
            public void Clear() => mSlots.Clear();
            public bool HasOnlySlot(string expectedSlotId) => mSlots.Count == 1 && mSlots.ContainsKey(expectedSlotId);
        }

        private MemoryRunSaveStore mMemoryStore;

        [SetUp]
        public void SetUp()
        {
            mMemoryStore = new MemoryRunSaveStore();
            RunSaveStoreHook.Set(mMemoryStore);
        }

        [TearDown]
        public void TearDown()
        {
            RunSaveStoreHook.Set(null);
            RunSaveStoreHook.SuppressMissingWarningForTests();
            RunSetupSelection.ResetToDefault();
        }

        [Test]
        public void TutorialProgressStore_DefaultState_AllFlagsFalseWhenNoSave()
        {
            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsFalse(TutorialProgressStore.IsCompleted());
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());
        }

        [Test]
        public void TutorialProgressStore_Steps1To9_MarkAndRead_PersistsAndReadsCorrectly()
        {
            TutorialProgressStore.MarkSteps1To9Completed();

            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsTrue(TutorialProgressStore.IsCompleted());
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());
        }

        [Test]
        public void TutorialProgressStore_DoorTutorialSeen_MarkAndRead_PersistsAndReadsCorrectly()
        {
            TutorialProgressStore.MarkDoorTutorialSeen();

            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());
        }

        [Test]
        public void TutorialProgressStore_ScarletUnlocked_MarkAndRead_PersistsAndReadsCorrectly()
        {
            TutorialProgressStore.MarkScarletUnlocked();

            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsTrue(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());
        }

        [Test]
        public void TutorialProgressStore_AssassinUnlocked_MarkAndRead_PersistsAndReadsCorrectly()
        {
            TutorialProgressStore.MarkAssassinUnlocked();

            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsTrue(TutorialProgressStore.IsAssassinUnlocked());
        }

        [Test]
        public void TutorialProgressStore_FourTracks_CanBeSetIndependently_WithoutClobberingEachOther()
        {
            TutorialProgressStore.MarkSteps1To9Completed();
            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());

            TutorialProgressStore.MarkDoorTutorialSeen();
            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());

            TutorialProgressStore.MarkScarletUnlocked();
            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsTrue(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());

            TutorialProgressStore.MarkAssassinUnlocked();
            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsTrue(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsTrue(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsTrue(TutorialProgressStore.IsAssassinUnlocked());
        }

        [Test]
        public void TutorialProgressStore_BackwardCompatibility_LegacyCompletedField_MapsToSteps1To9()
        {
            // 模拟旧版仅有 completed: true 的 JSON 档
            mMemoryStore.Write(TutorialProgressStore.SlotId, "{\"schemaVersion\":1,\"completed\":true}");

            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsTrue(TutorialProgressStore.IsCompleted());
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());
        }

        [Test]
        public void TutorialProgressStore_MissingBackend_ReturnsFalseAndDoesNotThrow()
        {
            RunSaveStoreHook.Set(null);
            RunSaveStoreHook.SuppressMissingWarningForTests();

            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());

            // 写入调用应安全返回不抛出异常
            Assert.DoesNotThrow(() => TutorialProgressStore.MarkSteps1To9Completed());
            Assert.DoesNotThrow(() => TutorialProgressStore.MarkDoorTutorialSeen());
            Assert.DoesNotThrow(() => TutorialProgressStore.MarkScarletUnlocked());
            Assert.DoesNotThrow(() => TutorialProgressStore.MarkAssassinUnlocked());
            Assert.DoesNotThrow(() => TutorialProgressStore.ResetAll());
        }

        [Test]
        public void TutorialProgressStore_ResetAll_ClearsAllFlags()
        {
            TutorialProgressStore.MarkSteps1To9Completed();
            TutorialProgressStore.MarkDoorTutorialSeen();
            TutorialProgressStore.MarkScarletUnlocked();
            TutorialProgressStore.MarkAssassinUnlocked();

            TutorialProgressStore.ResetAll();

            Assert.IsFalse(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsFalse(TutorialProgressStore.IsDoorTutorialSeen());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());
        }

        [Test]
        public void TutorialProgressStore_SlotIsolation_OnlyUsesTutorialProfileSlot()
        {
            TutorialProgressStore.MarkSteps1To9Completed();
            TutorialProgressStore.MarkDoorTutorialSeen();
            TutorialProgressStore.MarkScarletUnlocked();
            TutorialProgressStore.MarkAssassinUnlocked();

            Assert.IsTrue(mMemoryStore.HasOnlySlot(TutorialProgressStore.SlotId));
            Assert.IsFalse(mMemoryStore.Exists("auto_checkpoint"));
            Assert.IsFalse(mMemoryStore.Exists("manual_0"));
        }

        [Test]
        public void ScarletDifficulty_UnlockState_MatchesStoreFlag()
        {
            // 初始未通关：血色锁定
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());

            // 跑图胜利后：血色解锁
            TutorialProgressStore.MarkScarletUnlocked();
            Assert.IsTrue(TutorialProgressStore.IsScarletUnlocked());
        }

        [Test]
        public void AssassinUnlock_WarriorRunEnd_Unlocks_WinOrLose()
        {
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());

            TutorialProgressStore.MarkAssassinUnlockedIfWarriorRun(ProfessionCatalog.Jester);
            Assert.IsTrue(TutorialProgressStore.IsAssassinUnlocked(), "战士正式局收口应解锁刺客，不论胜负");
        }

        [Test]
        public void AssassinUnlock_NonWarriorRunEnd_DoesNotUnlock()
        {
            TutorialProgressStore.MarkAssassinUnlockedIfWarriorRun(ProfessionCatalog.Assassin);
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked(), "刺客局收口不得提前解锁自己");

            TutorialProgressStore.MarkAssassinUnlockedIfWarriorRun(null);
            TutorialProgressStore.MarkAssassinUnlockedIfWarriorRun(string.Empty);
            TutorialProgressStore.MarkAssassinUnlockedIfWarriorRun("profession.unknown");
            Assert.IsFalse(TutorialProgressStore.IsAssassinUnlocked());
        }

        [Test]
        public void AssassinUnlock_DoesNotClobberOtherFlags()
        {
            TutorialProgressStore.MarkSteps1To9Completed();
            TutorialProgressStore.MarkAssassinUnlockedIfWarriorRun(ProfessionCatalog.Jester);

            Assert.IsTrue(TutorialProgressStore.IsSteps1To9Completed());
            Assert.IsTrue(TutorialProgressStore.IsAssassinUnlocked());
            Assert.IsFalse(TutorialProgressStore.IsScarletUnlocked());
        }
    }
}
