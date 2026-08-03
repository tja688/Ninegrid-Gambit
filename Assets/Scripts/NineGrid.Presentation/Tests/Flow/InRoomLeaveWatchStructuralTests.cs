using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// 护栏：房内离开监视须在 mActive=true 之后启动，否则 UniTask 首帧 while 直接退出。
    /// </summary>
    public sealed class InRoomLeaveWatchStructuralTests
    {
        private static readonly string[] PresenterRelPaths =
        {
            Path.Combine("Flow", "ShopBoard", "ShopBoardPresenter.cs"),
            Path.Combine("Flow", "TavernBoard", "TavernBoardPresenter.cs"),
            Path.Combine("Flow", "RewardBoard", "RewardBoardPresenter.cs"),
        };

        [Test]
        public void TrySpawn_SetsActiveBeforeStartAvatarWatch()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation"));

            for (var i = 0; i < PresenterRelPaths.Length; i++)
            {
                var path = Path.Combine(root, PresenterRelPaths[i]);
                Assert.IsTrue(File.Exists(path), "missing " + path);
                var text = File.ReadAllText(path);

                // 在 TrySpawnFromPending 方法体内：最后一次 mActive= 赋值须早于 StartAvatarWatch()。
                var method = Regex.Match(
                    text,
                    @"public bool TrySpawnFromPending\s*\([^)]*\)\s*\{(?<body>[\s\S]*?)\n        public ",
                    RegexOptions.CultureInvariant);
                Assert.IsTrue(method.Success, "无法定位 TrySpawnFromPending: " + PresenterRelPaths[i]);
                var body = method.Groups["body"].Value;

                var activeIdx = body.LastIndexOf("mActive =", StringComparison.Ordinal);
                var watchIdx = body.IndexOf("StartAvatarWatch()", StringComparison.Ordinal);
                Assert.GreaterOrEqual(activeIdx, 0, "TrySpawn 须赋值 mActive: " + PresenterRelPaths[i]);
                Assert.GreaterOrEqual(watchIdx, 0, "TrySpawn 须 StartAvatarWatch: " + PresenterRelPaths[i]);
                Assert.Less(
                    activeIdx,
                    watchIdx,
                    "mActive 须在 StartAvatarWatch 之前，否则离开监视空转: " + PresenterRelPaths[i]);
            }
        }

        [Test]
        public void LeaveDwellFailure_RestartsTimer()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation"));

            for (var i = 0; i < PresenterRelPaths.Length; i++)
            {
                var path = Path.Combine(root, PresenterRelPaths[i]);
                var text = File.ReadAllText(path);
                Assert.IsTrue(
                    Regex.IsMatch(
                        text,
                        @"mLeaveDwell\.Begin\(slot,\s*0\);\s*RunLeaveDwellAsync\(slot,\s*parentCt\)\.Forget\(\);",
                        RegexOptions.CultureInvariant),
                    "离开失败须重开 RunLeaveDwellAsync: " + PresenterRelPaths[i]);
            }
        }

        [Test]
        public void ShopAndTavern_PresentGoldAfterSpend()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow"));
            var shop = File.ReadAllText(Path.Combine(root, "ShopBoard", "ShopBoardPresenter.cs"));
            var tavern = File.ReadAllText(Path.Combine(root, "TavernBoard", "TavernBoardPresenter.cs"));
            Assert.IsTrue(
                shop.IndexOf("InRoomGoldPresentation.PresentGoldChangesSince", StringComparison.Ordinal) >= 0,
                "商店购买/刷新须推金币 HUD");
            Assert.IsTrue(
                tavern.IndexOf("InRoomGoldPresentation.PresentGoldChangesSince", StringComparison.Ordinal) >= 0,
                "卡店扣费须推金币 HUD");
        }

        [Test]
        public void SoftBlockOnly_RegisteredForShelvesAndOptions()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow"));
            var shop = File.ReadAllText(Path.Combine(root, "ShopBoard", "ShopBoardPresenter.cs"));
            var tavern = File.ReadAllText(Path.Combine(root, "TavernBoard", "TavernBoardPresenter.cs"));
            Assert.IsTrue(
                shop.IndexOf("RoomIconWalkRole.SoftBlockOnly", StringComparison.Ordinal) >= 0,
                "商店货架/刷新须 SoftBlockOnly");
            Assert.IsTrue(
                shop.IndexOf("RoomIconWalkRole.WalkDestination", StringComparison.Ordinal) >= 0,
                "商店离开须 WalkDestination");
            Assert.IsTrue(
                tavern.IndexOf("RoomIconWalkRole.SoftBlockOnly", StringComparison.Ordinal) >= 0,
                "卡店服务/刷新/候选须 SoftBlockOnly");
            Assert.IsTrue(
                tavern.IndexOf("RoomIconWalkRole.WalkDestination", StringComparison.Ordinal) >= 0,
                "卡店离开须 WalkDestination");
        }

        /// <summary>
        /// 改写自退役的「选项须 Fit / 真卡禁 Fit」断言（#104 删 Fit 后不得静默删除）。
        /// 新不变量（ADR-0024 / #105）：选项与真卡一律预制体尺寸，禁止 Fit；落格只对齐世界位置。
        /// </summary>
        [Test]
        public void OptionAndShelfSpawns_UsePrefabScale_WithoutFit()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow"));
            var shop = File.ReadAllText(Path.Combine(root, "ShopBoard", "ShopBoardPresenter.cs"));
            var tavern = File.ReadAllText(Path.Combine(root, "TavernBoard", "TavernBoardPresenter.cs"));
            var reward = File.ReadAllText(Path.Combine(root, "RewardBoard", "RewardBoardPresenter.cs"));

            AssertNoFit(shop, "ShopBoardPresenter");
            AssertNoFit(tavern, "TavernBoardPresenter");
            AssertNoFit(reward, "RewardBoardPresenter");

            // 选项（刷新/服务/离开）与真卡货架均走世界对齐，不得再靠 Fit 压尺寸。
            Assert.IsTrue(
                shop.IndexOf("BoardSlotWorldPlacement.TryAlignToSlot", StringComparison.Ordinal) >= 0,
                "商店落格须 BoardSlotWorldPlacement");
            Assert.IsTrue(
                tavern.IndexOf("BoardSlotWorldPlacement.TryAlignToSlot", StringComparison.Ordinal) >= 0,
                "卡店落格须 BoardSlotWorldPlacement");
            Assert.IsTrue(
                reward.IndexOf("BoardSlotWorldPlacement.TryAlignToSlot", StringComparison.Ordinal) >= 0,
                "奖励房落格须 BoardSlotWorldPlacement");

            Assert.IsTrue(
                Regex.IsMatch(
                    shop,
                    @"SpawnPresentationOnly\s*\(\s*entry\.DefId\s*,\s*parent:\s*null",
                    RegexOptions.CultureInvariant),
                "商店货架真卡须 parent:null（预制体尺度）");
            Assert.IsTrue(
                Regex.IsMatch(
                    tavern,
                    @"SpawnPresentationOnly\s*\(\s*entry\.DefId\s*,\s*parent:\s*null",
                    RegexOptions.CultureInvariant),
                "卡店候选真卡须 parent:null（预制体尺度）");
            Assert.IsTrue(
                Regex.IsMatch(
                    reward,
                    @"SpawnPresentationOnly\s*\(\s*entry\.DefId\s*,\s*parent:\s*null",
                    RegexOptions.CultureInvariant),
                "奖励房真卡须 parent:null（预制体尺度）");

            // 选项 Spawn（刷新/服务/离开）同样 Instantiate(prefab) 无 parent，再 TryAlignToSlot。
            Assert.IsTrue(
                Regex.IsMatch(
                    shop,
                    @"Object\.Instantiate\s*\(\s*prefab\s*\)",
                    RegexOptions.CultureInvariant),
                "商店选项须 Instantiate(prefab) 无 parent");
            Assert.IsTrue(
                Regex.IsMatch(
                    tavern,
                    @"Object\.Instantiate\s*\(\s*prefab\s*\)",
                    RegexOptions.CultureInvariant),
                "卡店选项须 Instantiate(prefab) 无 parent");
            Assert.IsTrue(
                Regex.IsMatch(
                    reward,
                    @"Object\.Instantiate\s*\(\s*prefab\s*\)",
                    RegexOptions.CultureInvariant),
                "奖励房离开选项须 Instantiate(prefab) 无 parent");
            Assert.IsFalse(
                Regex.IsMatch(
                    shop + "\n" + tavern + "\n" + reward,
                    @"Object\.Instantiate\s*\(\s*prefab\s*,",
                    RegexOptions.CultureInvariant),
                "房内选项/离开不得 Instantiate(prefab, parent|anchor)");
        }

        private static void AssertNoFit(string text, string label)
        {
            Assert.IsFalse(
                text.IndexOf("FitRoomIcon", StringComparison.Ordinal) >= 0,
                label + " 不得调用 FitRoomIcon（选项与真卡均禁 Fit）");
            Assert.IsFalse(
                text.IndexOf("RoomIconVisualFit", StringComparison.Ordinal) >= 0,
                label + " 不得引用 RoomIconVisualFit");
            Assert.IsFalse(
                text.IndexOf("FitToTarget", StringComparison.Ordinal) >= 0,
                label + " 不得调用 FitToTarget");
        }
    }
}
