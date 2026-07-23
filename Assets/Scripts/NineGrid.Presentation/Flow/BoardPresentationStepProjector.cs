using System;
using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Core;

namespace NineGrid.Flow
{
    /// <summary>
    /// 将 Core EventLog 投影为带 commitment 标签的有序盘面表现步骤流。
    /// 多跳策略 S/C 仅影响独立 Move 批；Rotate/Swap/Deal/Remove 恒 Sync。
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
            CardRegistry registry,
            MultiHopProjectionStrategy multiHopStrategy = MultiHopProjectionStrategy.SerialVisible)
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
                            FlushPendingMoveStep(
                                steps,
                                legacyMoveList,
                                pendingMoves,
                                pendingActionId,
                                multiHopStrategy);
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
                        FlushPendingMoveStepIfAny(
                            steps,
                            legacyMoveList,
                            pendingMoves,
                            ref pendingActionId,
                            multiHopStrategy);
                        // 盘面→非盘面（exchangeToDraw / shuffleExisting 等）：投影为 Remove，避免幽灵占格。
                        if (e.CardUid > 0 && e.FromSlot.IsBoardSlot && !e.ToSlot.IsBoardSlot)
                        {
                            if (legacyRemovedSet.Add(e.CardUid))
                            {
                                legacyRemoveList.Add(e.CardUid);
                                steps.Add(new BoardPresentationStep
                                {
                                    CoreSequence = e.Sequence,
                                    ActionId = e.ActionId,
                                    Kind = BoardPresentationStepKind.Remove,
                                    Commitment = CommitmentKind.Sync,
                                    MultiHopStrategy = MultiHopProjectionStrategy.SerialVisible,
                                    RemovedUids = new[] { e.CardUid },
                                });
                            }

                            break;
                        }

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
                                Commitment = CommitmentKind.Sync,
                                MultiHopStrategy = MultiHopProjectionStrategy.SerialVisible,
                                Deals = new[] { deal },
                            });
                        }

                        break;

                    case CoreEventType.CardRemoved:
                    case CoreEventType.CardKilled:
                        FlushPendingMoveStepIfAny(
                            steps,
                            legacyMoveList,
                            pendingMoves,
                            ref pendingActionId,
                            multiHopStrategy);
                        if (e.CardUid > 0 && legacyRemovedSet.Add(e.CardUid))
                        {
                            legacyRemoveList.Add(e.CardUid);
                            steps.Add(new BoardPresentationStep
                            {
                                CoreSequence = e.Sequence,
                                ActionId = e.ActionId,
                                Kind = BoardPresentationStepKind.Remove,
                                Commitment = CommitmentKind.Sync,
                                MultiHopStrategy = MultiHopProjectionStrategy.SerialVisible,
                                RemovedUids = new[] { e.CardUid },
                            });
                        }

                        break;
                }
            }

            FlushPendingMoveStepIfAny(
                steps,
                legacyMoveList,
                pendingMoves,
                ref pendingActionId,
                multiHopStrategy);

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
            ref int pendingActionId,
            MultiHopProjectionStrategy multiHopStrategy)
        {
            if (pendingMoves.Count <= 0)
            {
                return;
            }

            FlushPendingMoveStep(
                steps,
                legacyMoveList,
                pendingMoves,
                pendingActionId,
                multiHopStrategy);
            pendingMoves.Clear();
            pendingActionId = -1;
        }

        private static void FlushPendingMoveStep(
            List<BoardPresentationStep> steps,
            List<PostKillCardMove> legacyMoveList,
            List<PostKillCardMove> pendingMoves,
            int pendingActionId,
            MultiHopProjectionStrategy multiHopStrategy)
        {
            if (pendingMoves.Count <= 0)
            {
                return;
            }

            var moves = pendingMoves.ToArray();
            legacyMoveList.AddRange(moves);

            if (multiHopStrategy == MultiHopProjectionStrategy.CollapsedEndpoint)
            {
                steps.Add(new BoardPresentationStep
                {
                    ActionId = pendingActionId,
                    Kind = BoardPresentationStepKind.Move,
                    Commitment = CommitmentKind.Async,
                    MultiHopStrategy = MultiHopProjectionStrategy.CollapsedEndpoint,
                    Moves = CollapseMovesToEndpoints(moves),
                });
                return;
            }

            // 策略 S：同 uid 多跳才拆成 N 个 Sync Step；单跳多卡仍一批并行。
            if (HasMultiHopUid(moves))
            {
                for (var i = 0; i < moves.Length; i++)
                {
                    steps.Add(new BoardPresentationStep
                    {
                        ActionId = pendingActionId,
                        Kind = BoardPresentationStepKind.Move,
                        Commitment = CommitmentKind.Sync,
                        MultiHopStrategy = MultiHopProjectionStrategy.SerialVisible,
                        Moves = new[] { moves[i] },
                    });
                }

                return;
            }

            steps.Add(new BoardPresentationStep
            {
                ActionId = pendingActionId,
                Kind = BoardPresentationStepKind.Move,
                Commitment = CommitmentKind.Sync,
                MultiHopStrategy = MultiHopProjectionStrategy.SerialVisible,
                Moves = moves,
            });
        }

        private static bool HasMultiHopUid(PostKillCardMove[] moves)
        {
            if (moves == null || moves.Length <= 1)
            {
                return false;
            }

            var seen = new HashSet<int>();
            for (var i = 0; i < moves.Length; i++)
            {
                var uid = moves[i].Uid;
                if (uid <= 0)
                {
                    continue;
                }

                if (!seen.Add(uid))
                {
                    return true;
                }
            }

            return false;
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
                Commitment = CommitmentKind.Sync,
                MultiHopStrategy = MultiHopProjectionStrategy.SerialVisible,
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
                Commitment = CommitmentKind.Sync,
                MultiHopStrategy = MultiHopProjectionStrategy.SerialVisible,
                Moves = moves,
            });
        }

        /// <summary>
        /// 策略 C：按 uid 首次出现序，折叠为 From=首跳起点、To=末跳终点。
        /// </summary>
        public static PostKillCardMove[] CollapseMovesToEndpoints(PostKillCardMove[] moves)
        {
            if (moves == null || moves.Length == 0)
            {
                return Array.Empty<PostKillCardMove>();
            }

            var order = new List<int>(moves.Length);
            var fromByUid = new Dictionary<int, int>(moves.Length);
            var toByUid = new Dictionary<int, int>(moves.Length);
            for (var i = 0; i < moves.Length; i++)
            {
                var move = moves[i];
                if (move.Uid <= 0)
                {
                    continue;
                }

                if (!fromByUid.ContainsKey(move.Uid))
                {
                    order.Add(move.Uid);
                    fromByUid[move.Uid] = move.FromSlot;
                }

                toByUid[move.Uid] = move.ToSlot;
            }

            var collapsed = new PostKillCardMove[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                var uid = order[i];
                collapsed[i] = new PostKillCardMove
                {
                    Uid = uid,
                    FromSlot = fromByUid[uid],
                    ToSlot = toByUid[uid],
                };
            }

            return collapsed;
        }
    }
}
