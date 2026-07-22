using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Pickup
{
    /// <summary>
    /// V3：PickupInputController → Command；并锁定 CombatHitSink 拾取入口已退役。
    /// </summary>
    public sealed class PickupInputControllerTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void Controller_HandlePickupRequested_AppliesPickupCommand()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                var uid = SpawnHelpOnBoard(arch, "help.throwing_knife", sAdjacentSlot);

                var controllerGo = new GameObject("PickupInputControllerProbe");
                var controller = controllerGo.AddComponent<PickupInputController>();

                try
                {
                    var summary = controller.HandlePickupRequested(sAdjacentSlot.Index);
                    Assert.IsTrue(summary.Accepted);
                    Assert.AreEqual(uid, summary.CardUid);
                    Assert.IsTrue(summary.AcquiredToHand);
                }
                finally
                {
                    Object.DestroyImmediate(controllerGo);
                }
            }
        }

        [Test]
        public void CombatHitSink_NoLongerExposesPickupBridges()
        {
            var sinkType = typeof(CombatHitSink);
            Assert.IsNull(
                sinkType.GetField("ApplyPickupItem"),
                "ApplyPickupItem 应已从 CombatHitSink 删除");
            Assert.IsNull(
                sinkType.GetMethod("RequestPickupItem"),
                "RequestPickupItem 应已从 CombatHitSink 删除");
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
    }
}