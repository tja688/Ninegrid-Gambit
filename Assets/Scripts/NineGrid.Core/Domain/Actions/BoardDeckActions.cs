using System;
using System.Collections.Generic;
using NineGrid.Core.Utilities;

namespace NineGrid.Core
{
    public sealed class SetupNodeDeckAction : GameAction
    {
        public SetupNodeDeckAction(NodeDeckOptions options)
        {
            Options = options ?? NodeDeckOptions.CreateDefaultBattle();
        }

        public NodeDeckOptions Options { get; private set; }
        public override string ActionName { get { return "SetupNodeDeck"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();

            board.ClearBoardCards();
            deck.Clear();
            RemoveNonAvatarCards(registry);

            for (var i = 0; i < Options.PlayerCards.Count; i++)
            {
                deck.AddToPlayerCardPool(Options.PlayerCards[i].Create(registry));
            }

            for (var i = 0; i < Options.EnemyCards.Count; i++)
            {
                deck.AddToEnemyCardPool(Options.EnemyCards[i].Create(registry));
            }

            return GameActionResult.Empty;
        }

        private static void RemoveNonAvatarCards(CardRegistry registry)
        {
            var removeUids = new List<int>();
            foreach (var pair in registry.Cards)
            {
                if (pair.Value.Kind != CardKind.Avatar)
                {
                    removeUids.Add(pair.Key);
                }
            }

            for (var i = 0; i < removeUids.Count; i++)
            {
                registry.Remove(removeUids[i]);
            }
        }
    }

    public sealed class OpeningDealAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal
        };

        public OpeningDealAction(NodeDeckOptions options)
        {
            Options = options ?? NodeDeckOptions.CreateDefaultBattle();
        }

