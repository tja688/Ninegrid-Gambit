using System.Collections.Generic;
using System.Text;

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

    public sealed class AdvanceNodeAction : GameAction
    {
        public override string ActionName { get { return "AdvanceNode"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var run = context.GetModel<RunModel>();
            run.AdvanceNode();

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.NodeAdvanced, context.ActionId, ActionName)
                    .WithAmount(run.NodeIndex.Value));
        }
    }

    public sealed class ClearPendingChoicesAction : GameAction
    {
        public override string ActionName { get { return "ClearPendingChoices"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<PendingChoiceModel>().Clear();
            return GameActionResult.Empty;
        }
    }

    public sealed class ClearPendingRewardChoiceAction : GameAction
    {
        public override string ActionName { get { return "ClearPendingRewardChoice"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<PendingChoiceModel>().ClearRewardChoices();
            return GameActionResult.Empty;
        }
    }

    public sealed class OfferRoomChoicesAction : GameAction
    {
        public OfferRoomChoicesAction(IReadOnlyList<RoomKind> options)
        {
            Options = options == null ? new RoomKind[0] : new List<RoomKind>(options).ToArray();
        }

        public IReadOnlyList<RoomKind> Options { get; private set; }
        public override string ActionName { get { return "OfferRoomChoices"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<PendingChoiceModel>().OfferRooms(Options);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RoomChoicesOffered, context.ActionId, ActionName)
                    .WithAmount(Options.Count)
                    .WithMessage(FormatRooms(Options)));
        }

        private static string FormatRooms(IReadOnlyList<RoomKind> options)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < options.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(",");
                }

                builder.Append(options[i]);
            }

            return builder.ToString();
        }
    }

    public sealed class SelectRoomChoiceAction : GameAction
    {
        public SelectRoomChoiceAction(int optionIndex, RoomKind roomKind)
        {
            OptionIndex = optionIndex;
            RoomKind = roomKind;
        }

        public int OptionIndex { get; private set; }
        public RoomKind RoomKind { get; private set; }
        public override string ActionName { get { return "SelectRoomChoice"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<PendingChoiceModel>().SelectRoom(RoomKind);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.RoomSelected, context.ActionId, ActionName)
                    .WithAmount((int)RoomKind)
                    .WithDelta(OptionIndex)
                    .WithMessage(RoomKind.ToString()));
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
            result.AddFollowUp(new DeactivateOwnerEffectsAction(CardUid, "pickup"));
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
            : this(itemUid, null, null)
        {
        }

        public UseItemAction(int itemUid, IReadOnlyList<int> selectedCardUids)
            : this(itemUid, selectedCardUids, null)
        {
        }

        public UseItemAction(int itemUid, IReadOnlyList<int> selectedCardUids, string selectedOption)
        {
            ItemUid = itemUid;
            SelectedCardUids = selectedCardUids == null ? new int[0] : new List<int>(selectedCardUids).ToArray();
            SelectedOption = selectedOption ?? string.Empty;
        }

        public int ItemUid { get; private set; }
        public IReadOnlyList<int> SelectedCardUids { get; private set; }
        public string SelectedOption { get; private set; }
        public override string ActionName { get { return "UseItem"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var sourceDefId = string.Empty;
            CardInstance item;
            if (context.GetModel<CardRegistry>().TryGet(ItemUid, out item))
            {
                sourceDefId = item.DefId;
            }

            var itemUsed = new CoreGameEvent(CoreEventType.ItemUsed, context.ActionId, ActionName)
                .WithCard(ItemUid)
                .WithSource(sourceDefId, "use");
            if (SelectedCardUids.Count > 0)
            {
                itemUsed.WithTarget(SelectedCardUids[0]);
            }

            if (!string.IsNullOrEmpty(SelectedOption) || SelectedCardUids.Count > 0)
            {
                itemUsed.WithMessage(BuildSelectionMessage());
            }

            return new GameActionResult()
                .AddEvent(itemUsed);
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        private string BuildSelectionMessage()
        {
            var message = "option=" + SelectedOption + ";cards=";
            for (var i = 0; i < SelectedCardUids.Count; i++)
            {
                if (i > 0)
                {
                    message += ",";
                }

                message += SelectedCardUids[i].ToString();
            }

            return message;
        }
    }
}
