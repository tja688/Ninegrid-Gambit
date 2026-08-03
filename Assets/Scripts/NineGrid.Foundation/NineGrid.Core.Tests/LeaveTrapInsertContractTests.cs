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
    /// #112 / ADR-0026：开局真怪击破达 ⌈N/2⌉ 后，将离开机关洗入战斗卡组随机位（经补牌上场，无强制占格）。
    /// 缝：StartNode 编组 → 真怪击破 → DrawPile/Board 出现 trap.leave；N 不含机关；奇数上取整；只插入一次。
    /// 本票不改写 <see cref="IDeckSystem.IsNodeCleared"/>（留给 #113）。
    /// </summary>
    public sealed class LeaveTrapInsertContractTests
    {
        private const string LeaveTrapDefId = "trap.leave";
        private const string FodderDefId = "monster.skull_head";

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            CardPresentationConfigCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 112UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
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
        public void EvenN_HalfTrueMonsterKills_InsertsLeaveTrapIntoBattleDeck()
        {
            // N=2 → ⌈2/2⌉=1
            Assert.IsTrue(mPhase.StartNode(CreateBattleNode(trueMonsterCount: 2, includeTrap: false)).Accepted);
            PrepareAvatar(99, 99, 0);
            Assert.AreEqual(0, CountLeaveTrapInBattleDeck(), "开局不得预先含离开机关");

            KillOneTrueMonsterOnBoard();
            Assert.AreEqual(1, CountLeaveTrapInBattleDeck(), "击破达 ⌈N/2⌉ 后离开机关应出现在战斗卡组（堆或经补牌上场）");
            Assert.IsTrue(HasShuffleIntoLeaveTrapEvent(), "须经 ShuffleIntoDrawPile 洗入，而非本票强制瞬占格");
        }

        [Test]
        public void OddN_UsesCeilingHalf_BeforeThresholdNoInsert()
        {
            // N=3 → ⌈3/2⌉=2
            Assert.IsTrue(mPhase.StartNode(CreateBattleNode(trueMonsterCount: 3, includeTrap: false)).Accepted);
            PrepareAvatar(99, 99, 0);

            KillOneTrueMonsterOnBoard();
            Assert.AreEqual(0, CountLeaveTrapInBattleDeck(), "未达 ⌈N/2⌉ 不得插入");

            KillOneTrueMonsterOnBoard();
            Assert.AreEqual(1, CountLeaveTrapInBattleDeck(), "奇数 N 用上取整：第三只编组中击破第二只后插入");
        }

        [Test]
        public void OpeningTrap_ExcludedFromDenominator()
        {
            // 2 真怪 + 1 机关 → N=2 → 阈值 1
            Assert.IsTrue(mPhase.StartNode(CreateBattleNode(trueMonsterCount: 2, includeTrap: true)).Accepted);
            PrepareAvatar(99, 99, 0);
            Assert.AreEqual(0, CountLeaveTrapInBattleDeck());

            KillOneTrueMonsterOnBoard();
            Assert.AreEqual(1, CountLeaveTrapInBattleDeck(), "半数分母不含开局机关");
        }

        [Test]
        public void Insert_IsIdempotent_FurtherKillsDoNotDuplicate()
        {
            Assert.IsTrue(mPhase.StartNode(CreateBattleNode(trueMonsterCount: 2, includeTrap: false)).Accepted);
            PrepareAvatar(99, 99, 0);

            KillOneTrueMonsterOnBoard();
            Assert.AreEqual(1, CountLeaveTrapInBattleDeck());

            KillOneTrueMonsterOnBoard();
            Assert.AreEqual(1, CountLeaveTrapInBattleDeck(), "只洗入一次");
        }

        [Test]
        public void MidBattleSpawnedTrueMonster_Kill_DoesNotCountTowardThreshold()
        {
            // N=2 → 阈值 1；局中新生怪击破不得提前出门。
            Assert.IsTrue(mPhase.StartNode(CreateBattleNode(trueMonsterCount: 2, includeTrap: false)).Accepted);
            PrepareAvatar(99, 99, 0);

            mPipeline.Enqueue(new SpawnCardAction(FodderDefId, CardKind.Monster, ZoneId.Board, SlotId.Board(8), 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var spawnedUid = mArch.GetModel<BoardModel>().GetCardUid(SlotId.Board(8));
            var spawned = mArch.GetModel<CardRegistry>().Get(spawnedUid);
            spawned.Stats.SetBase(StatId.MaxHp, 1);
            spawned.Stats.SetBase(StatId.Hp, 1);
            spawned.Stats.SetBase(StatId.Attack, 0);

            var board = mArch.GetModel<BoardModel>();
            Assert.IsTrue(mPhase.ApplyCombatHit(board.AvatarUid.Value, spawnedUid).Accepted);
            Assert.AreEqual(0, CountLeaveTrapInBattleDeck(), "局中新生真怪击破不计开局进度");

            KillOneTrueMonsterOnBoard();
            Assert.AreEqual(1, CountLeaveTrapInBattleDeck(), "击破一只开局真怪后才应插入");
        }

        [Test]
        public void ZeroOpeningTrueMonsters_NeverInserts()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
            Assert.AreEqual(0, CountLeaveTrapInBattleDeck(), "无开局真怪时不得插入离开机关");
        }

        private static NodeDeckOptions CreateBattleNode(int trueMonsterCount, bool includeTrap)
        {
            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = trueMonsterCount + (includeTrap ? 1 : 0)
            };
            for (var i = 0; i < trueMonsterCount; i++)
            {
                options.AddEnemyCard(new CardDraft(FodderDefId, CardKind.Monster)
                {
                    MaxHp = 1,
                    Hp = 1,
                    Attack = 0,
                    GoldReward = 0
                });
            }

            if (includeTrap)
            {
                options.AddEnemyCard(new CardDraft("trap.revive_stone", CardKind.Trap)
                {
                    MaxHp = 20,
                    Hp = 20,
                    Attack = 0
                });
            }

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

        private void KillOneTrueMonsterOnBoard()
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

                targetUid = uid;
                break;
            }

            Assert.Greater(targetUid, 0, "场上应有可击破真怪");
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

        private bool HasShuffleIntoLeaveTrapEvent()
        {
            var entries = mPipeline.EventLog.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var evt = entries[i];
                if (evt.Type == CoreEventType.CardDealt
                    && evt.ActionName == "ShuffleIntoDrawPile"
                    && evt.SourceDefId == LeaveTrapDefId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
