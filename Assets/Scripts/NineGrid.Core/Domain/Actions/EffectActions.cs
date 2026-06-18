using System;
using System.Collections.Generic;
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
        {
            ActorUid = actorUid;
            TargetUid = targetUid;
            Amount = amount;
        }

        public int ActorUid { get; private set; }
        public int TargetUid { get; private set; }
        public int Amount { get; private set; }
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

            return new GameActionResult().AddFollowUp(new DealDamageAction(ActorUid, TargetUid, Amount));
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
        {
            CardUid = cardUid;
            ToSlot = toSlot;
        }

        public int CardUid { get; private set; }
        public SlotId ToSlot { get; private set; }
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
                    .WithSlots(fromSlot, ToSlot));
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
        {
            TargetUid = targetUid;
            Stat = stat;
            Op = op;
            Value = value;
            Layer = layer;
            Scope = scope;
            Source = source ?? "effect.action";
            SourceDefId = sourceDefId ?? string.Empty;
        }

        public int TargetUid { get; private set; }
        public StatId Stat { get; private set; }
        public ModifierOp Op { get; private set; }
        public float Value { get; private set; }
        public ModifierLayer Layer { get; private set; }
        public ModifierScope Scope { get; private set; }
        public string Source { get; private set; }
        public string SourceDefId { get; private set; }
        public override string ActionName { get { return "AddStatModifier"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var card = context.GetModel<CardRegistry>().Get(TargetUid);
            var modifier = new StatModifier(Stat, Op, Value, Layer, new ModifierSource(Source), Scope);
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
        {
            TargetUid = targetUid;
            Rule = rule;
            Op = op;
            Value = value;
            Layer = layer;
            Scope = scope;
            Source = source ?? "effect.action.rule";
        }

        public int TargetUid { get; private set; }
        public RuleId Rule { get; private set; }
        public ModifierOp Op { get; private set; }
        public float Value { get; private set; }
        public ModifierLayer Layer { get; private set; }
        public ModifierScope Scope { get; private set; }
        public string Source { get; private set; }
        public override string ActionName { get { return "AddRuleModifier"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            IStatCondition condition = TargetUid == 0 ? null : new TargetUidCondition(TargetUid);
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
