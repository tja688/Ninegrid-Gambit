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
    }
}
