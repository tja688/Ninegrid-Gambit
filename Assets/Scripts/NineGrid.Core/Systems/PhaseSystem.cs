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

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new PickupCardAction(cardUid));
            var resolved = pipeline.RunToCompletion();
            resolved += ResolveInteractiveRotation();
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
            if (!this.GetSystem<IDeckSystem>().IsNodeCleared())
            {
                return 0;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.ClearCheck));
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.NodeCompleted));
            pipeline.Enqueue(new NodeCompletedAction());
            pipeline.Enqueue(new ChangePhaseAction(GamePhase.RewardItemChoice));
            return pipeline.RunToCompletion();
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
                case GamePhase.RewardItemChoice:
                    mLegalCommands.Add(GameCommandKind.StartNode);
                    break;
                case GamePhase.InteractionLoop:
                    mLegalCommands.Add(GameCommandKind.Attack);
                    mLegalCommands.Add(GameCommandKind.PickupItem);
                    mLegalCommands.Add(GameCommandKind.ClickEmpty);
                    mLegalCommands.Add(GameCommandKind.UseItem);
                    break;
            }
        }
    }
}
