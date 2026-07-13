using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;

namespace NineGrid.Flow
{
    /// <summary>
    /// 将 Core EventLog 投影为保序盘面表现步骤流，避免多转/换位被扁平 Moves 压成一步。
    /// </summary>
    public static class BoardPresentationStepProjector
    {
        public sealed class ProjectionResult
        {
            public BoardPresentationStep[] Steps = Array.Empty<BoardPresentationStep>();
            public PostKillCardMove[] LegacyMoves = Array.Empty<PostKillCardMove>();
            public PostKillCardDeal[] LegacyDeals = Array.Empty<PostKillCardDeal>();
            public int[] LegacyRemovedUids = Array.Empty<int>();
            public int PickedUid;
        }

        public static ProjectionResult Project(
            IReadOnlyList<CoreGameEvent> entries,
            int startIndex,
            CardRegistry registry)
        {
            var result = new ProjectionResult();
            if (entries == null || startIndex < 0 || startIndex >= entries.Count)
            {
                return result;
            }

            var steps = new List<BoardPresentationStep>(8);
            var pendingMoves = new List<PostKillCardMove>(8);
            var pendingActionId = -1;
            var legacyMoveList = new List<PostKillCardMove>(16);
            var legacyDealList = new List<PostKillCardDeal>(8);
            var legacyRemoveList = new List<int>(4);
            var legacyRemovedSet = new HashSet<int>(4);

            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                switch (e.Type)
                {
                    case CoreEventType.ItemPicked:
                        if (e.CardUid > 0 && result.PickedUid == 0)
                        {
                            result.PickedUid = e.CardUid;
                        }

                        break;

                    case CoreEventType.CardMoved:
                        if (e.CardUid <= 0 || !e.FromSlot.IsBoardSlot || !e.ToSlot.IsBoardSlot)
                        {
                            break;
                        }

                        if (pendingActionId >= 0
                            && pendingActionId != e.ActionId
                            && pendingMoves.Count > 0)
                        {
                            FlushPendingMoveStep(steps, legacyMoveList, pendingMoves, pendingActionId);
                            pendingMoves.Clear();
                            pendingActionId = -1;
                        }

                        pendingActionId = e.ActionId;
                        var move = new PostKillCardMove
                        {
                            Uid = e.CardUid,
                            FromSlot = e.FromSlot.Index,
                            ToSlot = e.ToSlot.Index,
                        };
                        pendingMoves.Add(move);
                        break;

                    case CoreEventType.BoardRotated:
                        FlushPendingRotateStep(steps, legacyMoveList, pendingMoves, pendingActionId, e);
                        pendingMoves.Clear();
                        pendingActionId = -1;
                        break;

                    case CoreEventType.CardSwapped:
                        FlushPendingSwapStep(steps, legacyMoveList, pendingMoves, pendingActionId, e);
                        pendingMoves.Clear();
                        pendingActionId = -1;
                        break;

                    case CoreEventType.CardDealt:
                        FlushPendingMoveStepIfAny(steps, legacyMoveList, pendingMoves, ref pendingActionId);
                        if (e.CardUid > 0 && e.ToSlot.IsBoardSlot)
                        {
                            var defId = string.Empty;
                            if (registry != null && registry.TryGet(e.CardUid, out var coreCard))
                            {
                                defId = coreCard.DefId;
                            }

                            var deal = new PostKillCardDeal
                            {
                                Uid = e.CardUid,
                                Slot = e.ToSlot.Index,
                                DefId = defId,
                            };
                            legacyDealList.Add(deal);
                            steps.Add(new BoardPresentationStep
                            {
                                CoreSequence = e.Sequence,
                                ActionId = e.ActionId,
                                Kind = BoardPresentationStepKind.Deal,
                                Deals = new[] { deal },
                            });
                        }

                        break;

                    case CoreEventType.CardRemoved:
                    case CoreEventType.CardKilled:
                        FlushPendingMoveStepIfAny(steps, legacyMoveList, pendingMoves, ref pendingActionId);
                        if (e.CardUid > 0 && legacyRemovedSet.Add(e.CardUid))
                        {
                            legacyRemoveList.Add(e.CardUid);
                            steps.Add(new BoardPresentationStep
                            {
                                CoreSequence = e.Sequence,
                                ActionId = e.ActionId,
                                Kind = BoardPresentationStepKind.Remove,
                                RemovedUids = new[] { e.CardUid },
                            });
                        }

                        break;
                }
            }

            FlushPendingMoveStepIfAny(steps, legacyMoveList, pendingMoves, ref pendingActionId);

            result.Steps = steps.ToArray();
            result.LegacyMoves = legacyMoveList.ToArray();
            result.LegacyDeals = legacyDealList.ToArray();
            result.LegacyRemovedUids = legacyRemoveList.Count > 0
                ? legacyRemoveList.ToArray()
                : Array.Empty<int>();
            return result;
        }

        private static void FlushPendingMoveStepIfAny(
            List<BoardPresentationStep> steps,
            List<PostKillCardMove> legacyMoveList,
            List<PostKillCardMove> pendingMoves,
            ref int pendingActionId)
        {
            if (pendingMoves.Count <= 0)
            {
                return;
            }

            FlushPendingMoveStep(steps, legacyMoveList, pendingMoves, pendingActionId);
            pendingMoves.Clear();
            pendingActionId = -1;
        }

        private static void FlushPendingMoveStep(
            List<BoardPresentationStep> steps,
            List<PostKillCardMove> legacyMoveList,
            List<PostKillCardMove> pendingMoves,
            int pendingActionId)
        {
            if (pendingMoves.Count <= 0)
            {
                return;
            }

            var moves = pendingMoves.ToArray();
            legacyMoveList.AddRange(moves);
            steps.Add(new BoardPresentationStep
            {
                ActionId = pendingActionId,
                Kind = BoardPresentationStepKind.Move,
                Moves = moves,
            });
        }

        private static void FlushPendingRotateStep(
            List<BoardPresentationStep> steps,
            List<PostKillCardMove> legacyMoveList,
            List<PostKillCardMove> pendingMoves,
            int pendingActionId,
            CoreGameEvent rotateEvent)
        {
            var moves = pendingActionId == rotateEvent.ActionId && pendingMoves.Count > 0
                ? pendingMoves.ToArray()
                : Array.Empty<PostKillCardMove>();
            if (moves.Length > 0)
            {
                legacyMoveList.AddRange(moves);
            }

            steps.Add(new BoardPresentationStep
            {
                CoreSequence = rotateEvent.Sequence,
                ActionId = rotateEvent.ActionId,
                Kind = BoardPresentationStepKind.Rotate,
                Clockwise = rotateEvent.Amount >= 0,
                Moves = moves,
            });
        }

        private static void FlushPendingSwapStep(
            List<BoardPresentationStep> steps,
            List<PostKillCardMove> legacyMoveList,
            List<PostKillCardMove> pendingMoves,
            int pendingActionId,
            CoreGameEvent swapEvent)
        {
            var moves = pendingActionId == swapEvent.ActionId && pendingMoves.Count > 0
                ? pendingMoves.ToArray()
                : Array.Empty<PostKillCardMove>();
            if (moves.Length > 0)
            {
                legacyMoveList.AddRange(moves);
            }

            steps.Add(new BoardPresentationStep
            {
                CoreSequence = swapEvent.Sequence,
                ActionId = swapEvent.ActionId,
                Kind = BoardPresentationStepKind.Swap,
                Moves = moves,
            });
        }
    }
}
