using NineGrid.Core;
using NineGrid.Core.Content;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class DungeonEnvironmentCatalogTests
    {
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
            Assert.AreEqual("#241527", env.MainBackgroundColorHex);
        }

        [Test]
        public void Resolve_Floor1_EmeraldMist_MainBgIs151d28()
        {
            var env = DungeonEnvironmentCatalog.Resolve(1, 1, false);
            Assert.AreEqual("密林_翡翠迷雾", env.DisplayName);
            Assert.AreEqual(
                DungeonEnvironmentCatalog.GroundPanelResourceFolder + "F_UI_Panel_H_密林_翡翠迷雾.png",
                env.GroundPanelResourcePath);
            Assert.AreEqual("#151d28", env.MainBackgroundColorHex);
        }

        [Test]
        public void Resolve_Floor2_BoneHall_MainBgIs090a14()
        {
            var env = DungeonEnvironmentCatalog.Resolve(2, 1, false);
            Assert.AreEqual("岩层_藏骨堂", env.DisplayName);
            Assert.AreEqual("#090a14", env.MainBackgroundColorHex);
        }
    }
}
