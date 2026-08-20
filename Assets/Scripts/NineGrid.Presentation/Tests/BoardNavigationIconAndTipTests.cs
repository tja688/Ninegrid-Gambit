using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Flow.BoardBriefTip;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    [TestFixture]
    public sealed class BoardNavigationIconAndTipTests
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
        public void CardChassisPaths_TerrainIconConstants_PointToCorrectPrefabs()
        {
            Assert.AreEqual("Assets/Resources/Prefabs/地形图标/通往藏骨堂图标.prefab", CardChassisPaths.RoomIconOssuary);
            Assert.AreEqual("Assets/Resources/Prefabs/地形图标/通往岩层图标.prefab", CardChassisPaths.RoomIconRockLayer);
            Assert.AreEqual("Assets/Resources/Prefabs/地形图标/遗物奖励图标.prefab", CardChassisPaths.RoomIconRelicReward);
            Assert.AreEqual("Assets/Resources/Prefabs/地形图标/通往失落遗迹图标.prefab", CardChassisPaths.RoomIconLostRuins);
            Assert.AreEqual("Assets/Resources/Prefabs/地形图标/通往熔岩之地图标.prefab", CardChassisPaths.RoomIconMagma);
            Assert.AreEqual("Assets/Resources/Prefabs/地形图标/通往苍白之路图标.prefab", CardChassisPaths.RoomIconPaleRoad);
            Assert.AreEqual("Assets/Resources/Prefabs/地形图标/通往黄昏礼堂图标.prefab", CardChassisPaths.RoomIconTwilightHall);
            Assert.AreEqual("Assets/Resources/Prefabs/地形图标/血色图标.prefab", CardChassisPaths.RoomIconBlood);
        }

        [Test]
        public void CardChassisPaths_ResolveRoomIconPrefab_ResolvesTerrainAliases()
        {
            Assert.AreEqual(CardChassisPaths.RoomIconLostRuins, CardChassisPaths.ResolveRoomIconPrefab("LostRuins"));
            Assert.AreEqual(CardChassisPaths.RoomIconLostRuins, CardChassisPaths.ResolveRoomIconPrefab("ToLostRuins"));
            Assert.AreEqual(CardChassisPaths.RoomIconOssuary, CardChassisPaths.ResolveRoomIconPrefab("Ossuary"));
            Assert.AreEqual(CardChassisPaths.RoomIconOssuary, CardChassisPaths.ResolveRoomIconPrefab("ToOssuary"));
            Assert.AreEqual(CardChassisPaths.RoomIconRockLayer, CardChassisPaths.ResolveRoomIconPrefab("RockLayer"));
            Assert.AreEqual(CardChassisPaths.RoomIconRockLayer, CardChassisPaths.ResolveRoomIconPrefab("ToRockLayer"));
            Assert.AreEqual(CardChassisPaths.RoomIconRelicReward, CardChassisPaths.ResolveRoomIconPrefab("TreasureReward"));
            Assert.AreEqual(CardChassisPaths.RoomIconTreasure, CardChassisPaths.ResolveRoomIconPrefab("Treasure"));
            Assert.AreEqual(CardChassisPaths.RoomIconItemReward, CardChassisPaths.ResolveRoomIconPrefab("ItemReward"));
            Assert.AreEqual(CardChassisPaths.RoomIconMagma, CardChassisPaths.ResolveRoomIconPrefab("Magma"));
            Assert.AreEqual(CardChassisPaths.RoomIconMagma, CardChassisPaths.ResolveRoomIconPrefab("ToMagma"));
            Assert.AreEqual(CardChassisPaths.RoomIconPaleRoad, CardChassisPaths.ResolveRoomIconPrefab("PaleRoad"));
            Assert.AreEqual(CardChassisPaths.RoomIconPaleRoad, CardChassisPaths.ResolveRoomIconPrefab("ToPaleRoad"));
            Assert.AreEqual(CardChassisPaths.RoomIconTwilightHall, CardChassisPaths.ResolveRoomIconPrefab("TwilightHall"));
            Assert.AreEqual(CardChassisPaths.RoomIconTwilightHall, CardChassisPaths.ResolveRoomIconPrefab("ToTwilightHall"));
            Assert.AreEqual(CardChassisPaths.RoomIconBlood, CardChassisPaths.ResolveRoomIconPrefab("Blood"));
        }

        [Test]
        public void BoardNavigationIconResolver_LeavePrefabs_MatchFloorDestinations()
        {
            Assert.AreEqual(CardChassisPaths.RoomIconLostRuins, BoardNavigationIconResolver.ResolveLeaveIconPrefab(1));
            Assert.AreEqual(CardChassisPaths.RoomIconMagma, BoardNavigationIconResolver.ResolveLeaveIconPrefab(2));
            Assert.AreEqual(CardChassisPaths.RoomIconTwilightHall, BoardNavigationIconResolver.ResolveLeaveIconPrefab(3));
            Assert.AreEqual(CardChassisPaths.RoomIconLeave, BoardNavigationIconResolver.ResolveLeaveIconPrefab(99));
        }

        [Test]
        public void BoardNavigationIconResolver_GoDownPrefabs_MatchFloorTransitions()
        {
            Assert.AreEqual(CardChassisPaths.RoomIconRockLayer, BoardNavigationIconResolver.ResolveGoDownIconPrefab(1));
            Assert.AreEqual(CardChassisPaths.RoomIconPaleRoad, BoardNavigationIconResolver.ResolveGoDownIconPrefab(2));
            Assert.AreEqual(CardChassisPaths.RoomIconGoDown, BoardNavigationIconResolver.ResolveGoDownIconPrefab(3));
            Assert.AreEqual(CardChassisPaths.RoomIconGoDown, BoardNavigationIconResolver.ResolveGoDownIconPrefab(99));
        }

        [Test]
        public void BoardBriefTipCopy_ForLeave_NormalDifficulty_FormatsCorrectCopy()
        {
            Assert.AreEqual("进入密林_失落遗迹", BoardBriefTipCopy.ForLeave(1, isHard: false));
            Assert.AreEqual("进入岩层_熔岩之地", BoardBriefTipCopy.ForLeave(2, isHard: false));
            Assert.AreEqual("进入溶洞_黄昏礼堂", BoardBriefTipCopy.ForLeave(3, isHard: false));
            Assert.AreEqual("离开本房", BoardBriefTipCopy.ForLeave(99, isHard: false));
        }

        [Test]
        public void BoardBriefTipCopy_ForLeave_HardDifficulty_AddsBloodPrefix()
        {
            Assert.AreEqual("进入血色密林_失落遗迹", BoardBriefTipCopy.ForLeave(1, isHard: true));
            Assert.AreEqual("进入血色岩层_熔岩之地", BoardBriefTipCopy.ForLeave(2, isHard: true));
            Assert.AreEqual("进入血色溶洞_黄昏礼堂", BoardBriefTipCopy.ForLeave(3, isHard: true));
            Assert.AreEqual("离开本房", BoardBriefTipCopy.ForLeave(99, isHard: true));
        }

        [Test]
        public void BoardBriefTipCopy_ForGoDown_NormalDifficulty_FormatsNextFloorLayer()
        {
            Assert.AreEqual("进入下一层：岩层", BoardBriefTipCopy.ForGoDown(1, isHard: false));
            Assert.AreEqual("进入下一层：溶洞", BoardBriefTipCopy.ForGoDown(2, isHard: false));
            Assert.AreEqual("前往下一层", BoardBriefTipCopy.ForGoDown(3, isHard: false));
        }

        [Test]
        public void BoardBriefTipCopy_ForGoDown_HardDifficulty_AddsBloodPrefix()
        {
            Assert.AreEqual("进入下一层：血色岩层", BoardBriefTipCopy.ForGoDown(1, isHard: true));
            Assert.AreEqual("进入下一层：血色溶洞", BoardBriefTipCopy.ForGoDown(2, isHard: true));
            Assert.AreEqual("前往下一层", BoardBriefTipCopy.ForGoDown(3, isHard: true));
        }

        [Test]
        public void BoardBriefTipCopy_ForContentId_WithoutArch_FallsBackToDefaultTips()
        {
            Assert.AreEqual("离开本房", BoardBriefTipCopy.ForContentId("Leave"));
            Assert.AreEqual("前往下一层", BoardBriefTipCopy.ForContentId("GoDown"));
        }
    }
}
