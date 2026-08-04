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
    /// #116：九件错位新建遗物 + Profession 开局（腐朽顺劈斧 / 护甲 0）。
    /// </summary>
    public sealed class RelicR1NineProfessionContractTests
    {
        public const string RottenCleaveAxeId = "relic.rotten_cleave_axe";

        private static readonly string[] NewRelicIds =
        {
            "relic.armor_strip_knife",
            RottenCleaveAxeId,
            "relic.thorn_mail",
            "relic.muscle_counter",
            "relic.golden_coffer",
            "relic.junk_amplifier",
            "relic.iron_shield",
            "relic.body_potential",
            "relic.beyond_dimension",
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
        public void Bootstrap_NineNewRelics_ExistOnLiveDeck_WithAssemblies()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            Assert.AreEqual(9, NewRelicIds.Length);
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
        public void RottenCleaveAxe_IsRed_AndAbsentFromChestPools()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            Assert.IsTrue(catalog.Relics.TryGetValue(RottenCleaveAxeId, out var axe));
            Assert.AreEqual(ContentRarity.Red, axe.Rarity);

            AssertPoolOmits(catalog, "relic.common_chest", RottenCleaveAxeId);
            AssertPoolOmits(catalog, "relic.blood_conversion", RottenCleaveAxeId);
            AssertPoolOmits(catalog, "relic.golden_chest", RottenCleaveAxeId);
            AssertPoolOmits(catalog, "relic.blue_chest", RottenCleaveAxeId);
        }

        [Test]
        public void Profession_GrantsRottenCleaveAxe_WithBaseArmorZero()
        {
            Assert.AreEqual(RottenCleaveAxeId, ProfessionCatalog.Default.InitialRelicDefId);
            Assert.AreEqual(0, ProfessionCatalog.Default.Armor);

            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 7UL });

            var owned = mArch.GetModel<PlayerModel>().RelicDefIds;
            Assert.IsTrue(ContainsId(owned, RottenCleaveAxeId), "开局应授予腐朽顺劈斧");

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            Assert.AreEqual(0, (int)avatar.Stats.GetBase(StatId.Armor), "战士基础护甲应为 0");
        }

        [Test]
        public void IronShield_GrantsBaseArmorAndDamageReduction()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL, AvatarArmor = 0 });

            var phase = mArch.GetSystem<IPhaseSystem>();
            var stats = mArch.GetSystem<IStatSystem>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();

            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.iron_shield");
            Assert.IsTrue(phase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            Assert.AreEqual(2, stats.GetEffectiveInt(avatar, StatId.Armor), "铁盾基础护甲+2");
            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(avatar));

            var hpBefore = (int)avatar.Stats.GetBase(StatId.Hp);
            pipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 3, "test.iron_shield"));
            pipeline.RunToCompletion();

            // 3 伤 − 1 减免 = 2，先扣当前甲 2 → 不掉血
            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(avatar));
            Assert.AreEqual(hpBefore, (int)avatar.Stats.GetBase(StatId.Hp), "伤害减免应在护甲吸收前生效");
        }

        [Test]
        public void BeyondDimension_RotatesBoard_OnPlayerBattleHit()
        {
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 13UL, AvatarArmor = 0 });

            var phase = mArch.GetSystem<IPhaseSystem>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();

            Assert.IsTrue(phase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            mArch.GetSystem<IContentSystem>().ActivateRelic("relic.beyond_dimension");

            var targetSlot = SlotId.Board(2);
            ClearNonAvatarBoardSlots(board);
            var monster = registry.Create("monster.melee_6", CardKind.Monster);
            monster.Stats.SetBase(StatId.Hp, 5);
            monster.Stats.SetBase(StatId.Attack, 1);
            board.PlaceCard(monster, targetSlot);

            var avatarUid = board.AvatarUid.Value;
            var before = CaptureBoardOccupancy(board);
            Assert.IsTrue(phase.ApplyCombatHit(avatarUid, monster.Uid).Accepted);
            pipeline.RunToCompletion();

            var after = CaptureBoardOccupancy(board);
            Assert.AreNotEqual(before, after, "超越维度战斗时应旋转一次");
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

        private static string CaptureBoardOccupancy(BoardModel board)
        {
            var parts = new List<string>(9);
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                parts.Add(board.GetCardUid(SlotId.Board(i)).ToString());
            }

            return string.Join(",", parts);
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

        private static void AssertPoolOmits(GameContentCatalog catalog, string poolId, string defId)
        {
            Assert.IsTrue(catalog.Rewards.TryGetPool(poolId, out var pool), poolId);
            for (var i = 0; i < pool.Entries.Count; i++)
            {
                Assert.AreNotEqual(defId, pool.Entries[i].DefId, poolId + " must omit " + defId);
            }
        }
    }
}
