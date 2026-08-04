using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #117：R1 可拼新建遗物包（13 件）— 在役内容、稀有度奖池与样本效果。
    /// </summary>
    public sealed class RelicR1AssemblableContractTests
    {
        private static readonly string[] NewRelicIds =
        {
            "relic.composite_armor",
            "relic.gold_blood",
            "relic.rpm_engine",
            "relic.trap_cell",
            "relic.junk_sword",
            "relic.blood_cycle",
            "relic.blood_violence",
            "relic.blood_burst",
            "relic.spinning_barb",
            "relic.blood_regen",
            "relic.sharp_longsword",
            "relic.berserker_axe",
            "relic.metal_blood",
        };

        private static readonly string[] WhiteIds =
        {
            "relic.composite_armor",
            "relic.gold_blood",
            "relic.rpm_engine",
            "relic.trap_cell",
        };

        private static readonly string[] BlueIds =
        {
            "relic.junk_sword",
            "relic.blood_cycle",
            "relic.blood_violence",
            "relic.blood_burst",
            "relic.spinning_barb",
            "relic.blood_regen",
            "relic.sharp_longsword",
        };

        private static readonly string[] GoldIds =
        {
            "relic.berserker_axe",
            "relic.metal_blood",
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
        public void Bootstrap_ThirteenAssemblableRelics_ExistOnLiveDeck_WithAssemblies()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            Assert.AreEqual(13, NewRelicIds.Length);
            for (var i = 0; i < NewRelicIds.Length; i++)
            {
                var id = NewRelicIds[i];
                Assert.IsTrue(catalog.Relics.TryGetValue(id, out var relic), "missing " + id);
                Assert.AreEqual(RelicDecks.Live, relic.DeckId, id);
                Assert.IsFalse(string.IsNullOrEmpty(relic.DisplayName), id + " displayName");
                Assert.Greater(relic.EffectIds.Count, 0, id + " needs effect assemblies");
            }
        }

        [Test]
        public void AssemblableRelics_AppearInRarityFilteredChestPools()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            AssertRarity(catalog, WhiteIds, ContentRarity.White);
            AssertRarity(catalog, BlueIds, ContentRarity.Blue);
            AssertRarity(catalog, GoldIds, ContentRarity.Gold);

            AssertPoolContainsAll(catalog, "relic.common_chest", WhiteIds);
            AssertPoolContainsAll(catalog, "relic.common_chest", BlueIds);
            AssertPoolContainsAll(catalog, "relic.common_chest", GoldIds);
            AssertPoolContainsAll(catalog, "relic.blood_conversion", WhiteIds);
            AssertPoolContainsAll(catalog, "relic.blood_conversion", BlueIds);
            AssertPoolContainsAll(catalog, "relic.blood_conversion", GoldIds);
        }

        [Test]
        public void JunkSword_HealsOnKill()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 117UL, AvatarArmor = 0 });

            var phase = mArch.GetSystem<IPhaseSystem>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            Assert.IsTrue(phase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.junk_sword");

            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Hp, 10);
            avatar.Stats.SetBase(StatId.MaxHp, 20);

            ClearNonAvatarBoardSlots(board);
            var monster = registry.Create("monster.melee_6", CardKind.Monster);
            monster.Stats.SetBase(StatId.Hp, 1);
            monster.Stats.SetBase(StatId.Attack, 1);
            board.PlaceCard(monster, SlotId.Board(2));

            Assert.IsTrue(phase.ApplyCombatHit(avatar.Uid, monster.Uid).Accepted);
            pipeline.RunToCompletion();

            Assert.AreEqual(12, (int)avatar.Stats.GetBase(StatId.Hp), "废物剑击杀应回复 2 血");
        }

        [Test]
        public void BloodViolence_AddsAttackWhenHpBelowHalf()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 118UL, AvatarArmor = 0 });

            var stats = mArch.GetSystem<IStatSystem>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.blood_violence");
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);

            var baseAtk = stats.GetEffectiveInt(avatar, StatId.Attack);
            avatar.Stats.SetBase(StatId.Hp, 9);
            Assert.AreEqual(baseAtk + 2, stats.GetEffectiveInt(avatar, StatId.Attack), "低于 50% 时应攻击+2");

            avatar.Stats.SetBase(StatId.Hp, 10);
            Assert.AreEqual(baseAtk, stats.GetEffectiveInt(avatar, StatId.Attack), "达到 50% 后应复原");
        }

        [Test]
        public void CompositeArmor_GrantsCurrentArmorFromAttackAtNodeStart()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 119UL, AvatarArmor = 0 });

            var phase = mArch.GetSystem<IPhaseSystem>();
            var stats = mArch.GetSystem<IStatSystem>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.composite_armor");
            var avatar = registry.Get(board.AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 7);
            Assert.IsTrue(phase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);

            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(avatar), "7 攻应换 2 当前甲");
            Assert.AreEqual(0, stats.GetEffectiveInt(avatar, StatId.Armor), "复合盔甲不提供基础甲");
        }

        private static void ClearNonAvatarBoardSlots(BoardModel board)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                if (board.GetCardUid(slot) != 0)
                {
                    board.ClearSlot(slot);
                }
            }
        }

        private static void AssertRarity(GameContentCatalog catalog, IReadOnlyList<string> ids, ContentRarity rarity)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                Assert.IsTrue(catalog.Relics.TryGetValue(ids[i], out var relic), ids[i]);
                Assert.AreEqual(rarity, relic.Rarity, ids[i]);
            }
        }

        private static void AssertPoolContainsAll(GameContentCatalog catalog, string poolId, IReadOnlyList<string> ids)
        {
            Assert.IsTrue(catalog.Rewards.TryGetPool(poolId, out var pool), poolId);
            var present = CollectIds(pool);
            for (var i = 0; i < ids.Count; i++)
            {
                Assert.IsTrue(present.Contains(ids[i]), poolId + " missing " + ids[i]);
            }
        }

        private static HashSet<string> CollectIds(RewardPoolDefinition pool)
        {
            var set = new HashSet<string>();
            for (var i = 0; i < pool.Entries.Count; i++)
            {
                set.Add(pool.Entries[i].DefId);
            }

            return set;
        }
    }
}
