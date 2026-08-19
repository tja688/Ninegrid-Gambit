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
    }
}
