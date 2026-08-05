using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #139：正式内容可达性终审 — 策划现行 51 件遗物 / 19 种道具卡全部存在、效果已装配、
    /// 有合法正式来源；归档内容（9 件错位遗物 + 7 张非现行道具卡）0 可达；奖池无悬空 ID、
    /// 无空内容；层主击杀固定洗入 1 金宝箱卡 + 2 金币卡。
    /// 产品权威 = 策划案（05-遗物 51 件；03 通用卡组 + 角色卡组 19 种）；旧 58 数审计已退役。
    /// </summary>
    public sealed class FormalContentReachabilityContractTests
    {
        /// <summary>策划案 05-遗物 51 件（白 27 / 蓝 15 / 金 8 / 独特 1），按稀有度分组。</summary>
        private static readonly string[] OfficialWhiteRelicIds =
        {
            "relic.junk_recycler", "relic.wood_shield", "relic.wood_sword", "relic.wood_armor",
            "relic.lucky_coin", "relic.throwing_knife_bag", "relic.potion_bag",
            "relic.junk_launcher", "relic.junk_coating", "relic.sling",
            "relic.shield_knife", "relic.gold_knife", "relic.punch_card_knife",
            "relic.armor_strip_knife", "relic.heavy_armor", "relic.gold_armor",
            "relic.foam_armor", "relic.composite_armor", "relic.blood_demon",
            "relic.body_potential", "relic.muscle_counter", "relic.gold_blood",
            "relic.swap_button", "relic.rotation_button", "relic.rpm_engine",
            "relic.trap_cell", "relic.rotation_trick",
        };

        private static readonly string[] OfficialBlueRelicIds =
        {
            "relic.iron_shield", "relic.forge_tool", "relic.terror_mask",
            "relic.sharp_longsword", "relic.vitality_amulet", "relic.thorn_mail",
            "relic.junk_sword", "relic.junk_amplifier", "relic.junk_cycler",
            "relic.junk_body", "relic.blood_cycle", "relic.blood_violence",
            "relic.blood_burst", "relic.spinning_barb", "relic.blood_regen",
        };

        private static readonly string[] OfficialGoldRelicIds =
        {
            "relic.golden_sword", "relic.dragon_scale_armor", "relic.golden_coffer",
            "relic.berserker_axe", "relic.phoenix_feather", "relic.metal_blood",
            "relic.beyond_dimension", "relic.craving",
        };

        private const string UniqueRelicId = "relic.rotten_cleave_axe";

        /// <summary>#115 归档的九件错位旧遗物。</summary>
        private static readonly string[] ArchivedRelicIds =
        {
            "relic.even_hatred", "relic.arsenal", "relic.thorn_skin",
            "relic.battle_hardened", "relic.tower_child", "relic.junk_slot_machine",
            "relic.hard_skin", "relic.blood_shockwave", "relic.easy_road",
        };

        /// <summary>策划现行道具卡 19 种（通用卡组 18 + 战士角色卡组 撞击教程）。</summary>
        private static readonly string[] OfficialHelpCardIds =
        {
            "help.healing_potion", "help.throwing_knife", "help.fireball",
            "help.rotation_wheel", "help.brutality_card", "help.bomb",
            "help.swap_card", "help.sturdy_shield", "help.teleport_card",
            "help.gold_card", "help.food_card", "help.kidnapping",
            "help.common_chest_card", "help.blue_chest_card", "help.golden_chest_card",
            "help.attack_card", "help.hp_card", "help.armor_card",
            "help.impact_tutorial",
        };

        /// <summary>非策划现行道具卡（历史残留）：归档保留 JSON 与效果，不参与正式接线。</summary>
        private static readonly string[] ArchivedHelpCardIds =
        {
            "help.armor_breaking_hammer", "help.blood_conversion", "help.doubling_tower",
            "help.shield_bash_tutorial", "help.stat_boost_card", "help.ward_magic_card",
            "help.watchtower",
        };

        /// <summary>战士角色卡组归属（设计案 06-玩家角色/03 角色卡组）。</summary>
        private const string WarriorClassCardId = "help.impact_tutorial";

        private static readonly string[] RelicPoolIds =
        {
            "relic.common_chest", "relic.blue_chest", "relic.golden_chest", "relic.blood_conversion",
        };

        private static readonly string[] HelpPoolIds =
        {
            "help.choice", "help.white.choice", "shop.helpCards", "kill.elite",
        };

        private IArchitecture mArch;
        private GameContentCatalog mCatalog;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            CardPresentationConfigCatalog.Invalidate();
            mCatalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, mCatalog);
        }

        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
            mArch = null;
            mCatalog = null;
        }

        [Test]
        public void ProductionJson_AllFiftyOneOfficialRelics_LiveDeckWithEffects()
        {
            var catalog = Catalog();
            Assert.AreEqual(51, OfficialWhiteRelicIds.Length + OfficialBlueRelicIds.Length
                + OfficialGoldRelicIds.Length + 1);

            AssertLiveRelics(catalog, OfficialWhiteRelicIds, ContentRarity.White);
            AssertLiveRelics(catalog, OfficialBlueRelicIds, ContentRarity.Blue);
            AssertLiveRelics(catalog, OfficialGoldRelicIds, ContentRarity.Gold);
            AssertLiveRelics(catalog, new[] { UniqueRelicId }, ContentRarity.Red);
        }

        [Test]
        public void ProductionJson_EveryPoolableOfficialRelic_HasLegalPoolChannel_AndRedIsGrantOnly()
        {
            var catalog = Catalog();
            for (var i = 0; i < OfficialWhiteRelicIds.Length; i++)
            {
                AssertRelicPoolable(catalog, OfficialWhiteRelicIds[i]);
            }

            for (var i = 0; i < OfficialBlueRelicIds.Length; i++)
            {
                AssertRelicPoolable(catalog, OfficialBlueRelicIds[i]);
            }

            for (var i = 0; i < OfficialGoldRelicIds.Length; i++)
            {
                AssertRelicPoolable(catalog, OfficialGoldRelicIds[i]);
            }

            Assert.IsTrue(catalog.Relics.TryGetValue(UniqueRelicId, out var unique));
            Assert.AreEqual(RelicDecks.Live, unique.DeckId);
            for (var p = 0; p < RelicPoolIds.Length; p++)
            {
                AssertPoolOmits(catalog, RelicPoolIds[p], UniqueRelicId,
                    "独特遗物仅由职业授予，不得进宝箱池");
            }

            Assert.AreEqual(UniqueRelicId, ProfessionCatalog.Default.InitialRelicDefId,
                "独特遗物必须由职业初始授予");
        }

        [Test]
        public void ProductionJson_NineArchivedRelics_AbsentFromEveryRelicPool()
        {
            var catalog = Catalog();
            Assert.AreEqual(9, ArchivedRelicIds.Length);
            for (var i = 0; i < ArchivedRelicIds.Length; i++)
            {
                var id = ArchivedRelicIds[i];
                Assert.IsTrue(catalog.Relics.TryGetValue(id, out var relic), "missing " + id);
                Assert.AreEqual(RelicDecks.Archive, relic.DeckId, id + " 应挂归档卡组");
                for (var p = 0; p < RelicPoolIds.Length; p++)
                {
                    AssertPoolOmits(catalog, RelicPoolIds[p], id, "归档遗物 0 可达");
                }
            }
        }

        [Test]
        public void ProductionJson_AllOfficialHelpCards_HaveLiveDeckEffectsAndFormalSource()
        {
            var catalog = Catalog();
            var sources = CollectFormalHelpSources(catalog);
            Assert.AreEqual(19, OfficialHelpCardIds.Length);

            for (var i = 0; i < OfficialHelpCardIds.Length; i++)
            {
                var id = OfficialHelpCardIds[i];
                Assert.IsTrue(catalog.Cards.TryGetValue(id, out var card), "missing " + id);
                Assert.AreEqual(CardKind.HelpCard, card.Kind, id);
                Assert.Greater(card.EffectIds.Count, 0, id + " 必须挂效果装配");
                Assert.IsTrue(HelpCardDecks.IsArchive(card.DeckId) == false, id + " 不得归档");

                var expectedDeck = id == WarriorClassCardId ? ProfessionCatalog.WarriorItemDeckId : HelpCardDecks.Live;
                Assert.AreEqual(expectedDeck, card.DeckId,
                    id + " 卡组归属应与设计（通用/战士角色卡组）一致");

                Assert.IsTrue(sources.Contains(id),
                    id + " 缺少至少一个明确正式来源（房间注入/商店/道具来源池/精英池/层主注入）");
            }
        }

        [Test]
        public void ProductionJson_LegacyHelpCards_Archived_AndAbsentFromFormalSources()
        {
            var catalog = Catalog();
            var sources = CollectFormalHelpSources(catalog);
            Assert.AreEqual(7, ArchivedHelpCardIds.Length);

            for (var i = 0; i < ArchivedHelpCardIds.Length; i++)
            {
                var id = ArchivedHelpCardIds[i];
                Assert.IsTrue(catalog.Cards.TryGetValue(id, out var card), "missing " + id);
                Assert.AreEqual(HelpCardDecks.Archive, card.DeckId,
                    id + " 应挂道具卡归档卡组");
                Assert.IsFalse(sources.Contains(id),
                    id + " 归档卡不得出现在任何正式来源（房间注入/道具来源池/精英池/层主注入）");

                for (var p = 0; p < HelpPoolIds.Length; p++)
                {
                    AssertPoolOmits(catalog, HelpPoolIds[p], id, "历史卡不得混入奖池");
                }
            }
        }

        [Test]
        public void ProductionJson_ProfessionItemSourcePool_OnlyOfficialHelpCards()
        {
            var catalog = Catalog();
            var pool = BuildProfessionItemSourcePool(catalog);
            Assert.IsTrue(pool.Contains("help.healing_potion"), "道具来源池不得为空");

            for (var i = 0; i < pool.Count; i++)
            {
                var id = pool[i];
                Assert.IsTrue(catalog.Cards.TryGetValue(id, out var card), "池中悬空 ID: " + id);
                Assert.IsTrue(HelpCardDecks.IsArchive(card.DeckId) == false,
                    "道具来源池不得含归档卡: " + id);
            }

            for (var i = 0; i < OfficialHelpCardIds.Length; i++)
            {
                Assert.IsTrue(pool.Contains(OfficialHelpCardIds[i]),
                    "官方道具卡必须在职业道具来源池中: " + OfficialHelpCardIds[i]);
            }

            for (var i = 0; i < ArchivedHelpCardIds.Length; i++)
            {
                Assert.IsFalse(pool.Contains(ArchivedHelpCardIds[i]),
                    "归档道具卡不得进职业道具来源池: " + ArchivedHelpCardIds[i]);
            }
        }

        [Test]
        public void ProductionJson_RewardPools_NoDanglingIds_NoEmptyContent()
        {
            var catalog = Catalog();
            var pools = catalog.Rewards.Pools;
            Assert.Greater(pools.Count, 0);
            foreach (var pair in pools)
            {
                var pool = pair.Value;
                Assert.IsNotNull(pool, "空池: " + pair.Key);
                Assert.Greater(pool.Entries.Count, 0, "奖池不得为空: " + pair.Key);
                for (var i = 0; i < pool.Entries.Count; i++)
                {
                    var entry = pool.Entries[i];
                    Assert.IsTrue(entry != null && !string.IsNullOrEmpty(entry.DefId),
                        pair.Key + " 悬空条目");
                    if (entry.Kind == CardKind.Relic)
                    {
                        Assert.IsTrue(catalog.Relics.ContainsKey(entry.DefId),
                            pair.Key + " 悬空遗物 ID: " + entry.DefId);
                    }
                    else
                    {
                        Assert.IsTrue(catalog.Cards.ContainsKey(entry.DefId),
                            pair.Key + " 悬空卡 ID: " + entry.DefId);
                    }
                }
            }
        }

        [Test]
        public void BossKill_ShufflesOneGoldenChestAndTwoGoldCardsIntoDrawPile()
        {
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 139UL });
            var phase = mArch.GetSystem<IPhaseSystem>();
            var pipeline = mArch.GetSystem<IActionPipelineSystem>();
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();

            Assert.IsTrue(phase.StartNode(CreateBossBattleNode()).Accepted, "开局层主战斗");

            var monsterUid = FindSoleBoardMonsterUid(board);
            Assert.Greater(monsterUid, 0);
            var boss = registry.Get(monsterUid);
            Assert.IsNotNull(boss);
            boss.Counters.Set(CoreCounterKeys.Boss, 1);
            boss.Counters.Set(CoreCounterKeys.Elite, 1);

            Assert.IsTrue(phase.ApplyCombatHit(board.AvatarUid.Value, monsterUid).Accepted);
            pipeline.RunToCompletion();

            Assert.AreEqual(
                1,
                CountDefInList(registry, deck.DrawPileUids, "help.golden_chest_card"),
                "层主击杀应固定洗入 1 张金色宝箱卡");
            Assert.AreEqual(
                2,
                CountDefInList(registry, deck.DrawPileUids, "help.gold_card"),
                "层主击杀应固定洗入 2 张金币卡");
        }

        private static int FindSoleBoardMonsterUid(BoardModel board)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid != 0)
                {
                    return uid;
                }
            }

            return 0;
        }

        private static NodeDeckOptions CreateBossBattleNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1,
                RequireElite = true,
            }.AddEnemyCard(new CardDraft("monster.boss.test", CardKind.Monster)
            {
                MaxHp = 1,
                Hp = 1,
                Attack = 0,
                GoldReward = 0,
            });
        }

        private GameContentCatalog Catalog()
        {
            return mCatalog;
        }

        private static void AssertLiveRelics(
            GameContentCatalog catalog,
            IReadOnlyList<string> ids,
            ContentRarity rarity)
        {
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                Assert.IsTrue(catalog.Relics.TryGetValue(id, out var relic), "missing " + id);
                Assert.AreEqual(RelicDecks.Live, relic.DeckId, id);
                Assert.AreEqual(rarity, relic.Rarity, id + " 稀有度应与策划一致");
                Assert.Greater(relic.EffectIds.Count, 0, id + " 必须挂效果装配");
                Assert.IsFalse(string.IsNullOrEmpty(relic.DisplayName), id + " displayName");
            }
        }

        private static void AssertRelicPoolable(GameContentCatalog catalog, string defId)
        {
            Assert.IsTrue(catalog.Relics.TryGetValue(defId, out var relic), "missing " + defId);
            var found = false;
            for (var p = 0; p < RelicPoolIds.Length; p++)
            {
                if (PoolContains(catalog, RelicPoolIds[p], defId))
                {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found,
                defId + " 必须至少出现在一个正式遗物宝箱池（普通/蓝/金/血液转换）");
        }

        /// <summary>收集全部正式帮助卡来源：房间注入、商店固定货架、职业道具来源池、精英池、层主注入。</summary>
        private static HashSet<string> CollectFormalHelpSources(GameContentCatalog catalog)
        {
            var sources = new HashSet<string>();

            foreach (var pair in catalog.Rewards.Rooms)
            {
                var room = pair.Value;
                if (room == null || room.OpeningInjects == null)
                {
                    continue;
                }

                for (var i = 0; i < room.OpeningInjects.Count; i++)
                {
                    var inject = room.OpeningInjects[i];
                    if (inject == null || inject.Side != RoomInjectSide.Player)
                    {
                        continue;
                    }

                    if (inject.SourceKind == RoomInjectSourceKind.FixedCard
                        && !string.IsNullOrEmpty(inject.CardDefId))
                    {
                        sources.Add(inject.CardDefId);
                    }

                    if (inject.Pool != null)
                    {
                        for (var j = 0; j < inject.Pool.Count; j++)
                        {
                            var option = inject.Pool[j];
                            if (option != null && !string.IsNullOrEmpty(option.CardDefId))
                            {
                                sources.Add(option.CardDefId);
                            }
                        }
                    }
                }
            }

            // 商店四货架固定项（RewardSystem.BuildShopShelves）+ 属性三卡。
            sources.Add(RewardSystem.ShopChestDefId);
            sources.Add(RewardSystem.ShopPotionDefId);
            sources.Add(RewardSystem.ShopFoodDefId);
            sources.Add("help.hp_card");
            sources.Add("help.armor_card");
            sources.Add("help.attack_card");

            // 层主击杀固定注入（RewardSystem.ReactToKill）。
            sources.Add("help.golden_chest_card");
            sources.Add("help.gold_card");

            // 职业道具来源池（每战斗节点随机装填）。
            var pool = BuildProfessionItemSourcePool(catalog);
            for (var i = 0; i < pool.Count; i++)
            {
                sources.Add(pool[i]);
            }

            // 精英击杀奖池（kill.elite）。
            AddPoolEntries(catalog, "kill.elite", sources);
            return sources;
        }

        private static List<string> BuildProfessionItemSourcePool(GameContentCatalog catalog)
        {
            var player = new PlayerModel();
            ProfessionCatalog.SeedItemGenerationRules(player, catalog, ProfessionCatalog.Jester);
            var pool = new List<string>(player.ItemSourcePoolDefIds);
            return pool;
        }

        private static void AddPoolEntries(
            GameContentCatalog catalog,
            string poolId,
            HashSet<string> ids)
        {
            if (catalog.Rewards.TryGetPool(poolId, out var pool))
            {
                for (var i = 0; i < pool.Entries.Count; i++)
                {
                    var entry = pool.Entries[i];
                    if (entry != null && !string.IsNullOrEmpty(entry.DefId))
                    {
                        ids.Add(entry.DefId);
                    }
                }
            }
        }

        private static bool PoolContains(GameContentCatalog catalog, string poolId, string defId)
        {
            if (!catalog.Rewards.TryGetPool(poolId, out var pool))
            {
                return false;
            }

            for (var i = 0; i < pool.Entries.Count; i++)
            {
                if (pool.Entries[i] != null && pool.Entries[i].DefId == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertPoolOmits(
            GameContentCatalog catalog,
            string poolId,
            string defId,
            string message)
        {
            Assert.IsFalse(PoolContains(catalog, poolId, defId), poolId + " must omit " + defId + ": " + message);
        }

        private static int CountDefInList(CardRegistry registry, IReadOnlyList<int> uids, string defId)
        {
            var count = 0;
            for (var i = 0; i < uids.Count; i++)
            {
                if (registry.TryGet(uids[i], out var card) && card != null && card.DefId == defId)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
