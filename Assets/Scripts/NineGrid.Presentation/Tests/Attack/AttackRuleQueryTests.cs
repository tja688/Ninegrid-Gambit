using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Presentation.Queries;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Attack
{
    /// <summary>
    /// V2：攻击规则裁决经 Query，不再经 CombatHitSink 反查。
    /// </summary>
    public sealed class AttackRuleQueryTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sOtherMonsterSlot = SlotId.Board(4);

        [Test]
        public void EstimateWillKillQuery_MatchesCoreStatMath()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);

                var attackerUid = arch.Board.AvatarUid.Value;
                var targetUid = arch.Board.GetCardUid(sAdjacentSlot);
                Assert.Greater(targetUid, 0);

                Assert.IsTrue(arch.Architecture.SendQuery(
                    new EstimateWillKillQuery(attackerUid, targetUid)));

                var target = arch.Registry.Get(targetUid);
                target.Stats.SetBase(StatId.Hp, 99);
                Assert.IsFalse(arch.Architecture.SendQuery(
                    new EstimateWillKillQuery(attackerUid, targetUid)));
            }
        }

        [Test]
        public void ResolvePlayerAttackTargetQuery_DelegatesToPhase()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateTwoMonsterNode()).Accepted);
                PlaceBoardMonsterAt(arch, sAdjacentSlot);
                PlaceBoardMonsterAt(arch, sOtherMonsterSlot);

                var intended = arch.Board.GetCardUid(sOtherMonsterSlot);
                var expected = arch.Architecture.GetSystem<IPhaseSystem>()
                    .ResolvePlayerAttackTargetUid(intended);
                var actual = arch.Architecture.SendQuery(
                    new ResolvePlayerAttackTargetQuery(intended));
                Assert.AreEqual(expected, actual);
            }
        }

        [Test]
        public void MonsterStrikesFirstQuery_DelegatesToPhase()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 10, attack: 1)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);

                var avatarUid = arch.Board.AvatarUid.Value;
                var monsterUid = arch.Board.GetCardUid(sAdjacentSlot);
                var expected = arch.Architecture.GetSystem<IPhaseSystem>()
                    .MonsterStrikesFirst(avatarUid, monsterUid);
                var actual = arch.Architecture.SendQuery(
                    new MonsterStrikesFirstQuery(avatarUid, monsterUid));
                Assert.AreEqual(expected, actual);
            }
        }

        private static void PlaceBoardMonsterAt(PresentationArchitectureFixture arch, SlotId slot)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var boardSlot = SlotId.Board(i);
                if (boardSlot == arch.Board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = arch.Board.GetCardUid(boardSlot);
                if (uid == 0)
                {
                    continue;
                }

                var card = arch.Registry.Get(uid);
                if (card.Slot.Value == slot)
                {
                    return;
                }

                if (arch.Board.IsEmpty(slot))
                {
                    arch.Board.ClearSlot(card.Slot.Value);
                    arch.Board.PlaceCard(card, slot);
                    return;
                }
            }

            Assert.Fail("No relocatable board monster for slot " + slot.Index);
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }

        private static NodeDeckOptions CreateTwoMonsterNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 2
            }
                .AddEnemyCard(new CardDraft("monster.a", CardKind.Monster) { MaxHp = 5, Attack = 1 })
                .AddEnemyCard(new CardDraft("monster.b", CardKind.Monster) { MaxHp = 5, Attack = 1 });
        }
    }
}
