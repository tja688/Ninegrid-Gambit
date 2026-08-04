using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #115：遗物归档卡组约定 + 奖池/授予路径排除错位旧卡。
    /// </summary>
    public sealed class RelicArchiveDeckContractTests
    {
        private static readonly string[] MisalignedLegacyRelicIds =
        {
            "relic.even_hatred",
            "relic.arsenal",
            "relic.thorn_skin",
            "relic.battle_hardened",
            "relic.tower_child",
            "relic.junk_slot_machine",
            "relic.hard_skin",
            "relic.blood_shockwave",
            "relic.easy_road",
        };

        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            CardPresentationConfigCatalog.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
            mArch = null;
        }

        [Test]
        public void QueryExpand_ExcludesArchiveDeckRelics_KeepsLive()
        {
            var catalog = new GameContentCatalog();
            catalog.AddRelic(new RelicContentDefinition("relic.live_wood", "木剑", ContentRarity.White, "live")
                .InDeck(RelicDecks.Live));
            catalog.AddRelic(new RelicContentDefinition("relic.archived_demo", "旧错位", ContentRarity.White, "archive")
                .InDeck(RelicDecks.Archive));
            catalog.Rewards.AddPool(new RewardPoolDefinition("relic.common_chest", 3)
                .WithQuery(new RewardPoolQueryRule
                {
                    Kind = CardKind.Relic,
                    DefaultWeight = 1,
                }
                    .AllowRarity(ContentRarity.White)
                    .AllowRarity(ContentRarity.Blue)
                    .AllowRarity(ContentRarity.Gold)));
            catalog.Rewards.AddPool(new RewardPoolDefinition("relic.blood_conversion", 3)
                .WithQuery(new RewardPoolQueryRule
                {
                    Kind = CardKind.Relic,
                    DefaultWeight = 1,
                }
                    .AllowRarity(ContentRarity.White)
                    .AllowRarity(ContentRarity.Blue)
                    .AllowRarity(ContentRarity.Gold)));

            RewardPoolQueryExpander.ExpandAll(catalog);

            AssertPoolContains(catalog, "relic.common_chest", "relic.live_wood");
            AssertPoolOmits(catalog, "relic.common_chest", "relic.archived_demo");
            AssertPoolContains(catalog, "relic.blood_conversion", "relic.live_wood");
            AssertPoolOmits(catalog, "relic.blood_conversion", "relic.archived_demo");
        }

        [Test]
        public void Bootstrap_NineMisalignedRelics_AreOnArchiveDeck_AndAbsentFromRelicPools()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            Assert.AreEqual(9, MisalignedLegacyRelicIds.Length);
            for (var i = 0; i < MisalignedLegacyRelicIds.Length; i++)
            {
                var id = MisalignedLegacyRelicIds[i];
                Assert.IsTrue(catalog.Relics.TryGetValue(id, out var relic), "missing " + id);
                Assert.AreEqual(
                    RelicDecks.Archive,
                    relic.DeckId,
                    id + " should be on archive deck");
            }

            Assert.IsTrue(catalog.Relics.TryGetValue("relic.wood_sword", out var live));
            Assert.AreEqual(RelicDecks.Live, live.DeckId);

            AssertPoolOmitsAll(catalog, "relic.common_chest", MisalignedLegacyRelicIds);
            AssertPoolOmitsAll(catalog, "relic.blood_conversion", MisalignedLegacyRelicIds);
            Assert.IsTrue(catalog.Rewards.TryGetPool("relic.common_chest", out var chest));
            Assert.Greater(chest.Entries.Count, 0, "live relic chest pool must stay non-empty");
        }

        [Test]
        public void Profession_DoesNotGrantArchiveRelic()
        {
            var initial = ProfessionCatalog.Default.InitialRelicDefId;
            Assert.IsTrue(
                string.IsNullOrEmpty(initial),
                "Profession initial relic must stay empty until #116 replacement; was: " + initial);
        }

        [Test]
        public void InitialGame_DoesNotSeedArchiveRelicOntoPlayer()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 7UL });

            var owned = mArch.GetModel<PlayerModel>().RelicDefIds;
            for (var i = 0; i < owned.Count; i++)
            {
                Assert.IsTrue(catalog.Relics.TryGetValue(owned[i], out var relic), owned[i]);
                Assert.IsFalse(
                    RelicDecks.IsArchive(relic.DeckId),
                    "run must not start with archive relic: " + owned[i]);
            }
        }

        [Test]
        public void GrantRelicAction_SkipsArchiveDeckRelic()
        {
            var catalog = new GameContentCatalog();
            catalog.AddRelic(new RelicContentDefinition("relic.archived_grant", "归档", ContentRarity.White, "a")
                .InDeck(RelicDecks.Archive));
            catalog.AddRelic(new RelicContentDefinition("relic.live_grant", "在役", ContentRarity.White, "l")
                .InDeck(RelicDecks.Live));
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL });

            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new GrantRelicAction("relic.archived_grant"));
            pipeline.RunToCompletion();

            var owned = mArch.GetModel<PlayerModel>().RelicDefIds;
            Assert.IsFalse(ContainsId(owned, "relic.archived_grant"));

            pipeline.Enqueue(new GrantRelicAction("relic.live_grant"));
            pipeline.RunToCompletion();
            Assert.IsTrue(ContainsId(mArch.GetModel<PlayerModel>().RelicDefIds, "relic.live_grant"));
        }

        private static bool ContainsId(IReadOnlyList<string> ids, string defId)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                if (ids[i] == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertPoolContains(GameContentCatalog catalog, string poolId, string defId)
        {
            Assert.IsTrue(catalog.Rewards.TryGetPool(poolId, out var pool), poolId);
            Assert.IsTrue(CollectIds(pool).Contains(defId), poolId + " missing " + defId);
        }

        private static void AssertPoolOmits(GameContentCatalog catalog, string poolId, string defId)
        {
            Assert.IsTrue(catalog.Rewards.TryGetPool(poolId, out var pool), poolId);
            Assert.IsFalse(CollectIds(pool).Contains(defId), poolId + " must omit " + defId);
        }

        private static void AssertPoolOmitsAll(GameContentCatalog catalog, string poolId, IReadOnlyList<string> defIds)
        {
            Assert.IsTrue(catalog.Rewards.TryGetPool(poolId, out var pool), poolId);
            var ids = CollectIds(pool);
            for (var i = 0; i < defIds.Count; i++)
            {
                Assert.IsFalse(ids.Contains(defIds[i]), poolId + " must omit " + defIds[i]);
            }
        }

        private static HashSet<string> CollectIds(RewardPoolDefinition pool)
        {
            var ids = new HashSet<string>();
            for (var i = 0; i < pool.Entries.Count; i++)
            {
                ids.Add(pool.Entries[i].DefId);
            }

            return ids;
        }
    }
}
