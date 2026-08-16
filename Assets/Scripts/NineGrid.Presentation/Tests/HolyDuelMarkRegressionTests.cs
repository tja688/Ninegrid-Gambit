using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 神圣决斗（skill.holy_duel）标记结算回归。
    /// 多标记叠加：与每只持有者交战后各自记标；打其他怪时所有存活正面持有者各罚 2；
    /// 仅该持有者死亡/离场摘标；翻面保留标记。
    /// </summary>
    public class HolyDuelMarkRegressionTests
    {
        private const string PunishmentSource = "skill.holy_duel";
        private const string ActivateMessage = "skill.holy_duel.activate";

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

        [Test]
        public void AttackHolderThenOtherHolder_PunishesFromFirstHolder_AndStacksMarks()
        {
            var avatar = CreateAvatarOnBoard(10);
            var holderA = CreateMonsterOnBoard("monster.test.duelist_a", 4, withHolyDuel: true);
            var holderB = CreateMonsterOnBoard("monster.test.duelist_b", 6, withHolyDuel: true);
            var player = mArch.GetModel<PlayerModel>();

            Hit(avatar, holderA);
            AssertMarkUids(player, "打持有者 A 后应记下决斗标记", holderA.Uid);

            var startIndex = EventCount();
            Hit(avatar, holderB);

            AssertPunished(startIndex, holderA.Uid, avatar.Uid);
            Assert.AreEqual(8, (int)avatar.Stats.GetBase(StatId.Hp), "惩罚应对玩家造成 2 伤");
            AssertMarkUids(player, "惩罚后应叠加 B 的标记，A 仍保留", holderA.Uid, holderB.Uid);
        }

        [Test]
        public void AttackHolderThenNonHolder_Punishes_AndKeepsMark()
        {
            var avatar = CreateAvatarOnBoard(10);
            var holder = CreateMonsterOnBoard("monster.test.duelist", 4, withHolyDuel: true);
            var plain = CreateMonsterOnBoard("monster.test.plain", 6, withHolyDuel: false);
            var player = mArch.GetModel<PlayerModel>();

            Hit(avatar, holder);
            var startIndex = EventCount();
            Hit(avatar, plain);

            AssertPunished(startIndex, holder.Uid, avatar.Uid);
            Assert.AreEqual(8, (int)avatar.Stats.GetBase(StatId.Hp));
            AssertMarkUids(player, "目标非持有者时标记应保留在原持有者上", holder.Uid);
        }

        [Test]
        public void AttackMarkedHolderAgain_NoPunishment()
        {
            var avatar = CreateAvatarOnBoard(10);
            var holder = CreateMonsterOnBoard("monster.test.duelist", 4, withHolyDuel: true);

            Hit(avatar, holder);
            var startIndex = EventCount();
            Hit(avatar, holder);

            Assert.AreEqual(0, CountPunishmentDamage(startIndex, avatar.Uid), "连打同一持有者不得惩罚");
            Assert.AreEqual(10, (int)avatar.Stats.GetBase(StatId.Hp));
        }

        [Test]
        public void ThreeMarkedHolders_AttackingFourth_PunishesAllThreeInOrder()
        {
            var avatar = CreateAvatarOnBoard(20);
            var holderA = CreateMonsterOnBoard("monster.test.duelist_a", 1, withHolyDuel: true);
            var holderB = CreateMonsterOnBoard("monster.test.duelist_b", 2, withHolyDuel: true);
            var holderC = CreateMonsterOnBoard("monster.test.duelist_c", 3, withHolyDuel: true);
            var plain = CreateMonsterOnBoard("monster.test.plain", 7, withHolyDuel: false);

            Hit(avatar, holderA);
            Hit(avatar, holderB);
            Hit(avatar, holderC);

            var startIndex = EventCount();
            Hit(avatar, plain);

            var punishedHolders = GetPunishedHolderOrder(startIndex, avatar.Uid);
            CollectionAssert.AreEqual(
                new[] { holderA.Uid, holderB.Uid, holderC.Uid },
                punishedHolders,
                "应按挂标顺序逐个惩罚");
            Assert.AreEqual(6, CountPunishmentDamage(startIndex, avatar.Uid), "三只各 2 伤共 6");
        }

        [Test]
        public void AttackMarkedHolderWithOtherMarked_OnlyOtherHolderPunishes()
        {
            var avatar = CreateAvatarOnBoard(10);
            var holderA = CreateMonsterOnBoard("monster.test.duelist_a", 4, withHolyDuel: true);
            var holderB = CreateMonsterOnBoard("monster.test.duelist_b", 6, withHolyDuel: true);
            var player = mArch.GetModel<PlayerModel>();

            Hit(avatar, holderA);
            Hit(avatar, holderB);

            var startIndex = EventCount();
            Hit(avatar, holderA);

            AssertPunished(startIndex, holderB.Uid, avatar.Uid);
            Assert.AreEqual(2, CountPunishmentDamage(startIndex, avatar.Uid), "只应 B 罚，A 为当前目标");
            AssertMarkUids(player, "两只标记均应保留", holderA.Uid, holderB.Uid);
        }

        [Test]
        public void MarkedHolderTurnedFaceDown_KeepsMarkWithoutPunishment_UntilFaceUpAgain()
        {
            var avatar = CreateAvatarOnBoard(10);
            var holder = CreateMonsterOnBoard("monster.test.duelist", 4, withHolyDuel: true);
            var plain = CreateMonsterOnBoard("monster.test.plain", 6, withHolyDuel: false);
            var player = mArch.GetModel<PlayerModel>();

            Hit(avatar, holder);
            holder.FaceUp = false;

            var startIndex = EventCount();
            Hit(avatar, plain);

            Assert.AreEqual(0, CountPunishmentDamage(startIndex, avatar.Uid), "背面持有者不得惩罚");
            Assert.AreEqual(10, (int)avatar.Stats.GetBase(StatId.Hp));
            AssertMarkUids(player, "翻面应保留标记", holder.Uid);

            holder.FaceUp = true;
            startIndex = EventCount();
            Hit(avatar, plain);

            AssertPunished(startIndex, holder.Uid, avatar.Uid);
            Assert.AreEqual(8, (int)avatar.Stats.GetBase(StatId.Hp), "翻回正面后打别人应再罚");
        }

        [Test]
        public void KillingMarkedHolder_RemovesOnlyThatMark_OthersStillPunish()
        {
            var avatar = CreateAvatarOnBoard(20);
            var holderA = CreateMonsterOnBoard("monster.test.duelist_a", 4, withHolyDuel: true, hp: 5);
            var holderB = CreateMonsterOnBoard("monster.test.duelist_b", 6, withHolyDuel: true);
            var plain = CreateMonsterOnBoard("monster.test.plain", 7, withHolyDuel: false);
            var player = mArch.GetModel<PlayerModel>();

            Hit(avatar, holderA);
            Hit(avatar, holderB);

            avatar.Stats.SetBase(StatId.Attack, 99);
            Hit(avatar, holderA);

            var startIndex = EventCount();
            Hit(avatar, plain);

            AssertMarkUids(player, "打别人时惰性摘掉已死 A，只留 B", holderB.Uid);

            AssertPunished(startIndex, holderB.Uid, avatar.Uid);
            Assert.AreEqual(2, CountPunishmentDamage(startIndex, avatar.Uid), "只剩 B 应罚一次");
        }

        private CardInstance CreateAvatarOnBoard(int hp)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var avatar = registry.Create("avatar.default", CardKind.Avatar);
            avatar.Stats.SetBase(StatId.MaxHp, hp);
            avatar.Stats.SetBase(StatId.Hp, hp);
            mArch.GetModel<BoardModel>().SetAvatar(avatar, SlotId.Board(5));
            return avatar;
        }

        private CardInstance CreateMonsterOnBoard(
            string defId,
            int slotIndex,
            bool withHolyDuel,
            int hp = 5)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, hp);
            monster.Stats.SetBase(StatId.Hp, hp);
            mArch.GetModel<BoardModel>().PlaceCard(monster, SlotId.Board(slotIndex));

            if (withHolyDuel)
            {
                mArch.GetSystem<IStatSystem>().RuleModifiers.Add(new RuleModifier(
                    RuleId.HolyDuel,
                    ModifierOp.Add,
                    1f,
                    ModifierLayer.Persistent,
                    new ModifierSource("test." + PunishmentSource),
                    ModifierScope.Permanent,
                    new TargetUidCondition(monster.Uid)));
            }

            return monster;
        }

        private void Hit(CardInstance avatar, CardInstance monster)
        {
            var result = mArch.GetSystem<IPhaseSystem>().ApplyCombatHit(avatar.Uid, monster.Uid);
            Assert.IsTrue(result.Accepted, "ApplyCombatHit 应被接受");
        }

        private int EventCount()
        {
            return mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries.Count;
        }

        private static void AssertMarkUids(PlayerModel player, string message, params int[] expectedUids)
        {
            CollectionAssert.AreEqual(expectedUids, new List<int>(player.DuelMarkMonsterUids), message);
        }

        private void AssertPunished(int startIndex, int holderUid, int avatarUid)
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var pulseSeen = false;
            var damage = 0;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.EffectTriggered
                    && e.CardUid == holderUid
                    && e.Message == ActivateMessage)
                {
                    pulseSeen = true;
                }
                else if (e.Type == CoreEventType.DamageDealt
                    && e.TargetUid == avatarUid
                    && e.SourceDefId == PunishmentSource
                    && (e.ActorUid == holderUid || e.ActorUid == 0))
                {
                    damage += e.Amount;
                }
            }

            Assert.IsTrue(pulseSeen, "批内应有持有者的 skill.holy_duel.activate 触发事件");
            Assert.AreEqual(2, damage, "批内应有 source=skill.holy_duel 的对玩家 2 伤");
        }

        private int CountPunishmentDamage(int startIndex, int avatarUid)
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var damage = 0;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.DamageDealt
                    && e.TargetUid == avatarUid
                    && e.SourceDefId == PunishmentSource)
                {
                    damage += e.Amount;
                }
            }

            return damage;
        }

        private List<int> GetPunishedHolderOrder(int startIndex, int avatarUid)
        {
            var entries = mArch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            var holders = new List<int>();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.EffectTriggered
                    && e.Message == ActivateMessage
                    && e.CardUid > 0)
                {
                    holders.Add(e.CardUid);
                }
            }

            return holders;
        }
    }
}
