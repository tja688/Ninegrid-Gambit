using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow;

namespace NineGrid.Cards.Tests
{
    public sealed class BoardPresentationStepProjectorTests
    {
        [Test]
        public void Project_SingleRotate_EmitsOneRotateStep()
        {
            var events = BuildRotateAction(actionId: 10, clockwise: true, startUid: 100);
            var result = BoardPresentationStepProjector.Project(events, 0, registry: null);

            Assert.AreEqual(1, result.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, result.Steps[0].Kind);
            Assert.IsTrue(result.Steps[0].Clockwise);
            Assert.AreEqual(10, result.Steps[0].ActionId);
            Assert.Greater(result.Steps[0].Moves.Length, 0);
        }

        [Test]
        public void Project_DoubleRotate_EmitsTwoRotateStepsInOrder()
        {
            var events = new List<CoreGameEvent>();
            events.AddRange(BuildRotateAction(actionId: 1, clockwise: true, startUid: 100));
            events.AddRange(BuildRotateAction(actionId: 2, clockwise: false, startUid: 200));

            var result = BoardPresentationStepProjector.Project(events, 0, registry: null);

            Assert.AreEqual(2, result.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, result.Steps[0].Kind);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, result.Steps[1].Kind);
            Assert.IsTrue(result.Steps[0].Clockwise);
            Assert.IsFalse(result.Steps[1].Clockwise);
            Assert.AreEqual(1, result.Steps[0].ActionId);
            Assert.AreEqual(2, result.Steps[1].ActionId);
        }

        [Test]
        public void Project_RotateThenSwap_PreservesEventOrder()
        {
            var events = new List<CoreGameEvent>();
            events.AddRange(BuildRotateAction(actionId: 1, clockwise: true, startUid: 100));
            events.AddRange(BuildSwapAction(actionId: 2, leftUid: 301, rightUid: 302, leftSlot: 1, rightSlot: 3));

            var result = BoardPresentationStepProjector.Project(events, 0, registry: null);

            Assert.AreEqual(2, result.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, result.Steps[0].Kind);
            Assert.AreEqual(BoardPresentationStepKind.Swap, result.Steps[1].Kind);
            Assert.AreEqual(2, result.Steps[1].Moves.Length);
        }

        [Test]
        public void Project_TripleRotateWithSwapBetween_PreservesInterleavedOrder()
        {
            var events = new List<CoreGameEvent>();
            events.AddRange(BuildRotateAction(actionId: 1, clockwise: true, startUid: 100));
            events.AddRange(BuildSwapAction(actionId: 2, leftUid: 201, rightUid: 202, leftSlot: 2, rightSlot: 4));
            events.AddRange(BuildRotateAction(actionId: 3, clockwise: true, startUid: 300));
            events.AddRange(BuildRotateAction(actionId: 4, clockwise: false, startUid: 400));

            var result = BoardPresentationStepProjector.Project(events, 0, registry: null);

            Assert.AreEqual(4, result.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, result.Steps[0].Kind);
            Assert.AreEqual(BoardPresentationStepKind.Swap, result.Steps[1].Kind);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, result.Steps[2].Kind);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, result.Steps[3].Kind);
            Assert.IsFalse(result.Steps[3].Clockwise);
        }

        [Test]
        public void Project_RemoveAfterRotate_DoesNotStripEarlierMovesFromLegacy()
        {
            var events = new List<CoreGameEvent>();
            events.AddRange(BuildRotateAction(actionId: 1, clockwise: true, startUid: 100));
            events.Add(new CoreGameEvent(CoreEventType.CardKilled, 2, "Kill")
                .WithCard(105));

            var result = BoardPresentationStepProjector.Project(events, 0, registry: null);

            Assert.AreEqual(2, result.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Remove, result.Steps[1].Kind);
            CollectionAssert.Contains(result.LegacyRemovedUids, 105);
            Assert.Greater(result.LegacyMoves.Length, 0, "Legacy moves must not be filtered by later remove");
            Assert.IsTrue(ContainsUid(result.LegacyMoves, 105));
        }

        [Test]
        public void Project_StandaloneMove_EmitsMoveStep()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardMoved, 7, "Hop")
                    .WithCard(501)
                    .WithSlots(SlotId.Board(1), SlotId.Board(2)),
            };

            var result = BoardPresentationStepProjector.Project(events, 0, registry: null);

            Assert.AreEqual(1, result.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Move, result.Steps[0].Kind);
            Assert.AreEqual(1, result.Steps[0].Moves.Length);
            Assert.AreEqual(501, result.Steps[0].Moves[0].Uid);
        }

        private static bool ContainsUid(PostKillCardMove[] moves, int uid)
        {
            for (var i = 0; i < moves.Length; i++)
            {
                if (moves[i].Uid == uid)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<CoreGameEvent> BuildRotateAction(int actionId, bool clockwise, int startUid)
        {
            var ring = GroundSlotTopology.ClockwiseRing;
            var events = new List<CoreGameEvent>(ring.Count + 1);
            for (var i = 0; i < ring.Count; i++)
            {
                var from = ring[i];
                var toIndex = clockwise
                    ? (i + 1) % ring.Count
                    : (i + ring.Count - 1) % ring.Count;
                var to = ring[toIndex];
                events.Add(new CoreGameEvent(CoreEventType.CardMoved, actionId, "RotateBoardClockwise")
                    .WithCard(startUid + i)
                    .WithSlots(SlotId.Board(from), SlotId.Board(to)));
            }

            events.Add(new CoreGameEvent(CoreEventType.BoardRotated, actionId, "RotateBoardClockwise")
                .WithAmount(clockwise ? 1 : -1));
            return events;
        }

        private static List<CoreGameEvent> BuildSwapAction(
            int actionId,
            int leftUid,
            int rightUid,
            int leftSlot,
            int rightSlot)
        {
            return new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardMoved, actionId, "SwapBoardSlots")
                    .WithCard(leftUid)
                    .WithSlots(SlotId.Board(leftSlot), SlotId.Board(rightSlot)),
                new CoreGameEvent(CoreEventType.CardMoved, actionId, "SwapBoardSlots")
                    .WithCard(rightUid)
                    .WithSlots(SlotId.Board(rightSlot), SlotId.Board(leftSlot)),
                new CoreGameEvent(CoreEventType.CardSwapped, actionId, "SwapBoardSlots")
                    .WithSlots(SlotId.Board(leftSlot), SlotId.Board(rightSlot)),
            };
        }
    }
}
