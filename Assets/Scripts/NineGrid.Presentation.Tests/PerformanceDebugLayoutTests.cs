using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Debugging;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using NineGrid.Presentation.Tests.Support;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class PerformanceDebugLayoutTests
    {
        [Test]
        public void ResolveTargetSlot_DirectionUp_FromCenter_ReturnsSlot2()
        {
            var payload = new PerformanceDebugPayload();
            payload.Set(PerformanceDebugPayloadKeys.Direction, CardBattleDirection.Up.ToString());

            int targetSlot = PerformanceDebugLayoutApplier.ResolveTargetSlot(payload, 5);

            Assert.AreEqual(2, targetSlot);
        }

        [Test]
        public void ResolveTargetSlot_DirectionLeft_FromCenter_ReturnsSlot4()
        {
            var payload = new PerformanceDebugPayload();
            payload.Set(PerformanceDebugPayloadKeys.Direction, CardBattleDirection.Left.ToString());

            int targetSlot = PerformanceDebugLayoutApplier.ResolveTargetSlot(payload, 5);

            Assert.AreEqual(4, targetSlot);
        }

        [Test]
        public void ResolveTargetSlot_AutoTarget_FollowsDirection()
        {
            var payload = new PerformanceDebugPayload();
            payload.Set(PerformanceDebugPayloadKeys.TargetSlot, "Auto");
            payload.Set(PerformanceDebugPayloadKeys.Direction, CardBattleDirection.Down.ToString());

            int targetSlot = PerformanceDebugLayoutApplier.ResolveTargetSlot(payload, 5);

            Assert.AreEqual(8, targetSlot);
        }

        [Test]
        public void ResolveTargetSlot_ExplicitTargetSlot_TakesPrecedence()
        {
            var payload = new PerformanceDebugPayload();
            payload.Set(PerformanceDebugPayloadKeys.Direction, CardBattleDirection.Up.ToString());
            payload.Set(PerformanceDebugPayloadKeys.TargetSlot, "8");

            int targetSlot = PerformanceDebugLayoutApplier.ResolveTargetSlot(payload, 5);

            Assert.AreEqual(8, targetSlot);
        }

        [Test]
        public void CardBattleDirectionUtil_OrthogonalNeighbor_FromSlot5Up_ReturnsSlot2()
        {
            Assert.IsTrue(CardBattleDirectionUtil.TryGetNeighborForDirection(
                SlotId.Board(5),
                CardBattleDirection.Up,
                out SlotId neighbor));
            Assert.AreEqual(2, neighbor.Index);
        }

        [Test]
        public void FormatDerivedDirection_PlayerAt5TargetAt2_ReturnsUp()
        {
            var payload = new PerformanceDebugPayload();
            payload.Set(PerformanceDebugPayloadKeys.PlayerSlot, "5");
            payload.Set(PerformanceDebugPayloadKeys.TargetSlot, "2");

            Assert.AreEqual(CardBattleDirection.Up.ToString(), PerformanceDebugLayoutApplier.FormatDerivedDirection(payload));
        }

        [Test]
        public void BatchHijack_Amount_OverridesReactionPayload()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 1, "attack")
                    .WithActor(PerformanceDebugActorUids.Player)
                    .WithTarget(PerformanceDebugActorUids.Enemy)
                    .WithAmount(3),
            };

            var batch = PresentationBatchFixture.Create(12, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            var payload = new PerformanceDebugPayload();
            payload.Set(PerformanceDebugPayloadKeys.Amount, "9");
            PerformanceDebugBatchHijack.ApplyEditorOverrides(plan, payload);

            Assert.AreEqual(9, plan.Reactions[0].Payload.Amount);
        }
    }
}
