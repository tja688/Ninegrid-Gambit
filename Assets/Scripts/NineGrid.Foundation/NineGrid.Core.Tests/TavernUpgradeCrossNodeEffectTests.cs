using System.Collections.Generic;
using NineGrid.Content;
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
    /// 用户流程回归：牌店买两次「道具强化」→ 离开 → 下一场战斗用飞刀应打 6+6=12（#159 跨节点生效）。
    /// Seam：EnterRoom(Tavern) → SelectReward ×2 → SkipHelpChoice → StartNode → ApplyUseItem。
    /// </summary>
    public sealed class TavernUpgradeCrossNodeEffectTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IStatSystem mStats;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL, AvatarArmor = 0, AvatarMaxHp = 30 });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void TavernUpgradeTwice_ThenNextBattle_KnifeDeals12()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(500);
            EnterTavern();

            Assert.IsTrue(mPhase.SelectReward(0).Accepted, "第一次购买道具强化");
            Assert.IsTrue(mPhase.SelectReward(0).Accepted, "第二次购买道具强化");
            Assert.AreEqual(6, player.ItemStatBonus, "买两次累计 +6");

            var leave = mPhase.SkipHelpChoice();
            Assert.IsTrue(leave.Accepted, leave.Reason);

            var node = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster)
            {
                MaxHp = 40,
                Attack = 0
            });
            var start = mPhase.StartNode(node);
            Assert.IsTrue(start.Accepted, start.Reason);
            var monsterUid = FindSoleMonsterUid();

            var knifeUid = SpawnIntoItemSlots("help.throwing_knife");
            var use = mPhase.ApplyUseItem(knifeUid, new List<int> { monsterUid }, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            var hp = mStats.GetEffectiveInt(mArch.GetModel<CardRegistry>().Get(monsterUid), StatId.Hp);
            Assert.AreEqual(40 - 12, hp, "强化两次后飞刀 6+6=12");
        }

        [Test]
        public void TavernUpgradeTwice_StaysInSamePlayerModel_AfterLeave()
        {
            var player = mArch.GetModel<PlayerModel>();
            player.AddCoins(500);
            EnterTavern();

            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.IsTrue(mPhase.SelectReward(0).Accepted);
            Assert.AreEqual(6, player.ItemStatBonus);

            var leave = mPhase.SkipHelpChoice();
            Assert.IsTrue(leave.Accepted, leave.Reason);

            Assert.AreEqual(6, mArch.GetModel<PlayerModel>().ItemStatBonus, "离开牌店后加成仍保留");
        }

        private void EnterTavern()
        {
            var start = mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            });
            Assert.IsTrue(start.Accepted, start.Reason);
            mPipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            mPipeline.RunToCompletion();
            mArch.GetModel<PendingChoiceModel>().SelectRoom(RoomKind.Tavern);

            var enter = mPhase.EnterRoom();
            Assert.IsTrue(enter.Accepted, enter.Reason);
            Assert.AreEqual(GamePhase.RewardItemChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceModel.TavernPoolId, mArch.GetModel<PendingChoiceModel>().PoolId.Value);
        }

        private int FindSoleMonsterUid()
        {
            var board = mArch.GetModel<BoardModel>();
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

            Assert.Fail("未找到场上怪物卡");
            return 0;
        }

        private int SpawnIntoItemSlots(string defId)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
            var deck = mArch.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
        }
    }
}
