using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Pickup
{
    /// <summary>
    /// V3：ApplyPickupItemCommand 写 Core 并返回表现摘要。
    /// </summary>
    public sealed class ApplyPickupItemCommandTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

        [Test]
        public void Command_LegalPickup_AcquiresToItemSlots()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                var uid = SpawnHelpOnBoard(arch, "help.throwing_knife", sAdjacentSlot);

                var summary = arch.Architecture.SendCommand(
                    new ApplyPickupItemCommand(sAdjacentSlot.Index));

                Assert.IsTrue(summary.Accepted);
                Assert.AreEqual(uid, summary.CardUid);
                Assert.IsTrue(summary.AcquiredToHand);
                Assert.IsFalse(summary.RemovedWithoutHand);
                Assert.AreEqual(ZoneId.ItemSlots, arch.Registry.Get(uid).Zone.Value);
                Assert.IsTrue(arch.Board.IsEmpty(sAdjacentSlot));
            }
        }

        [Test]
        public void Command_NonAdjacentPickup_Rejects()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                SpawnHelpOnBoard(arch, "help.throwing_knife", sFarCornerSlot);

                var summary = arch.Architecture.SendCommand(
                    new ApplyPickupItemCommand(sFarCornerSlot.Index));

                Assert.IsFalse(summary.Accepted);
                Assert.IsFalse(summary.AcquiredToHand);
            }
        }

        [Test]
        public void Command_MonsterSlot_Rejects()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 5, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);

                var summary = arch.Architecture.SendCommand(
                    new ApplyPickupItemCommand(sAdjacentSlot.Index));

                Assert.IsFalse(summary.Accepted);
            }
        }

        private static int SpawnHelpOnBoard(
            PresentationArchitectureFixture arch,
            string defId,
            SlotId slot)
        {
            arch.Pipeline.Enqueue(
                new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
            var uid = arch.Board.GetCardUid(slot);
            Assert.Greater(uid, 0);
            return uid;
        }

        private static NodeDeckOptions CreateEmptyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }
    }
}
