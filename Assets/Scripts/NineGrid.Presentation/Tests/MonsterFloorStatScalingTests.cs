using NineGrid.Core;
using NineGrid.Core.Content;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class MonsterFloorStatScalingTests
    {
        [Test]
        public void Easy_Difficulty_Floor1To3_Scaling()
        {
            // Floor 1
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(0, MonsterFloorStatScaling.TotalTiers(1, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(0, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(0, MonsterFloorStatScaling.HpBonus(1, nodeIndex, RunDifficultyIds.Normal));
            }
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(1, MonsterFloorStatScaling.TotalTiers(1, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(1, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(2, MonsterFloorStatScaling.HpBonus(1, nodeIndex, RunDifficultyIds.Normal));
            }

            // Floor 2 (过层不加档，前半保持 1 档，中段后加到 2 档)
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(1, MonsterFloorStatScaling.TotalTiers(2, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(1, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(2, MonsterFloorStatScaling.HpBonus(2, nodeIndex, RunDifficultyIds.Normal));
            }
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(2, MonsterFloorStatScaling.TotalTiers(2, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(2, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(4, MonsterFloorStatScaling.HpBonus(2, nodeIndex, RunDifficultyIds.Normal));
            }

            // Floor 3 (过层不加档，前半保持 2 档，中段后加到 3 档)
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(2, MonsterFloorStatScaling.TotalTiers(3, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(2, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(4, MonsterFloorStatScaling.HpBonus(3, nodeIndex, RunDifficultyIds.Normal));
            }
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(3, MonsterFloorStatScaling.TotalTiers(3, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(3, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, RunDifficultyIds.Normal));
                Assert.AreEqual(6, MonsterFloorStatScaling.HpBonus(3, nodeIndex, RunDifficultyIds.Normal));
            }
        }

        [Test]
        public void Advanced_Difficulty_Floor1To3_Scaling()
        {
            // Floor 1: 前半 0 档，后半 1 档
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(0, MonsterFloorStatScaling.TotalTiers(1, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(0, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(0, MonsterFloorStatScaling.HpBonus(1, nodeIndex, RunDifficultyIds.Advanced));
            }
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(1, MonsterFloorStatScaling.TotalTiers(1, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(1, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(2, MonsterFloorStatScaling.HpBonus(1, nodeIndex, RunDifficultyIds.Advanced));
            }

            // Floor 2: 前半 2 档，后半 3 档
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(2, MonsterFloorStatScaling.TotalTiers(2, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(2, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(4, MonsterFloorStatScaling.HpBonus(2, nodeIndex, RunDifficultyIds.Advanced));
            }
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(3, MonsterFloorStatScaling.TotalTiers(2, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(3, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(6, MonsterFloorStatScaling.HpBonus(2, nodeIndex, RunDifficultyIds.Advanced));
            }

            // Floor 3: 前半 4 档，后半 5 档
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(4, MonsterFloorStatScaling.TotalTiers(3, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(4, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(8, MonsterFloorStatScaling.HpBonus(3, nodeIndex, RunDifficultyIds.Advanced));
            }
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(5, MonsterFloorStatScaling.TotalTiers(3, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(5, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, RunDifficultyIds.Advanced));
                Assert.AreEqual(10, MonsterFloorStatScaling.HpBonus(3, nodeIndex, RunDifficultyIds.Advanced));
            }
        }

        [Test]
        public void Hard_Difficulty_Floor1To3_Scaling()
        {
            // Floor 1: 前半 0 档 (0/0)，后半 1 档翻倍 (2/4)
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(0, MonsterFloorStatScaling.TotalTiers(1, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(0, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(0, MonsterFloorStatScaling.HpBonus(1, nodeIndex, RunDifficultyIds.Hard));
            }
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(1, MonsterFloorStatScaling.TotalTiers(1, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(2, MonsterFloorStatScaling.AttackBonus(1, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(4, MonsterFloorStatScaling.HpBonus(1, nodeIndex, RunDifficultyIds.Hard));
            }

            // Floor 2: 前半 2 档翻倍 (4/8)，后半 3 档翻倍 (6/12)
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(2, MonsterFloorStatScaling.TotalTiers(2, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(4, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(8, MonsterFloorStatScaling.HpBonus(2, nodeIndex, RunDifficultyIds.Hard));
            }
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(3, MonsterFloorStatScaling.TotalTiers(2, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(6, MonsterFloorStatScaling.AttackBonus(2, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(12, MonsterFloorStatScaling.HpBonus(2, nodeIndex, RunDifficultyIds.Hard));
            }

            // Floor 3: 前半 4 档翻倍 (8/16)，后半 5 档翻倍 (10/20)
            for (var nodeIndex = 0; nodeIndex < 3; nodeIndex++)
            {
                Assert.AreEqual(4, MonsterFloorStatScaling.TotalTiers(3, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(8, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(16, MonsterFloorStatScaling.HpBonus(3, nodeIndex, RunDifficultyIds.Hard));
            }
            for (var nodeIndex = 3; nodeIndex < 8; nodeIndex++)
            {
                Assert.AreEqual(5, MonsterFloorStatScaling.TotalTiers(3, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(10, MonsterFloorStatScaling.AttackBonus(3, nodeIndex, RunDifficultyIds.Hard));
                Assert.AreEqual(20, MonsterFloorStatScaling.HpBonus(3, nodeIndex, RunDifficultyIds.Hard));
            }
        }

        [Test]
        public void ApplyToDraft_Monster_IncreasesStats()
        {
            var draftEasy = new CardDraft("test_monster", CardKind.Monster)
            {
                Attack = 5,
                MaxHp = 10,
                Hp = 10
            };
            // Easy: Floor 2, Node 2 (nodeIndex 1) -> 1 tier: Atk +1, Hp +2
            MonsterFloorStatScaling.ApplyToDraft(draftEasy, 2, 1, RunDifficultyIds.Normal);
            Assert.AreEqual(6, draftEasy.Attack);
            Assert.AreEqual(12, draftEasy.MaxHp);
            Assert.AreEqual(12, draftEasy.Hp);

            var draftAdvanced = new CardDraft("test_monster", CardKind.Monster)
            {
                Attack = 5,
                MaxHp = 10,
                Hp = 10
            };
            // Advanced: Floor 2, Node 2 (nodeIndex 1) -> 2 tiers: Atk +2, Hp +4
            MonsterFloorStatScaling.ApplyToDraft(draftAdvanced, 2, 1, RunDifficultyIds.Advanced);
            Assert.AreEqual(7, draftAdvanced.Attack);
            Assert.AreEqual(14, draftAdvanced.MaxHp);
            Assert.AreEqual(14, draftAdvanced.Hp);

            var draftHard = new CardDraft("test_monster", CardKind.Monster)
            {
                Attack = 5,
                MaxHp = 10,
                Hp = 10
            };
            // Hard: Floor 2, Node 2 (nodeIndex 1) -> 2 tiers doubled: Atk +4, Hp +8
            MonsterFloorStatScaling.ApplyToDraft(draftHard, 2, 1, RunDifficultyIds.Hard);
            Assert.AreEqual(9, draftHard.Attack);
            Assert.AreEqual(18, draftHard.MaxHp);
            Assert.AreEqual(18, draftHard.Hp);
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

            MonsterFloorStatScaling.ApplyToDraft(draft, 2, 5, RunDifficultyIds.Hard);
            Assert.AreEqual(5, draft.Attack);
            Assert.AreEqual(10, draft.MaxHp);
            Assert.AreEqual(10, draft.Hp);
        }
    }
}
