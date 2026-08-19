using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.InfoNotice;
using NineGrid.Presentation.Ui;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 主菜单「继续游戏」存档门禁：无档 / 版本不兼容不算有档，合法 v1 快照才 HasAnySave。
    /// </summary>
    [TestFixture]
    public sealed class MainMenuContinueSaveRegressionTests
    {
        private sealed class MemoryRunSaveStore : IRunSaveStore
        {
            private readonly Dictionary<string, string> mSlots = new Dictionary<string, string>();

            public bool Exists(string slotId) => mSlots.ContainsKey(slotId);

            public bool TryRead(string slotId, out string json) => mSlots.TryGetValue(slotId, out json);

            public void Write(string slotId, string json) => mSlots[slotId] = json;

            public void Delete(string slotId) => mSlots.Remove(slotId);
        }

        private MemoryRunSaveStore mStore;

        [SetUp]
        public void SetUp()
        {
            mStore = new MemoryRunSaveStore();
            RunSaveStoreHook.Set(mStore);
        }

        [TearDown]
        public void TearDown()
        {
            RunSaveStoreHook.Set(null);
            RunSaveStoreHook.SuppressMissingWarningForTests();
        }

        [Test]
        public void HasAnySave_FalseWhenStoreEmpty()
        {
            Assert.IsFalse(MainMenuLoadPanel.HasAnySave());
        }

        [Test]
        public void HasAnySave_TrueForCompatibleAutoSlot()
        {
            var snapshot = new RunSaveSnapshot
            {
                version = RunSaveSnapshot.CurrentVersion,
                seed = "1",
            };
            mStore.Write(RunSaveService.AutoSlotId, JsonUtility.ToJson(snapshot));
            Assert.IsTrue(MainMenuLoadPanel.HasAnySave());
        }

        [Test]
        public void HasAnySave_FalseForIncompatibleVersion()
        {
            mStore.Write(RunSaveService.AutoSlotId, "{\"version\":99,\"seed\":\"1\"}");
            Assert.IsFalse(MainMenuLoadPanel.HasAnySave());
        }

        [Test]
        public void ShouldSuppressFieldHover_MainMenuBlocksHpTip()
        {
            Assert.IsTrue(
                UIInfoHoverRouter.ShouldSuppressFieldHover(false, false, false, mainMenu: true));
        }
    }
}
