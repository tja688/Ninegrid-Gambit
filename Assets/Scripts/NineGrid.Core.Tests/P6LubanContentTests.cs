using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NineGrid.Content;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class P6LubanContentTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void GeneratedLubanTablesCanFeedContentSystem()
        {
            var dataDirectory = Path.Combine(
                Environment.CurrentDirectory,
                TableNineLubanCatalogFactory.DefaultDataRelativePath);

            var catalog = TableNineLubanCatalogFactory.CreateFromDirectory(dataDirectory);
            Assert.AreEqual(87, catalog.Cards.Count);
            Assert.AreEqual(135, catalog.Effects.Count);
            Assert.AreEqual(68, catalog.Skills.Count);
            Assert.AreEqual(19, catalog.Relics.Count);
            Assert.IsTrue(catalog.Rewards.Pools.ContainsKey("help.choice"));
            Assert.IsTrue(catalog.Rewards.Rooms.ContainsKey(RoomKind.Fountain));
            Assert.AreEqual(9, catalog.Rewards.NodeDeckRules.Count);

            var architecture = NineGridArchitecture.Current;
            architecture.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var content = architecture.GetSystem<IContentSystem>();
            Assert.IsTrue(content.TryReloadFromConfig());

            var report = content.ValidateCatalog();
            Assert.IsTrue(report.IsValid, FirstIssue(report));
            Assert.GreaterOrEqual(report.ImplementedEffectIds.Count, 127);
            Assert.LessOrEqual(report.PendingEffectIds.Count, 8);

            var mage = content.CreateDraft("monster.skeleton_mage");
            Assert.AreEqual(CardKind.Monster, mage.Kind);
            Assert.AreEqual(6, mage.MaxHp);
            Assert.AreEqual(3, mage.Attack);
            Assert.AreEqual(5, mage.Armor);
            Assert.IsTrue(Contains(mage.EffectIds, "skill.range_expand.rule"));
            Assert.IsTrue(Contains(catalog.Cards["monster.skeleton_mage"].SkillIds, "skill.range_expand"));
        }

        [Test]
        public void BatchEightGeneratedLubanCatalogMatchesHardcodedDefaultCatalog()
        {
            var dataDirectory = Path.Combine(
                Environment.CurrentDirectory,
                TableNineLubanCatalogFactory.DefaultDataRelativePath);

            var hardcoded = TableNineContentCatalog.CreateDefault();
            var luban = TableNineLubanCatalogFactory.CreateFromDirectory(dataDirectory);

            CollectionAssert.AreEqual(SortedKeys(hardcoded.Effects), SortedKeys(luban.Effects));
            CollectionAssert.AreEqual(SortedKeys(hardcoded.Cards), SortedKeys(luban.Cards));
            CollectionAssert.AreEqual(SortedKeys(hardcoded.Skills), SortedKeys(luban.Skills));
            CollectionAssert.AreEqual(SortedKeys(hardcoded.Relics), SortedKeys(luban.Relics));
            CollectionAssert.AreEqual(SortedKeys(hardcoded.MonsterDecks), SortedKeys(luban.MonsterDecks));
            CollectionAssert.AreEqual(
                hardcoded.Rewards.Pools.Keys.OrderBy(value => value).ToArray(),
                luban.Rewards.Pools.Keys.OrderBy(value => value).ToArray());
            CollectionAssert.AreEqual(
                hardcoded.Rewards.Rooms.Keys.OrderBy(value => value.ToString()).ToArray(),
                luban.Rewards.Rooms.Keys.OrderBy(value => value.ToString()).ToArray());

            foreach (var id in hardcoded.Effects.Keys)
            {
                AssertEffectEqual(hardcoded.Effects[id], luban.Effects[id]);
            }

            foreach (var id in hardcoded.Cards.Keys)
            {
                AssertCardEqual(hardcoded.Cards[id], luban.Cards[id]);
            }

            foreach (var id in hardcoded.Skills.Keys)
            {
                AssertSkillEqual(hardcoded.Skills[id], luban.Skills[id]);
            }

            foreach (var id in hardcoded.Relics.Keys)
            {
                AssertRelicEqual(hardcoded.Relics[id], luban.Relics[id]);
            }

            foreach (var id in hardcoded.MonsterDecks.Keys)
            {
                AssertMonsterDeckEqual(hardcoded.MonsterDecks[id], luban.MonsterDecks[id]);
            }

            foreach (var id in hardcoded.Rewards.Pools.Keys)
            {
                AssertRewardPoolEqual(hardcoded.Rewards.Pools[id], luban.Rewards.Pools[id]);
            }

            foreach (var kind in hardcoded.Rewards.Rooms.Keys)
            {
                AssertRoomEqual(hardcoded.Rewards.Rooms[kind], luban.Rewards.Rooms[kind]);
            }

            AssertNodeRulesEqual(hardcoded.Rewards.NodeDeckRules, luban.Rewards.NodeDeckRules);
            AssertEconomyEqual(hardcoded.Economy, luban.Economy);
        }

        private static string FirstIssue(ContentValidationReport report)
        {
            return report.Issues.Count == 0 ? string.Empty : report.Issues[0];
        }

        private static bool Contains<T>(System.Collections.Generic.IReadOnlyList<T> list, T value)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (Equals(list[i], value))
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] SortedKeys<T>(IReadOnlyDictionary<string, T> values)
        {
            return values.Keys.OrderBy(value => value).ToArray();
        }

        private static void AssertEffectEqual(ContentEffectDefinition expected, ContentEffectDefinition actual)
        {
            Assert.AreEqual(expected.ContainerType, actual.ContainerType, expected.Id);
            Assert.AreEqual(expected.State, actual.State, expected.Id);
            Assert.AreEqual(expected.Json, actual.Json, expected.Id);
            Assert.AreEqual(expected.DesignText, actual.DesignText, expected.Id);
        }

        private static void AssertCardEqual(CardContentDefinition expected, CardContentDefinition actual)
        {
            Assert.AreEqual(expected.DisplayName, actual.DisplayName, expected.DefId);
            Assert.AreEqual(expected.Kind, actual.Kind, expected.DefId);
            Assert.AreEqual(expected.Rarity, actual.Rarity, expected.DefId);
            Assert.AreEqual(expected.Price, actual.Price, expected.DefId);
            Assert.AreEqual(expected.Level, actual.Level, expected.DefId);
            Assert.AreEqual(expected.IsElite, actual.IsElite, expected.DefId);
            Assert.AreEqual(expected.IsBoss, actual.IsBoss, expected.DefId);
            Assert.AreEqual(expected.IsReserve, actual.IsReserve, expected.DefId);
            Assert.AreEqual(expected.DeckId ?? string.Empty, actual.DeckId ?? string.Empty, expected.DefId);
            Assert.AreEqual(expected.Stats.MaxHp, actual.Stats.MaxHp, expected.DefId);
            Assert.AreEqual(expected.Stats.Attack, actual.Stats.Attack, expected.DefId);
            Assert.AreEqual(expected.Stats.Armor, actual.Stats.Armor, expected.DefId);
            Assert.AreEqual(expected.Stats.Recovery, actual.Stats.Recovery, expected.DefId);
            CollectionAssert.AreEqual(expected.Tags, actual.Tags, expected.DefId);
            CollectionAssert.AreEqual(expected.EffectIds, actual.EffectIds, expected.DefId);
            CollectionAssert.AreEqual(expected.SkillIds, actual.SkillIds, expected.DefId);
        }

        private static void AssertSkillEqual(SkillContentDefinition expected, SkillContentDefinition actual)
        {
            Assert.AreEqual(expected.DisplayName, actual.DisplayName, expected.DefId);
            Assert.AreEqual(expected.ContainerType, actual.ContainerType, expected.DefId);
            Assert.AreEqual(expected.DesignText, actual.DesignText, expected.DefId);
            CollectionAssert.AreEqual(expected.EffectIds, actual.EffectIds, expected.DefId);
        }

        private static void AssertRelicEqual(RelicContentDefinition expected, RelicContentDefinition actual)
        {
            Assert.AreEqual(expected.DisplayName, actual.DisplayName, expected.DefId);
            Assert.AreEqual(expected.Rarity, actual.Rarity, expected.DefId);
            Assert.AreEqual(expected.DesignText, actual.DesignText, expected.DefId);
            CollectionAssert.AreEqual(expected.Tags, actual.Tags, expected.DefId);
            CollectionAssert.AreEqual(expected.EffectIds, actual.EffectIds, expected.DefId);
        }

        private static void AssertMonsterDeckEqual(MonsterDeckDefinition expected, MonsterDeckDefinition actual)
        {
            Assert.AreEqual(expected.DisplayName, actual.DisplayName, expected.Id);
            Assert.AreEqual(expected.Kind, actual.Kind, expected.Id);
            CollectionAssert.AreEqual(expected.MonsterDefIds, actual.MonsterDefIds, expected.Id);
        }

        private static void AssertRewardPoolEqual(RewardPoolDefinition expected, RewardPoolDefinition actual)
        {
            Assert.AreEqual(expected.PickCount, actual.PickCount, expected.Id);
            Assert.AreEqual(expected.Entries.Count, actual.Entries.Count, expected.Id);
            for (var i = 0; i < expected.Entries.Count; i++)
            {
                Assert.AreEqual(expected.Entries[i].DefId, actual.Entries[i].DefId, expected.Id);
                Assert.AreEqual(expected.Entries[i].Kind, actual.Entries[i].Kind, expected.Id);
                Assert.AreEqual(expected.Entries[i].Weight, actual.Entries[i].Weight, expected.Id);
                Assert.AreEqual(expected.Entries[i].Count, actual.Entries[i].Count, expected.Id);
            }
        }

        private static void AssertRoomEqual(RoomDefinition expected, RoomDefinition actual)
        {
            Assert.AreEqual(expected.DisplayName, actual.DisplayName, expected.Kind.ToString());
            Assert.AreEqual(expected.Weight, actual.Weight, expected.Kind.ToString());
            Assert.AreEqual(expected.GoldDelta, actual.GoldDelta, expected.Kind.ToString());
            Assert.AreEqual(expected.MaxHpDelta, actual.MaxHpDelta, expected.Kind.ToString());
            Assert.AreEqual(expected.HealToFull, actual.HealToFull, expected.Kind.ToString());
            Assert.AreEqual(expected.RewardPoolId ?? string.Empty, actual.RewardPoolId ?? string.Empty, expected.Kind.ToString());
            Assert.AreEqual(expected.ShopOfferCount, actual.ShopOfferCount, expected.Kind.ToString());
        }

        private static void AssertNodeRulesEqual(IReadOnlyList<NodeDeckRule> expected, IReadOnlyList<NodeDeckRule> actual)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].NodeIndex, actual[i].NodeIndex);
                Assert.AreEqual(expected[i].TotalMonsterCount, actual[i].TotalMonsterCount);
                Assert.AreEqual(expected[i].Level1Min, actual[i].Level1Min);
                Assert.AreEqual(expected[i].Level1Max, actual[i].Level1Max);
                Assert.AreEqual(expected[i].Level2Min, actual[i].Level2Min);
                Assert.AreEqual(expected[i].Level2Max, actual[i].Level2Max);
                Assert.AreEqual(expected[i].Level3Min, actual[i].Level3Min);
                Assert.AreEqual(expected[i].Level3Max, actual[i].Level3Max);
                Assert.AreEqual(expected[i].EliteCount, actual[i].EliteCount);
                Assert.AreEqual(expected[i].BossCount, actual[i].BossCount);
                Assert.AreEqual(expected[i].DeckKind, actual[i].DeckKind);
            }
        }

        private static void AssertEconomyEqual(EconomyConfig expected, EconomyConfig actual)
        {
            Assert.AreEqual(expected.MonsterRemovedGold, actual.MonsterRemovedGold);
            Assert.AreEqual(expected.UnusedHelpCardGold, actual.UnusedHelpCardGold);
            Assert.AreEqual(expected.SkipHelpChoiceGold, actual.SkipHelpChoiceGold);
            Assert.AreEqual(expected.SkipRelicChoiceGold, actual.SkipRelicChoiceGold);
            Assert.AreEqual(expected.DiscardRelicGold, actual.DiscardRelicGold);
            Assert.AreEqual(expected.ShopDeleteHelpCardGold, actual.ShopDeleteHelpCardGold);
        }
    }
}
