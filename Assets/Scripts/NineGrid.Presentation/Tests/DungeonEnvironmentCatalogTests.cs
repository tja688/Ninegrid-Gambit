using NineGrid.Core;
using NineGrid.Core.Content;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class DungeonEnvironmentCatalogTests
    {
        [SetUp]
        public void SetUp()
        {
            DungeonEnvironmentCatalog.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            DungeonEnvironmentCatalog.Invalidate();
        }

        [Test]
        public void Resolve_Floor1_FirstHalf_IsEmeraldMist()
        {
            var env = DungeonEnvironmentCatalog.Resolve(1, 2, false);
            Assert.AreEqual("密林_翡翠迷雾", env.DisplayName);
            Assert.False(env.IsBloodTheme);
        }

        [Test]
        public void Resolve_Floor2_SecondHalf_IsMagma()
        {
            var env = DungeonEnvironmentCatalog.Resolve(2, 6, false);
            Assert.AreEqual("岩层_熔岩之地", env.DisplayName);
        }

        [Test]
        public void Resolve_Hard_Floor3_IsBloodThemeWholeFloor()
        {
            var env = DungeonEnvironmentCatalog.Resolve(3, 2, true);
            Assert.AreEqual("溶洞_血色", env.DisplayName);
            Assert.True(env.IsBloodTheme);
        }

        [Test]
        public void Resolve_Hard_Floor3_UsesBloodGroundPanelAndMainBg()
        {
            var env = DungeonEnvironmentCatalog.Resolve(3, 2, true);
            Assert.AreEqual("溶洞_血色", env.DisplayName);
            Assert.AreEqual(
                DungeonEnvironmentCatalog.GroundPanelResourceFolder
                + DungeonEnvironmentCatalog.GroundPanelBloodSpriteName
                + ".png",
                env.GroundPanelResourcePath);
            Assert.AreEqual("#411d31", env.MainBackgroundColorHex);
            Assert.AreEqual("#a53030", env.SlotColorHex);
        }

        [Test]
        public void Resolve_Floor1_EmeraldMist_UsesMistNavyAndMossSlots()
        {
            var env = DungeonEnvironmentCatalog.Resolve(1, 1, false);
            Assert.AreEqual("密林_翡翠迷雾", env.DisplayName);
            Assert.AreEqual(
                DungeonEnvironmentCatalog.GroundPanelResourceFolder + "F_UI_Panel_H_密林_翡翠迷雾.png",
                env.GroundPanelResourcePath);
            Assert.AreEqual("#172038", env.MainBackgroundColorHex);
            Assert.AreEqual("#468232", env.SlotColorHex);
        }

        [Test]
        public void Resolve_Floor2_BoneHall_UsesCoolNavyAndBoneSlots()
        {
            var env = DungeonEnvironmentCatalog.Resolve(2, 1, false);
            Assert.AreEqual("岩层_藏骨堂", env.DisplayName);
            Assert.AreEqual("#151d28", env.MainBackgroundColorHex);
            Assert.AreEqual("#4d2b32", env.SlotColorHex);
        }

        [Test]
        public void Resolve_Floor2_Magma_UsesWarmNightAndCopperSlots()
        {
            var env = DungeonEnvironmentCatalog.Resolve(2, 6, false);
            Assert.AreEqual("岩层_熔岩之地", env.DisplayName);
            Assert.AreEqual("#341c27", env.MainBackgroundColorHex);
            Assert.AreEqual("#884b2b", env.SlotColorHex);
        }

        [Test]
        public void Resolve_TableOverride_UsesLoadedFaceAndHex()
        {
            var table = DungeonEnvironmentCatalog.CreateDefaultTable();
            table.variants[0].faceBackground = "Assets/Resources/ContentArt/Png/Other/密林_阴森沼泽.png";
            table.variants[0].mainBackgroundHex = "#090a14";
            table.variants[0].slotHex = "#577277";
            DungeonEnvironmentCatalog.SetTable(table);

            var env = DungeonEnvironmentCatalog.Resolve(1, 1, false);
            Assert.AreEqual("密林_翡翠迷雾", env.DisplayName);
            Assert.AreEqual(
                "Assets/Resources/ContentArt/Png/Other/密林_阴森沼泽.png",
                env.FaceBackgroundResourcePath);
            Assert.AreEqual("#090a14", env.MainBackgroundColorHex);
            Assert.AreEqual("#577277", env.SlotColorHex);
        }
    }
}
