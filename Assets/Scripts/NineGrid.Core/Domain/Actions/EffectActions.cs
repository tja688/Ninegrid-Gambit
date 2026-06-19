using System;
using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;

namespace NineGrid.Core
{
    public sealed class ExecuteEffectAction : GameAction
    {
        private readonly TriggerContext mTriggerContext;
        private readonly IReadOnlyList<GameAction> mPreparedActions;

        public ExecuteEffectAction(string instanceId, TriggerContext triggerContext)
            : this(instanceId, triggerContext, null)
        {
        }

        public ExecuteEffectAction(string instanceId, TriggerContext triggerContext, IReadOnlyList<GameAction> preparedActions)
        {
            InstanceId = instanceId ?? string.Empty;
            mTriggerContext = triggerContext;
            mPreparedActions = preparedActions;
        }

        public string InstanceId { get; private set; }
        public override string ActionName { get { return "ExecuteEffect"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var effectSystem = context.GetSystem<IEffectSystem>();
            EffectInstance instance;
            if (!effectSystem.TryGetInstance(InstanceId, out instance))
            {
                return GameActionResult.Empty;
            }

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectTriggered, context.ActionId, ActionName)
                    .WithCard(instance.Owner == null ? 0 : instance.Owner.OwnerUid)
                    .WithMessage(instance.Definition.Id)
                    .WithSource(instance.Owner == null ? string.Empty : instance.Owner.SourceDefId, instance.Definition.Id));

            var actions = mPreparedActions ?? effectSystem.BuildTriggeredActions(InstanceId, mTriggerContext);
            for (var i = 0; i < actions.Count; i++)
            {
                result.AddFollowUp(actions[i]);
            }

