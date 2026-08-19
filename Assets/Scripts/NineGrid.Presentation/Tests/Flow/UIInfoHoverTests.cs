using NineGrid.Flow.InfoNotice;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    [TestFixture]
    public sealed class UIInfoHoverTests
    {
        [Test]
        public void UIInfoCatalog_ResolvesKnownKeys()
        {
            Assert.IsTrue(UIInfoCatalog.TryGetDescription("血条", out var hpDesc));
            Assert.IsTrue(hpDesc.Contains("这是你的血量"));

            Assert.IsTrue(UIInfoCatalog.TryGetDescription("基础护甲", out var armorDesc));
            Assert.IsTrue(armorDesc.Contains("基础护甲会给你提供"));

            Assert.IsTrue(UIInfoCatalog.TryGetDescription("金币", out var goldDesc));
            Assert.IsTrue(goldDesc.Contains("商店或者卡店"));

            Assert.IsTrue(UIInfoCatalog.TryGetDescription("RelicPanel", out var relicDesc));
            Assert.IsTrue(relicDesc.Contains("回收区进行回收"));

            Assert.IsTrue(UIInfoCatalog.TryGetDescription("CardDeckAnchors", out var deckDesc));
            Assert.IsTrue(deckDesc.Contains("卡组的顶部卡牌"));
        }

        [Test]
        public void UIInfoCatalog_ResolvesHierarchicalObjects()
        {
            var parent = new GameObject("基础护甲");
            var child = new GameObject("UI描述信息判定框 (1)");
            child.transform.SetParent(parent.transform, false);

            Assert.IsTrue(UIInfoCatalog.TryResolveDescription(child, out var desc));
            Assert.IsTrue(desc.Contains("基础护甲会给你提供"));

            Object.DestroyImmediate(child);
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void UIInfoCatalog_ResolvesBloodBarRoot()
        {
            var parent = new GameObject("bloodBarRoot");
            var child = new GameObject("UI描述信息判定框");
            child.transform.SetParent(parent.transform, false);

            Assert.IsTrue(UIInfoCatalog.TryResolveDescription(child, out var desc));
            Assert.IsTrue(desc.Contains("这是你的血量"));

            Object.DestroyImmediate(child);
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void ShouldSuppressFieldHover_OnlyWhenOverlayIsOpen()
        {
            Assert.IsFalse(UIInfoHoverRouter.ShouldSuppressFieldHover(false, false, false));
            Assert.IsTrue(UIInfoHoverRouter.ShouldSuppressFieldHover(true, false, false), "半黑屏叠层应抑制");
            Assert.IsTrue(UIInfoHoverRouter.ShouldSuppressFieldHover(false, true, false), "右键详述应抑制");
            Assert.IsTrue(UIInfoHoverRouter.ShouldSuppressFieldHover(false, false, true), "开战前准备菜单应抑制");
            Assert.IsTrue(UIInfoHoverRouter.ShouldSuppressFieldHover(true, true, true));
        }

        [Test]
        public void ShouldSuppressFieldHover_MainMenu_SuppressesHudTips()
        {
            Assert.IsFalse(UIInfoHoverRouter.ShouldSuppressFieldHover(false, false, false, mainMenu: false));
            Assert.IsTrue(
                UIInfoHoverRouter.ShouldSuppressFieldHover(false, false, false, mainMenu: true),
                "主菜单相位应抑制血条等局内 HUD 介绍");
        }

        [Test]
        public void ShouldSuppressFieldHover_PointerHeldSuppressesTooltip()
        {
            Assert.IsFalse(
                UIInfoHoverRouter.ShouldSuppressFieldHover(false, false, false, mainMenu: false, pointerHeld: false),
                "鼠标无动作时应允许悬停介绍");
            Assert.IsTrue(
                UIInfoHoverRouter.ShouldSuppressFieldHover(false, false, false, mainMenu: false, pointerHeld: true),
                "按住鼠标（拖动卡牌/遗物）时应抑制悬停介绍");
            Assert.IsTrue(
                UIInfoHoverRouter.ShouldSuppressFieldHover(true, false, false, mainMenu: false, pointerHeld: true));
        }

        [Test]
        public void DragReleaseLatch_ClickWithoutMove_DoesNotBlockHover()
        {
            var latch = new UIInfoHoverRouter.DragReleaseLatch();
            latch.ObservePointer(pointerHeld: true, new Vector2(10f, 20f));
            latch.ObservePointer(pointerHeld: false, new Vector2(10f, 20f));

            Assert.IsFalse(latch.AwaitLeave, "原地按下松开不是拖动，松手后应允许介绍");
            Assert.IsFalse(latch.BlockHover(hasUiInfoHit: true));
        }

        [Test]
        public void DragReleaseLatch_ReleaseOverTarget_BlocksUntilPointerLeaves()
        {
            var latch = new UIInfoHoverRouter.DragReleaseLatch();
            latch.ObservePointer(pointerHeld: true, new Vector2(0f, 0f));
            latch.ObservePointer(pointerHeld: true, new Vector2(24f, 0f));
            latch.ObservePointer(pointerHeld: false, new Vector2(24f, 0f));

            Assert.IsTrue(latch.AwaitLeave, "拖动后松手应闩住，避免回收区/卡组立刻冒介绍");
            Assert.IsTrue(
                latch.BlockHover(hasUiInfoHit: true),
                "松手后指针仍停在判定框上时不得展示介绍");
            Assert.IsTrue(
                latch.BlockHover(hasUiInfoHit: true),
                "指针未移动离开前持续抑制");

            Assert.IsFalse(
                latch.BlockHover(hasUiInfoHit: false),
                "指针移出判定框后解除闩");
            Assert.IsFalse(latch.AwaitLeave);
            Assert.IsFalse(
                latch.BlockHover(hasUiInfoHit: true),
                "移出后再无动作移入判定框时应允许介绍");
        }

        [Test]
        public void DragReleaseLatch_ReleaseOverEmpty_DoesNotBlockLaterHover()
        {
            var latch = new UIInfoHoverRouter.DragReleaseLatch();
            latch.ObservePointer(pointerHeld: true, new Vector2(0f, 0f));
            latch.ObservePointer(pointerHeld: true, new Vector2(0f, 16f));
            latch.ObservePointer(pointerHeld: false, new Vector2(0f, 16f));

            Assert.IsTrue(latch.AwaitLeave);
            Assert.IsFalse(
                latch.BlockHover(hasUiInfoHit: false),
                "拖到空白处松手应立刻解除闩");
            Assert.IsFalse(
                latch.BlockHover(hasUiInfoHit: true),
                "随后移入判定框属于正式悬停，应展示介绍");
        }

        [Test]
        public void CenterOutReveal_BuildOrder_SpreadsFromMiddle()
        {
            CollectionAssert.AreEqual(new[] { 0 }, InfoNoticeCenterOutReveal.BuildOrder(1));
            CollectionAssert.AreEqual(new[] { 0, 1 }, InfoNoticeCenterOutReveal.BuildOrder(2));
            CollectionAssert.AreEqual(new[] { 1, 0, 2 }, InfoNoticeCenterOutReveal.BuildOrder(3));
            CollectionAssert.AreEqual(new[] { 2, 1, 3, 0, 4 }, InfoNoticeCenterOutReveal.BuildOrder(5));
        }

        [Test]
        public void CenterOutReveal_CountRevealedAt_FinishesWithinTwoTenths()
        {
            Assert.AreEqual(0, InfoNoticeCenterOutReveal.CountRevealedAt(0f, 0.2f, 0));
            Assert.AreEqual(1, InfoNoticeCenterOutReveal.CountRevealedAt(0f, 0.2f, 10));
            Assert.AreEqual(10, InfoNoticeCenterOutReveal.CountRevealedAt(0.2f, 0.2f, 10));
            Assert.AreEqual(10, InfoNoticeCenterOutReveal.CountRevealedAt(0.5f, 0.2f, 10));
            Assert.Less(InfoNoticeCenterOutReveal.CountRevealedAt(0.1f, 0.2f, 10), 10);
        }
    }
}
