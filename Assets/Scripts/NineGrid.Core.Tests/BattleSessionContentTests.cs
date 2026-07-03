using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// P2：职业初始牌组 + 怪物技能 / 帮助卡效果回归（战斗段数据面）。
    /// </summary>
    public sealed class BattleSessionContentTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            var architecture = NineGridArchitecture.Current;
            var catalog = TableNineContentCatalog.CreateDefault();
            P5CatalogTestSupport.RegisterCatalog(architecture.GetUtility<IConfigUtility>(), catalog);
            architecture.GetSystem<IContentSystem>().Load(catalog);
            InitialGameFactory.Create(architecture);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void JesterProfession_InitialGameFactory_AppliesStatsSkillAndDeckBinding()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var player = architecture.GetModel<PlayerModel>();
            var avatar = registry.Get(architecture.GetModel<BoardModel>().AvatarUid.Value);

            Assert.AreEqual(ProfessionCatalog.Jester, player.ProfessionId.Value);
            Assert.AreEqual(10, avatar.Stats.GetBase(StatId.MaxHp));
            Assert.AreEqual(10, avatar.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(3, avatar.Stats.GetBase(StatId.Attack));
            Assert.AreEqual(1, avatar.Stats.GetBase(StatId.Armor));
            CollectionAssert.Contains(player.SkillDefIds, "skill.easy_road");
        }

        [Test]
        public void BuildNodeDeckOptions_UsesJesterInitialHelpCards()
        {
            var reward = NineGridArchitecture.Current.GetSystem<IRewardSystem>();
            var options = reward.BuildNodeDeckOptions(1, "deck.wandering_legion");

            Assert.AreEqual(8, options.PlayerCards.Count);
            Assert.AreEqual(3, CountPlayerCardsByDef(options, "help.healing_potion"));
            Assert.AreEqual(1, CountPlayerCardsByDef(options, "help.common_chest_card"));
            Assert.AreEqual(3, CountPlayerCardsByDef(options, "help.throwing_knife"));
            Assert.AreEqual(1, CountPlayerCardsByDef(options, "help.stat_boost_card"));
        }

        [Test]
        public void MinimalDeckMonsters_ApplyCatalogMonsterSkills()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();

            var cub = content.CreateDraft("monster.wandering_child").Create(registry);
            content.ApplyContentToCard(cub);
            Assert.IsTrue(ContainsEffect(cub.EffectIds, "skill.stray_cub.slot6"));
            Assert.IsTrue(ContainsEffect(cub.EffectIds, "skill.stray_cub.first_strike"));

            var thief = content.CreateDraft("monster.pickpocket").Create(registry);
            content.ApplyContentToCard(thief);
            Assert.IsTrue(ContainsEffect(thief.EffectIds, "skill.thief_claims.move"));
        }

        [Test]
        public void StartNode_MinimalDeck_ActivatesMonsterSkillsOnEnemyPool()
        {
            var architecture = NineGridArchitecture.Current;
            var content = architecture.GetSystem<IContentSystem>();
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();

            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 2,
                RequireElite = false,
            };
            options.AddEnemyCard(content.CreateDraft("monster.wandering_child"));
            options.AddEnemyCard(content.CreateDraft("monster.pickpocket"));

            architecture.GetSystem<IActionPipelineSystem>().Execute(new SetupNodeDeckAction(options));

            Assert.AreEqual(2, deck.EnemyCardPoolUids.Count);
            var activatedSkillEffects = new HashSet<string>();
            for (var i = 0; i < deck.EnemyCardPoolUids.Count; i++)
            {
                var card = registry.Get(deck.EnemyCardPoolUids[i]);
                for (var j = 0; j < card.EffectIds.Count; j++)
                {
                    activatedSkillEffects.Add(card.EffectIds[j]);
                }
            }

            Assert.IsTrue(activatedSkillEffects.Contains("skill.stray_cub.slot6"));
            Assert.IsTrue(activatedSkillEffects.Contains("skill.thief_claims.move"));
        }

        [Test]
        public void JesterInitialHelpCards_ExecuteFromCatalog()
        {
            var architecture = NineGridArchitecture.Current;
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var board = architecture.GetModel<BoardModel>();
            var avatar = registry.Get(board.AvatarUid.Value);
            var content = architecture.GetSystem<IContentSystem>();
            avatar.Stats.SetBase(StatId.Hp, 5);

            var potion = content.CreateDraft("help.healing_potion").Create(registry);
            content.ApplyContentToCard(potion);
            deck.AddToItemSlots(potion);
            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(potion.Uid));
            Assert.AreEqual(10, avatar.Stats.GetBase(StatId.Hp));

            var target = content.CreateDraft("monster.vagrant").Create(registry);
            target.Stats.SetBase(StatId.MaxHp, 20);
            target.Stats.SetBase(StatId.Hp, 20);
            target.Stats.SetBase(StatId.Armor, 0);
            content.ApplyContentToCard(target);
            board.PlaceCard(target, SlotId.Board(2));

            var knife = content.CreateDraft("help.throwing_knife").Create(registry);
            content.ApplyContentToCard(knife);
            deck.AddToItemSlots(knife);
            architecture.GetSystem<IActionPipelineSystem>().Execute(new UseItemAction(knife.Uid, new[] { target.Uid }));
            Assert.AreEqual(14, target.Stats.GetBase(StatId.Hp));
        }

        private static int CountPlayerCardsByDef(NodeDeckOptions options, string defId)
        {
            var count = 0;
            for (var i = 0; i < options.PlayerCards.Count; i++)
            {
                if (options.PlayerCards[i].DefId == defId)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool ContainsEffect(IReadOnlyList<string> effectIds, string effectId)
        {
            for (var i = 0; i < effectIds.Count; i++)
            {
                if (effectIds[i] == effectId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