            return result;
        }
    }

    public sealed class KillIfDeadAction : GameAction
    {
        public KillIfDeadAction(int killerUid, int targetUid)
        {
            KillerUid = killerUid;
            TargetUid = targetUid;
        }

        public int KillerUid { get; private set; }
        public int TargetUid { get; private set; }
        public override string ActionName { get { return "KillIfDead"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            CardInstance target;
            if (!context.GetModel<CardRegistry>().TryGet(TargetUid, out target))
            {
                return GameActionResult.Empty;
            }

            if (target.Zone.Value == ZoneId.Graveyard || target.Zone.Value == ZoneId.Removed)
            {
                return GameActionResult.Empty;
            }

            if ((int)Math.Round(target.Stats.GetBase(StatId.Hp)) > 0)
            {
                return GameActionResult.Empty;
            }

            return new GameActionResult().AddFollowUp(new KillAction(KillerUid, TargetUid));
        }
    }

    public sealed class ConditionalDealDamageIfAliveAction : GameAction
    {
        public ConditionalDealDamageIfAliveAction(int actorUid, int targetUid, int amount)
            : this(actorUid, targetUid, amount, null, null)
        {
        }

        public ConditionalDealDamageIfAliveAction(int actorUid, int targetUid, int amount, string sourceDefId, string cause)
        {
            ActorUid = actorUid;
            TargetUid = targetUid;
            Amount = amount;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public int ActorUid { get; private set; }
        public int TargetUid { get; private set; }
        public int Amount { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "ConditionalDealDamageIfAlive"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            CardInstance actor;
            if (!context.GetModel<CardRegistry>().TryGet(ActorUid, out actor))
            {
                return GameActionResult.Empty;
            }

            if (actor.Zone.Value == ZoneId.Graveyard
                || actor.Zone.Value == ZoneId.Removed
                || (int)Math.Round(actor.Stats.GetBase(StatId.Hp)) <= 0)
            {
                return GameActionResult.Empty;
            }

            return new GameActionResult().AddFollowUp(new DealDamageAction(ActorUid, TargetUid, Amount, SourceDefId, Cause));
        }
    }

    public sealed class ForceBattleAction : GameAction
    {
        public ForceBattleAction(int targetUid, string sourceDefId = null, string cause = null)
        {
            TargetUid = targetUid;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public int TargetUid { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "ForceBattle"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            CardInstance avatar;
            CardInstance target;
            if (!registry.TryGet(board.AvatarUid.Value, out avatar)
                || !registry.TryGet(TargetUid, out target)
                || target.Kind != CardKind.Monster
                || target.Zone.Value == ZoneId.Graveyard
                || target.Zone.Value == ZoneId.Removed)
            {
                return GameActionResult.Empty;
            }

            var statSystem = context.GetSystem<IStatSystem>();
            var avatarDamage = GetAttackDamage(statSystem, avatar);
            var targetDamage = GetAttackDamage(statSystem, target);
            var avatarFirstStrike = HasFirstStrike(statSystem, avatar);
            var targetFirstStrike = HasFirstStrike(statSystem, target);
            var result = new GameActionResult();
            if (targetFirstStrike && !avatarFirstStrike)
            {
                result.AddFollowUp(new DealDamageAction(target.Uid, avatar.Uid, targetDamage, SourceDefId, Cause));
                result.AddFollowUp(new ConditionalDealDamageIfAliveAction(avatar.Uid, target.Uid, avatarDamage, SourceDefId, Cause));
            }
            else
            {
                result.AddFollowUp(new DealDamageAction(avatar.Uid, target.Uid, avatarDamage, SourceDefId, Cause));
                result.AddFollowUp(new ConditionalDealDamageIfAliveAction(target.Uid, avatar.Uid, targetDamage, SourceDefId, Cause));
            }

            return result;
        }

        private static int GetAttackDamage(IStatSystem statSystem, CardInstance card)
        {
            return Math.Max(0, statSystem.GetEffectiveInt(card, StatId.Attack));
        }

        private static bool HasFirstStrike(IStatSystem statSystem, CardInstance card)
        {
            return statSystem.EvaluateRule(RuleId.FirstStrike, 0f, statSystem.CreateContext(card)) > 0f;
        }
    }

    public sealed class MoveCardAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnMove,
            TriggerPoint.OnMoveToSlot
        };

        public MoveCardAction(int cardUid, SlotId toSlot)
            : this(cardUid, toSlot, null, null)
        {
        }

        public MoveCardAction(int cardUid, SlotId toSlot, string sourceDefId, string cause)
        {
            CardUid = cardUid;
            ToSlot = toSlot;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public int CardUid { get; private set; }
        public SlotId ToSlot { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "MoveCard"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();
            var card = registry.Get(CardUid);
            var fromSlot = card.Slot.Value;

            if (card.Kind == CardKind.Avatar)
            {
                board.SetAvatar(card, ToSlot);
            }
            else
            {
                deck.RemoveCard(card);
                board.PlaceCard(card, ToSlot);
            }

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardMoved, context.ActionId, ActionName)
                    .WithCard(CardUid)
                    .WithSlots(fromSlot, ToSlot)
                    .WithSource(SourceDefId, Cause));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }
    }

    public sealed class SetBoardMarkAction : GameAction
    {
        public SetBoardMarkAction(SlotId slot, BoardMarkId mark, bool marked, string sourceDefId = null, string cause = null)
        {
            Slot = slot;
            Mark = mark;
            Marked = marked;
            SourceDefId = sourceDefId ?? string.Empty;
            Cause = cause ?? string.Empty;
        }

        public SlotId Slot { get; private set; }
        public BoardMarkId Mark { get; private set; }
        public bool Marked { get; private set; }
        public string SourceDefId { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "SetBoardMark"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            if (!Slot.IsBoardSlot || Mark == BoardMarkId.None)
            {
                return GameActionResult.Empty;
            }

            var board = context.GetModel<BoardModel>();
            if (board.IsMarked(Slot, Mark) == Marked)
            {
                return GameActionResult.Empty;
            }

            board.SetMark(Slot, Mark, Marked);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.BoardMarked, context.ActionId, ActionName)
                    .WithSlots(Slot, Slot)
                    .WithAmount((int)Mark)
                    .WithDelta(Marked ? 1 : -1)
                    .WithMessage(Mark.ToString())
                    .WithSource(SourceDefId, Cause));
        }
    }

    public sealed class ShuffleIntoDrawPileAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal
        };

        public ShuffleIntoDrawPileAction(string defId, CardKind kind, int count, bool top, string cause = null)
        {
            DefId = defId ?? string.Empty;
            Kind = kind;
            Count = Math.Max(0, count);
            Top = top;
            Cause = cause ?? string.Empty;
        }

        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public int Count { get; private set; }
        public bool Top { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "ShuffleIntoDrawPile"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var deck = context.GetModel<DeckModel>();
            var result = new GameActionResult();

            for (var i = 0; i < Count; i++)
            {
                var card = CreateConfiguredCard(context, registry, DefId, Kind);
                deck.AddToDrawPile(card, Top);
                result.AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                    .WithCard(card.Uid)
                    .WithMessage("shuffleInto:" + DefId)
                    .WithSource(DefId, Cause));
            }

            if (!Top && Count > 0)
            {
                Shuffle(deck, context.Architecture.GetUtility<IRngUtility>());
            }

            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        private static void Shuffle(DeckModel deck, IRngUtility rng)
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

        private static CardInstance CreateConfiguredCard(GameActionContext context, CardRegistry registry, string defId, CardKind fallbackKind)
        {
            var content = context.GetSystem<IContentSystem>();
            var draft = content.CreateDraft(defId);
            var card = draft.Kind == CardKind.Unknown ? registry.Create(defId, fallbackKind) : draft.Create(registry);
            content.ApplyContentToCard(card);
            return card;
        }
    }

    public sealed class ShuffleCardIntoDrawPileAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal
        };

        public ShuffleCardIntoDrawPileAction(int cardUid, bool top, string cause = null)
        {
            CardUid = cardUid;
            Top = top;
            Cause = cause ?? string.Empty;
        }

        public int CardUid { get; private set; }
        public bool Top { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "ShuffleCardIntoDrawPile"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            CardInstance card;
            if (!registry.TryGet(CardUid, out card) || card.Kind == CardKind.Avatar)
            {
                return GameActionResult.Empty;
            }

            var fromSlot = card.Slot.Value;
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();
            board.RemoveCard(card);
            deck.AddToDrawPile(card, Top);

            if (!Top)
            {
                Shuffle(deck, context.Architecture.GetUtility<IRngUtility>());
            }

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                    .WithCard(card.Uid)
                    .WithSlots(fromSlot, SlotId.None)
                    .WithMessage("shuffleExisting:" + card.DefId)
                    .WithSource(card.DefId, Cause));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        private static void Shuffle(DeckModel deck, IRngUtility rng)
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

    public sealed class SpawnCardAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal,
            TriggerPoint.OnEnter
        };

        public SpawnCardAction(string defId, CardKind kind, ZoneId zone, SlotId slot, int count, string cause = null)
        {
            DefId = defId ?? string.Empty;
            Kind = kind;
            Zone = zone;
            Slot = slot;
            Count = Math.Max(0, count);
            Cause = cause ?? string.Empty;
        }

        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public ZoneId Zone { get; private set; }
        public SlotId Slot { get; private set; }
        public int Count { get; private set; }
        public string Cause { get; private set; }
        public override string ActionName { get { return "SpawnCard"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();
            var result = new GameActionResult();

            for (var i = 0; i < Count; i++)
            {
                var card = CreateConfiguredCard(context, registry, DefId, Kind);
                Place(card, board, deck);
                result.AddEvent(new CoreGameEvent(CoreEventType.CardSpawned, context.ActionId, ActionName)
                    .WithCard(card.Uid)
                    .WithSlots(SlotId.None, card.Slot.Value)
                    .WithMessage(DefId)
                    .WithSource(DefId, Cause));
                if (card.Zone.Value == ZoneId.Board)
                {
                    result.AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                        .WithCard(card.Uid)
                        .WithSlots(SlotId.None, card.Slot.Value)
                        .WithSource(DefId, Cause));
                }
            }

            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        private void Place(CardInstance card, BoardModel board, DeckModel deck)
        {
            switch (Zone)
            {
                case ZoneId.Board:
                    board.PlaceCard(card, Slot);
                    break;
                case ZoneId.PlayerCardPool:
                    deck.AddToPlayerCardPool(card);
                    break;
                case ZoneId.EnemyCardPool:
                    deck.AddToEnemyCardPool(card);
                    break;
                case ZoneId.ItemSlots:
                    deck.AddToItemSlots(card);
                    break;
                case ZoneId.DrawPile:
                default:
                    deck.AddToDrawPile(card, false);
                    break;
            }
        }

        private static CardInstance CreateConfiguredCard(GameActionContext context, CardRegistry registry, string defId, CardKind fallbackKind)
        {
            var content = context.GetSystem<IContentSystem>();
            var draft = content.CreateDraft(defId);
            var card = draft.Kind == CardKind.Unknown ? registry.Create(defId, fallbackKind) : draft.Create(registry);
            content.ApplyContentToCard(card);
            return card;
        }
    }

    public sealed class ShuffleRandomContentIntoDrawPileAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal
        };

        public ShuffleRandomContentIntoDrawPileAction(
            CardKind kind,
            int count,
            bool top,
            int minLevel,
            int maxLevel,
            bool excludeElite,
            bool excludeBoss,
            string excludeDeckId,
            string sourceDefId = null)
        {
            Kind = kind;
            Count = Math.Max(0, count);
            Top = top;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
            ExcludeElite = excludeElite;
            ExcludeBoss = excludeBoss;
            ExcludeDeckId = excludeDeckId ?? string.Empty;
            SourceDefId = sourceDefId ?? string.Empty;
        }

        public CardKind Kind { get; private set; }
        public int Count { get; private set; }
        public bool Top { get; private set; }
        public int MinLevel { get; private set; }
        public int MaxLevel { get; private set; }
        public bool ExcludeElite { get; private set; }
        public bool ExcludeBoss { get; private set; }
        public string ExcludeDeckId { get; private set; }
        public string SourceDefId { get; private set; }
        public override string ActionName { get { return "ShuffleRandomContentIntoDrawPile"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var content = context.GetSystem<IContentSystem>();
            var candidates = FindCatalogCandidates(content.Catalog, Kind, MinLevel, MaxLevel, ExcludeElite, ExcludeBoss, ExcludeDeckId);
            if (candidates.Count == 0 || Count <= 0)
            {
                return GameActionResult.Empty;
            }

            var registry = context.GetModel<CardRegistry>();
            var deck = context.GetModel<DeckModel>();
            var rng = context.Architecture.GetUtility<IRngUtility>();
            var result = new GameActionResult();
            for (var i = 0; i < Count; i++)
            {
                var definition = candidates[rng.Range(0, candidates.Count)];
                var draft = content.CreateDraft(definition.DefId);
                var card = draft.Kind == CardKind.Unknown ? registry.Create(definition.DefId, Kind) : draft.Create(registry);
                content.ApplyContentToCard(card);
                deck.AddToDrawPile(card, Top);
                result.AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                    .WithCard(card.Uid)
                    .WithMessage("shuffleRandom:" + definition.DefId)
                    .WithSource(definition.DefId, SourceDefId));
            }

            if (!Top)
            {
                Shuffle(deck, rng);
            }

            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        internal static List<CardContentDefinition> FindCatalogCandidates(
            GameContentCatalog catalog,
            CardKind kind,
            int minLevel,
            int maxLevel,
            bool excludeElite,
            bool excludeBoss,
            string excludeDeckId)
        {
            var result = new List<CardContentDefinition>();
            if (catalog == null)
            {
                return result;
            }

            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (kind != CardKind.Unknown && card.Kind != kind)
                {
                    continue;
                }

                if (minLevel > 0 && card.Level < minLevel)
                {
                    continue;
                }

                if (maxLevel > 0 && card.Level > maxLevel)
                {
                    continue;
                }

                if (excludeElite && card.IsElite)
                {
                    continue;
                }

                if (excludeBoss && card.IsBoss)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(excludeDeckId) && string.Equals(card.DeckId, excludeDeckId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(card);
            }

            return result;
        }

        private static void Shuffle(DeckModel deck, IRngUtility rng)
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

    public sealed class ExchangeWithDrawPileAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal,
            TriggerPoint.OnEnter
        };

        public ExchangeWithDrawPileAction(
            int targetUid,
            CardKind drawKind,
            int minLevel,
            int maxLevel,
            bool excludeElite,
            bool excludeBoss,
            string sourceDefId = null)
        {
            TargetUid = targetUid;
            DrawKind = drawKind;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
            ExcludeElite = excludeElite;
            ExcludeBoss = excludeBoss;
            SourceDefId = sourceDefId ?? string.Empty;
        }

        public int TargetUid { get; private set; }
        public CardKind DrawKind { get; private set; }
        public int MinLevel { get; private set; }
        public int MaxLevel { get; private set; }
        public bool ExcludeElite { get; private set; }
        public bool ExcludeBoss { get; private set; }
        public string SourceDefId { get; private set; }
        public override string ActionName { get { return "ExchangeWithDrawPile"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var registry = context.GetModel<CardRegistry>();
            CardInstance target;
            if (!registry.TryGet(TargetUid, out target) || !target.Slot.Value.IsBoardSlot)
            {
                return GameActionResult.Empty;
            }

            var deck = context.GetModel<DeckModel>();
            var candidates = FindDrawPileCandidates(deck, registry);
            if (candidates.Count == 0)
            {
                return GameActionResult.Empty;
            }

            var rng = context.Architecture.GetUtility<IRngUtility>();
            var drawnUid = candidates[rng.Range(0, candidates.Count)];
            var drawn = registry.Get(drawnUid);
            var board = context.GetModel<BoardModel>();
            var fromSlot = target.Slot.Value;

            board.RemoveCard(target);
            deck.RemoveUid(drawnUid);
            board.PlaceCard(drawn, fromSlot);
            deck.AddToDrawPile(target, false);
            Shuffle(deck, rng);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                    .WithCard(drawn.Uid)
                    .WithSlots(SlotId.None, fromSlot)
                    .WithMessage("exchangeDraw:" + drawn.DefId)
                    .WithSource(drawn.DefId, SourceDefId))
                .AddEvent(new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                    .WithCard(target.Uid)
                    .WithSlots(fromSlot, SlotId.None)
                    .WithMessage("exchangeToDraw:" + target.DefId)
                    .WithSource(target.DefId, SourceDefId));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        private List<int> FindDrawPileCandidates(DeckModel deck, CardRegistry registry)
        {
            var result = new List<int>();
            for (var i = 0; i < deck.DrawPileUids.Count; i++)
            {
                CardInstance card;
                if (!registry.TryGet(deck.DrawPileUids[i], out card))
                {
                    continue;
                }

                if (DrawKind != CardKind.Unknown && card.Kind != DrawKind)
                {
                    continue;
                }

                var level = card.Counters.Get(CoreCounterKeys.Level);
                if (MinLevel > 0 && level < MinLevel)
                {
                    continue;
                }

                if (MaxLevel > 0 && level > MaxLevel)
                {
                    continue;
                }

                if (ExcludeElite && card.Counters.Get(CoreCounterKeys.Elite) > 0)
                {
                    continue;
                }

                if (ExcludeBoss && card.Counters.Get(CoreCounterKeys.Boss) > 0)
                {
                    continue;
                }

                result.Add(card.Uid);
            }

            return result;
        }

        private static void Shuffle(DeckModel deck, IRngUtility rng)
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

    public sealed class AddStatModifierAction : GameAction
    {
        public AddStatModifierAction(
            int targetUid,
            StatId stat,
            ModifierOp op,
            float value,
            ModifierLayer layer,
            ModifierScope scope,
            string source,
            string sourceDefId = null)
            : this(targetUid, stat, op, value, layer, scope, source, sourceDefId, null)
        {
        }

        public AddStatModifierAction(
            int targetUid,
            StatId stat,
            ModifierOp op,
            float value,
            ModifierLayer layer,
            ModifierScope scope,
            string source,
            string sourceDefId,
            IStatCondition condition)
        {
            TargetUid = targetUid;
            Stat = stat;
            Op = op;
            Value = value;
            Layer = layer;
            Scope = scope;
            Source = source ?? "effect.action";
            SourceDefId = sourceDefId ?? string.Empty;
            Condition = condition;
        }

        public int TargetUid { get; private set; }
        public StatId Stat { get; private set; }
        public ModifierOp Op { get; private set; }
        public float Value { get; private set; }
        public ModifierLayer Layer { get; private set; }
        public ModifierScope Scope { get; private set; }
        public string Source { get; private set; }
        public string SourceDefId { get; private set; }
        public IStatCondition Condition { get; private set; }
        public override string ActionName { get { return "AddStatModifier"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var card = context.GetModel<CardRegistry>().Get(TargetUid);
            var modifier = new StatModifier(Stat, Op, Value, Layer, new ModifierSource(Source), Scope, Condition);
            context.GetSystem<IStatSystem>().AddModifier(card, modifier);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectModifierApplied, context.ActionId, ActionName)
                    .WithCard(TargetUid)
                    .WithTarget(TargetUid)
                    .WithAmount((int)Stat)
                    .WithDelta((int)Math.Round(Value))
                    .WithMessage(Source)
                    .WithSource(SourceDefId, Source));
        }
    }

    public sealed class AddRuleModifierAction : GameAction
    {
        public AddRuleModifierAction(
            int targetUid,
            RuleId rule,
            ModifierOp op,
            float value,
            ModifierLayer layer,
            ModifierScope scope,
            string source)
            : this(targetUid, true, 0, CardKind.Unknown, rule, op, value, layer, scope, source)
        {
        }

        public AddRuleModifierAction(
            int targetUid,
            bool useTargetCondition,
            int actorUid,
            CardKind targetKind,
            RuleId rule,
            ModifierOp op,
            float value,
            ModifierLayer layer,
            ModifierScope scope,
            string source)
        {
            TargetUid = targetUid;
            UseTargetCondition = useTargetCondition;
            ActorUid = actorUid;
            TargetKind = targetKind;
            Rule = rule;
            Op = op;
            Value = value;
            Layer = layer;
            Scope = scope;
            Source = source ?? "effect.action.rule";
        }

        public int TargetUid { get; private set; }
        public bool UseTargetCondition { get; private set; }
        public int ActorUid { get; private set; }
        public CardKind TargetKind { get; private set; }
        public RuleId Rule { get; private set; }
        public ModifierOp Op { get; private set; }
        public float Value { get; private set; }
        public ModifierLayer Layer { get; private set; }
        public ModifierScope Scope { get; private set; }
        public string Source { get; private set; }
        public override string ActionName { get { return "AddRuleModifier"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var conditions = new List<IStatCondition>();
            if (UseTargetCondition && TargetUid != 0)
            {
                conditions.Add(new TargetUidCondition(TargetUid));
            }

            if (ActorUid != 0)
            {
                conditions.Add(new ActorUidCondition(ActorUid));
            }

            if (TargetKind != CardKind.Unknown)
            {
                conditions.Add(new CardKindCondition(TargetKind));
            }

            IStatCondition condition = conditions.Count == 0
                ? null
                : conditions.Count == 1 ? conditions[0] : new AllStatCondition(conditions);
            var modifier = new RuleModifier(Rule, Op, Value, Layer, new ModifierSource(Source), Scope, condition);
            context.GetSystem<IStatSystem>().RuleModifiers.Add(modifier);

            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectModifierApplied, context.ActionId, ActionName)
                    .WithCard(TargetUid)
                    .WithAmount((int)Rule)
                    .WithDelta((int)Math.Round(Value))
                    .WithMessage(Source));
        }
    }

    public sealed class ReplayHelpCardEffectsAction : GameAction
    {
        public ReplayHelpCardEffectsAction(int towerUid, string towerEffectInstanceId, UseItemAction useItem, CardKind targetKind, bool deactivateSelf)
        {
            TowerUid = towerUid;
            TowerEffectInstanceId = towerEffectInstanceId ?? string.Empty;
            UseItem = useItem;
            TargetKind = targetKind;
            DeactivateSelf = deactivateSelf;
        }

        public int TowerUid { get; private set; }
        public string TowerEffectInstanceId { get; private set; }
        public UseItemAction UseItem { get; private set; }
        public CardKind TargetKind { get; private set; }
        public bool DeactivateSelf { get; private set; }
        public override string ActionName { get { return "ReplayHelpCardEffects"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            if (UseItem == null || UseItem.ItemUid == 0 || UseItem.ItemUid == TowerUid)
            {
                return GameActionResult.Empty;
            }

            var registry = context.GetModel<CardRegistry>();
            CardInstance usedCard;
            if (!registry.TryGet(UseItem.ItemUid, out usedCard) || usedCard.Kind != CardKind.HelpCard)
            {
                return GameActionResult.Empty;
            }

            if (!MatchesSelectedTargetKind(context, UseItem, TargetKind))
            {
                return GameActionResult.Empty;
            }

            var content = context.GetSystem<IContentSystem>();
            var catalog = content.Catalog;
            CardContentDefinition cardDefinition;
            if (catalog == null || !catalog.TryGetCard(usedCard.DefId, out cardDefinition))
            {
                return GameActionResult.Empty;
            }

            var triggerEvent = new CoreGameEvent(CoreEventType.ItemUsed, context.ActionId, ActionName)
                .WithCard(UseItem.ItemUid)
                .WithSource(usedCard.DefId, "replay");
            if (UseItem.SelectedCardUids.Count > 0)
            {
                triggerEvent.WithTarget(UseItem.SelectedCardUids[0]);
            }

            var triggerContext = new TriggerContext(
                TriggerPoint.OnUseHelpCard,
                TriggerTiming.Post,
                UseItem,
                new[] { triggerEvent },
                context);

            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectTriggered, context.ActionId, ActionName)
                    .WithCard(TowerUid)
                    .WithTarget(UseItem.ItemUid)
                    .WithMessage("replay:" + usedCard.DefId)
                    .WithSource("help.doubling_tower", "replay"));

            AddReplayFollowUps(context.GetSystem<IEffectSystem>(), catalog, cardDefinition.EffectIds, usedCard.DefId, UseItem.ItemUid, triggerContext, result);

            if (DeactivateSelf && TowerUid != 0)
            {
                result.AddFollowUp(new RemoveCardAction(TowerUid, ZoneId.Removed, "doublingTower", "help.doubling_tower"));
                result.AddFollowUp(new DeactivateEffectAction(TowerEffectInstanceId));
            }

            return result;
        }

        private static void AddReplayFollowUps(
            IEffectSystem effectSystem,
            GameContentCatalog catalog,
            IReadOnlyList<string> effectIds,
            string sourceDefId,
            int ownerUid,
            TriggerContext triggerContext,
            GameActionResult result)
        {
            for (var i = 0; i < effectIds.Count; i++)
            {
                ContentEffectDefinition contentEffect;
                if (!catalog.TryGetEffect(effectIds[i], out contentEffect)
                    || contentEffect.State != ContentImplementationState.Implemented
                    || contentEffect.ContainerType != EffectContainerType.HelpCard
                    || contentEffect.Id == "help.doubling_tower.board_monster"
                    || contentEffect.Id == "help.doubling_tower.item_player")
                {
                    continue;
                }

                var definition = effectSystem.ParseJson(contentEffect.Json);
                if (definition.Kind != EffectKind.Triggered)
                {
                    continue;
                }

                var temporary = effectSystem.Activate(definition, new EffectOwner(EffectContainerType.HelpCard, sourceDefId, ownerUid));
                var actions = effectSystem.BuildTriggeredActions(temporary.InstanceId, triggerContext);
                effectSystem.Deactivate(temporary.InstanceId);
                for (var j = 0; j < actions.Count; j++)
                {
                    result.AddFollowUp(actions[j]);
                }
            }
        }

        private static bool MatchesSelectedTargetKind(GameActionContext context, UseItemAction useItem, CardKind targetKind)
        {
            if (targetKind == CardKind.Unknown)
            {
                return true;
            }

            var registry = context.GetModel<CardRegistry>();
            for (var i = 0; i < useItem.SelectedCardUids.Count; i++)
            {
                CardInstance card;
                if (registry.TryGet(useItem.SelectedCardUids[i], out card) && card.Kind == targetKind)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class GrantSkillAction : GameAction
    {
        public GrantSkillAction(string skillDefId)
        {
            SkillDefId = skillDefId ?? string.Empty;
        }

        public string SkillDefId { get; private set; }
        public override string ActionName { get { return "GrantSkill"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<PlayerModel>().AddSkill(SkillDefId);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.SkillGranted, context.ActionId, ActionName)
                    .WithMessage(SkillDefId));
        }
    }

    public sealed class DeactivateEffectAction : GameAction
    {
        public DeactivateEffectAction(string instanceId)
        {
            InstanceId = instanceId ?? string.Empty;
        }

        public string InstanceId { get; private set; }
        public override string ActionName { get { return "DeactivateEffect"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var effectSystem = context.GetSystem<IEffectSystem>();
            EffectInstance instance;
            if (!effectSystem.TryGetInstance(InstanceId, out instance))
            {
                return GameActionResult.Empty;
            }

            if (instance.Owner != null)
            {
                if (instance.Owner.ContainerType == EffectContainerType.Relic)
                {
                    context.GetModel<PlayerModel>().RemoveRelic(instance.Owner.SourceDefId);
                }
                else if (instance.Owner.ContainerType == EffectContainerType.PlayerSkill)
                {
                    context.GetModel<PlayerModel>().RemoveSkill(instance.Owner.SourceDefId);
                }
            }

            effectSystem.Deactivate(InstanceId);
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectDeactivated, context.ActionId, ActionName)
                    .WithMessage(InstanceId));
        }
    }
}
