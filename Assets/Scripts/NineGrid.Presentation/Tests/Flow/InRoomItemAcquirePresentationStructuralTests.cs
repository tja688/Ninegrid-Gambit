using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// ADR-0025：商店/奖励房购领须把 ItemSlots 新卡接入手牌，禁止只碎裂纯表现货架。
    /// </summary>
    public sealed class InRoomItemAcquirePresentationStructuralTests
    {
        [Test]
        public void ShopAndReward_PresentAcquireInsteadOfShatterOnly()
        {
            var root = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow"));
            var shop = File.ReadAllText(Path.Combine(root, "ShopBoard", "ShopBoardPresenter.cs"));
            var reward = File.ReadAllText(Path.Combine(root, "RewardBoard", "RewardBoardPresenter.cs"));
            var helper = File.ReadAllText(Path.Combine(root, "InRoomBoard", "InRoomItemAcquirePresentation.cs"));

            Assert.IsTrue(
                helper.IndexOf("TryAcquireShelfHelpCardToHand", StringComparison.Ordinal) >= 0,
                "须提供货架购领接手助手");
            Assert.IsTrue(
                helper.IndexOf("PullFromGroundAsync", StringComparison.Ordinal) >= 0,
                "购领须走手牌飞入");
            Assert.IsTrue(
                shop.IndexOf("TryAcquireShelfHelpCardToHand", StringComparison.Ordinal) >= 0,
                "商店购买须接手 ItemSlots 视图");
            Assert.IsTrue(
                reward.IndexOf("TryAcquireShelfHelpCardToHand", StringComparison.Ordinal) >= 0,
                "奖励房领取须接手 ItemSlots 视图");
            Assert.IsFalse(
                shop.IndexOf("ShatterShelfVisual", StringComparison.Ordinal) >= 0,
                "商店不得只碎裂货架而不接手");
            Assert.IsFalse(
                reward.IndexOf("ShatterShelfVisual", StringComparison.Ordinal) >= 0,
                "奖励房不得只碎裂货架而不接手");
        }
    }
}
