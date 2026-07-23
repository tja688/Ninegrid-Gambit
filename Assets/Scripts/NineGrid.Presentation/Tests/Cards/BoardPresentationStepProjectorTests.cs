using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Flow;

namespace NineGrid.Presentation.Tests
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
            Assert.AreEqual(CommitmentKind.Sync, result.Steps[0].Commitment);
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
            Assert.AreEqual(CommitmentKind.Sync, result.Steps[0].Commitment);
            Assert.AreEqual(CommitmentKind.Sync, result.Steps[1].Commitment);
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
            Assert.AreEqual(CommitmentKind.Sync, result.Steps[1].Commitment);
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
            Assert.AreEqual(CommitmentKind.Sync, result.Steps[1].Commitment);
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
            Assert.AreEqual(CommitmentKind.Sync, result.Steps[0].Commitment);
            Assert.AreEqual(1, result.Steps[0].Moves.Length);
            Assert.AreEqual(501, result.Steps[0].Moves[0].Uid);
        }

        [Test]
        public void Project_MultiHop_SerialVisible_EmitsNSyncSteps()
        {
            var events = BuildMultiHopAction(actionId: 42, uid: 701, hops: new[]
            {
                (1, 2),
                (2, 3),
                (3, 5),
            });

            var result = BoardPresentationStepProjector.Project(
                events,
                0,
                registry: null,
                MultiHopProjectionStrategy.SerialVisible);

            Assert.AreEqual(3, result.Steps.Length, "策略 S：N 跳 → N 个 Step");
            for (var i = 0; i < result.Steps.Length; i++)
            {
                Assert.AreEqual(BoardPresentationStepKind.Move, result.Steps[i].Kind);
                Assert.AreEqual(CommitmentKind.Sync, result.Steps[i].Commitment);
                Assert.AreEqual(
                    MultiHopProjectionStrategy.SerialVisible,
                    result.Steps[i].MultiHopStrategy);
                Assert.AreEqual(1, result.Steps[i].Moves.Length);
                Assert.AreEqual(701, result.Steps[i].Moves[0].Uid);
            }

            Assert.AreEqual(1, result.Steps[0].Moves[0].FromSlot);
            Assert.AreEqual(2, result.Steps[0].Moves[0].ToSlot);
            Assert.AreEqual(2, result.Steps[1].Moves[0].FromSlot);
            Assert.AreEqual(3, result.Steps[1].Moves[0].ToSlot);
            Assert.AreEqual(3, result.Steps[2].Moves[0].FromSlot);
            Assert.AreEqual(5, result.Steps[2].Moves[0].ToSlot);
            Assert.AreEqual(3, result.LegacyMoves.Length, "Legacy 保留全部跳");
        }

        [Test]
        public void Project_MultiHop_CollapsedEndpoint_EmitsOneAsyncStep()
        {
            var events = BuildMultiHopAction(actionId: 42, uid: 701, hops: new[]
            {
                (1, 2),
                (2, 3),
                (3, 5),
            });

            var result = BoardPresentationStepProjector.Project(
                events,
                0,
                registry: null,
                MultiHopProjectionStrategy.CollapsedEndpoint);

            Assert.AreEqual(1, result.Steps.Length, "策略 C：N 跳 → 1 个 Step");
            Assert.AreEqual(BoardPresentationStepKind.Move, result.Steps[0].Kind);
            Assert.AreEqual(CommitmentKind.Async, result.Steps[0].Commitment);
            Assert.AreEqual(
                MultiHopProjectionStrategy.CollapsedEndpoint,
                result.Steps[0].MultiHopStrategy);
            Assert.AreEqual(1, result.Steps[0].Moves.Length);
            Assert.AreEqual(701, result.Steps[0].Moves[0].Uid);
            Assert.AreEqual(1, result.Steps[0].Moves[0].FromSlot);
            Assert.AreEqual(5, result.Steps[0].Moves[0].ToSlot);
            Assert.AreEqual(3, result.LegacyMoves.Length, "Legacy 仍保留中间跳");
        }

        [Test]
        public void CollapseMovesToEndpoints_PreservesUidOrder()
        {
            var moves = new[]
            {
                new PostKillCardMove { Uid = 10, FromSlot = 1, ToSlot = 2 },
                new PostKillCardMove { Uid = 20, FromSlot = 3, ToSlot = 4 },
                new PostKillCardMove { Uid = 10, FromSlot = 2, ToSlot = 5 },
            };

            var collapsed = BoardPresentationStepProjector.CollapseMovesToEndpoints(moves);

            Assert.AreEqual(2, collapsed.Length);
            Assert.AreEqual(10, collapsed[0].Uid);
            Assert.AreEqual(1, collapsed[0].FromSlot);
            Assert.AreEqual(5, collapsed[0].ToSlot);
            Assert.AreEqual(20, collapsed[1].Uid);
            Assert.AreEqual(3, collapsed[1].FromSlot);
            Assert.AreEqual(4, collapsed[1].ToSlot);
        }

        [Test]
        public void Project_ExchangeToDraw_EmitsRemoveBeforeExchangeDrawDeal()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardDealt, 74, "ExchangeWithDrawPile")
                    .WithCard(9)
                    .WithSlots(SlotId.Board(9), SlotId.None)
                    .WithMessage("exchangeToDraw:help.stat_boost_card"),
                new CoreGameEvent(CoreEventType.CardDealt, 74, "ExchangeWithDrawPile")
                    .WithCard(21)
                    .WithSlots(SlotId.None, SlotId.Board(9))
                    .WithMessage("exchangeDraw:monster.skull_head"),
            };

            var result = BoardPresentationStepProjector.Project(events, 0, registry: null);

            Assert.AreEqual(2, result.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Remove, result.Steps[0].Kind);
            Assert.AreEqual(9, result.Steps[0].RemovedUids[0]);
            Assert.AreEqual(BoardPresentationStepKind.Deal, result.Steps[1].Kind);
            Assert.AreEqual(21, result.Steps[1].Deals[0].Uid);
            Assert.AreEqual(9, result.Steps[1].Deals[0].Slot);
            CollectionAssert.Contains(result.LegacyRemovedUids, 9);
            Assert.AreEqual(1, result.LegacyDeals.Length);
            Assert.AreEqual(21, result.LegacyDeals[0].Uid);
        }

        [Test]
        public void Project_BoardToNonBoardCardDealt_DoesNotEmitDeal()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardDealt, 3, "ShuffleCardIntoDrawPile")
                    .WithCard(401)
                    .WithSlots(SlotId.Board(2), SlotId.None)
                    .WithMessage("shuffleExisting:help.flame"),
            };

            var result = BoardPresentationStepProjector.Project(events, 0, registry: null);

            Assert.AreEqual(1, result.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Remove, result.Steps[0].Kind);
            Assert.AreEqual(0, result.LegacyDeals.Length);
            CollectionAssert.Contains(result.LegacyRemovedUids, 401);
        }

        [Test]
        public void Project_RotateThenExchange_PreservesRemoveBeforeDeal()
        {
            var events = new List<CoreGameEvent>();
            events.AddRange(BuildRotateAction(actionId: 58, clockwise: true, startUid: 100));
            events.Add(new CoreGameEvent(CoreEventType.CardDealt, 60, "ExchangeWithDrawPile")
                .WithCard(9)
                .WithSlots(SlotId.Board(9), SlotId.None)
                .WithMessage("exchangeToDraw:help.stat_boost_card"));
            events.Add(new CoreGameEvent(CoreEventType.CardDealt, 60, "ExchangeWithDrawPile")
                .WithCard(21)
                .WithSlots(SlotId.None, SlotId.Board(9))
                .WithMessage("exchangeDraw:monster.skull_head"));

            var result = BoardPresentationStepProjector.Project(events, 0, registry: null);

            Assert.AreEqual(3, result.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, result.Steps[0].Kind);
            Assert.AreEqual(BoardPresentationStepKind.Remove, result.Steps[1].Kind);
            Assert.AreEqual(BoardPresentationStepKind.Deal, result.Steps[2].Kind);
            Assert.AreEqual(9, result.Steps[1].RemovedUids[0]);
            Assert.AreEqual(21, result.Steps[2].Deals[0].Uid);
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

        private static List<CoreGameEvent> BuildMultiHopAction(
            int actionId,
            int uid,
            (int from, int to)[] hops)
        {
            var events = new List<CoreGameEvent>(hops.Length);
            for (var i = 0; i < hops.Length; i++)
            {
                events.Add(new CoreGameEvent(CoreEventType.CardMoved, actionId, "MultiHop")
                    .WithCard(uid)
                    .WithSlots(SlotId.Board(hops[i].from), SlotId.Board(hops[i].to)));
            }

            return events;
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
