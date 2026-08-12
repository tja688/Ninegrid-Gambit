using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 神圣决斗（skill.holy_duel）标记结算回归。
    /// 锁死的 bug 形态：新目标自己也持有神圣决斗时，旧实现先转移标记并提前 return，
    /// 旧持有者的惩罚（EffectTriggered + 对玩家 2 伤）从未入队——
    /// 决斗套（同场怪物全员持有）里该技能整场静默，表现层追加攻击也无从触发。
    /// 正确语义（effect_templates design_text）：与持有者交战后再主动与「其他怪物卡」
    /// 战斗即惩罚，不排除其他怪同为持有者；惩罚先结算，标记后转移。
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
        public void AttackHolderThenOtherHolder_PunishesFromFirstHolder_AndTransfersMark()
        {
            var avatar = CreateAvatarOnBoard(10);
            var holderA = CreateMonsterOnBoard("monster.test.duelist_a", 4, withHolyDuel: true);
            var holderB = CreateMonsterOnBoard("monster.test.duelist_b", 6, withHolyDuel: true);
            var player = mArch.GetModel<PlayerModel>();

            Hit(avatar, holderA);
            Assert.AreEqual(holderA.Uid, player.DuelMarkMonsterUid, "打持有者 A 后应记下决斗标记");

            var startIndex = EventCount();
            Hit(avatar, holderB);

            AssertPunished(startIndex, holderA.Uid, avatar.Uid);
            Assert.AreEqual(8, (int)avatar.Stats.GetBase(StatId.Hp), "惩罚应对玩家造成 2 伤");
            Assert.AreEqual(
                holderB.Uid,
                player.DuelMarkMonsterUid,
                "惩罚结算后标记应转移到新持有者 B");
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
            Assert.AreEqual(
                holder.Uid,
                player.DuelMarkMonsterUid,
                "目标非持有者时标记应保留在原持有者上");
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
        public void MarkedHolderTurnedFaceDown_ClearsMarkWithoutPunishment()
        {
            var avatar = CreateAvatarOnBoard(10);
            var holder = CreateMonsterOnBoard("monster.test.duelist", 4, withHolyDuel: true);
            var plain = CreateMonsterOnBoard("monster.test.plain", 6, withHolyDuel: false);
            var player = mArch.GetModel<PlayerModel>();

            Hit(avatar, holder);
            // 惰性校验路径：直接注入背面状态（FlipCardAction 主动清标记是另一条路径）。
            holder.FaceUp = false;

            var startIndex = EventCount();
            Hit(avatar, plain);

            Assert.AreEqual(0, CountPunishmentDamage(startIndex, avatar.Uid), "持有者已翻面不得惩罚");
            Assert.AreEqual(10, (int)avatar.Stats.GetBase(StatId.Hp));
            Assert.AreEqual(0, player.DuelMarkMonsterUid, "持有者失效应清空标记");
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

        private CardInstance CreateMonsterOnBoard(string defId, int slotIndex, bool withHolyDuel)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var monster = registry.Create(defId, CardKind.Monster);
            monster.Stats.SetBase(StatId.MaxHp, 5);
            monster.Stats.SetBase(StatId.Hp, 5);
            mArch.GetModel<BoardModel>().PlaceCard(monster, SlotId.Board(slotIndex));

            if (withHolyDuel)
            {
                // 与 tpl.skill.holy_duel.activate 等价的 Persistent 规则（测试不走内容装配）。
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

        /// <summary>断言批内出现完整惩罚事件对：持有者 EffectTriggered 脉冲 + 对玩家 2 伤。</summary>
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
                    && e.SourceDefId == PunishmentSource)
                {
                    damage += e.Amount;
                }
            }

            Assert.IsTrue(pulseSeen, "批内应有持有者的 skill.holy_duel.activate 触发事件（表现层脉冲/追加攻击依赖它）");
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
    }
}