        public NodeDeckOptions Options { get; private set; }
        public override string ActionName { get { return "OpeningDeal"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var deck = context.GetModel<DeckModel>();
            var selected = new List<int>();

            SelectCards(selected, deck.PlayerCardPoolUids, registry, Options.PlayerOpeningCount, false);
            SelectCards(selected, deck.EnemyCardPoolUids, registry, Options.EnemyOpeningCount, Options.RequireElite);

            for (var i = 0; i < selected.Count; i++)
            {
                var card = registry.Get(selected[i]);
                deck.AddToDrawPile(card, false);
            }

            // RUL_发牌 step 3: remaining staging pools merge into runtime draw pile (not in-node reserve).
            DrainStagingPool(deck, registry, deck.PlayerCardPoolUids);
            DrainStagingPool(deck, registry, deck.EnemyCardPoolUids);

            ShuffleDrawPile(deck, context.Architecture.GetUtility<IRngUtility>());

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                    .WithAmount(selected.Count)
                    .WithMessage("opening"));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        private static void SelectCards(List<int> selected, IReadOnlyList<int> pool, CardRegistry registry, int count, bool requireElite)
        {
            if (count <= 0)
            {
                return;
            }

            var selectedFromPool = 0;
            if (requireElite)
            {
                for (var i = 0; i < pool.Count; i++)
                {
                    var card = registry.Get(pool[i]);
                    if (card.Counters.Get(CoreCounterKeys.Elite) > 0)
                    {
                        selected.Add(card.Uid);
                        selectedFromPool++;
                        break;
                    }
                }
            }

            for (var i = 0; i < pool.Count && selectedFromPool < count; i++)
            {
                if (!selected.Contains(pool[i]))
                {
                    selected.Add(pool[i]);
                    selectedFromPool++;
                }
            }
        }

        private static void DrainStagingPool(DeckModel deck, CardRegistry registry, IReadOnlyList<int> poolUids)
        {
            if (poolUids.Count == 0)
            {
                return;
            }

            var remaining = new List<int>(poolUids.Count);
            for (var i = 0; i < poolUids.Count; i++)
            {
                remaining.Add(poolUids[i]);
            }

            for (var i = 0; i < remaining.Count; i++)
            {
                deck.AddToDrawPile(registry.Get(remaining[i]), false);
            }
        }

        private static void ShuffleDrawPile(DeckModel deck, IRngUtility rng)
        {
            var shuffled = new List<int>(deck.DrawPileUids);
            for (var i = shuffled.Count - 1; i > 0; i--)
            {
                var swapIndex = rng.Range(0, i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[swapIndex];
                shuffled[swapIndex] = temp;
            }

            deck.ReorderDrawPile(shuffled);
        }
    }

    public sealed class FillEmptySlotsAction : GameAction
    {
        private static readonly SlotId[] sFillOrder =
        {
            SlotId.Board(2),
            SlotId.Board(4),
            SlotId.Board(6),
            SlotId.Board(8),
            SlotId.Board(1),
            SlotId.Board(3),
            SlotId.Board(7),
            SlotId.Board(9)
        };

        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal
        };

        public override string ActionName { get { return "FillEmptySlots"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();
            var result = new GameActionResult();
            var filled = 0;

            for (var i = 0; i < sFillOrder.Length; i++)
            {
                var slot = sFillOrder[i];
                if (slot == board.AvatarSlot.Value || !board.IsEmpty(slot))
                {
                    continue;
                }

                int uid;
                if (!deck.TryPeekDrawPile(out uid))
                {
                    break;
                }

                var card = registry.Get(uid);
                deck.RemoveUid(uid);
                board.PlaceCard(card, slot);
                filled++;

                result.AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                    .WithCard(uid)
                    .WithSlots(SlotId.None, slot)
                    .WithAmount(filled));
            }

            result.AddEvent(new CoreGameEvent(CoreEventType.SlotsFilled, context.ActionId, ActionName)
                .WithAmount(filled));

            if (deck.DrawPileUids.Count == 0)
            {
                result.AddEvent(new CoreGameEvent(CoreEventType.DrawPileExhausted, context.ActionId, ActionName));
            }

            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class RotateBoardClockwiseAction : GameAction
    {
        private static readonly SlotId[] sClockwisePath =
        {
            SlotId.Board(1),
            SlotId.Board(2),
            SlotId.Board(3),
            SlotId.Board(6),
            SlotId.Board(9),
            SlotId.Board(8),
            SlotId.Board(7),
            SlotId.Board(4)
        };

        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnRotate,
            TriggerPoint.OnMove
        };

        public static IReadOnlyList<SlotId> ClockwisePath
        {
            get { return sClockwisePath; }
        }

        public override string ActionName { get { return "RotateBoardClockwise"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var uids = new int[sClockwisePath.Length];
            var result = new GameActionResult();

            for (var i = 0; i < sClockwisePath.Length; i++)
            {
                uids[i] = board.GetCardUid(sClockwisePath[i]);
            }

            for (var i = 0; i < sClockwisePath.Length; i++)
            {
                board.ClearSlot(sClockwisePath[i]);
            }

            for (var i = 0; i < sClockwisePath.Length; i++)
            {
                if (uids[i] == 0)
                {
                    continue;
                }

                var fromSlot = sClockwisePath[i];
                var toSlot = sClockwisePath[(i + 1) % sClockwisePath.Length];
                board.PlaceCard(registry.Get(uids[i]), toSlot);
                result.AddEvent(new CoreGameEvent(CoreEventType.CardMoved, context.ActionId, ActionName)
                    .WithCard(uids[i])
                    .WithSlots(fromSlot, toSlot));
            }

            result.AddEvent(new CoreGameEvent(CoreEventType.BoardRotated, context.ActionId, ActionName)
                .WithAmount(1)
                .WithMessage("clockwise"));
            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class SwapBoardSlotsAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnSwap,
            TriggerPoint.OnMove
        };

        public SwapBoardSlotsAction(SlotId left, SlotId right)
        {
            Left = left;
            Right = right;
        }

        public SlotId Left { get; private set; }
        public SlotId Right { get; private set; }
        public override string ActionName { get { return "SwapBoardSlots"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var leftUid = board.GetCardUid(Left);
            var rightUid = board.GetCardUid(Right);
            board.ClearSlot(Left);
            board.ClearSlot(Right);

            if (leftUid != 0)
            {
                board.PlaceCard(registry.Get(leftUid), Right);
            }

            if (rightUid != 0)
            {
                board.PlaceCard(registry.Get(rightUid), Left);
            }

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardSwapped, context.ActionId, ActionName)
                    .WithSlots(Left, Right));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }
}
