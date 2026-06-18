using System.Collections.Generic;

namespace NineGrid.Core
{
    public sealed class ChangePhaseAction : GameAction
    {
        public ChangePhaseAction(GamePhase nextPhase)
        {
            NextPhase = nextPhase;
        }

        public GamePhase NextPhase { get; private set; }
        public override string ActionName { get { return "ChangePhase"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var run = context.GetModel<RunModel>();
            var previous = run.Phase.Value;
            run.SetPhase(NextPhase);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.PhaseChanged, context.ActionId, ActionName)
                    .WithAmount((int)NextPhase)
                    .WithDelta((int)previous)
                    .WithMessage(previous + "->" + NextPhase));
        }
    }

    public sealed class NodeStartedAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnNodeStart
        };

        public override string ActionName { get { return "NodeStarted"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.NodeStarted, context.ActionId, ActionName));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class NodeCompletedAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnNodeEnd
        };

        public override string ActionName { get { return "NodeCompleted"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.NodeCompleted, context.ActionId, ActionName));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class ModifyInteractionCountAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnInteract
        };

        public ModifyInteractionCountAction(int delta)
        {
            Delta = delta;
        }

        public int Delta { get; private set; }
        public override string ActionName { get { return "ModifyInteractionCount"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var player = context.GetModel<PlayerModel>();
            player.AddInteractionCount(Delta);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.InteractionChanged, context.ActionId, ActionName)
                    .WithDelta(Delta)
                    .WithAmount(player.InteractionCount.Value));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class PickupCardAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnRemove
        };

        public PickupCardAction(int cardUid)
        {
            CardUid = cardUid;
        }

        public int CardUid { get; private set; }
        public override string ActionName { get { return "PickupCard"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();
            var card = registry.Get(CardUid);
            var fromSlot = card.Slot.Value;
            board.RemoveCard(card);
            deck.RemoveCard(card);
            card.Zone.Value = ZoneId.Removed;
            card.Slot.Value = SlotId.None;

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.ItemPicked, context.ActionId, ActionName)
                    .WithCard(CardUid)
                    .WithSlots(fromSlot, SlotId.None))
                .AddEvent(new CoreGameEvent(CoreEventType.CardRemoved, context.ActionId, ActionName)
                    .WithCard(CardUid)
                    .WithSlots(fromSlot, SlotId.None)
                    .WithMessage("pickup"));

            var goldReward = card.Counters.Get(CoreCounterKeys.GoldReward);
            if (goldReward != 0)
            {
                result.AddFollowUp(new ModifyGoldAction(goldReward, "pickup:" + card.DefId));
            }

            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class ClickEmptySlotAction : GameAction
    {
        public ClickEmptySlotAction(SlotId slot)
        {
            Slot = slot;
        }

        public SlotId Slot { get; private set; }
        public override string ActionName { get { return "ClickEmptySlot"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EmptyClicked, context.ActionId, ActionName)
                    .WithSlots(Slot, Slot));
        }
    }

    public sealed class UseItemAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnUseHelpCard
        };

        public UseItemAction(int itemUid)
        {
            ItemUid = itemUid;
        }

        public int ItemUid { get; private set; }
        public override string ActionName { get { return "UseItem"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.ItemUsed, context.ActionId, ActionName)
                    .WithCard(ItemUid));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }
}
