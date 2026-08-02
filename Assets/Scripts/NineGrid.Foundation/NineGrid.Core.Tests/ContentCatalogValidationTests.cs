using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 内容门禁：小型夹具与生产 JSON Catalog 均须 ValidateCatalog 绿（ADR-0008 / ADR-0009 / #70）。
    /// </summary>
    public sealed class ContentCatalogValidationTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void DefaultCatalog_ValidateCatalog_IsValidWithNoPendingEffects()
        {
            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();

            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending effect ids: " + string.Join(", ", report.PendingEffectIds));
            Assert.Greater(report.ImplementedEffectIds.Count, 0, "Expected implemented effects in default catalog.");
        }

        [Test]
        public void BootstrapCatalog_ValidateCatalog_IsValidWithProductionJsonProjection()
        {
            CardPresentationConfigCatalog.Invalidate();
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();

            Assert.IsTrue(report.IsValid, FormatIssues(report));
            Assert.AreEqual(0, report.PendingEffectIds.Count, "Pending effect ids: " + string.Join(", ", report.PendingEffectIds));

            Assert.IsFalse(catalog.Cards.TryGetValue("help.healing_spring", out _), "help.healing_spring 应已迁 Trap");
            Assert.IsTrue(catalog.Cards.TryGetValue("trap.healing_spring", out var spring));
            Assert.AreEqual(CardKind.Trap, spring.Kind);
            Assert.AreEqual(1, spring.EffectIds.Count, "trap.healing_spring mounts must come from JSON projection");
            Assert.AreEqual("trap.healing_spring.heal_on_move", spring.EffectIds[0]);
            Assert.AreEqual("疗愈之光", spring.DisplayName);

            Assert.IsTrue(catalog.Cards.TryGetValue("help.doubling_tower", out var tower));
            Assert.AreEqual(2, tower.EffectIds.Count);
            Assert.AreEqual("help.doubling_tower.board_monster", tower.EffectIds[0]);
            Assert.AreEqual("help.doubling_tower.item_player", tower.EffectIds[1]);

            Assert.IsTrue(catalog.Relics.TryGetValue("relic.arsenal", out var arsenal));
            Assert.AreEqual("军械库", arsenal.DisplayName);
            Assert.AreEqual(1, arsenal.EffectIds.Count);
            Assert.AreEqual("relic.arsenal.node_end", arsenal.EffectIds[0]);

            Assert.IsTrue(catalog.Skills.TryGetValue("skill.absorb_bone", out var absorb));
            Assert.Greater(absorb.EffectIds.Count, 0);

            Assert.IsTrue(catalog.Cards.TryGetValue("monster.beggar", out var beggar));
            Assert.AreEqual(CardKind.Monster, beggar.Kind);
            Assert.AreEqual(AttackPattern.OrthogonalMelee, beggar.AttackPattern);
            Assert.AreEqual(3, beggar.Stats.Action);
            // 技能仍以独立 Skill JSON 存在；怪物挂载可为空（当前乞丐无 skillIds）。
            Assert.IsTrue(catalog.Skills.ContainsKey("skill.beggar_bond"));
            Assert.AreEqual(0, beggar.SkillIds.Count);

            Assert.IsTrue(catalog.MonsterDecks.TryGetValue("deck.dragon", out var dragon));
            Assert.Greater(dragon.MonsterDefIds.Count, 0);

            Assert.IsTrue(catalog.Rewards.TryGetRoom(RoomKind.Fountain, out var fountain));
            Assert.AreEqual(1, fountain.OpeningInjects.Count);
            Assert.AreEqual(RoomInjectSourceKind.FixedCard, fountain.OpeningInjects[0].SourceKind);
            Assert.AreEqual("help.food_card", fountain.OpeningInjects[0].CardDefId);

            Assert.IsTrue(catalog.Rewards.TryGetRoom(RoomKind.Gold, out var gold));
            Assert.AreEqual("help.gold_card", gold.OpeningInjects[0].CardDefId);
            Assert.IsTrue(catalog.Rewards.TryGetRoom(RoomKind.Attribute, out var attribute));
            Assert.AreEqual(RoomInjectSourceKind.WeightedPool, attribute.OpeningInjects[0].SourceKind);
            Assert.AreEqual(2, attribute.OpeningInjects[0].Count);
            Assert.IsTrue(attribute.OpeningInjects[0].AllowDuplicates);
            Assert.IsTrue(catalog.Rewards.TryGetRoom(RoomKind.Elite, out var elite));
            Assert.AreEqual(3, elite.OpeningInjects.Count);
            Assert.IsTrue(catalog.Rewards.TryGetRoom(RoomKind.Boss, out var boss));
            Assert.AreEqual(0, boss.OpeningInjects.Count);
            Assert.IsTrue(catalog.Rewards.TryGetRoom(RoomKind.TreasureReward, out _));
            Assert.IsTrue(catalog.Rewards.TryGetRoom(RoomKind.ItemReward, out _));
            Assert.IsFalse(System.Enum.IsDefined(typeof(RoomKind), "Battle"));
            Assert.IsFalse(System.Enum.IsDefined(typeof(RoomKind), "Event"));
            Assert.IsTrue(catalog.Cards.ContainsKey("help.hp_card"));
            Assert.IsTrue(catalog.Cards.ContainsKey("help.armor_card"));
            Assert.IsTrue(catalog.Cards.ContainsKey("help.attack_card"));

            Assert.IsTrue(catalog.Effects.ContainsKey("help.healing_potion.use"));
            Assert.IsTrue(catalog.Effects.ContainsKey("relic.junk_recycler.use"));
            var potion = catalog.Effects["help.healing_potion.use"];
            var recycler = catalog.Effects["relic.junk_recycler.use"];
            Assert.AreEqual(10, EffectDefinitionParser.ParseJson(potion.Json).Action.Get("amount").AsInt(0));
            Assert.AreEqual(2, EffectDefinitionParser.ParseJson(recycler.Json).Action.Get("amount").AsInt(0));
            Assert.AreEqual(EffectContainerType.HelpCard, EffectDefinitionParser.ParseJson(potion.Json).ContainerType);
            Assert.AreEqual(EffectContainerType.Relic, EffectDefinitionParser.ParseJson(recycler.Json).ContainerType);
            Assert.Greater(EffectTemplateCatalog.Count, 0, "effect templates must load");
            Assert.IsTrue(EffectTemplateCatalog.TryGet("tpl.heal_player_on_use_help_card", out _));

            Assert.AreEqual(5, catalog.Economy.MonsterRemovedGold);
            Assert.AreEqual(6, catalog.Rewards.NodeDeckRules.Count, "战斗节点 1/2/3/5/6/8 各一行");
            NodeDeckRule node1 = null;
            NodeDeckRule node8 = null;
            for (var i = 0; i < catalog.Rewards.NodeDeckRules.Count; i++)
            {
                var rule = catalog.Rewards.NodeDeckRules[i];
                if (rule.NodeIndex == 1)
                {
                    node1 = rule;
                }

                if (rule.NodeIndex == 8)
                {
                    node8 = rule;
                }
            }

            Assert.IsNotNull(node1);
            Assert.IsNotNull(node8);
            Assert.AreEqual(8, node1.Seq1Count);
            Assert.AreEqual(1, node8.Seq5Count);

            Assert.IsTrue(catalog.MonsterDecks.TryGetValue("deck.insect", out var insect));
            Assert.AreNotEqual(MonsterDeckKind.Reserve, insect.Kind);
            Assert.IsTrue(catalog.Cards.TryGetValue("monster.fire_dragon", out var floorBoss));
            Assert.AreEqual(5, floorBoss.Sequence);
            Assert.IsTrue(floorBoss.IsBoss);
            Assert.AreEqual(MonsterRank.FloorBoss, floorBoss.Rank);
        }

        private static string FormatIssues(ContentValidationReport report)
        {
            if (report.Issues.Count == 0)
            {
                return string.Empty;
            }

            return string.Join("; ", report.Issues);
        }
    }
}
