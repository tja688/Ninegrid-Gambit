using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// 护栏：卡组入槽须始终 Propagate SortingLayer 到子 Renderer/Mask，
    /// 否则新入组牌（离开机关等）会逃出 BG 约束压在最上。
    /// </summary>
    public sealed class DeckSortingLayerPropagateStructuralTests
    {
        [Test]
        public void ApplySortingOrder_AlwaysPropagatesSortingLayerFromGroup()
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "Scripts",
                    "NineGrid.Presentation",
                    "Cards",
                    "CardDeckSlotContainer.cs"));
            Assert.IsTrue(File.Exists(path), path);
            var source = File.ReadAllText(path);
            var method = Regex.Match(
                source,
                @"public void ApplySortingOrder\(ManagedCard card, int slotIndex\)\s*\{[\s\S]*?\n        \}",
                RegexOptions.CultureInvariant);
            Assert.IsTrue(method.Success, "找不到 ApplySortingOrder");
            Assert.IsTrue(
                method.Value.Contains("PropagateSortingLayerFromGroup"),
                "ApplySortingOrder 须 PropagateSortingLayerFromGroup");
            Assert.IsFalse(
                Regex.IsMatch(
                    method.Value,
                    @"if\s*\(\s*sortingGroup\.sortingLayerName\s*!=\s*_sortingLayerName\s*\)\s*\{[^}]*PropagateSortingLayerFromGroup",
                    RegexOptions.CultureInvariant),
                "Propagate 不得只包在 layer 名变化分支内（层名已对齐时子节点仍可能错层）");
        }

        [Test]
        public void ApplyDisplayMode_CardDeckMode_DelegatesEnsureDeckSorting()
        {
            var path = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    "Scripts",
                    "NineGrid.Presentation",
                    "Cards",
                    "CardManagerSingleton.cs"));
            Assert.IsTrue(File.Exists(path), path);
            var source = File.ReadAllText(path);
            var method = Regex.Match(
                source,
                @"private static void ApplyDisplayMode\(ManagedCard card, CardDisplayMode mode\)\s*\{[\s\S]*?\n        \}",
                RegexOptions.CultureInvariant);
            Assert.IsTrue(method.Success, "找不到 ApplyDisplayMode");
            Assert.IsTrue(
                method.Value.Contains("EnsureDeckSorting"),
                "CardDeckMode 已入槽时须 EnsureDeckSorting，禁止打回默认 -30");
            Assert.IsTrue(
                File.ReadAllText(
                        Path.GetFullPath(
                            Path.Combine(
                                Application.dataPath,
                                "Scripts",
                                "NineGrid.Presentation",
                                "Cards",
                                "CardDeckManagerSingleton.cs")))
                    .Contains("internal void EnsureDeckSorting"),
                "CardDeckManagerSingleton 须提供 EnsureDeckSorting");
        }
    }
}
