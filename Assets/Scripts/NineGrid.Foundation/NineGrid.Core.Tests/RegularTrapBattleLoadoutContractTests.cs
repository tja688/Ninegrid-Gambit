using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #135：正式战斗开局随机注入三张常规机关（Core 契约 <see cref="RegularTrapPool"/>）。
    /// 数量=3、池过滤（排除离开机关与技能/遗物专用特殊机关）、无放回、同种子可复现、不足三张按池量；
    /// 常规机关不计真怪：击杀赏金为零、离开机关 ⌈N/2⌉ 分母不含机关、层主房仍按击破开局层主洗入。
    /// </summary>
    public sealed class RegularTrapBattleLoadoutContractTests
    {
        private const string LeaveTrapDefId = RegularTrapPool.LeaveTrapDefId;

        private static readonly string[] sExpectedRegularTraps =
        {
            "trap.rolling_stone",
            "trap.attack_totem",
            "trap.armor_totem",
            "trap.recovery_totem",
            "trap.spike",
            "trap.bear_trap"
        };

        private static readonly string[] sSpecialTraps =
        {
            "trap.healing_spring",
            "trap.flame",
            "trap.revive_stone"
        };

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IRewardSystem mReward;
        private IContentSystem mContent;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 135UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mReward = mArch.GetSystem<IRewardSystem>();
            mContent = mArch.GetSystem<IContentSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ProductionPool_IsExactlySixRegularTraps_ExcludingLeaveAndSpecial()
        {
            var pool = RegularTrapPool.CollectRegularTraps(mContent.Catalog);
            Assert.AreEqual(6, pool.Count, "常规机关池应为策划六张常规（稀有度 White）");
            CollectionAssert.AreEquivalent(sExpectedRegularTraps, CollectDefIds(pool));

            for (var i = 0; i < pool.Count; i++)
            {
                Assert.AreEqual(CardKind.Trap, pool[i].Kind, pool[i].DefId + " kind");
                Assert.AreEqual(ContentRarity.White, pool[i].Rarity, pool[i].DefId + " 稀有度应标记常规");
                Assert.IsFalse(pool[i].IsReserve, pool[i].DefId + " 常规机关不得归档");
            }

            Assert.IsFalse(ContainsDefId(pool, LeaveTrapDefId), "离开机关不得进入常规机关池");
            for (var i = 0; i < sSpecialTraps.Length; i++)
            {
                Assert.IsFalse(
                    ContainsDefId(pool, sSpecialTraps[i]),
                    sSpecialTraps[i] + " 属技能/遗物专用特殊机关，不得进入常规机关池");
            }
        }

        [Test]
        public void FormalBattleLoadout_ContainsExactlyThreeDistinctRegularTraps_NoLeaveTrap()
        {
            var options = mReward.BuildNodeDeckOptions(1, null);
            Assert.Greater(options.EnemyCards.Count, 3);

            var pool = CollectDefIds(RegularTrapPool.CollectRegularTraps(mContent.Catalog));
            var traps = new List<string>();
            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                var draft = options.EnemyCards[i];
                if (draft.Kind != CardKind.Trap)
                {
                    continue;
                }

                traps.Add(draft.DefId);
            }

            Assert.AreEqual(3, traps.Count, "正式战斗初始装填应含随机三张常规机关");
            for (var i = 0; i < traps.Count; i++)
            {
                Assert.IsTrue(pool.Contains(traps[i]), traps[i] + " 必须来自常规机关池");
                Assert.AreNotEqual(LeaveTrapDefId, traps[i], "离开机关不得出现在初始随机三张内");
            }

            Assert.AreEqual(3, new HashSet<string>(traps).Count, "同场三张不得重复（无放回）");
        }

        [Test]
        public void FormalLoadout_SameSeed_IsReproducible_IncludingTrapOrder()
        {
            var first = CollectTrapDefIds(mReward.BuildNodeDeckOptions(1, null));
            Assert.AreEqual(3, first.Count);

            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            var arch2 = NineGridArchitecture.Current;
            arch2.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(arch2, new InitialGameOptions { Seed = 135UL });
            var second = CollectTrapDefIds(arch2.GetSystem<IRewardSystem>().BuildNodeDeckOptions(1, null));

            CollectionAssert.AreEqual(first, second, "同种子下随机三张与顺序应可复现");
        }

        [Test]
        public void PoolWithTwoRegularTraps_InsertsTwoDistinct_NoDuplicatesNoSpecials()
        {
            using (var fixture = new TrapLoadoutFixture(regularCount: 2))
            {
                fixture.SetUpArchitecture();
                var options = fixture.Reward.BuildNodeDeckOptions(1, null);
                var traps = CollectTrapDefIds(options);
                Assert.AreEqual(2, traps.Count, "池不足三张按池量注入");
                CollectionAssert.AreEquivalent(
                    new[] { "trap.rolling_stone", "trap.attack_totem" },
                    traps,
                    "只应抽到常规机关且无重复（无放回）");
            }
        }

        [Test]
        public void PoolWithOneRegularTrap_InsertsOne_NoReplacementTripling()
        {
            using (var fixture = new TrapLoadoutFixture(regularCount: 1))
            {
                fixture.SetUpArchitecture();
                var options = fixture.Reward.BuildNodeDeckOptions(1, null);
                CollectionAssert.AreEqual(
                    new[] { "trap.rolling_stone" },
                    CollectTrapDefIds(options),
                    "单张常规机关时不得靠放回抽成三张");
            }
        }

        [Test]
        public void EmptyRegularTrapPool_InsertsNoTraps()
        {
            using (var fixture = new TrapLoadoutFixture(regularCount: 0))
            {
                fixture.SetUpArchitecture();
                var options = fixture.Reward.BuildNodeDeckOptions(1, null);
                Assert.AreEqual(0, CollectTrapDefIds(options).Count, "池空不得注入");
            }
        }

        [Test]
        public void FormalLoadout_TrapsExcludedFromLeaveInsertDenominator()
        {
            // 通过完整正式装填（节点 1：三张常规机关 + 节点规则真怪）验证 N 不含机关。
            var options = mReward.BuildNodeDeckOptions(1, null);
            var trueMonsterCount = CountTrueMonsterDrafts(options);
            Assert.Greater(trueMonsterCount, 0);
            Assert.IsTrue(mPhase.StartNode(options).Accepted);

            var battle = mArch.GetModel<BattleContextModel>();
            Assert.AreEqual(
                trueMonsterCount,
                battle.OpeningTrueMonsterCount,
                "离开机关插入分母 N 只含开局真怪，不含常规机关");
        }

        [Test]
        public void RegularTrapKill_DoesNotCountTowardLeaveThreshold_AndThresholdStillWorks()
        {
            // 2 真怪 + 1 常规机关 → N=2 → ⌈N/2⌉=1；杀机关不计进度。
            Assert.IsTrue(mPhase.StartNode(CreateMixedBattleNode()).Accepted);
            PrepareAvatar(99, 99, 0);
            var battle = mArch.GetModel<BattleContextModel>();
            Assert.AreEqual(2, battle.OpeningTrueMonsterCount);

            KillBoardTrap();
            Assert.AreEqual(0, battle.DefeatedTrueMonsterCount, "击杀常规机关不得计入离开机关进度");
            Assert.AreEqual(0, CountLeaveTrapInBattleDeck(), "仅杀机关不得触发离开机关洗入");

            KillOneTrueMonsterOnBoard();
            Assert.AreEqual(1, CountLeaveTrapInBattleDeck(), "击破 ⌈N/2⌉ 后离开机关仍按 ADR-0026 洗入");
        }

        [Test]
        public void BossRoomLoadout_ContainsThreeRegularTraps_AndBossKillInsertsLeave()
        {
            var options = mReward.BuildNodeDeckOptions(8, null);
            Assert.IsTrue(options.RequireElite, "节点 8 应要求层主");
            var traps = CollectTrapDefIds(options);
            Assert.AreEqual(3, traps.Count, "层主房也应装填三张常规机关");
            Assert.IsFalse(traps.Contains(LeaveTrapDefId));

            var hasFloorBoss = false;
            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                if (mContent.Catalog.TryGetCard(options.EnemyCards[i].DefId, out var card)
                    && card.IsBoss
                    && card.Sequence == 5)
                {
                    hasFloorBoss = true;
                }
            }

            Assert.IsTrue(hasFloorBoss, "节点 8 应含 seq5 层主");

            // 层主房插入语义（与常规机关共存）：过半阈值无效，须击破开局层主才洗入。
            mArch.GetModel<RunModel>().Room.Value = RoomKind.Boss;
            Assert.IsTrue(mPhase.StartNode(CreateBossBattleNodeWithTrap()).Accepted);
            PrepareAvatar(99, 99, 0);

            KillOneTrueMonsterOnBoard(preferBoss: false);
            Assert.AreEqual(0, CountLeaveTrapInBattleDeck(), "层主房过半阈值不得触发");

            KillOneTrueMonsterOnBoard(preferBoss: true);
            Assert.AreEqual(1, CountLeaveTrapInBattleDeck(), "层主房仍按击破开局层主洗入离开机关");
        }

        [Test]
        public void RegularTrapKill_GrantsNoGold()
        {
            Assert.IsTrue(mPhase.StartNode(CreateMixedBattleNode()).Accepted);
            PrepareAvatar(99, 99, 0);

            var player = mArch.GetModel<PlayerModel>();
            var coinsBefore = player.Coins.Value;
            KillBoardTrap();
            Assert.AreEqual(coinsBefore, player.Coins.Value, "击杀常规机关无赏金");
        }

        private static int CountTrueMonsterDrafts(NodeDeckOptions options)
        {
            var count = 0;
            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                if (options.EnemyCards[i].Kind == CardKind.Monster)
                {
                    count++;
                }
            }

            return count;
        }

        private static List<string> CollectTrapDefIds(NodeDeckOptions options)
        {
            var result = new List<string>();
            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                if (options.EnemyCards[i].Kind == CardKind.Trap)
                {
                    result.Add(options.EnemyCards[i].DefId);
                }
            }

            return result;
        }

        private static List<string> CollectDefIds(IReadOnlyList<CardContentDefinition> cards)
        {
            var result = new List<string>();
            for (var i = 0; i < cards.Count; i++)
            {
                result.Add(cards[i].DefId);
            }

            return result;
        }

        private static bool ContainsDefId(IReadOnlyList<CardContentDefinition> cards, string defId)
        {
            for (var i = 0; i < cards.Count; i++)
            {
                if (cards[i].DefId == defId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>2 真怪 + 1 常规机关（trap.rolling_stone）的确定性战斗节点。</summary>
        private static NodeDeckOptions CreateMixedBattleNode()
        {
            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 3
            };
            for (var i = 0; i < 2; i++)
            {
                options.AddEnemyCard(new CardDraft("monster.skull_head", CardKind.Monster)
                {
                    MaxHp = 1,
                    Hp = 1,
                    Attack = 0,
                    GoldReward = 0
                });
            }

            options.AddEnemyCard(new CardDraft("trap.rolling_stone", CardKind.Trap)
            {
                MaxHp = 6,
                Hp = 6,
                Attack = 0
            });
            return options;
        }

        /// <summary>层主房确定性节点：2 杂兵（其一为层主）+ 1 常规机关；发牌必全部上板。</summary>
        /// 层主血量为 6：初始遗物顺劈斧会波及邻格真怪 1 伤，若层主只有 1 血会被误清，无法验证「过半不插入」。
        private static NodeDeckOptions CreateBossBattleNodeWithTrap()
        {
            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 3,
                RequireElite = true
            };
            options.AddEnemyCard(new CardDraft("monster.skull_head", CardKind.Monster)
            {
                MaxHp = 1,
                Hp = 1,
                Attack = 0,
                GoldReward = 0
            });
            options.AddEnemyCard(new CardDraft("monster.skull_head", CardKind.Monster)
            {
                MaxHp = 6,
                Hp = 6,
                Attack = 0,
                GoldReward = 0,
                IsBoss = true
            });
            options.AddEnemyCard(new CardDraft("trap.rolling_stone", CardKind.Trap)
            {
                MaxHp = 6,
                Hp = 6,
                Attack = 0
            });
            return options;
        }

        private void PrepareAvatar(int hp, int attack, int armor)
        {
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            avatar.Stats.SetBase(StatId.Attack, attack);
            avatar.Stats.SetBase(StatId.Armor, armor);
            avatar.Stats.SetBase(StatId.CurrentArmor, armor);
        }

        private void KillBoardTrap()
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var targetUid = 0;
            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (!registry.TryGet(uid, out card) || card.Kind != CardKind.Trap)
                {
                    continue;
                }

                targetUid = uid;
                break;
            }

            Assert.Greater(targetUid, 0, "场上应有可击杀常规机关");
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, targetUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
        }

        private void KillOneTrueMonsterOnBoard(bool preferBoss = false)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var targetUid = 0;
            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (!registry.TryGet(uid, out card) || !CardCombatRules.IsTrueMonster(card.Kind))
                {
                    continue;
                }

                var isBoss = card.Counters.Get(CoreCounterKeys.Boss) > 0;
                if (preferBoss != isBoss)
                {
                    continue;
                }

                targetUid = uid;
                break;
            }

            Assert.Greater(targetUid, 0, preferBoss ? "场上应有可击破层主" : "场上应有可击破非层主真怪");
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, targetUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
        }

        private int CountLeaveTrapInBattleDeck()
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var board = mArch.GetModel<BoardModel>();
            var count = 0;
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                if (registry.Get(deck.DrawPileUids[i]).DefId == LeaveTrapDefId)
                {
                    count++;
                }
            }

            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (registry.TryGet(uid, out card) && card.DefId == LeaveTrapDefId)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>小型装填夹具：常规机关数可控（0–2）+ 特殊机关 + 离开机关 + 单层主题卡组。</summary>
        private sealed class TrapLoadoutFixture : System.IDisposable
        {
            private readonly int mRegularCount;

            public TrapLoadoutFixture(int regularCount)
            {
                mRegularCount = regularCount;
            }

            public IArchitecture Arch { get; private set; }
            public IRewardSystem Reward { get; private set; }

            public void SetUpArchitecture()
            {
                NineGridArchitecture.ResetForTests();
                Arch = NineGridArchitecture.Current;
                Arch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildCatalog());
                InitialGameFactory.Create(Arch, new InitialGameOptions { Seed = 135UL });
                Reward = Arch.GetSystem<IRewardSystem>();
            }

            public void Dispose()
            {
                NineGridArchitecture.ResetForTests();
            }

            private GameContentCatalog BuildCatalog()
            {
                var catalog = new GameContentCatalog();
                var deck = new MonsterDeckDefinition("deck.trap_test", "TrapTest", MonsterDeckKind.Unknown)
                    .AddMonster("monster.trap_test_1")
                    .AddMonster("monster.trap_test_2");
                catalog.AddMonsterDeck(deck);
                for (var seq = 1; seq <= 2; seq++)
                {
                    catalog.AddCard(new CardContentDefinition("monster.trap_test_" + seq, "怪" + seq, CardKind.Monster)
                        .WithStats(4, 1, 0)
                        .WithSequence(seq)
                        .WithAttackPattern(AttackPattern.OrthogonalMelee)
                        .InDeck("deck.trap_test"));
                }

                if (mRegularCount >= 1)
                {
                    AddTrap(catalog, "trap.rolling_stone", ContentRarity.White);
                }

                if (mRegularCount > 1)
                {
                    AddTrap(catalog, "trap.attack_totem", ContentRarity.White);
                }

                AddTrap(catalog, "trap.flame", ContentRarity.Red);
                AddTrap(catalog, "trap.leave", ContentRarity.None);

                catalog.Rewards.AddNodeRule(new NodeDeckRule { NodeIndex = 1, Seq1Count = 1, Seq2Count = 1 });
                return catalog;
            }

            private static void AddTrap(GameContentCatalog catalog, string defId, ContentRarity rarity)
            {
                catalog.AddCard(new CardContentDefinition(defId, defId, CardKind.Trap)
                    .WithStats(6, 0, 0)
                    .WithRarity(rarity)
                    .InDeck("deck.trap"));
            }
        }
    }
}
