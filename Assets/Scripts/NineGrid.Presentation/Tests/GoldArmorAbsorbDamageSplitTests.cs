using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 金币盔甲吸收口径回归（对照 Notes 卡面护甲只增不减复盘 2026-08-16）：
    /// DamageDealt 的 ArmorDamage 是毛甲伤（含金甲代偿），卡面当前甲只按净额下降。
    /// 守护三条不变量：全代偿不动甲且不产 ArmorChanged；代偿量显式进事件
    /// （GoldAbsorbedArmor / GoldModified delta）；无金币 / 无遗物行为与旧版一致。
    /// </summary>
    public class GoldArmorAbsorbDamageSplitTests
    {
        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            mArch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        // ==================== 1. 全代偿 ====================

        [Test]
        public void FullAbsorb_CurrentArmorUnchanged_NoArmorChanged_GoldAndSplitReported()
        {
            var avatar = CreateAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 4);
            mArch.GetModel<PlayerModel>().AddCoins(30);
            MountGoldArmorRule();

            var monster = CreateMonster();
            Run(new DealDamageAction(monster.Uid, avatar.Uid, 7));

            Assert.AreEqual(4, StatArmorUtility.GetCurrentArmor(avatar), "全代偿：当前甲必须原样保留（卡面不动是对的）");
            Assert.AreEqual(17, avatar.Stats.GetBase(StatId.Hp), "7 点伤害中 3 点溢出照扣血");
            Assert.AreEqual(10, mArch.GetModel<PlayerModel>().Coins.Value, "4 点甲伤 × 5 金 = 20 金");

            Assert.IsFalse(HasEvent(CoreEventType.ArmorChanged), "全代偿不得发 ArmorChanged（无净扣甲指令）");
            var gold = FindEvent(CoreEventType.GoldModified);
            Assert.IsNotNull(gold, "必须发 GoldModified 记录金币扣款");
            Assert.AreEqual(-20, gold.Delta, "金币扣款 = 代偿甲伤 × 5");

            var dealt = FindEvent(CoreEventType.DamageDealt);
            Assert.IsNotNull(dealt, "DamageDealt 必须携带拆账字段");
            Assert.AreEqual(4, dealt.ArmorDamage, "毛甲伤 = 4（含代偿）");
            Assert.AreEqual(4, dealt.GoldAbsorbedArmor, "代偿量 = 4");
            Assert.AreEqual(3, dealt.HpDamage, "血伤 = 溢出 3");
            Assert.AreEqual(4, dealt.RemainingArmor, "结算后当前甲 = 4（净口径）");
        }

        // ==================== 2. 部分代偿 ====================

        [Test]
        public void PartialAbsorb_NetArmorLoss_SplitReported()
        {
            var avatar = CreateAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 4);
            mArch.GetModel<PlayerModel>().AddCoins(10);
            MountGoldArmorRule();

            var monster = CreateMonster();
            Run(new DealDamageAction(monster.Uid, avatar.Uid, 7));

            // 10 金只能代偿 2 点甲伤，净扣甲 2。
            Assert.AreEqual(2, StatArmorUtility.GetCurrentArmor(avatar), "部分代偿：当前甲按净额下降");
            Assert.AreEqual(17, avatar.Stats.GetBase(StatId.Hp), "血伤 = 7 - 4 = 3");
            Assert.AreEqual(0, mArch.GetModel<PlayerModel>().Coins.Value, "2 点代偿 × 5 金 = 10 金");

            var armorChanged = FindEvent(CoreEventType.ArmorChanged);
            Assert.IsNotNull(armorChanged, "有净扣甲必须发 ArmorChanged");
            Assert.AreEqual(-2, armorChanged.Delta);
            Assert.AreEqual(2, armorChanged.RemainingArmor);

            var dealt = FindEvent(CoreEventType.DamageDealt);
            Assert.AreEqual(4, dealt.ArmorDamage, "毛甲伤 = 4");
            Assert.AreEqual(2, dealt.GoldAbsorbedArmor, "代偿量 = 2");
            Assert.AreEqual(3, dealt.HpDamage);
        }

        // ==================== 3. 无金币（有遗物） ====================

        [Test]
        public void NoGold_NoAbsorb_BehaviorUnchanged()
        {
            var avatar = CreateAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 4);
            MountGoldArmorRule();
            Assert.AreEqual(0, mArch.GetModel<PlayerModel>().Coins.Value, "前提：身无分文");

            var monster = CreateMonster();
            Run(new DealDamageAction(monster.Uid, avatar.Uid, 7));

            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(avatar), "无金币不代偿，甲照扣");
            Assert.AreEqual(17, avatar.Stats.GetBase(StatId.Hp));
            Assert.IsFalse(HasEvent(CoreEventType.GoldModified), "无金币不得发金币事件");

            var dealt = FindEvent(CoreEventType.DamageDealt);
            Assert.AreEqual(4, dealt.ArmorDamage);
            Assert.AreEqual(0, dealt.GoldAbsorbedArmor, "无金币代偿量为 0");
        }

        // ==================== 4. 无遗物（有金币） ====================

        [Test]
        public void NoRelic_GoldAbsorbedZero_GoldUntouched()
        {
            var avatar = CreateAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 4);
            mArch.GetModel<PlayerModel>().AddCoins(30);

            var monster = CreateMonster();
            Run(new DealDamageAction(monster.Uid, avatar.Uid, 7));

            Assert.AreEqual(0, StatArmorUtility.GetCurrentArmor(avatar), "无金甲遗物：甲照扣");
            Assert.AreEqual(30, mArch.GetModel<PlayerModel>().Coins.Value, "无遗物不得花钱");
            Assert.IsFalse(HasEvent(CoreEventType.GoldModified));

            var dealt = FindEvent(CoreEventType.DamageDealt);
            Assert.AreEqual(4, dealt.ArmorDamage);
            Assert.AreEqual(0, dealt.GoldAbsorbedArmor, "无遗物代偿量为 0");
        }

        // ==================== 5. IgnoreArmor 路径 ====================

        [Test]
        public void IgnoreArmor_GoldAbsorbedZero()
        {
            var avatar = CreateAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 4);
            mArch.GetModel<PlayerModel>().AddCoins(30);
            MountGoldArmorRule();

            var monster = CreateMonster();
            Run(new DealDamageAction(monster.Uid, avatar.Uid, 7, ignoreArmor: true));

            Assert.AreEqual(4, StatArmorUtility.GetCurrentArmor(avatar), "无视护甲：甲不动");
            Assert.AreEqual(13, avatar.Stats.GetBase(StatId.Hp), "全额打血");
            Assert.AreEqual(30, mArch.GetModel<PlayerModel>().Coins.Value, "IgnoreArmor 不得触发金币代偿");

            var dealt = FindEvent(CoreEventType.DamageDealt);
            Assert.AreEqual(0, dealt.ArmorDamage);
            Assert.AreEqual(0, dealt.GoldAbsorbedArmor);
            Assert.AreEqual(7, dealt.HpDamage);
        }

        // ==================== 6. 账本重放 ====================

        [Test]
        public void LedgerReplay_AvatarArmorMatchesOracle()
        {
            var avatar = CreateAvatar();
            StatArmorUtility.SetCurrentArmor(avatar, 4);
            mArch.GetModel<PlayerModel>().AddCoins(30);
            MountGoldArmorRule();

            var monster = CreateMonster();
            Run(new DealDamageAction(monster.Uid, avatar.Uid, 7));

            // 管线已自动跑 CardFaceReconciliation：账本甲 = 结算同源 oracle = 当前甲。
            Assert.IsTrue(
                mArch.GetModel<CardFaceLedgerModel>().TryGet(avatar.Uid, out var entry),
                "账本应有 Avatar 条目");
            Assert.AreEqual(
                StatArmorUtility.GetCurrentArmor(avatar),
                entry.Armor,
                "账本重放后甲必须等于 oracle 当前甲（4，全代偿不动）");
            Assert.AreEqual(17, entry.Hp, "账本血 = 溢出扣血后基础血");
        }

        // ==================== 基建 ====================

        private CardInstance CreateAvatar()
        {
            var avatar = mArch.GetModel<CardRegistry>().Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, 20);
            avatar.Stats.SetBase(StatId.Hp, 20);
            avatar.Stats.SetBase(StatId.Attack, 2);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, SlotId.Board(5));
            return avatar;
        }

        private CardInstance CreateMonster()
        {
            var monster = mArch.GetModel<CardRegistry>().Create("monster.test", CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, 10);
            monster.Stats.SetBase(StatId.Hp, 10);
            monster.Stats.SetBase(StatId.Attack, 1);
            mArch.GetModel<BoardModel>().PlaceCard(monster, SlotId.Board(1));
            return monster;
        }

        /// <summary>等效遗物装配：GoldArmorAbsorb Override 1@Persistent（同 tpl.relic.gold_armor.rule）。</summary>
        private void MountGoldArmorRule()
        {
            mArch.GetSystem<IStatSystem>().RuleModifiers.Add(new RuleModifier(
                RuleId.GoldArmorAbsorb,
                ModifierOp.Override,
                1f,
                ModifierLayer.Persistent,
                new ModifierSource("relic.gold_armor"),
                ModifierScope.Permanent));
        }

        private void Run(GameAction action)
        {
            mArch.GetSystem<IActionPipelineSystem>().Execute(action);
        }

        private bool HasEvent(CoreEventType type)
        {
            return FindEvent(type) != null;
        }

        private CoreGameEvent FindEvent(CoreEventType type)
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                if (entries[i].Type == type)
                {
                    return entries[i];
                }
            }

            return null;
        }
    }
}
