using System;
using System.Collections.Generic;
using System.Text;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

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

    public sealed class RevealAvatarAction : GameAction
    {
        public override string ActionName { get { return "RevealAvatar"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var board = context.GetModel<BoardModel>();
            var registry = context.GetModel<CardRegistry>();
            int avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0 || !registry.TryGet(avatarUid, out var avatar))
            {
                return GameActionResult.Empty;
            }

            return new GameActionResult()
                .AddWithFaceAbsolutes(
                    context,
                    avatar,
                    new CoreGameEvent(CoreEventType.AvatarAppeared, context.ActionId, ActionName)
                        .WithCard(avatarUid)
                        .WithSlots(SlotId.None, board.AvatarSlot.Value));
        }
    }

    public sealed class ResetCurrentArmorAction : GameAction
    {
        public override string ActionName { get { return "ResetCurrentArmor"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var statSystem = context.GetSystem<IStatSystem>();
            var avatarUid = board.AvatarUid.Value;
            CardInstance avatar;
            if (avatarUid > 0 && registry.TryGet(avatarUid, out avatar))
            {
                StatArmorUtility.ResetCurrentToEffective(statSystem, avatar);
            }

            return GameActionResult.Empty;
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
            var completedCampaign = run.AdvanceNode();
            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.NodeAdvanced, context.ActionId, ActionName)
                    .WithAmount(run.NodeIndex.Value)
                    .WithDelta(run.Floor.Value)
                    .WithMessage("floor=" + run.Floor.Value + ";node=" + run.NodeIndex.Value));

            if (completedCampaign && run.Phase.Value != GamePhase.Victory)
            {
                var previous = run.Phase.Value;
                run.SetPhase(GamePhase.Victory);
                result.AddEvent(new CoreGameEvent(CoreEventType.PhaseChanged, context.ActionId, ActionName)
                    .WithAmount((int)GamePhase.Victory)
                    .WithDelta((int)previous)
                    .WithMessage(previous + "->" + GamePhase.Victory));
            }

            return result;
        }
    }

    public sealed class DefeatIfAvatarDeadAction : GameAction
    {
        public override string ActionName { get { return "DefeatIfAvatarDead"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var run = context.GetModel<RunModel>();
            if (run.Phase.Value == GamePhase.Victory || run.Phase.Value == GamePhase.Defeat)
            {
                return GameActionResult.Empty;
            }

            var board = context.GetModel<BoardModel>();
            CardInstance avatar;
            if (!context.GetModel<CardRegistry>().TryGet(board.AvatarUid.Value, out avatar))
            {
                return GameActionResult.Empty;
            }

            if ((int)Math.Round(avatar.Stats.GetBase(StatId.Hp)) > 0)
            {
                return GameActionResult.Empty;
            }

            return new GameActionResult().AddFollowUp(new ChangePhaseAction(GamePhase.Defeat));
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

    /// <summary>
    /// 写入攻击模式行动倒计时并广播卡面绝对值（ADR-0005 / #81）。
    /// </summary>
    public sealed class SetAttackPatternCountdownAction : GameAction
    {
        public SetAttackPatternCountdownAction(int cardUid, int remaining)
        {
            CardUid = cardUid;
            Remaining = remaining < 0 ? 0 : remaining;
        }

        public int CardUid { get; private set; }
        public int Remaining { get; private set; }
        public override string ActionName { get { return "SetAttackPatternCountdown"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            CardInstance card;
            if (!context.GetModel<CardRegistry>().TryGet(CardUid, out card) || card == null)
            {
                return GameActionResult.Empty;
            }

            var previous = card.Counters.Get(CoreCounterKeys.AttackPatternCountdown);
            card.Counters.Set(CoreCounterKeys.AttackPatternCountdown, Remaining);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.ActionCountdownChanged, context.ActionId, ActionName)
                    .WithCard(card.Uid)
                    .WithDelta(Remaining - previous)
                    .WithResultValue(Remaining));
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

            if (ShouldAcquireHelpCardToItemSlots(card))
            {
                deck.AddToItemSlots(card);

                return new GameActionResult()
                    .AddEvent(new CoreGameEvent(CoreEventType.ItemPicked, context.ActionId, ActionName)
                        .WithCard(CardUid)
                        .WithSlots(fromSlot, SlotId.None));
            }

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

        private static bool ShouldAcquireHelpCardToItemSlots(CardInstance card)
        {
            return card != null
                && card.Kind == CardKind.HelpCard
                && card.Counters.Get(CoreCounterKeys.GoldReward) == 0;
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

    /// <summary>非战斗 Avatar 单步邻格迁移（跳格路径的一跳）。</summary>
    public sealed class MoveAvatarAction : GameAction
    {
        public MoveAvatarAction(SlotId toSlot)
        {
            ToSlot = toSlot;
        }

        public SlotId ToSlot { get; private set; }
        public override string ActionName { get { return "MoveAvatar"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var board = context.GetModel<BoardModel>();
            var registry = context.GetModel<CardRegistry>();
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0 || !registry.TryGet(avatarUid, out var avatar))
            {
                return GameActionResult.Empty;
            }

            var fromSlot = board.AvatarSlot.Value;
            board.SetAvatar(avatar, ToSlot);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.AvatarMoved, context.ActionId, ActionName)
                    .WithCard(avatarUid)
                    .WithSlots(fromSlot, ToSlot));
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
