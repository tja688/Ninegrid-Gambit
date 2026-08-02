using NineGrid.Cards;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Systems;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Output
{
    /// <summary>
    /// 动态 HUD 描述 TMP 管道已退役；卡面基础描述权威在 Basic_Description Commit。
    /// </summary>
    public sealed class DescriptionOutputControllerTests
    {
        [Test]
        public void DescriptionDisplayHook_RequestApisAreNoOps()
        {
            Assert.DoesNotThrow(() =>
                DescriptionDisplayHook.RequestShow("help.throwing_knife", DescriptionShowRoute.Hover));
            Assert.DoesNotThrow(() =>
                DescriptionDisplayHook.RequestShowText("选一张牌", DescriptionShowRoute.BoardSelect));
            Assert.DoesNotThrow(() =>
                DescriptionDisplayHook.RequestClear(DescriptionShowRoute.Hover));
            Assert.IsNull(DescriptionDisplayHook.Show);
            Assert.IsNull(DescriptionDisplayHook.ShowText);
            Assert.IsNull(DescriptionDisplayHook.Clear);
            Assert.IsNull(DescriptionDisplayHook.EnsureWired);
        }

        [Test]
        public void DescriptionOutputController_EnsureInstalled_ReturnsNull()
        {
            Assert.IsNull(DescriptionOutputController.EnsureInstalled());
        }

        [Test]
        public void DescriptionHoverSink_TypeIsRemoved()
        {
            var cardsAssembly = typeof(IBattleSessionSystem).Assembly;
            Assert.IsNull(
                cardsAssembly.GetType("NineGrid.Cards.DescriptionHoverSink"),
                "DescriptionHoverSink 应已删除；动态描述管道退役后不再回流");
        }

        [Test]
        public void BoardBriefTip_IsSeparateFromRetiredDescriptionHud()
        {
            var assembly = typeof(NineGrid.Flow.BoardBriefTip.BoardBriefTipPresenter).Assembly;
            Assert.IsNotNull(assembly.GetType("NineGrid.Flow.BoardBriefTip.BoardBriefTipCopy"));
            Assert.IsNotNull(assembly.GetType("NineGrid.Flow.BoardBriefTip.BoardBriefTipHitProxy"));
            Assert.IsNotNull(assembly.GetType("NineGrid.Flow.BoardBriefTip.FloorHintPresenter"));

            var groundHit = typeof(GroundCardHitProxy);
            var sourcePath = "Assets/Scripts/NineGrid.Presentation/Cards/GroundCardHitProxy.cs";
            var text = System.IO.File.ReadAllText(sourcePath);
            Assert.IsFalse(
                text.Contains("BoardBriefTip"),
                "战斗真卡悬停不得写简要解释文字框");
            Assert.AreEqual(typeof(GroundCardHitProxy), groundHit);
        }
    }
}
