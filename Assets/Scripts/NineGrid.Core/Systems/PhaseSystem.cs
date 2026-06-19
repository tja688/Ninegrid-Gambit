using System.Collections.Generic;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IPhaseSystem : ISystem
    {
        GamePhase CurrentPhase { get; }
        IReadOnlyList<GameCommandKind> LegalCommands { get; }
        bool CanExecute(GameCommandKind command);
        CoreCommandResult StartNode(NodeDeckOptions options);
        CoreCommandResult Attack(SlotId targetSlot);
        CoreCommandResult PickupItem(SlotId targetSlot);
        CoreCommandResult ClickEmpty(SlotId targetSlot);
        CoreCommandResult UseItem(int itemUid);
        CoreCommandResult SelectReward(int optionIndex);
        CoreCommandResult SkipHelpChoice();
        CoreCommandResult SelectRoom(int optionIndex);
        CoreCommandResult EnterRoom();
    }

    public sealed class PhaseSystem : AbstractSystem, IPhaseSystem
    {
        private readonly List<GameCommandKind> mLegalCommands = new List<GameCommandKind>();

        public GamePhase CurrentPhase
        {
            get { return this.GetModel<RunModel>().Phase.Value; }
        }

        public IReadOnlyList<GameCommandKind> LegalCommands
        {
            get
            {
                RefreshLegalCommands();
                return mLegalCommands;
            }
        }

        protected override void OnInit()
        {
            RefreshLegalCommands();
        }

        public bool CanExecute(GameCommandKind command)
        {
            RefreshLegalCommands();
            return mLegalCommands.Contains(command);
        }

        public CoreCommandResult StartNode(NodeDeckOptions options)
        {
            if (!CanExecute(GameCommandKind.StartNode))
            {
                return Reject(GameCommandKind.StartNode, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            options = options ?? NodeDeckOptions.CreateDefaultBattle();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ClearPendingChoicesAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.BuildEnemyPool));
            pipeline.Enqueue(new SetupNodeDeckAction(options));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.ResetNode));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.DealOpeningCards));
            pipeline.Enqueue(new OpeningDealAction(options));
            pipeline.Enqueue(new FillEmptySlotsAction());
            pipeline.Enqueue(new NodeStartedAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.InteractionLoop));
            var resolved = pipeline.RunToCompletion();
            resolved += CompleteNodeIfCleared();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult Attack(SlotId targetSlot)
        {
            if (!CanExecute(GameCommandKind.Attack))
            {
                return Reject(GameCommandKind.Attack, "Command is not legal in phase " + CurrentPhase, targetSlot, 0);
            }

            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            if (!targetSlot.IsBoardSlot || targetSlot == board.AvatarSlot.Value)
            {
                return Reject(GameCommandKind.Attack, "Attack target is not a board card slot.", targetSlot, 0);
            }

            var targetUid = board.GetCardUid(targetSlot);
            if (targetUid == 0)
            {
                return Reject(GameCommandKind.Attack, "Attack target slot is empty.", targetSlot, 0);
            }

            var target = registry.Get(targetUid);
            if (target.Kind != CardKind.Monster)
            {
                return Reject(GameCommandKind.Attack, "Attack target is not a monster.", targetSlot, targetUid);
            }

            if (!this.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, targetSlot))
            {
                return Reject(GameCommandKind.Attack, "Attack target is outside interaction range.", targetSlot, targetUid);
            }

            if (!CanAttackTargetUnderRules(target))
            {
                return Reject(GameCommandKind.Attack, "Attack target is restricted by taunt.", targetSlot, targetUid);
            }

            var avatar = registry.Get(board.AvatarUid.Value);
            var statSystem = this.GetSystem<IStatSystem>();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            var startIndex = pipeline.EventLog.Entries.Count;
            var avatarFirstStrike = HasFirstStrike(statSystem, avatar);
            var targetFirstStrike = HasFirstStrike(statSystem, target);
            if (targetFirstStrike && !avatarFirstStrike)
            {
                pipeline.Enqueue(new DealDamageAction(target.Uid, avatar.Uid, GetAttackDamage(statSystem, target)));
                pipeline.Enqueue(new ConditionalDealDamageIfAliveAction(avatar.Uid, target.Uid, GetAttackDamage(statSystem, avatar)));
            }
            else
            {
                pipeline.Enqueue(new DealDamageAction(avatar.Uid, targetUid, GetAttackDamage(statSystem, avatar)));
                pipeline.Enqueue(new ConditionalDealDamageIfAliveAction(target.Uid, avatar.Uid, GetAttackDamage(statSystem, target)));
            }

            var resolved = pipeline.RunToCompletion();

            if (ContainsEventSince(startIndex, CoreEventType.CardKilled, targetUid))
            {
                resolved += ResolveInteractiveRotation();
            }

            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult PickupItem(SlotId targetSlot)
        {
            if (!CanExecute(GameCommandKind.PickupItem))
            {
                return Reject(GameCommandKind.PickupItem, "Command is not legal in phase " + CurrentPhase, targetSlot, 0);
            }

            var registry = this.GetModel<CardRegistry>();
            var board = this.GetModel<BoardModel>();
            if (!targetSlot.IsBoardSlot || targetSlot == board.AvatarSlot.Value)
            {
                return Reject(GameCommandKind.PickupItem, "Pickup target is not a board card slot.", targetSlot, 0);
            }

            if (!this.GetSystem<IBoardSystem>().AreAdjacent(board.AvatarSlot.Value, targetSlot))
            {
                return Reject(GameCommandKind.PickupItem, "Pickup target is outside interaction range.", targetSlot, 0);
            }

            var cardUid = board.GetCardUid(targetSlot);
            if (cardUid == 0)
            {
                return Reject(GameCommandKind.PickupItem, "Pickup target slot is empty.", targetSlot, 0);
            }

            var card = registry.Get(cardUid);
            if (card.Kind == CardKind.Monster)
            {
                return Reject(GameCommandKind.PickupItem, "Monsters must be attacked, not picked up.", targetSlot, cardUid);
            }

            var shouldRotate = CurrentPhase == GamePhase.InteractionLoop;
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new PickupCardAction(cardUid));
            var resolved = pipeline.RunToCompletion();
            if (shouldRotate)
            {
                resolved += ResolveInteractiveRotation();
            }

            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult ClickEmpty(SlotId targetSlot)
        {
            if (!CanExecute(GameCommandKind.ClickEmpty))
            {
                return Reject(GameCommandKind.ClickEmpty, "Command is not legal in phase " + CurrentPhase, targetSlot, 0);
            }

            var board = this.GetModel<BoardModel>();
            if (!targetSlot.IsBoardSlot || targetSlot == board.AvatarSlot.Value || !board.IsEmpty(targetSlot))
            {
                return Reject(GameCommandKind.ClickEmpty, "Clicked slot is not empty.", targetSlot, 0);
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ClickEmptySlotAction(targetSlot));
            var resolved = pipeline.RunToCompletion();
            resolved += ResolveInteractiveRotation();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult UseItem(int itemUid)
        {
            if (!CanExecute(GameCommandKind.UseItem))
            {
                return Reject(GameCommandKind.UseItem, "Command is not legal in phase " + CurrentPhase, SlotId.None, itemUid);
            }

            if (itemUid <= 0)
            {
                return Reject(GameCommandKind.UseItem, "Item uid is invalid.", SlotId.None, itemUid);
            }

            var registry = this.GetModel<CardRegistry>();
            CardInstance card;
            if (!registry.TryGet(itemUid, out card))
            {
                return Reject(GameCommandKind.UseItem, "Item card uid does not exist.", SlotId.None, itemUid);
            }

            if (card.Zone.Value != ZoneId.ItemSlots)
            {
                return Reject(GameCommandKind.UseItem, "Item is not in item slots.", SlotId.None, itemUid);
            }

            if (!IsRegisteredInItemSlots(this.GetModel<DeckModel>(), itemUid))
            {
                return Reject(GameCommandKind.UseItem, "Item is not registered in item slots.", SlotId.None, itemUid);
            }

            if (!IsUsableItemKind(card.Kind))
            {
                return Reject(GameCommandKind.UseItem, "Card is not a usable item.", SlotId.None, itemUid);
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new UseItemAction(itemUid));
            var resolved = pipeline.RunToCompletion();
            resolved += CompleteNodeIfCleared();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult SelectReward(int optionIndex)
        {
            if (!CanExecute(GameCommandKind.SelectReward))
            {
                return Reject(GameCommandKind.SelectReward, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var pending = this.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value != PendingChoiceKind.Reward)
            {
                return Reject(GameCommandKind.SelectReward, "No pending reward choice.", SlotId.None, 0);
            }

            if (optionIndex < 0 || optionIndex >= pending.RewardOptions.Count)
            {
                return Reject(GameCommandKind.SelectReward, "Reward option index is out of range.", SlotId.None, 0);
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new GrantRewardChoiceAction(pending.RewardOptions[optionIndex], optionIndex));
            pipeline.Enqueue(new ClearPendingRewardChoiceAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomChoice));
            pipeline.Enqueue(new OfferRoomChoicesAction(RollRoomChoicesOrFallback()));
            var resolved = pipeline.RunToCompletion();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult SkipHelpChoice()
        {
            if (!CanExecute(GameCommandKind.SkipHelpChoice))
            {
                return Reject(GameCommandKind.SkipHelpChoice, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var resolved = this.GetSystem<IEconomySystem>().AwardSkipHelpChoice();
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new SkipRewardChoiceAction());
            pipeline.Enqueue(new ClearPendingRewardChoiceAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomChoice));
            pipeline.Enqueue(new OfferRoomChoicesAction(RollRoomChoicesOrFallback()));
            resolved += pipeline.RunToCompletion();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult SelectRoom(int optionIndex)
        {
            if (!CanExecute(GameCommandKind.SelectRoom))
            {
                return Reject(GameCommandKind.SelectRoom, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var pending = this.GetModel<PendingChoiceModel>();
            if (pending.Kind.Value != PendingChoiceKind.Room)
            {
                return Reject(GameCommandKind.SelectRoom, "No pending room choice.", SlotId.None, 0);
            }

            if (optionIndex < 0 || optionIndex >= pending.RoomOptions.Count)
            {
                return Reject(GameCommandKind.SelectRoom, "Room option index is out of range.", SlotId.None, 0);
            }

            var room = pending.RoomOptions[optionIndex];
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new SelectRoomChoiceAction(optionIndex, room));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RoomEvent));
            var resolved = pipeline.RunToCompletion();
            resolved += this.GetSystem<IEconomySystem>().SettleUnusedHelpCards();
            return CoreCommandResult.Accept(resolved);
        }

        public CoreCommandResult EnterRoom()
        {
            if (!CanExecute(GameCommandKind.EnterRoom))
            {
                return Reject(GameCommandKind.EnterRoom, "Command is not legal in phase " + CurrentPhase, SlotId.None, 0);
            }

            var room = this.GetModel<PendingChoiceModel>().SelectedRoom.Value;
            if (room == RoomKind.None)
            {
                return Reject(GameCommandKind.EnterRoom, "No selected room to enter.", SlotId.None, 0);
            }

            var resolved = this.GetSystem<IRewardSystem>().ResolveRoom(room);
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ClearPendingChoicesAction());
            pipeline.Enqueue(new AdvanceNodeAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.NodeCompleted));
            resolved += pipeline.RunToCompletion();
            return CoreCommandResult.Accept(resolved);
        }

        private static bool IsRegisteredInItemSlots(DeckModel deck, int itemUid)
        {
            var itemSlots = deck.ItemSlotUids;
            for (var i = 0; i < itemSlots.Count; i++)
            {
                if (itemSlots[i] == itemUid)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUsableItemKind(CardKind kind)
        {
            return kind == CardKind.Item || kind == CardKind.HelpCard;
        }

        private int ResolveInteractiveRotation()
        {
            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ModifyInteractionCountAction(1));
            pipeline.Enqueue(new RotateBoardClockwiseAction());
            pipeline.Enqueue(new FillEmptySlotsAction());
            var resolved = pipeline.RunToCompletion();
            resolved += CompleteNodeIfCleared();
            return resolved;
        }

        private int CompleteNodeIfCleared()
        {
            if (CurrentPhase != GamePhase.InteractionLoop)
            {
                return 0;
            }

            if (!this.GetSystem<IDeckSystem>().IsNodeCleared())
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.ClearCheck));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.NodeCompleted));
            pipeline.Enqueue(new NodeCompletedAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RewardItemChoice));
            pipeline.Enqueue(new OfferRewardChoiceAction("help.choice", 3));
            return pipeline.RunToCompletion();
        }

        private IReadOnlyList<RoomKind> RollRoomChoicesOrFallback()
        {
            var choices = this.GetSystem<IRewardSystem>().RollRoomChoices(2);
            if (choices.Count > 0)
            {
                return choices;
            }

            return new[]
            {
                RoomKind.Gold,
                RoomKind.Fountain
            };
        }

        private CoreCommandResult Reject(GameCommandKind command, string reason, SlotId slot, int cardUid)
        {
            this.GetSystem<IActionPipelineSystem>().RejectCommand(command, reason, slot, cardUid);
            return CoreCommandResult.Reject(reason);
        }

        private bool ContainsEventSince(int startIndex, CoreEventType eventType, int cardUid)
        {
            var entries = this.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == eventType && entry.CardUid == cardUid)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFirstStrike(IStatSystem statSystem, CardInstance card)
        {
            return statSystem.EvaluateRule(RuleId.FirstStrike, 0f, statSystem.CreateContext(card)) > 0f;
        }

        private bool CanAttackTargetUnderRules(CardInstance target)
        {
            var statSystem = this.GetSystem<IStatSystem>();
            var restrictedUid = (int)System.Math.Round(statSystem.EvaluateRule(RuleId.AttackTargetRestriction, 0f, statSystem.CreateContext(target)));
            return restrictedUid == 0 || restrictedUid == target.Uid;
        }

        private static int GetAttackDamage(IStatSystem statSystem, CardInstance card)
        {
            var damage = statSystem.GetEffectiveInt(card, StatId.Attack);
            if (card.Kind == CardKind.Monster)
            {
                damage += (int)System.Math.Round(statSystem.EvaluateRule(RuleId.EnemyAttackDelta, 0f, statSystem.CreateContext(card)));
            }

            return System.Math.Max(0, damage);
        }

        private void RefreshLegalCommands()
        {
            mLegalCommands.Clear();
            switch (CurrentPhase)
            {
                case GamePhase.None:
                case GamePhase.BuildEnemyPool:
                case GamePhase.NodeCompleted:
                    mLegalCommands.Add(GameCommandKind.StartNode);
                    break;
                case GamePhase.InteractionLoop:
                    mLegalCommands.Add(GameCommandKind.Attack);
                    mLegalCommands.Add(GameCommandKind.PickupItem);
                    mLegalCommands.Add(GameCommandKind.ClickEmpty);
                    mLegalCommands.Add(GameCommandKind.UseItem);
                    break;
                case GamePhase.RewardItemChoice:
                    mLegalCommands.Add(GameCommandKind.SelectReward);
                    mLegalCommands.Add(GameCommandKind.SkipHelpChoice);
                    mLegalCommands.Add(GameCommandKind.PickupItem);
                    mLegalCommands.Add(GameCommandKind.UseItem);
                    break;
                case GamePhase.RoomChoice:
                    mLegalCommands.Add(GameCommandKind.SelectRoom);
                    mLegalCommands.Add(GameCommandKind.PickupItem);
                    mLegalCommands.Add(GameCommandKind.UseItem);
                    break;
                case GamePhase.RoomEvent:
                    mLegalCommands.Add(GameCommandKind.EnterRoom);
                    break;
            }
        }
    }
}
