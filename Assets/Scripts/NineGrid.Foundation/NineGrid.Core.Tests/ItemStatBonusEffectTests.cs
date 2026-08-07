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
            // AvatarMaxHp=30：#159 治疗量 10/13 可区分（默认 10 会封顶）。
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL, AvatarArmor = 0, AvatarMaxHp = 30 });
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
        public void Bomb_WithoutBonus_DealsBase4()
        {
            // #158：爆弹伤害 4 是装配实参（help.bomb.use.amount），kernel 消费为基础伤害。
            var monsterUid = StartBattleWithSingleMonster(20);
            var bombUid = SpawnIntoItemSlots("help.bomb");

            var use = mPhase.ApplyUseItem(bombUid, new List<int> { monsterUid }, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            Assert.AreEqual(16, CurrentHp(monsterUid), "爆弹基础伤害 4");
        }

        [Test]
        public void Bomb_WithStatBonus3_Deals7()
        {
            // #158：爆弹伤害 4 是牌店可升级数值——数值强化后 4+3=7。
            var monsterUid = StartBattleWithSingleMonster(20);
            mArch.GetModel<PlayerModel>().AddItemStatBonus(3);
            var bombUid = SpawnIntoItemSlots("help.bomb");

            var use = mPhase.ApplyUseItem(bombUid, new List<int> { monsterUid }, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            Assert.AreEqual(13, CurrentHp(monsterUid), "数值强化后爆弹伤害 4+3=7");
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
        public void SturdyShield_WithoutBonus_GainArmor5()
        {
            // #159：耐用盾牌 5 是装配实参（help.sturdy_shield.use.amount），kernel 消费为基础护甲。
            StartBattleWithSingleMonster(20);
            var shieldUid = SpawnIntoItemSlots("help.sturdy_shield");

            var use = mPhase.ApplyUseItem(shieldUid, null, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            Assert.AreEqual(5, StatArmorUtility.GetCurrentArmor(avatar), "耐用盾牌基础护甲 5");
        }

        [Test]
        public void SturdyShield_WithStatBonus3_GainArmor8()
        {
            // #159：耐用盾牌 5 是牌店可升级数值——数值强化后 5+3=8。
            StartBattleWithSingleMonster(20);
            mArch.GetModel<PlayerModel>().AddItemStatBonus(3);
            var shieldUid = SpawnIntoItemSlots("help.sturdy_shield");

            var use = mPhase.ApplyUseItem(shieldUid, null, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            Assert.AreEqual(8, StatArmorUtility.GetCurrentArmor(avatar), "数值强化后耐用盾牌护甲 5+3=8");
        }

        [Test]
        public void HealingPotion_WithoutBonus_Heals10()
        {
            // #159：恢复药水 10 是装配实参（help.healing_potion.use.amount），kernel 消费为基础治疗。
            StartBattleWithSingleMonster(20);
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 15, "test"));
            mPipeline.RunToCompletion();
            Assert.AreEqual(15, CurrentHp(avatar.Uid), "avatar 30-15=15");

            var potionUid = SpawnIntoItemSlots("help.healing_potion");
            var use = mPhase.ApplyUseItem(potionUid, null, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            Assert.AreEqual(25, CurrentHp(avatar.Uid), "恢复药水基础治疗 10：15+10=25");
        }

        [Test]
        public void HealingPotion_WithStatBonus3_Heals13()
        {
            // #159：恢复药水 10 是牌店可升级数值——数值强化后 10+3=13。
            StartBattleWithSingleMonster(20);
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            mPipeline.Enqueue(new DealDamageAction(0, avatar.Uid, 15, "test"));
            mPipeline.RunToCompletion();
            mArch.GetModel<PlayerModel>().AddItemStatBonus(3);

            var potionUid = SpawnIntoItemSlots("help.healing_potion");
            var use = mPhase.ApplyUseItem(potionUid, null, null);
            Assert.IsTrue(use.Accepted, use.Reason);

            Assert.AreEqual(28, CurrentHp(avatar.Uid), "数值强化后恢复药水治疗 10+3=13：15+13=28");
        }

        [Test]
        public void AvatarCombatDamage_NotBoosted()
        {
            // 修复 #159 碰到的陈旧断言：开局遗物顺劈斧 [attack]+1 参与有效攻击，
            // 玩家交战伤害=有效攻击（基础5+遗物1=6），强化 3 绝不叠加入玩家攻击。
            var monsterUid = StartBattleWithSingleMonster(20);
            var avatar = mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().AvatarUid.Value);
            avatar.Stats.SetBase(StatId.Attack, 5);
            mArch.GetModel<PlayerModel>().AddItemStatBonus(3);

            mPipeline.Enqueue(new ForceBattleAction(monsterUid));
            mPipeline.RunToCompletion();

            var effective = mStats.GetEffectiveInt(avatar, StatId.Attack);
            Assert.AreEqual(6, effective, "有效攻击 = 基础5 + 顺劈斧1；强化 3 不叠加");
            Assert.AreEqual(20 - effective, CurrentHp(monsterUid), "玩家交战伤害=玩家有效攻击，强化不叠加入玩家攻击");
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
