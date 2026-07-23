using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Pickup
{
    /// <summary>
    /// V3：PickupInputController → Hold→Apply；并锁定 CombatHitSink 拾取入口已退役。
    /// </summary>
    public sealed class PickupInputControllerTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void Controller_HandlePickupRequested_HoldThenAppliesPickupCommand()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            using (var runtime = PresentationRuntimeFixture.Install(
                       arch, new RecordingScriptFactory(continueTicks: 0)))
            {
                IntentIntakeSystem.EnsureRegistered(arch.Architecture);
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                var uid = SpawnHelpOnBoard(arch, "help.throwing_knife", sAdjacentSlot);

                var controllerGo = new GameObject("PickupInputControllerProbe");
                var controller = controllerGo.AddComponent<PickupInputController>();

                try
                {
                    Assert.IsFalse(runtime.Runtime.HasExternalHold);
                    var summary = controller.HandlePickupRequested(sAdjacentSlot.Index);
                    Assert.IsTrue(summary.Accepted, summary.Reason);
                    Assert.AreEqual(uid, summary.CardUid);
                    Assert.IsTrue(summary.AcquiredToHand);
                    Assert.IsTrue(
                        runtime.Runtime.HasExternalHold,
                        "Apply 成功后须仍持 ExternalHold 供表现承接");
                    Assert.IsTrue(runtime.MainlineBusy.Value);
                }
                finally
                {
                    PresentationInputGates.EndExternalHold("PickupInputControllerTests");
                    runtime.Tick();
                    Object.DestroyImmediate(controllerGo);
                }
            }
        }

        [Test]
        public void Controller_LockFail_DoesNotApplyCore()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                // 不安装 Runtime：Allow 后 TryBeginExternalHold 失败 → lockFail，Core 不得改。
                IntentIntakeSystem.EnsureRegistered(arch.Architecture);
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                var uid = SpawnHelpOnBoard(arch, "help.throwing_knife", sAdjacentSlot);

                var controllerGo = new GameObject("PickupInputControllerLockFail");
                var controller = controllerGo.AddComponent<PickupInputController>();

                try
                {
                    var summary = controller.HandlePickupRequested(sAdjacentSlot.Index);
                    Assert.IsFalse(summary.Accepted);
                    Assert.AreEqual("lockFail", summary.Reason);
                    Assert.AreEqual(
                        uid,
                        arch.Board.GetCardUid(sAdjacentSlot),
                        "Hold 失败不得改 Core 占格");
                }
                finally
                {
                    Object.DestroyImmediate(controllerGo);
                }
            }
        }

        [Test]
        public void CombatHitSink_TypeIsDeleted()
        {
            Assert.IsNull(
                System.Type.GetType("NineGrid.Cards.CombatHitSink, NineGrid.Presentation"),
                "CombatHitSink 应已删除");
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
