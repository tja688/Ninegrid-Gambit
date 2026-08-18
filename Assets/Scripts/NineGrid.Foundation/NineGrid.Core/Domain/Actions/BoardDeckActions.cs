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
            var playerPlaced = PlaceCardsDirectly(deck, board, registry, rng, deck.PlayerCardPoolUids, Options.PlayerOpeningCount);

            // Step 2: select enemy cards.
            // 设计案发牌机制：怪物侧选出的卡直接放置上盘（存在层主则必定抽出）。
            // 层主对战时所有层主必须第一波发牌上场，不得洗入抽牌堆随缘补出（ADR-0026「开局编入的层主」以本关首次发牌在场为前提）。
            var selected = new List<int>();
            SelectCards(selected, deck.EnemyCardPoolUids, registry, Options.EnemyOpeningCount, Options.RequireElite);
            var enemyPlaced = PlaceCardsDirectly(deck, board, registry, rng, selected, selected.Count);

            // 盘面放不下时（理论不可达）剩余选中卡退回抽牌堆，避免吞卡。
            for (var i = enemyPlaced; i < selected.Count; i++)
            {
                var card = registry.Get(selected[i]);
                deck.AddToDrawPile(card, false);
            }

            // RUL_发牌 step 3: remaining staging pools merge into runtime draw pile (not in-node reserve).
            DrainStagingPool(deck, registry, deck.PlayerCardPoolUids);
            DrainStagingPool(deck, registry, deck.EnemyCardPoolUids);

            // 教学关卡受控发牌：保持装填顺序，跳过洗牌与离开机关落点重排。
            if (!Options.PreserveDealOrder)
            {
                ShuffleDrawPile(deck, rng);
                LeaveTrapDrawPileRules.EnsureInSecondHalf(deck, registry, rng);
            }

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                    .WithAmount(playerPlaced + enemyPlaced)
                    .WithMessage("opening"));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        private static int PlaceCardsDirectly(
            DeckModel deck,
            BoardModel board,
            CardRegistry registry,
            IRngUtility rng,
            IReadOnlyList<int> uids,
            int maxCount)
        {
            if (uids.Count == 0 || maxCount <= 0)
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
            // 拷贝入参：玩家侧传的是活池列表，RemoveUid 会边放边变（原 PlacePlayerCardsDirectly 同款保护）。
            var orderedUids = new List<int>(uids.Count);
            for (var i = 0; i < uids.Count; i++)
            {
                orderedUids.Add(uids[i]);
            }

            var count = orderedUids.Count < maxCount ? orderedUids.Count : maxCount;
            for (var i = 0; i < count && availableSlots.Count > 0; i++)
            {
                var card = registry.Get(orderedUids[i]);
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

            if (requireElite)
            {
                // 层主（精英）全部必选：层主对战时所有层主必须第一波发牌上场。
                for (var i = 0; i < pool.Count; i++)
                {
                    var card = registry.Get(pool[i]);
                    if (card.Counters.Get(CoreCounterKeys.Elite) > 0)
                    {
                        selected.Add(card.Uid);
                    }
                }
            }

            for (var i = 0; i < pool.Count && selected.Count < count; i++)
            {
                if (!selected.Contains(pool[i]))
                {
                    selected.Add(pool[i]);
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

    /// <summary>
    /// 离开机关在抽牌堆中的落点契约（ADR-0026）：普通房开局编入后必在后半段；层主房击破层主后置顶。
    /// </summary>
    internal static class LeaveTrapDrawPileRules
    {
        public static void EnsureInSecondHalf(DeckModel deck, CardRegistry registry, IRngUtility rng)
        {
            if (deck == null || registry == null || rng == null)
            {
                return;
            }

            var pile = deck.DrawPileUids;
            var count = pile.Count;
            if (count <= 1)
            {
                return;
            }

            var secondHalfStart = (count + 1) / 2;
            if (secondHalfStart >= count)
            {
                return;
            }

            var leaveIndex = -1;
            var leaveUid = 0;
            for (var i = 0; i < count; i++)
            {
                var card = registry.Get(pile[i]);
                if (string.Equals(card.DefId, RegularTrapPool.LeaveTrapDefId, StringComparison.Ordinal))
                {
                    leaveIndex = i;
                    leaveUid = card.Uid;
                    break;
                }
            }

            if (leaveUid == 0)
            {
                return;
            }

            var targetIndex = leaveIndex >= secondHalfStart
                ? leaveIndex
                : rng.Range(secondHalfStart, count);
            if (targetIndex == leaveIndex)
            {
                return;
            }

            var ordered = new List<int>(pile);
            ordered.RemoveAt(leaveIndex);
            var insertIndex = leaveIndex < targetIndex ? targetIndex - 1 : targetIndex;
            ordered.Insert(insertIndex, leaveUid);
            deck.ReorderDrawPile(ordered);
        }
    }

    /// <summary>
    /// Top=false 洗入语义：只移动刚插入的卡到随机下标（非空排除 slot 0），不整堆 Fisher–Yates 重洗。
    /// 与表现层 <c>RandomInsertIndex</c> / ADR-0034 抽牌堆视觉序对账一致。
    /// </summary>
    internal static class DrawPileInsertRules
    {
        public static void RandomizeNonTopInsert(DeckModel deck, int cardUid, IRngUtility rng)
        {
            if (deck == null || rng == null || cardUid <= 0)
            {
                return;
            }

            var pile = deck.DrawPileUids;
            var count = pile.Count;
            if (count <= 1)
            {
                return;
            }

            var currentIndex = -1;
            for (var i = 0; i < count; i++)
            {
                if (pile[i] == cardUid)
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                return;
            }

            var targetIndex = rng.Range(1, count);
            if (targetIndex == currentIndex)
            {
                return;
            }

            var ordered = new List<int>(pile);
            ordered.RemoveAt(currentIndex);
            ordered.Insert(targetIndex, cardUid);
            deck.ReorderDrawPile(ordered);
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
            SlotId.Board(4)
        };

        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal,
            TriggerPoint.OnEnter
        };

        /// <summary>机关效果（滚石等）移除卡造成的空位补牌事件 cause；捕熊陷阱等以该 cause 排除响应。</summary>
        public const string TrapVacatedRefillCause = "refillAfterTrapRemoval";

        public FillEmptySlotsAction(SlotId prioritySlot = default)
        {
            PrioritySlot = prioritySlot;
        }

        public SlotId PrioritySlot { get; private set; }

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

            if (PrioritySlot.IsBoardSlot && !PrioritySlot.IsCenter)
            {
                var list = new List<SlotId>(sFillOrder.Length + 1) { PrioritySlot };
                for (var i = 0; i < sFillOrder.Length; i++)
                {
                    if (sFillOrder[i] != PrioritySlot)
                    {
                        list.Add(sFillOrder[i]);
                    }
                }

                fillOrder = list;
            }

            var isSuspended = context.GetSystem<IBoardStabilizationSystem>()?.IsRefillSuspended ?? false;

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

            // 两遍填充：先正常空位，后机关效果空位（trap-vacated）。
            // 机关效果（滚石等）移除卡造成的空位补牌不算「补牌触发」事件：捕熊陷阱等通过
            // EventFilterExcludeCause(cause=refillAfterTrapRemoval) 不响应；且两遍分派保证
            // 机关空位的 CardDealt 事件与正常补牌同批时也保持语义正确（事件携带 cause）。
            for (var pass = 0; pass < 2; pass++)
            {
                var trapVacatedPass = pass == 1;
                var pileEmpty = false;
                for (var i = 0; i < fillOrder.Count; i++)
                {
                    var slot = fillOrder[i];
                    if (slot.IsCenter || slot == board.AvatarSlot.Value || !board.IsEmpty(slot))
                    {
                        continue;
                    }

                    if (trapVacatedPass != board.IsTrapVacated(slot))
                    {
                        continue;
                    }

                    int uid;
                    if (!deck.TryPeekDrawPile(out uid))
                    {
                        pileEmpty = true;
                        break;
                    }

                    var card = registry.Get(uid);
                    deck.RemoveUid(uid);
                    board.PlaceCard(card, slot);
                    filled++;

                    var dealt = new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                        .WithCard(uid)
                        .WithSlots(SlotId.None, slot)
                        .WithAmount(filled);
                    if (trapVacatedPass)
                    {
                        dealt = dealt.WithSource(string.Empty, TrapVacatedRefillCause);
                    }

                    result.AddWithFaceAbsolutes(context, card, dealt);

                    if (isSuspended && PrioritySlot.IsBoardSlot && slot == PrioritySlot)
                    {
                        pileEmpty = true;
                        break;
                    }
                }

                if (pileEmpty)
                {
                    break;
                }
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

        private static readonly TriggerPoint[] sPreTriggers =
        {
            TriggerPoint.BeforeAction,
            TriggerPoint.BeforeBoardMotion
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

            // ADR-0056 / #217: 搬完卡表后，若 Avatar 占格在环上，沿同一环序搬走 Avatar，并发出占格变化
            var avatarFromSlot = board.AvatarSlot.Value;
            var avatarRingIndex = -1;
            for (var i = 0; i < sClockwisePath.Length; i++)
            {
                if (sClockwisePath[i] == avatarFromSlot)
                {
                    avatarRingIndex = i;
                    break;
                }
            }

            if (avatarRingIndex >= 0 && board.AvatarUid.Value > 0)
            {
                var toAvatarIndex = Clockwise ? (avatarRingIndex + 1) % sClockwisePath.Length : (avatarRingIndex + sClockwisePath.Length - 1) % sClockwisePath.Length;
                var toAvatarSlot = sClockwisePath[toAvatarIndex];
                if (registry.TryGet(board.AvatarUid.Value, out var avatar) && avatar != null)
                {
                    board.SetAvatar(avatar, toAvatarSlot);
                    result.AddEvent(new CoreGameEvent(CoreEventType.AvatarMoved, context.ActionId, ActionName)
                        .WithCard(avatar.Uid)
                        .WithSlots(avatarFromSlot, toAvatarSlot)
                        .WithSource(SourceDefId, Cause));
                    AvatarHomingRules.TryTrackAvatarDisplacement(result, context, avatarFromSlot, toAvatarSlot, SourceDefId, Cause);
                }
            }

            result.AddEvent(new CoreGameEvent(CoreEventType.BoardRotated, context.ActionId, ActionName)
                .WithAmount(Clockwise ? 1 : -1)
                .WithMessage(Clockwise ? "clockwise" : "counterClockwise")
                .WithSource(SourceDefId, Cause));
            CardRhythmMoveTicks.AppendFromMovedEvents(result, context, result.Events);
            // 旋转后的邻接光环卡面刷新由统一对账缝自动提交（ADR-0045）。
            return result;
        }

        public override IEnumerable<TriggerPoint> GetPreTriggerPoints(GameActionContext context)
        {
            return sPreTriggers;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class SwapBoardSlotsAction : GameAction
    {
        private static readonly TriggerPoint[] sPreTriggers =
        {
            TriggerPoint.BeforeAction,
            TriggerPoint.BeforeBoardMotion
        };

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

            // ADR-0056 / #217: 两卡交换若其中一格是 Avatar 当前格，Avatar 被带到对格，避免人卡重叠
            var avatarUid = board.AvatarUid.Value;
            if (avatarUid > 0 && Left != Right && registry.TryGet(avatarUid, out var avatar) && avatar != null)
            {
                var avatarSlot = board.AvatarSlot.Value;
                if (avatarSlot == Left)
                {
                    board.SetAvatar(avatar, Right);
                    result.AddEvent(new CoreGameEvent(CoreEventType.AvatarMoved, context.ActionId, ActionName)
                        .WithCard(avatarUid)
                        .WithSlots(Left, Right)
                        .WithSource(SourceDefId, Cause));
                    AvatarHomingRules.TryTrackAvatarDisplacement(result, context, Left, Right, SourceDefId, Cause);
                }
                else if (avatarSlot == Right)
                {
                    board.SetAvatar(avatar, Left);
                    result.AddEvent(new CoreGameEvent(CoreEventType.AvatarMoved, context.ActionId, ActionName)
                        .WithCard(avatarUid)
                        .WithSlots(Right, Left)
                        .WithSource(SourceDefId, Cause));
                    AvatarHomingRules.TryTrackAvatarDisplacement(result, context, Right, Left, SourceDefId, Cause);
                }
            }

            result.AddEvent(new CoreGameEvent(CoreEventType.CardSwapped, context.ActionId, ActionName)
                    .WithSlots(Left, Right)
                    .WithSource(SourceDefId, Cause));
            CardRhythmMoveTicks.AppendFromMovedEvents(result, context, result.Events);
            // 换位后的邻接光环卡面刷新由统一对账缝自动提交（ADR-0045）。
            return result;
        }

        public override IEnumerable<TriggerPoint> GetPreTriggerPoints(GameActionContext context)
        {
            return sPreTriggers;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    /// <summary>
    /// Avatar 与场上卡（或盘面格位）的原子换位（#219 / ADR-0056 / ADR-0057）。
    /// 人与目标卡互换占格；目标卡发出盘面→盘面换格事件并推进移动计数，不给玩家换位开节奏例外。
    /// 目标格为空时仅 Avatar 单独迁格。
    /// </summary>
    public sealed class SwapAvatarWithCardAction : GameAction
    {
        private static readonly TriggerPoint[] sPreTriggers =
        {
            TriggerPoint.BeforeAction,
            TriggerPoint.BeforeBoardMotion
        };

        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnSwap,
            TriggerPoint.OnMove,
            TriggerPoint.OnMoveToSlot
        };

        public SwapAvatarWithCardAction(SlotId targetSlot)
            : this(targetSlot, 0, null, null)
        {
        }

        public SwapAvatarWithCardAction(SlotId targetSlot, string sourceDefId, string cause)
            : this(targetSlot, 0, sourceDefId, cause)
        {
        }

        public SwapAvatarWithCardAction(int targetCardUid)
            : this(SlotId.None, targetCardUid, null, null)
        {
        }

        public SwapAvatarWithCardAction(int targetCardUid, string sourceDefId, string cause)
            : this(SlotId.None, targetCardUid, sourceDefId, cause)
        {
        }

        private SwapAvatarWithCardAction(SlotId targetSlot, int targetCardUid, string sourceDefId, string cause)
        {
            TargetSlot = targetSlot;
            TargetCardUid = targetCardUid;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public SlotId TargetSlot { get; private set; }
        public int TargetCardUid { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "SwapAvatarWithCard"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();

            var avatarUid = board.AvatarUid.Value;
            if (avatarUid <= 0 || !registry.TryGet(avatarUid, out var avatar) || avatar == null)
            {
                return GameActionResult.Empty;
            }

            var avatarSlot = board.AvatarSlot.Value;
            if (!avatarSlot.IsBoardSlot)
            {
                return GameActionResult.Empty;
            }

            var resolvedTargetSlot = TargetSlot;
            var resolvedTargetUid = TargetCardUid;

            if (resolvedTargetUid > 0)
            {
                if (!registry.TryGet(resolvedTargetUid, out var targetCard) || targetCard == null)
                {
                    return GameActionResult.Empty;
                }

                if (targetCard.Zone.Value != ZoneId.Board || !targetCard.Slot.Value.IsBoardSlot)
                {
                    return GameActionResult.Empty;
                }

                resolvedTargetSlot = targetCard.Slot.Value;
            }
            else if (resolvedTargetSlot.IsBoardSlot)
            {
                resolvedTargetUid = board.GetCardUid(resolvedTargetSlot);
            }
            else
            {
                return GameActionResult.Empty;
            }

            if (!resolvedTargetSlot.IsBoardSlot || resolvedTargetSlot == avatarSlot)
            {
                return GameActionResult.Empty;
            }

            CardInstance targetCardInstance = null;
            if (resolvedTargetUid > 0 && registry.TryGet(resolvedTargetUid, out targetCardInstance) && targetCardInstance != null)
            {
                // 目标格有场上卡：原子互换
                board.ClearSlot(resolvedTargetSlot);
                board.SetAvatar(avatar, resolvedTargetSlot);
                board.PlaceCard(targetCardInstance, avatarSlot);
            }
            else
            {
                // 目标格为空格：Avatar 单独迁格
                resolvedTargetUid = 0;
                board.SetAvatar(avatar, resolvedTargetSlot);
            }

            if (resolvedTargetSlot == SlotId.Center || resolvedTargetSlot == SlotId.Board(5))
            {
                board.ClearAvatarOffHome();
            }
            else
            {
                board.StartAvatarOffHomeCountdown();
            }

            var result = new GameActionResult();

            if (resolvedTargetUid > 0 && targetCardInstance != null)
            {
                // 目标卡换格事件（发出盘面格→盘面格换格）
                result.AddEvent(new CoreGameEvent(CoreEventType.CardMoved, context.ActionId, ActionName)
                    .WithCard(resolvedTargetUid)
                    .WithSlots(resolvedTargetSlot, avatarSlot)
                    .WithSource(SourceDefId, Cause));
            }

            // Avatar 换格事件（供盘面表现步骤与对账投影）
            result.AddEvent(new CoreGameEvent(CoreEventType.CardMoved, context.ActionId, ActionName)
                .WithCard(avatarUid)
                .WithSlots(avatarSlot, resolvedTargetSlot)
                .WithSource(SourceDefId, Cause));

            result.AddEvent(new CoreGameEvent(CoreEventType.AvatarMoved, context.ActionId, ActionName)
                .WithCard(avatarUid)
                .WithSlots(avatarSlot, resolvedTargetSlot)
                .WithSource(SourceDefId, Cause));

            result.AddEvent(new CoreGameEvent(CoreEventType.CardSwapped, context.ActionId, ActionName)
                .WithSlots(avatarSlot, resolvedTargetSlot)
                .WithSource(SourceDefId, Cause));

            // 推进被移动卡的移动计数（不给玩家换位开例外）
            CardRhythmMoveTicks.AppendFromMovedEvents(result, context, result.Events);
            return result;
        }

        public override IEnumerable<TriggerPoint> GetPreTriggerPoints(GameActionContext context)
        {
            return sPreTriggers;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }
}
