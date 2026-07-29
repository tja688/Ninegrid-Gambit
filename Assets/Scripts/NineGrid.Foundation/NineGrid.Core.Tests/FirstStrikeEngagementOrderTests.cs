using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class FirstStrikeEngagementOrderTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;
        private IStatSystem mStats;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 42UL });
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
        public void MonsterStrikesFirst_OnlyWhenMonsterHasFirstStrikeAndAvatarDoesNot()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 10, attack: 1)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sAdjacentSlot);

            Assert.IsFalse(mPhase.MonsterStrikesFirst(avatarUid, monsterUid));

            GrantFirstStrike(monsterUid);
            Assert.IsTrue(mPhase.MonsterStrikesFirst(avatarUid, monsterUid));

            GrantFirstStrike(avatarUid);
            Assert.IsFalse(
                mPhase.MonsterStrikesFirst(avatarUid, monsterUid),
                "双方都有先攻时玩家先");
        }

        [Test]
        public void Attack_MonsterFirstStrike_DealsMonsterDamageBeforePlayer()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 2)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sAdjacentSlot);
            registry.Get(avatarUid).Stats.SetBase(StatId.Attack, 1);
            GrantFirstStrike(monsterUid);

            var startIndex = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.Attack(sAdjacentSlot).Accepted);

            var damageActors = CollectDamageActors(startIndex);
            Assert.GreaterOrEqual(damageActors.Count, 2);
            Assert.AreEqual(monsterUid, damageActors[0], "先攻怪物应先造成伤害");
            Assert.AreEqual(avatarUid, damageActors[1], "玩家后反击");
        }

        [Test]
        public void Attack_MonsterFirstStrikeLethal_SkipsPlayerReply()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 99)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(sAdjacentSlot);
            var avatar = registry.Get(avatarUid);
            avatar.Stats.SetBase(StatId.MaxHp, 3);
            avatar.Stats.SetBase(StatId.Hp, 3);
            avatar.Stats.SetBase(StatId.Armor, 0);
            GrantFirstStrike(monsterUid);

            var startIndex = mPipeline.EventLog.Entries.Count;
            Assert.IsTrue(mPhase.Attack(sAdjacentSlot).Accepted);

            var damageActors = CollectDamageActors(startIndex);
            Assert.AreEqual(1, damageActors.Count, "先攻击杀后玩家不得反击");
            Assert.AreEqual(monsterUid, damageActors[0]);
            Assert.AreEqual(GamePhase.Defeat, mPhase.CurrentPhase);
        }

        [Test]
        public void AtSlot6_ConditionalFirstStrike_OnlyWhenOnSlot6()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 10, attack: 1)).Accepted);
            var slot6 = SlotId.Board(6);
            PlaceSoleBoardCardAt(slot6);

            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            var monsterUid = board.GetCardUid(slot6);
            GrantConditionalFirstStrikeAtSlot(monsterUid, slot6);

            Assert.IsTrue(mPhase.MonsterStrikesFirst(avatarUid, monsterUid));

            PlaceSoleBoardCardAt(sAdjacentSlot);
            monsterUid = board.GetCardUid(sAdjacentSlot);
            Assert.IsFalse(
                mPhase.MonsterStrikesFirst(avatarUid, monsterUid),
                "离开格6后先攻应失效");
        }

        private void GrantFirstStrike(int cardUid)
        {
            mStats.RuleModifiers.Add(new RuleModifier(
                RuleId.FirstStrike,
                ModifierOp.Override,
                1f,
                ModifierLayer.Persistent,
                new ModifierSource("test:first_strike:" + cardUid),
                ModifierScope.Permanent,
                new TargetUidCondition(cardUid)));
        }

        private void GrantConditionalFirstStrikeAtSlot(int cardUid, SlotId slot)
        {
            mStats.RuleModifiers.Add(new RuleModifier(
                RuleId.FirstStrike,
                ModifierOp.Override,
                1f,
                ModifierLayer.Conditional,
                new ModifierSource("test:first_strike_slot:" + cardUid),
                ModifierScope.Permanent,
                new AtSlotCondition(slot)));
        }

        private List<int> CollectDamageActors(int startIndex)
        {
            var actors = new List<int>();
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == CoreEventType.DamageDealt && entries[i].Amount > 0)
                {
                    actors.Add(entries[i].ActorUid);
                }
            }

            return actors;
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            CardInstance sole = null;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                Assert.IsNull(sole, "Expected at most one non-avatar board card for relocate helper.");
                sole = registry.Get(uid);
            }

            Assert.IsNotNull(sole, "No board card to relocate.");
            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            board.ClearSlot(sole.Slot.Value);
            board.PlaceCard(sole, targetSlot);
        }
    }
}
