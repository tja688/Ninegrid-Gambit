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
    }
}
