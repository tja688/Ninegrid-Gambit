using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow;
using NUnit.Framework;
using QFramework;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    public sealed class HelpCardBoardSelectResolverTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            var arch = NineGridArchitecture.Current;
            var catalog = ContentCatalogBootstrap.Load();
            arch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            arch.GetSystem<IContentSystem>().Load(catalog);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ThrowingKnife_IsSingleDrag_NotMultiSelect()
        {
            Assert.IsFalse(HelpCardBoardSelectResolver.TryGetRequiredBoardSelectCount(
                "help.throwing_knife",
                out _));
            Assert.IsTrue(HelpCardBoardSelectResolver.TryGetSingleTargetSpec(
                "help.throwing_knife",
                out var spec));
            Assert.AreEqual(1, spec.Count);
            Assert.IsTrue(spec.RequiresMonster);
        }

        [Test]
        public void Fireball_IsSingleDrag_NotMultiSelect()
        {
            Assert.IsFalse(HelpCardBoardSelectResolver.TryGetRequiredBoardSelectCount(
                "help.fireball",
                out _));
            HelpCardBoardSelectResolver.TryGetPlayKind(
                "help.fireball",
                out var kind,
                out var spec);
            Assert.AreEqual(HelpCardPlayKind.SingleDragTarget, kind);
            Assert.AreEqual(1, spec.Count);
        }

        [Test]
        public void TeleportCard_IsSingleDrag_NotMultiSelect()
        {
            Assert.IsFalse(HelpCardBoardSelectResolver.TryGetRequiredBoardSelectCount(
                "help.teleport_card",
                out _));
            Assert.IsTrue(HelpCardBoardSelectResolver.TryGetSingleTargetSpec(
                "help.teleport_card",
                out var spec));
            Assert.AreEqual(1, spec.Count);
            Assert.IsFalse(spec.RequiresMonster);
        }

        [Test]
        public void SwapCard_IsMultiSelectCountTwo()
        {
            Assert.IsTrue(HelpCardBoardSelectResolver.TryGetRequiredBoardSelectCount(
                "help.swap_card",
                out var count));
            Assert.AreEqual(2, count);
            HelpCardBoardSelectResolver.TryGetPlayKind(
                "help.swap_card",
                out var kind,
                out _);
            Assert.AreEqual(HelpCardPlayKind.MultiBoardSelect, kind);
        }

        [Test]
        public void HealingPotion_HasNoBoardTarget()
        {
            HelpCardBoardSelectResolver.TryGetPlayKind(
                "help.healing_potion",
                out var kind,
                out _);
            Assert.AreEqual(HelpCardPlayKind.None, kind);
            Assert.IsFalse(HelpCardBoardSelectResolver.TryGetRequiredBoardSelectCount(
                "help.healing_potion",
                out _));
        }

        [Test]
        public void SwapCard_HasBoardSelectPrompt()
        {
            Assert.IsTrue(HelpCardBoardSelectResolver.TryGetBoardSelectPrompt(
                "help.swap_card",
                out var prompt));
            Assert.IsFalse(string.IsNullOrWhiteSpace(prompt));
        }
    }
}
