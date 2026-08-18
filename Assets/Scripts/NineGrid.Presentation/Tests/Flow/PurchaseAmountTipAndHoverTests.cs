using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NineGrid.Flow.BoardBriefTip;
using NineGrid.Flow.PurchaseAmountTip;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests.Flow
{
    [TestFixture]
    public sealed class PurchaseAmountTipAndHoverTests
    {
        [TearDown]
        public void TearDown()
        {
            PurchaseAmountTipPresenter.ResetForTests();
            BoardBriefTipPresenter.InstanceOrNull()?.HardClear();
        }

        [Test]
        public void BoardBriefTip_HoverAndNotice_LifecycleAndOverride()
        {
            var presenter = BoardBriefTipPresenter.EnsureExists();
            presenter.HardClear();

            // 1. 悬停文案
            var hoverGen = presenter.ShowHover("卡牌描述：大宝剑 [50 金币]");
            Assert.AreEqual("卡牌描述：大宝剑 [50 金币]", presenter.Session.DisplayText);
            Assert.IsTrue(presenter.Session.IsVisible);

            // 2. Notice 覆盖悬停
            var noticeGen = presenter.ShowNotice("金币不足");
            Assert.AreEqual("金币不足", presenter.Session.DisplayText);
            Assert.IsTrue(presenter.Session.HasNotice);

            // 3. Notice 清除后恢复悬停文案
            presenter.ClearNotice(noticeGen);
            Assert.IsFalse(presenter.Session.HasNotice);
            Assert.AreEqual("卡牌描述：大宝剑 [50 金币]", presenter.Session.DisplayText);

            // 4. 悬停离开后清除全部
            presenter.ClearHover(hoverGen);
            Assert.AreEqual(string.Empty, presenter.Session.DisplayText);
            Assert.IsFalse(presenter.Session.IsVisible);
        }

        [Test]
        public void BoardBriefTip_GenerationsPreventDirtyWrite()
        {
            var presenter = BoardBriefTipPresenter.EnsureExists();
            presenter.HardClear();

            var gen1 = presenter.ShowHover("第一格");
            var gen2 = presenter.ShowHover("第二格");

            // 旧代的清退不应冲掉新悬停
            presenter.ClearHover(gen1);
            Assert.AreEqual("第二格", presenter.Session.DisplayText);

            // 匹配代数正常清退
            presenter.ClearHover(gen2);
            Assert.AreEqual(string.Empty, presenter.Session.DisplayText);
        }

        [Test]
        public void PurchaseAmountTip_GenerationsPreventDirtyWrite()
        {
            PurchaseAmountTipPresenter.ResetForTests();

            // 验证 Hide(0) / 不匹配代数安全无副作用
            PurchaseAmountTipPresenter.Hide(0);
            PurchaseAmountTipPresenter.Hide(999);
            Assert.IsFalse(PurchaseAmountTipPresenter.IsVisible);
        }

        [UnityTest]
        public IEnumerator BoardBriefTip_AutoClearNotice_DismissesAfterTimeout() => UniTask.ToCoroutine(async () =>
        {
            var presenter = BoardBriefTipPresenter.EnsureExists();
            presenter.HardClear();

            presenter.ShowNotice("短时提示", 0.05f);
            Assert.AreEqual("短时提示", presenter.Session.DisplayText);
            Assert.IsTrue(presenter.Session.IsVisible);

            await UniTask.Delay(TimeSpan.FromMilliseconds(150), DelayType.Realtime);

            Assert.AreEqual(string.Empty, presenter.Session.DisplayText);
            Assert.IsFalse(presenter.Session.IsVisible);
        });

        [Test]
        public void BoardBriefTipCopy_ForCard_ResolvesParametersAndPrice()
        {
            var tip = BoardBriefTipCopy.ForCard("help.bomb", content: null, priceGold: 50);
            StringAssert.Contains("爆弹", tip);
            StringAssert.Contains("4", tip);
            StringAssert.DoesNotContain("{help.bomb.use.amount}", tip);
            StringAssert.Contains("50 金币", tip);
        }

        [Test]
        public void BoardBriefTip_RendersRichTextAndBuildsSpriteAsset()
        {
            var presenter = BoardBriefTipPresenter.EnsureExists();
            presenter.HardClear();

            var go = new GameObject("TestText");
            var tmp = go.AddComponent<TextMeshProUGUI>();
            var tip = "泡沫铠甲：[armor]+3，获得:[[伤害护盾]] · 40 金币";

            presenter.ShowHover(tip);

            if (presenter.BodyTextOrNull != null)
            {
                var bodyText = presenter.BodyTextOrNull;
                StringAssert.Contains("<sprite name=\"armor\">", bodyText.text);
                StringAssert.Contains("伤害护盾", bodyText.text);
                StringAssert.DoesNotContain("[[", bodyText.text);
                StringAssert.DoesNotContain("]]", bodyText.text);
                Assert.IsNotNull(bodyText.spriteAsset, "Should build TMP_SpriteAsset for [armor]");
            }

            presenter.ClearHover();
            if (presenter.BodyTextOrNull != null)
            {
                Assert.IsNull(presenter.BodyTextOrNull.spriteAsset, "SpriteAsset should be cleared on hover clear");
            }

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
