using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Presentation.Cheat;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cheat
{
    /// <summary>
    /// 作弊面板「战斗加卡」搜索索引契约：
    /// 仅收录怪物 / 机关 / 道具三类；排除归档卡组；可按卡名 / 卡组名 / 技能名 / 技能效果描述搜索。
    /// </summary>
    public sealed class CheatToolCardSearchIndexTests
    {
        private static GameContentCatalog BuildCatalog()
        {
            var catalog = new GameContentCatalog()
                .AddCard(new CardContentDefinition("monster.melee_3", "小小莱姆", CardKind.Monster)
                    .InDeck("deck.dragon")
                    .AddSkill("skill.battle_hardened"))
                .AddCard(new CardContentDefinition("trap.flame", "烈焰", CardKind.Trap).InDeck("deck.trap"))
                .AddCard(new CardContentDefinition("help.throwing_knife", "飞刀", CardKind.HelpCard).InDeck("deck.help"))
                .AddCard(new CardContentDefinition("help.archived_tool", "归档道具", CardKind.HelpCard).InDeck("deck.help_archive"))
                .AddCard(new CardContentDefinition("relic.shiny", "闪亮遗物", CardKind.Relic).InDeck("deck.relic"))
                .AddCard(new CardContentDefinition("player.coin", "金币", CardKind.Item).InDeck("deck.player"))
                .AddCard(new CardContentDefinition("avatar.default", "角色", CardKind.Avatar).InDeck("deck.player"))
                .AddSkill(new SkillContentDefinition("skill.battle_hardened", "历战", EffectContainerType.MonsterSkill, "受伤后攻击+1"))
                .AddMonsterDeck(new MonsterDeckDefinition("deck.dragon", "莱姆牌组", MonsterDeckKind.Unknown));
            return catalog;
        }

        [Test]
        public void Build_OnlyCollectsMonsterTrapHelpCard()
        {
            var entries = CheatToolCardSearchIndex.Build(BuildCatalog());
            Assert.AreEqual(3, entries.Count, "应仅收录 monster/trap/helpcard 三类，排除遗物/道具玩家卡/Avatar");
        }

        [Test]
        public void Build_ExcludesArchivedHelpCardDeck()
        {
            var entries = CheatToolCardSearchIndex.Build(BuildCatalog());
            for (var i = 0; i < entries.Count; i++)
            {
                Assert.AreNotEqual("help.archived_tool", entries[i].DefId, "归档卡组成员不得出现在可加卡列表");
            }
        }

        [Test]
        public void Match_EmptyQuery_ReturnsAll()
        {
            var entries = CheatToolCardSearchIndex.Build(BuildCatalog());
            var matched = CheatToolCardSearchIndex.Match(entries, string.Empty);
            Assert.AreEqual(entries.Count, matched.Count);
        }

        [Test]
        public void Match_ByCardDisplayName_Chinese()
        {
            var matched = CheatToolCardSearchIndex.Match(CheatToolCardSearchIndex.Build(BuildCatalog()), "莱姆");
            Assert.AreEqual(1, matched.Count);
            Assert.AreEqual("monster.melee_3", matched[0].DefId);
        }

        [Test]
        public void Match_ByCardDefId_OrdinalIgnoreCase()
        {
            var matched = CheatToolCardSearchIndex.Match(CheatToolCardSearchIndex.Build(BuildCatalog()), "MELEE_3");
            Assert.AreEqual(1, matched.Count);
            Assert.AreEqual("monster.melee_3", matched[0].DefId);
        }

        [Test]
        public void Match_ByDeckDisplayName()
        {
            var matched = CheatToolCardSearchIndex.Match(CheatToolCardSearchIndex.Build(BuildCatalog()), "莱姆牌组");
            Assert.AreEqual(1, matched.Count);
            Assert.AreEqual("monster.melee_3", matched[0].DefId);
        }

        [Test]
        public void Match_BySkillName()
        {
            var matched = CheatToolCardSearchIndex.Match(CheatToolCardSearchIndex.Build(BuildCatalog()), "历战");
            Assert.AreEqual(1, matched.Count);
            Assert.AreEqual("monster.melee_3", matched[0].DefId);
        }

        [Test]
        public void Match_BySkillDesignText()
        {
            var matched = CheatToolCardSearchIndex.Match(CheatToolCardSearchIndex.Build(BuildCatalog()), "受伤后攻击");
            Assert.AreEqual(1, matched.Count);
            Assert.AreEqual("monster.melee_3", matched[0].DefId);
        }

        [Test]
        public void Match_ByInjectedCardDescription()
        {
            var entries = CheatToolCardSearchIndex.Build(BuildCatalog(), defId =>
                defId == "trap.flame" ? "踩中会受到灼烧伤害" : null);
            var matched = CheatToolCardSearchIndex.Match(entries, "灼烧");
            Assert.AreEqual(1, matched.Count);
            Assert.AreEqual("trap.flame", matched[0].DefId);
        }

        [Test]
        public void OptionLabel_ShowsDeckAndCardName()
        {
            var entries = CheatToolCardSearchIndex.Build(BuildCatalog());
            var label = CheatToolCardSearchIndex.BuildOptionLabel(entries[0]);
            StringAssert.Contains("莱姆牌组", label);
            StringAssert.Contains("小小莱姆", label);
        }
    }
}
