using NineGrid.Core;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class MonsterFloorStatScalingTests
    {
        [Test]
        public void Floor1_Nodes1To3_ZeroBonus()
        {
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(0, MonsterFloorStatScaling.TotalTiers(1, nodeIndex));
                Assert.AreEqual(0, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, false));
                Assert.AreEqual(0, MonsterFloorStatScaling.HpBonus(1, nodeIndex, false));
                Assert.AreEqual(0, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, true));
                Assert.AreEqual(0, MonsterFloorStatScaling.HpBonus(1, nodeIndex, true));
            }
        }

        [Test]
        public void Floor1_Nodes4To8_IncludingBoss_OneTier()
        {
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(1, MonsterFloorStatScaling.TotalTiers(1, nodeIndex));
                Assert.AreEqual(1, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, false));
                Assert.AreEqual(2, MonsterFloorStatScaling.HpBonus(1, nodeIndex, false));
                // Hard difficulty doubles
                Assert.AreEqual(2, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, true));
                Assert.AreEqual(4, MonsterFloorStatScaling.HpBonus(1, nodeIndex, true));
            }
        }

        [Test]
        public void Floor2_Nodes1To3_TwoTiers_AfterFloor1BossDefeated()
        {
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(2, MonsterFloorStatScaling.TotalTiers(2, nodeIndex));
                Assert.AreEqual(2, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, false));
                Assert.AreEqual(4, MonsterFloorStatScaling.HpBonus(2, nodeIndex, false));
                Assert.AreEqual(4, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, true));
                Assert.AreEqual(8, MonsterFloorStatScaling.HpBonus(2, nodeIndex, true));
            }
        }

        [Test]
        public void Floor2_Nodes4To8_ThreeTiers()
        {
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(3, MonsterFloorStatScaling.TotalTiers(2, nodeIndex));
                Assert.AreEqual(3, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, false));
                Assert.AreEqual(6, MonsterFloorStatScaling.HpBonus(2, nodeIndex, false));
                Assert.AreEqual(6, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, true));
                Assert.AreEqual(12, MonsterFloorStatScaling.HpBonus(2, nodeIndex, true));
            }
        }

        [Test]
        public void Floor3_Nodes1To3_FourTiers_AfterFloor2BossDefeated()
        {
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(4, MonsterFloorStatScaling.TotalTiers(3, nodeIndex));
                Assert.AreEqual(4, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, false));
                Assert.AreEqual(8, MonsterFloorStatScaling.HpBonus(3, nodeIndex, false));
                Assert.AreEqual(8, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, true));
                Assert.AreEqual(16, MonsterFloorStatScaling.HpBonus(3, nodeIndex, true));
            }
        }

        [Test]
        public void Floor3_Nodes4To8_FiveTiers()
        {
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(5, MonsterFloorStatScaling.TotalTiers(3, nodeIndex));
                Assert.AreEqual(5, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, false));
                Assert.AreEqual(10, MonsterFloorStatScaling.HpBonus(3, nodeIndex, false));
                Assert.AreEqual(10, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, true));
                Assert.AreEqual(20, MonsterFloorStatScaling.HpBonus(3, nodeIndex, true));
            }
        }

        [Test]
        public void ApplyToDraft_Monster_IncreasesStats()
        {
            var draft = new CardDraft("test_monster", CardKind.Monster)
            {
                Attack = 5,
                MaxHp = 10,
                Hp = 10
            };

            // Floor 1, Node 4 (nodeIndex 3) -> 1 tier: Atk +1, Hp +2
            MonsterFloorStatScaling.ApplyToDraft(draft, 1, 3, false);
            Assert.AreEqual(6, draft.Attack);
            Assert.AreEqual(12, draft.MaxHp);
            Assert.AreEqual(12, draft.Hp);
        }

        [Test]
        public void ApplyToDraft_NonMonster_Ignored()
        {
            var draft = new CardDraft("test_item", CardKind.Item)
            {
                Attack = 5,
                MaxHp = 10,
                Hp = 10
            };

            MonsterFloorStatScaling.ApplyToDraft(draft, 2, 5, true);
            Assert.AreEqual(5, draft.Attack);
            Assert.AreEqual(10, draft.MaxHp);
            Assert.AreEqual(10, draft.Hp);
        }
    }
}
