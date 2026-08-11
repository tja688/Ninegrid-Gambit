using System;
using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
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
            var battle = context.GetModel<BattleContextModel>();
            var deactivatedEffects = DeactivateNonPersistentRuntimeEffects(context, registry, deck);

            board.ClearBoardCards();
            deck.ClearBattleZones();
            RemoveNonPersistentCards(registry, deck);

            // #112 / ADR-0026：每节点重置离开机关进度；开局真怪 UID 在造卡后登记（N 不含机关）。
            battle.ResetLeaveTrapProgress();

            var result = new GameActionResult();
            for (var i = 0; i < Options.PlayerCards.Count; i++)
            {
                var card = CreateConfiguredCard(context, Options.PlayerCards[i], registry);
                if (card.Kind == CardKind.HelpCard)
                {
                    card.Counters.Set(CoreCounterKeys.PlayerSideDeck, 1);
                }

                deck.AddToPlayerCardPool(card);
                result.AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(CoreEventType.CardSpawned, context.ActionId, ActionName)
                        .WithCard(card.Uid)
                        .WithMessage(card.DefId)
                        .WithSource(card.DefId, "setupNodeDeck"));
            }

            var openingLeaveTrap = false;
            for (var i = 0; i < Options.EnemyCards.Count; i++)
            {
                var draft = Options.EnemyCards[i];
                var card = CreateConfiguredCard(context, draft, registry);
                deck.AddToEnemyCardPool(card);
                if (string.Equals(draft.DefId, RegularTrapPool.LeaveTrapDefId, StringComparison.Ordinal))
                {
                    openingLeaveTrap = true;
                }

                if (CardCombatRules.IsTrueMonster(card.Kind))
                {
                    var isBoss = card.Counters.Get(CoreCounterKeys.Boss) > 0;
                    battle.RegisterOpeningTrueMonster(card.Uid, isBoss);
                }

                result.AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(CoreEventType.CardSpawned, context.ActionId, ActionName)
                        .WithCard(card.Uid)
                        .WithMessage(card.DefId)
                        .WithSource(card.DefId, "setupNodeDeck"));
            }

            if (openingLeaveTrap)
            {
                battle.MarkLeaveTrapInserted();
            }

            var deactivation = BuildDeactivationResult(context, deactivatedEffects);
            if (deactivation.Events != null)
            {
                for (var i = 0; i < deactivation.Events.Count; i++)
                {
                    result.AddEvent(deactivation.Events[i]);
                }
            }

            return result;
        }

        private static CardInstance CreateConfiguredCard(GameActionContext context, CardDraft draft, CardRegistry registry)
        {
            var card = draft.Create(registry);
            context.GetSystem<IContentSystem>().ApplyContentToCard(card);
            return card;
        }

        private static void RemoveNonPersistentCards(CardRegistry registry, DeckModel deck)
        {
            var keep = new HashSet<int>();
            var itemSlots = deck.ItemSlotUids;
            for (var i = 0; i < itemSlots.Count; i++)
            {
                keep.Add(itemSlots[i]);
            }

            var removeUids = new List<int>();
            foreach (var pair in registry.Cards)
            {
                if (pair.Value.Kind == CardKind.Avatar || keep.Contains(pair.Key))
                {
                    continue;
                }

                removeUids.Add(pair.Key);
            }

            for (var i = 0; i < removeUids.Count; i++)
            {
                registry.Remove(removeUids[i]);
            }
        }

        private static List<DeactivatedEffectRecord> DeactivateNonPersistentRuntimeEffects(
            GameActionContext context,
            CardRegistry registry,
            DeckModel deck)
        {
            var keep = new HashSet<int>();
            var itemSlots = deck.ItemSlotUids;
            for (var i = 0; i < itemSlots.Count; i++)
            {
                keep.Add(itemSlots[i]);
            }

            var result = new List<DeactivatedEffectRecord>();
            var content = context.GetSystem<IContentSystem>();
            foreach (var pair in registry.Cards)
            {
                if (pair.Value.Kind == CardKind.Avatar || keep.Contains(pair.Key))
                {
                    continue;
                }

                var ids = content.DeactivateRuntimeEffectsByOwner(pair.Key);
                for (var i = 0; i < ids.Count; i++)
                {
                    result.Add(new DeactivatedEffectRecord(pair.Key, ids[i]));
                }
            }

            return result;
        }

        private static GameActionResult BuildDeactivationResult(GameActionContext context, IReadOnlyList<DeactivatedEffectRecord> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return GameActionResult.Empty;
            }

            var result = new GameActionResult();
            for (var i = 0; i < effects.Count; i++)
            {
                result.AddEvent(new CoreGameEvent(CoreEventType.EffectDeactivated, context.ActionId, "SetupNodeDeck")
                    .WithCard(effects[i].OwnerUid)
                    .WithMessage(effects[i].InstanceId)
                    .WithSource(string.Empty, "nodeReset"));
            }

            return result;
        }

        private sealed class DeactivatedEffectRecord
        {
            public DeactivatedEffectRecord(int ownerUid, string instanceId)
            {
                OwnerUid = ownerUid;
                InstanceId = instanceId ?? string.Empty;
            }

            public int OwnerUid { get; private set; }
            public string InstanceId { get; private set; }
        }
    }

    public sealed class OpeningDealAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal,
            TriggerPoint.OnEnter
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
            var board = context.GetModel<BoardModel>();
            var rng = context.Architecture.GetUtility<IRngUtility>();

            // Step 1: place player-side cards directly on the board (soft guarantee).
            var playerPlaced = PlacePlayerCardsDirectly(deck, board, registry, rng, Options.PlayerOpeningCount);

            // Step 2: select enemy cards.
            var selected = new List<int>();
            SelectCards(selected, deck.EnemyCardPoolUids, registry, Options.EnemyOpeningCount, Options.RequireElite);

            for (var i = 0; i < selected.Count; i++)
            {
                var card = registry.Get(selected[i]);
                deck.AddToDrawPile(card, false);
            }

            // RUL_发牌 step 3: remaining staging pools merge into runtime draw pile (not in-node reserve).
            DrainStagingPool(deck, registry, deck.PlayerCardPoolUids);
            DrainStagingPool(deck, registry, deck.EnemyCardPoolUids);

            ShuffleDrawPile(deck, rng);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                    .WithAmount(playerPlaced + selected.Count)
                    .WithMessage("opening"));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        private static int PlacePlayerCardsDirectly(
            DeckModel deck,
            BoardModel board,
            CardRegistry registry,
            IRngUtility rng,
            int maxCount)
        {
            var poolUids = new List<int>(deck.PlayerCardPoolUids);
            if (poolUids.Count == 0 || maxCount <= 0)
            {
                return 0;
            }

            var availableSlots = new List<SlotId>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot != board.AvatarSlot.Value && board.IsEmpty(slot))
                {
                    availableSlots.Add(slot);
                }
            }

            var placed = 0;
            var count = poolUids.Count < maxCount ? poolUids.Count : maxCount;
            for (var i = 0; i < count && availableSlots.Count > 0; i++)
            {
                var card = registry.Get(poolUids[i]);
                var slotIdx = rng.Range(0, availableSlots.Count);
                var slot = availableSlots[slotIdx];
                availableSlots.RemoveAt(slotIdx);

                deck.RemoveUid(card.Uid);
                board.PlaceCard(card, slot);
                placed++;
            }

            return placed;
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
            SlotId.Board(1),
            SlotId.Board(2),
            SlotId.Board(3),
            SlotId.Board(6),
            SlotId.Board(9),
            SlotId.Board(8),
            SlotId.Board(7),
            SlotId.Board(4),
            SlotId.Board(5)
        };

        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal,
            TriggerPoint.OnEnter
        };

        public override string ActionName { get { return "FillEmptySlots"; } }

        public static IReadOnlyList<SlotId> FillOrder { get { return sFillOrder; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();
            var run = context.GetModel<RunModel>();
            var result = new GameActionResult();
            var filled = 0;
            IReadOnlyList<SlotId> fillOrder = sFillOrder;

            if (run.Phase.Value == GamePhase.DealOpeningCards && board.AvatarUid.Value > 0)
            {
                var avatar = registry.Get(board.AvatarUid.Value);
                result.AddWithFaceAbsolutes(
                    context,
                    avatar,
                    new CoreGameEvent(CoreEventType.AvatarAppeared, context.ActionId, ActionName)
                        .WithCard(board.AvatarUid.Value)
                        .WithSlots(SlotId.None, board.AvatarSlot.Value));
            }

            for (var i = 0; i < fillOrder.Count; i++)
            {
                var slot = fillOrder[i];
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

                result.AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
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
            TriggerPoint.OnMove,
            TriggerPoint.OnMoveToSlot
        };

        public static IReadOnlyList<SlotId> ClockwisePath
        {
            get { return sClockwisePath; }
        }

        public RotateBoardClockwiseAction()
            : this(true)
        {
        }

        public RotateBoardClockwiseAction(bool clockwise)
            : this(clockwise, null, null)
        {
        }

        public RotateBoardClockwiseAction(bool clockwise, string sourceDefId, string cause)
        {
            Clockwise = clockwise;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public bool Clockwise { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
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
                var toIndex = Clockwise ? (i + 1) % sClockwisePath.Length : (i + sClockwisePath.Length - 1) % sClockwisePath.Length;
                var toSlot = sClockwisePath[toIndex];
                board.PlaceCard(registry.Get(uids[i]), toSlot);
                result.AddEvent(new CoreGameEvent(CoreEventType.CardMoved, context.ActionId, ActionName)
                    .WithCard(uids[i])
                    .WithSlots(fromSlot, toSlot)
                    .WithSource(SourceDefId, Cause));
            }

            result.AddEvent(new CoreGameEvent(CoreEventType.BoardRotated, context.ActionId, ActionName)
                .WithAmount(Clockwise ? 1 : -1)
                .WithMessage(Clockwise ? "clockwise" : "counterClockwise")
                .WithSource(SourceDefId, Cause));
            CardRhythmMoveTicks.AppendFromMovedEvents(result, context, result.Events);
            CardFaceEventValues.AppendConditionalPermanentAttackFaceCommitsForBoard(
                result,
                context,
                ActionName);
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
            TriggerPoint.OnMove,
            TriggerPoint.OnMoveToSlot
        };

        public SwapBoardSlotsAction(SlotId left, SlotId right)
            : this(left, right, null, null)
        {
        }

        public SwapBoardSlotsAction(SlotId left, SlotId right, string sourceDefId, string cause)
        {
            Left = left;
            Right = right;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public SlotId Left { get; private set; }
        public SlotId Right { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "SwapBoardSlots"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var leftUid = board.GetCardUid(Left);
            var rightUid = board.GetCardUid(Right);
            board.ClearSlot(Left);
            board.ClearSlot(Right);

            var result = new GameActionResult();
            if (leftUid != 0)
            {
                board.PlaceCard(registry.Get(leftUid), Right);
                result.AddEvent(new CoreGameEvent(CoreEventType.CardMoved, context.ActionId, ActionName)
                    .WithCard(leftUid)
                    .WithSlots(Left, Right)
                    .WithSource(SourceDefId, Cause));
            }

            if (rightUid != 0)
            {
                board.PlaceCard(registry.Get(rightUid), Left);
                result.AddEvent(new CoreGameEvent(CoreEventType.CardMoved, context.ActionId, ActionName)
                    .WithCard(rightUid)
                    .WithSlots(Right, Left)
                    .WithSource(SourceDefId, Cause));
            }

            result.AddEvent(new CoreGameEvent(CoreEventType.CardSwapped, context.ActionId, ActionName)
                    .WithSlots(Left, Right)
                    .WithSource(SourceDefId, Cause));
            CardRhythmMoveTicks.AppendFromMovedEvents(result, context, result.Events);
            CardFaceEventValues.AppendConditionalPermanentAttackFaceCommitsForBoard(
                result,
                context,
                ActionName);
            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }
}
