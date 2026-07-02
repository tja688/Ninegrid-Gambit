using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Tests.Support;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class Phase5FlowRouterTests
    {
        [Test]
        public void Router_OfferRooms_RoutesRoomChoiceIn()
        {
            AssertRoute(
                CoreEventType.RoomChoicesOffered,
                PresentationInstructionKind.OfferRooms,
                FlowId.RoomChoiceIn);
        }

        [Test]
        public void Router_SelectRoom_RoutesRoomChoiceOut()
        {
            AssertRoute(
                CoreEventType.RoomSelected,
                PresentationInstructionKind.SelectRoom,
                FlowId.RoomChoiceOut);
        }

        private static void AssertRoute(CoreEventType eventType, PresentationInstructionKind kind, FlowId expectedFlow)
        {
            var instruction = new PresentationInstruction(
                new CoreGameEvent(eventType, 1, "test"),
                PresentationEventMap.Get(eventType));

            Assert.AreEqual(kind, instruction.Kind);
            Assert.IsTrue(InstructionKindFlowRouter.TryRoute(instruction, out InstructionRoute route));
            Assert.AreEqual(expectedFlow, route.FlowId);
        }
    }
}
