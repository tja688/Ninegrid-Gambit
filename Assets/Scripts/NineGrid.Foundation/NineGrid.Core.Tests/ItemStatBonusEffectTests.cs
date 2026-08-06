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
    /// 卡店「道具卡数值强化」（RewardSystem.TavernUpgradeDefId → PlayerModel.ItemStatBonus）生效契约：
    /// 道具卡（HelpCard）自有固定数值每购一次 +3；派生数值（火球=玩家攻击等）与非道具卡来源不叠加。
    /// </summary>
    public sealed class ItemStatBonusEffectTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IStatSystem mStats;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL, AvatarArmor = 0 });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mStats = mArch.GetSystem<IStatSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ThrowingKnife_WithoutBonus_DealsBase6()
        {
            var monsterUid = StartBattleWithSingleMonster(20);
            var knifeUid = SpawnIntoItemSlots("help.throwing_knife");

            var use = mPhase.ApplyUseItem(knifeUid, new List<int> { monsterUid }, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            Assert.AreEqual(14, CurrentHp(monsterUid), "飞刀基础伤害 6");
        }

        [Test]
        public void ThrowingKnife_WithStatBonus3_Deals9()
        {
            var monsterUid = StartBattleWithSingleMonster(20);
            mArch.GetModel<PlayerModel>().AddItemStatBonus(3);
            var knifeUid = SpawnIntoItemSlots("help.throwing_knife");

            var use = mPhase.ApplyUseItem(knifeUid, new List<int> { monsterUid }, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            Assert.AreEqual(11, CurrentHp(monsterUid), "数值强化后飞刀伤害 6+3=9");
        }

        [Test]
        public void ArmorBreakingHammer_WithStatBonus3_Amount13()
        {
            var node = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster)
            {
                MaxHp = 20,
                Attack = 0,
                Armor = 15
            });
            Assert.IsTrue(mPhase.StartNode(node).Accepted);
            var monsterUid = FindSoleMonsterUid();
            mArch.GetModel<PlayerModel>().AddItemStatBonus(3);
            var hammerUid = SpawnIntoItemSlots("help.armor_breaking_hammer");

            var use = mPhase.ApplyUseItem(hammerUid, new List<int> { monsterUid }, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            var monster = mArch.GetModel<CardRegistry>().Get(monsterUid);
            Assert.AreEqual(15, StatArmorUtility.GetBaseArmor(monster));
            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(monster), "破击锤 10+3=13，15 甲余 2");
        }

        [Test]
        public void AvatarCombatDamage_NotBoosted()
        {
            var monsterUid = StartBattleWithSingleMonster(20);
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 5);
            mArch.GetModel<PlayerModel>().AddItemStatBonus(3);

            mPipeline.Enqueue(new ForceBattleAction(monsterUid));
            mPipeline.RunToCompletion();

            Assert.AreEqual(15, CurrentHp(monsterUid), "玩家交战伤害=玩家攻击5，强化不叠加入玩家攻击");
        }

        private int StartBattleWithSingleMonster(int maxHp)
        {
            var node = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster)
            {
                MaxHp = maxHp,
                Attack = 0
            });
            Assert.IsTrue(mPhase.StartNode(node).Accepted);
            return FindSoleMonsterUid();
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

        private int CurrentHp(int uid)
        {
            var card = mArch.GetModel<CardRegistry>().Get(uid);
            return mStats.GetEffectiveInt(card, StatId.Hp);
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
