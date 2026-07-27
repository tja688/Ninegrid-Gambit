using NineGrid.Content.Editor;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// 表现层配置器条目门槛：Skill / skill.* 不进窗口；玩家道具卡（HelpCard/Item/PlayerCard）保留。
    /// </summary>
    public sealed class CardPresentationEditorScopeTests
    {
        [Test]
        public void IsCardLikeKind_ExcludesSkill_KeepsHelpAndItemKinds()
        {
            Assert.IsFalse(CardPresentationMigration.IsCardLikeKind("Skill"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("HelpCard"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("Item"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("PlayerCard"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("Monster"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("Relic"));
            Assert.IsTrue(CardPresentationMigration.IsCardLikeKind("Avatar"));
        }

        [Test]
        public void IsPresentationEditorEntry_BlocksSkillPrefixEvenIfKindLooksLikeHelp()
        {
            Assert.IsFalse(
                CardPresentationMigration.IsPresentationEditorEntry("Skill", "skill.breathe_fire"));
            Assert.IsFalse(
                CardPresentationMigration.IsPresentationEditorEntry("HelpCard", "skill.breathe_fire"));
            Assert.IsTrue(
                CardPresentationMigration.IsPresentationEditorEntry("HelpCard", "help.bomb"));
            Assert.IsTrue(
                CardPresentationMigration.IsPresentationEditorEntry("Item", "player.gold_card"));
        }

        [Test]
        public void MapSidebarCategory_SkillIsNotItem_HelpCardStillIs()
        {
            Assert.AreEqual(
                CardPresentationSidebarCategory.Other,
                CardPresentationEditorSession.MapSidebarCategory("Skill"));
            Assert.AreEqual(
                CardPresentationSidebarCategory.Item,
                CardPresentationEditorSession.MapSidebarCategory("HelpCard"));
            Assert.AreEqual(
                CardPresentationSidebarCategory.Item,
                CardPresentationEditorSession.MapSidebarCategory("Item"));
            Assert.AreEqual(
                CardPresentationSidebarCategory.Item,
                CardPresentationEditorSession.MapSidebarCategory("PlayerCard"));
        }
    }
}
