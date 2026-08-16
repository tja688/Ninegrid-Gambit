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
            var battleScope = context.GetSystem<IBattleScopeSystem>();
            for (var i = 0; i < actions.Count; i++)
            {
                // ADR-0044：结算窗口（交战窗 / 敌方行动阶段）内的效果位移挂起到收尾锚点落地，
                // 其余效果动作照常插在结算链原位。
                if (battleScope != null && battleScope.TryDeferBoardMotion(actions[i]))
                {
                    continue;
                }

                result.AddFollowUp(actions[i]);
            }

            return result;
        }
    }

    /// <summary>
    /// 无效果实例时仍发出 EffectTriggered（ADR-0018 基础触发表现 fallback，如神圣决斗）。
    /// </summary>
    public sealed class EmitEffectTriggeredAction : GameAction
    {
        public EmitEffectTriggeredAction(int ownerUid, string effectDefId, string sourceDefId = null)
        {
            OwnerUid = ownerUid;
            EffectDefId = effectDefId ?? string.Empty;
            SourceDefId = sourceDefId ?? effectDefId ?? string.Empty;
        }

        public int OwnerUid { get; private set; }
        public string EffectDefId { get; private set; }
        public string SourceDefId { get; private set; }
        public override string ActionName { get { return "EmitEffectTriggered"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectTriggered, context.ActionId, ActionName)
                    .WithCard(OwnerUid)
                    .WithMessage(EffectDefId)
                    .WithSource(SourceDefId, EffectDefId));
        }
    }

        /// <summary>
        /// 效果倒计时剩余提交（ADR-0035）：读取效果触发器的倒计时计数器当前值，
        /// 以 <see cref="CoreEventType.EffectCountdownChanged"/> 结算指令广播到表现层
        /// （Settled 消费，键为完整「装配id.键」）。剩余只经本指令投影，禁止 View 直读 Core 计数器。
        /// 遗物效果 OwnerUid=0：计数器落在 Avatar 上，<paramref name="sourceDefId"/> 须为 relic.* 供 HUD 路由。
        /// </summary>
        public sealed class CommitEffectCountdownRemainingAction : GameAction
        {
            public CommitEffectCountdownRemainingAction(int cardUid, string projectionKey, string counterKey)
                : this(cardUid, projectionKey, counterKey, null)
            {
            }

            public CommitEffectCountdownRemainingAction(
                int cardUid,
                string projectionKey,
                string counterKey,
                string sourceDefId)
            {
                CardUid = cardUid;
                ProjectionKey = projectionKey ?? string.Empty;
                CounterKey = counterKey ?? string.Empty;
                SourceDefId = sourceDefId ?? string.Empty;
            }

            public int CardUid { get; private set; }
            public string ProjectionKey { get; private set; }
            public string CounterKey { get; private set; }
            public string SourceDefId { get; private set; }
            public override string ActionName { get { return "CommitEffectCountdownRemaining"; } }

            public override GameActionResult Apply(GameActionContext context)
            {
                if (string.IsNullOrEmpty(ProjectionKey) || string.IsNullOrEmpty(CounterKey))
                {
                    return GameActionResult.Empty;
                }

                var registry = context.GetModel<CardRegistry>();
                CardInstance counterHost = null;
                if (CardUid > 0)
                {
                    registry.TryGet(CardUid, out counterHost);
                }
                else
                {
                    // 遗物：触发器写在 Avatar 上（OwnerCard ?? AvatarCard）。
                    var avatarUid = context.GetModel<BoardModel>().AvatarUid.Value;
                    if (avatarUid > 0)
                    {
                        registry.TryGet(avatarUid, out counterHost);
                    }
                }

                if (counterHost == null)
                {
                    return GameActionResult.Empty;
                }

                var remaining = Math.Max(0, counterHost.Counters.Get(CounterKey));
                var eventSource = !string.IsNullOrEmpty(SourceDefId)
                    ? SourceDefId
                    : (counterHost.DefId ?? string.Empty);
                return new GameActionResult()
                    .AddEvent(new CoreGameEvent(CoreEventType.EffectCountdownChanged, context.ActionId, ActionName)
                        .WithCard(counterHost.Uid)
                        .WithResultValue(remaining)
                        .WithMessage(ProjectionKey)
                        .WithSource(eventSource, ActionName));
            }
        }

        /// <summary>
        /// 效果倒计时投影清除（ADR-0035 / #157）：效果被卸载（Deactivate）或持有者离开战斗时，
        /// 广播清除指令让表现层移除该投影键的已提交剩余（回退静态/初始），
        /// 避免失效效果继续投影「剩余N次」。键为完整「装配id.键」。
        /// 遗物 OwnerUid=0 时仍广播，靠 <paramref name="sourceDefId"/>=relic.* 路由到遗物栏。
        /// </summary>
        public sealed class ClearEffectCountdownRemainingAction : GameAction
        {
            public ClearEffectCountdownRemainingAction(int cardUid, string projectionKey)
                : this(cardUid, projectionKey, null)
            {
            }

            public ClearEffectCountdownRemainingAction(int cardUid, string projectionKey, string sourceDefId)
            {
                CardUid = cardUid;
                ProjectionKey = projectionKey ?? string.Empty;
                SourceDefId = sourceDefId ?? string.Empty;
            }

            public int CardUid { get; private set; }
            public string ProjectionKey { get; private set; }
            public string SourceDefId { get; private set; }
            public override string ActionName { get { return "ClearEffectCountdownRemaining"; } }

            public override GameActionResult Apply(GameActionContext context)
            {
                if (string.IsNullOrEmpty(ProjectionKey))
                {
                    return GameActionResult.Empty;
                }

                var registry = context.GetModel<CardRegistry>();
                CardInstance card = null;
                if (CardUid > 0)
                {
                    registry.TryGet(CardUid, out card);
                }

                var eventSource = !string.IsNullOrEmpty(SourceDefId)
                    ? SourceDefId
                    : (card != null ? card.DefId ?? string.Empty : string.Empty);

                // 遗物清除：无 CardUid 也必须发出，否则 HUD 无法回退。
                if (card == null && string.IsNullOrEmpty(eventSource))
                {
                    return GameActionResult.Empty;
                }

                var evt = new CoreGameEvent(CoreEventType.EffectCountdownCleared, context.ActionId, ActionName)
                    .WithMessage(ProjectionKey)
                    .WithSource(eventSource, ActionName);
                if (card != null)
                {
                    evt = evt.WithCard(card.Uid);
                }

                return new GameActionResult().AddEvent(evt);
            }
        }

    /// <summary>
    /// 离开战斗时重置 Battle 作用域倒计时（ADR-0035 / #157）：遍历激活的效果实例，
    /// 对 <see cref="ICountdownProjectionTrigger"/> 且有效作用域为 Battle 的倒计时，
    /// 把持有者计数器复位到阈值（period）并广播剩余提交（投影同步回阈值）。
    /// 机关默认 Battle；遗物累计计数默认 Run（跨战斗保留），仅显式 <c>scope:battle</c> 时离战重置。
    /// 作用域标记仅作者/系统可见，绝不进入玩家文本。
    /// </summary>
    public sealed class ResetBattleScopedCountdownsAction : GameAction
    {
        public override string ActionName { get { return "ResetBattleScopedCountdowns"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var result = new GameActionResult();
            var effectSystem = context.GetSystem<IEffectSystem>();
            var registry = context.GetModel<CardRegistry>();
            var instances = effectSystem.Instances;
            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                if (instance == null || instance.Trigger == null)
                {
                    continue;
                }

                var countdown = instance.Trigger as ICountdownProjectionTrigger;
                if (countdown == null
                    || CountdownScopePolicy.ResolveResetScope(instance.Owner, countdown) != CountdownScope.Battle
                    || string.IsNullOrEmpty(countdown.CountdownProjectionKey)
                    || instance.Owner == null)
                {
                    continue;
                }

                CardInstance owner = null;
                if (instance.Owner.OwnerUid > 0)
                {
                    registry.TryGet(instance.Owner.OwnerUid, out owner);
                }
                else if (instance.Owner.ContainerType == EffectContainerType.Relic)
                {
                    // 遗物计数器落在 Avatar（与 OnCumulative 等触发器一致）。
                    var avatarUid = context.GetModel<BoardModel>().AvatarUid.Value;
                    if (avatarUid > 0)
                    {
                        registry.TryGet(avatarUid, out owner);
                    }
                }

                if (owner == null)
                {
                    continue;
                }

                var counterKey = countdown.ResolveCounterKey(instance.InstanceId);
                owner.Counters.Set(counterKey, Math.Max(1, countdown.CountdownPeriod));
                result.AddFollowUp(new CommitEffectCountdownRemainingAction(
                    instance.Owner.OwnerUid > 0 ? owner.Uid : 0,
                    countdown.CountdownProjectionKey,
                    counterKey,
                    instance.Owner.SourceDefId));
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
            var result = new GameActionResult();
            // #78：ForceBattle 本身是交战。外层已在交战窗内（如 OnBattle→ForceBattle）则复用；
            // 独立触发（如 OnSelfMove→ForceBattle）须自开 Begin…End，否则 OnBattle 不会 raise。
            var openOwnScope = !context.GetSystem<IBattleScopeSystem>().IsEngagementActive;
            if (openOwnScope)
            {
                result.AddFollowUp(new BeginPlayerMonsterEngagementAction(target.Uid));
            }

            if (CombatEngagementOrder.MonsterStrikesFirst(statSystem, avatar, target))
            {
                result.AddFollowUp(new DealDamageAction(target.Uid, avatar.Uid, targetDamage, SourceDefId, Cause));
                result.AddFollowUp(new ConditionalDealDamageIfAliveAction(avatar.Uid, target.Uid, avatarDamage, SourceDefId, Cause));
            }
            else
            {
                result.AddFollowUp(new DealDamageAction(avatar.Uid, target.Uid, avatarDamage, SourceDefId, Cause));
                result.AddFollowUp(new ConditionalDealDamageIfAliveAction(target.Uid, avatar.Uid, targetDamage, SourceDefId, Cause));
            }

            if (openOwnScope)
            {
                result.AddFollowUp(new EndBattleScopeCleanupAction());
            }

            return result;
        }

        private static int GetAttackDamage(IStatSystem statSystem, CardInstance card)
        {
            // 与 PhaseSystem.GetAttackDamage 同口径：怪物攻击含 EnemyAttackDelta 规则（龙鳞甲等）。
            var damage = statSystem.GetEffectiveInt(card, StatId.Attack);
            if (card.Kind == CardKind.Monster)
            {
                damage += (int)Math.Round(
                    statSystem.EvaluateRule(RuleId.EnemyAttackDelta, 0f, statSystem.CreateContext(card)));
            }

            return Math.Max(0, damage);
        }
    }

    public sealed class MoveCardAction : GameAction
    {
        private static readonly TriggerPoint[] sPreTriggers =
        {
            TriggerPoint.BeforeAction,
            TriggerPoint.BeforeBoardMotion
        };

        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnMove,
            TriggerPoint.OnMoveToSlot
        };

        private static readonly TriggerPoint[] sDealPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnDeal,
            TriggerPoint.OnEnter
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

            // 入场落地不是盘面换格：发牌应走 CardDealt，避免移动位置效果误触发。
            var isDeal = card.Kind != CardKind.Avatar
                && !fromSlot.IsBoardSlot
                && ToSlot.IsBoardSlot;
            var eventType = isDeal ? CoreEventType.CardDealt : CoreEventType.CardMoved;
            var gameEvent = new CoreGameEvent(eventType, context.ActionId, ActionName)
                .WithCard(CardUid)
                .WithSlots(fromSlot, ToSlot)
                .WithSource(SourceDefId, Cause);
            var result = new GameActionResult();
            if (isDeal)
            {
                result.AddWithFaceAbsolutes(context, card, gameEvent);
            }
            else
            {
                result.AddEvent(gameEvent);
            }

            CardRhythmMoveTicks.AppendFromMovedEvents(result, context, result.Events);
            return result;
        }

        public override IEnumerable<TriggerPoint> GetPreTriggerPoints(GameActionContext context)
        {
            return sPreTriggers;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            if (events != null)
            {
                for (var i = 0; i < events.Count; i++)
                {
                    if (events[i].Type == CoreEventType.CardDealt)
                    {
                        return sDealPostTriggers;
                    }
                }
            }

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

        public ShuffleIntoDrawPileAction(
            string defId,
            CardKind kind,
            int count,
            bool top,
            string cause = null,
            bool deferDuringBoardStabilization = false)
        {
            DefId = defId ?? string.Empty;
            Kind = kind;
            Count = Math.Max(0, count);
            Top = top;
            Cause = cause ?? string.Empty;
            DeferDuringBoardStabilization = deferDuringBoardStabilization;
        }

        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public int Count { get; private set; }
        public bool Top { get; private set; }
        public string Cause { get; private set; }
        public bool DeferDuringBoardStabilization { get; private set; }
        public override string ActionName { get { return "ShuffleIntoDrawPile"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var content = context.GetSystem<IContentSystem>();
            if (FormalContentWiring.IsUnofficialDefId(content != null ? content.Catalog : null, DefId))
            {
                return GameActionResult.Empty;
            }

            var registry = context.GetModel<CardRegistry>();
            var deck = context.GetModel<DeckModel>();
            var result = new GameActionResult();

            var rng = context.Architecture.GetUtility<IRngUtility>();
            for (var i = 0; i < Count; i++)
            {
                var card = CreateConfiguredCard(context, registry, DefId, Kind);
                deck.AddToDrawPile(card, Top);
                if (!Top)
                {
                    DrawPileInsertRules.RandomizeNonTopInsert(deck, card.Uid, rng);
                }

                if (DeferDuringBoardStabilization)
                {
                    context.GetSystem<IBoardStabilizationSystem>().DeferDrawUid(card.Uid, Top);
                }
                result.AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                        .WithCard(card.Uid)
                        .WithMessage("shuffleInto:" + DefId)
                        .WithSource(DefId, Cause));
            }

            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
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
                DrawPileInsertRules.RandomizeNonTopInsert(
                    deck,
                    card.Uid,
                    context.Architecture.GetUtility<IRngUtility>());
            }

            return new GameActionResult()
                .AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                        .WithCard(card.Uid)
                        .WithSlots(fromSlot, SlotId.None)
                        .WithMessage("shuffleExisting:" + card.DefId)
                        .WithSource(card.DefId, Cause));
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
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

        public SpawnCardAction(
            string defId,
            CardKind kind,
            ZoneId zone,
            SlotId slot,
            int count,
            string cause = null,
            bool nodeStartDrawPileGrant = false)
        {
            DefId = defId ?? string.Empty;
            Kind = kind;
            Zone = zone;
            Slot = slot;
            Count = Math.Max(0, count);
            Cause = cause ?? string.Empty;
            NodeStartDrawPileGrant = nodeStartDrawPileGrant;
        }

        public string DefId { get; private set; }
        public CardKind Kind { get; private set; }
        public ZoneId Zone { get; private set; }
        public SlotId Slot { get; private set; }
        public int Count { get; private set; }
        public string Cause { get; private set; }
        public bool NodeStartDrawPileGrant { get; private set; }
        public override string ActionName { get { return "SpawnCard"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var content = context.GetSystem<IContentSystem>();
            if (FormalContentWiring.IsUnofficialDefId(content != null ? content.Catalog : null, DefId))
            {
                return GameActionResult.Empty;
            }

            var registry = context.GetModel<CardRegistry>();
            var board = context.GetModel<BoardModel>();
            var deck = context.GetModel<DeckModel>();
            var result = new GameActionResult();

            // 战斗互动期向 PlayerCardPool 授予帮助卡（如黄金鱼竿获得时给宝箱卡）：
            // 玩家侧暂存池只在开局发牌被消费，互动期投入会滞留死区、下节点 ClearBattleZones 直接丢卡。
            // 改洗入抽牌堆（与战斗内奖励帮助卡同语义），空位补牌时自然上场。
            if (Count > 0
                && Zone == ZoneId.PlayerCardPool
                && Kind == CardKind.HelpCard
                && !NodeStartDrawPileGrant
                && context.GetModel<RunModel>().Phase.Value == GamePhase.InteractionLoop)
            {
                result.AddFollowUp(new ShuffleIntoDrawPileAction(DefId, Kind, Count, false, Cause));
                return result;
            }

            for (var i = 0; i < Count; i++)
            {
                if (NodeStartDrawPileGrant
                    && Zone == ZoneId.PlayerCardPool
                    && Kind == CardKind.HelpCard)
                {
                    var grantCard = CreateConfiguredCard(context, registry, DefId, Kind);
                    grantCard.Counters.Set(CoreCounterKeys.PlayerSideDeck, 1);
                    deck.AddToDrawPile(grantCard, false);
                    result.AddWithFaceAbsolutes(
                        context,
                        grantCard,
                        new CoreGameEvent(CoreEventType.CardSpawned, context.ActionId, ActionName)
                            .WithCard(grantCard.Uid)
                            .WithMessage(DefId)
                            .WithSource(DefId, Cause));
                    continue;
                }

                if (Zone == ZoneId.PlayerCardPool
                    && Kind == CardKind.HelpCard
                    && HelpCardGrantRouting.ShouldGrantToItemSlots(context))
                {
                    var player = context.GetModel<PlayerModel>();
                    if (player.IsItemSlotsFull(deck))
                    {
                        continue;
                    }

                    var holdCard = CreateConfiguredCard(context, registry, DefId, Kind);
                    deck.AddToItemSlots(holdCard);
                    result.AddWithFaceAbsolutes(
                        context,
                        holdCard,
                        new CoreGameEvent(CoreEventType.CardSpawned, context.ActionId, ActionName)
                            .WithCard(holdCard.Uid)
                            .WithMessage(DefId)
                            .WithSource(DefId, Cause));
                    continue;
                }

                // 同槽竞态（如死亡召唤+死亡之主同拍、复活石亡语+死亡之主）：先到先得，后到放弃。
                if (Zone == ZoneId.Board && Slot.IsBoardSlot && !board.IsEmpty(Slot))
                {
                    continue;
                }

                if (Zone == ZoneId.ItemSlots
                    && context.GetModel<PlayerModel>().IsItemSlotsFull(deck))
                {
                    continue;
                }

                var card = CreateConfiguredCard(context, registry, DefId, Kind);
                Place(card, board, deck);

                result.AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(CoreEventType.CardSpawned, context.ActionId, ActionName)
                        .WithCard(card.Uid)
                        .WithSlots(SlotId.None, card.Slot.Value)
                        .WithMessage(DefId)
                        .WithSource(DefId, Cause));
                if (card.Zone.Value == ZoneId.Board)
                {
                    result.AddWithFaceAbsolutes(
                        context,
                        card,
                        new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
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
                if (!Top)
                {
                    DrawPileInsertRules.RandomizeNonTopInsert(deck, card.Uid, rng);
                }

                result.AddWithFaceAbsolutes(
                    context,
                    card,
                    new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                        .WithCard(card.Uid)
                        .WithMessage("shuffleRandom:" + definition.DefId)
                        .WithSource(definition.DefId, SourceDefId));
            }

            return result;
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        public static List<CardContentDefinition> FindCatalogCandidates(
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

                if (card.IsReserve)
                {
                    continue;
                }

                if (FormalContentWiring.IsUnofficialDeck(card.DeckId))
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
            DrawPileInsertRules.RandomizeNonTopInsert(deck, target.Uid, rng);

            // 换出腿必须先于换入 Deal：Present 投影为 Remove→Deal，避免幽灵占格导致 placeDenied。
            return new GameActionResult()
                .AddWithFaceAbsolutes(
                    context,
                    target,
                    new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                        .WithCard(target.Uid)
                        .WithSlots(fromSlot, SlotId.None)
                        .WithMessage("exchangeToDraw:" + target.DefId)
                        .WithSource(target.DefId, SourceDefId))
                .AddWithFaceAbsolutes(
                    context,
                    drawn,
                    new CoreGameEvent(CoreEventType.CardDealt, context.ActionId, ActionName)
                        .WithCard(drawn.Uid)
                        .WithSlots(SlotId.None, fromSlot)
                        .WithMessage("exchangeDraw:" + drawn.DefId)
                        .WithSource(drawn.DefId, SourceDefId));
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
            : this(targetUid, stat, op, value, layer, scope, source, sourceDefId, null, false)
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
            : this(targetUid, stat, op, value, layer, scope, source, sourceDefId, condition, false)
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
            IStatCondition condition,
            bool replaceSameSource)
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
            ReplaceSameSource = replaceSameSource;
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
        public bool ReplaceSameSource { get; private set; }
        public override string ActionName { get { return "AddStatModifier"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var card = context.GetModel<CardRegistry>().Get(TargetUid);
            var statSystem = context.GetSystem<IStatSystem>();
            var source = new ModifierSource(Source);
            if (ReplaceSameSource)
            {
                statSystem.RemoveModifiersBySource(card, source);
            }

            var modifier = new StatModifier(Stat, Op, Value, Layer, source, Scope, Condition);
            statSystem.AddModifier(card, modifier);

            // 卡面有效攻/甲变化由统一对账缝自动提交（ADR-0045），无需在此手工补发。
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
            : this(targetUid, true, 0, CardKind.Unknown, rule, op, value, layer, scope, source, string.Empty, string.Empty)
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
            : this(targetUid, useTargetCondition, actorUid, targetKind, rule, op, value, layer, scope, source, string.Empty, string.Empty, string.Empty)
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
            string source,
            string sourceAction,
            string excludeSourcePrefix)
            : this(targetUid, useTargetCondition, actorUid, targetKind, rule, op, value, layer, scope, source, sourceAction, string.Empty, excludeSourcePrefix, false)
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
            string source,
            string sourceAction,
            string sourceDefId,
            string excludeSourcePrefix)
            : this(targetUid, useTargetCondition, actorUid, targetKind, rule, op, value, layer, scope, source, sourceAction, sourceDefId, excludeSourcePrefix, false)
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
            string source,
            string sourceAction,
            string sourceDefId,
            string excludeSourcePrefix,
            bool requireEmptySource)
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
            SourceAction = sourceAction ?? string.Empty;
            SourceDefId = sourceDefId ?? string.Empty;
            ExcludeSourcePrefix = excludeSourcePrefix ?? string.Empty;
            RequireEmptySource = requireEmptySource;
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
        public string SourceAction { get; private set; }
        public string SourceDefId { get; private set; }
        public string ExcludeSourcePrefix { get; private set; }

        /// <summary>
        /// 为真时仅匹配来源为空（SourceDefId 为空）的结算——即玩家直接交战攻击，
        /// 遗物齐射 / 机关伤害等带 defId 来源的伤害不满足（暴力卡回归修复）。
        /// </summary>
        public bool RequireEmptySource { get; private set; }
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

            if (!string.IsNullOrEmpty(SourceAction) || !string.IsNullOrEmpty(SourceDefId))
            {
                conditions.Add(new ActionSourceCondition(SourceAction, SourceDefId, string.Empty, string.Empty, string.Empty));
            }

            if (!string.IsNullOrEmpty(ExcludeSourcePrefix))
            {
                conditions.Add(new ExcludeSourcePrefixCondition(ExcludeSourcePrefix));
            }

            if (RequireEmptySource)
            {
                conditions.Add(new EmptySourceCondition());
            }

            IStatCondition condition = conditions.Count == 0
                ? null
                : conditions.Count == 1 ? conditions[0] : new AllStatCondition(conditions);
            var modifier = new RuleModifier(Rule, Op, Value, Layer, new ModifierSource(Source), Scope, condition);
            context.GetSystem<IStatSystem>().RuleModifiers.Add(modifier);

            // 玩家下一击乘区投影（DamageMultiplier 等）由统一对账缝按 Avatar 攻 oracle 自动提交（ADR-0045）。
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectModifierApplied, context.ActionId, ActionName)
                    .WithCard(TargetUid)
                    .WithAmount((int)Rule)
                    .WithDelta((int)Math.Round(Value))
                    .WithMessage(Source));
        }
    }

    /// <summary>#121：按 ModifierSource 移除 RuleModifier。</summary>
    public sealed class RemoveRuleModifiersBySourceAction : GameAction
    {
        public RemoveRuleModifiersBySourceAction(string source)
        {
            Source = source ?? string.Empty;
        }

        public string Source { get; private set; }
        public override string ActionName { get { return "RemoveRuleModifiersBySource"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            if (string.IsNullOrEmpty(Source))
            {
                return GameActionResult.Empty;
            }

            context.GetSystem<IStatSystem>().RuleModifiers.RemoveBySource(new ModifierSource(Source));
            return GameActionResult.Empty;
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

    public sealed class DeactivateEffectAction : GameAction
    {
        public DeactivateEffectAction(string instanceId, bool removeOwnerRelic = false)
        {
            InstanceId = instanceId ?? string.Empty;
            RemoveOwnerRelic = removeOwnerRelic;
        }

        public string InstanceId { get; private set; }

        /// <summary>
        /// 遗物容器效果显式声明「本遗物随效果消耗」（如凤凰羽毛）时为 true：
        /// 反激活同时把遗物从装备栏移除。默认 false——一次性效果（如黄金鱼竿
        /// 获得时给宝箱卡）只停用效果本身、遗物留在装备栏，并落消费标记防止
        /// 存档恢复 / 跨层重装 / StartNode 自愈重挂时重复发放。
        /// </summary>
        public bool RemoveOwnerRelic { get; private set; }

        public override string ActionName { get { return "DeactivateEffect"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            var effectSystem = context.GetSystem<IEffectSystem>();
            EffectInstance instance;
            if (!effectSystem.TryGetInstance(InstanceId, out instance))
            {
                return GameActionResult.Empty;
            }

            // #157：效果卸载时若其倒计时投影键已提交过剩余，广播清除，避免失效效果继续投影。
            var countdown = instance.Trigger as ICountdownProjectionTrigger;
            var projectionKey = countdown != null ? countdown.CountdownProjectionKey : string.Empty;
            var ownerUid = instance.Owner != null ? instance.Owner.OwnerUid : 0;
            var sourceDefId = instance.Owner != null ? instance.Owner.SourceDefId : string.Empty;
            var isRelic = instance.Owner != null
                && instance.Owner.ContainerType == EffectContainerType.Relic;

            if (isRelic)
            {
                var player = context.GetModel<PlayerModel>();
                if (RemoveOwnerRelic)
                {
                    player.RemoveRelic(sourceDefId);
                    RelicConsumedEffectMarks.ClearForRelic(context, player, sourceDefId);
                }
                else if (instance.Definition != null && !string.IsNullOrEmpty(instance.Definition.Id))
                {
                    player.MarkRelicEffectConsumed(instance.Definition.Id);
                }
            }

            effectSystem.Deactivate(InstanceId);
            var result = new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.EffectDeactivated, context.ActionId, ActionName)
                    .WithMessage(InstanceId));

            if (!string.IsNullOrEmpty(projectionKey) && (ownerUid > 0 || isRelic))
            {
                result.AddFollowUp(new ClearEffectCountdownRemainingAction(
                    ownerUid,
                    projectionKey,
                    sourceDefId));
            }

            return result;
        }
    }

    public sealed class DeactivateOwnerEffectsAction : GameAction
    {
        public DeactivateOwnerEffectsAction(int ownerUid, string reason)
        {
            OwnerUid = ownerUid;
            Reason = reason ?? string.Empty;
        }

        public int OwnerUid { get; private set; }
        public string Reason { get; private set; }
        public override string ActionName { get { return "DeactivateOwnerEffects"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            if (OwnerUid == 0)
            {
                return GameActionResult.Empty;
            }

            // 光环卸载后的卡面有效攻回落由统一对账缝自动提交（ADR-0045）。
            var ids = context.GetSystem<IContentSystem>().DeactivateRuntimeEffectsByOwner(OwnerUid);
            var result = new GameActionResult();
            for (var i = 0; i < ids.Count; i++)
            {
                result.AddEvent(new CoreGameEvent(CoreEventType.EffectDeactivated, context.ActionId, ActionName)
                    .WithCard(OwnerUid)
                    .WithMessage(ids[i])
                    .WithSource(string.Empty, Reason));
            }

            return result;
        }
    }

    /// <summary>
    /// 邻接图腾借甲：每转盘一笔贷款——位移开始前 <see cref="BorrowedArmorSyncPhase.Settle"/> 收回未耗借甲，
    /// 落地后 <see cref="BorrowedArmorSyncPhase.Grant"/> 对邻接目标无条件再借；交战中打掉不补，下次位移再借。
    /// </summary>
    public sealed class SyncAdjacentBorrowedArmorAction : GameAction
    {
        private static readonly TriggerPoint[] sPostTriggers =
        {
            TriggerPoint.AfterAction,
            TriggerPoint.OnArmorGained
        };

        public SyncAdjacentBorrowedArmorAction(
            int targetUid,
            int sourceUid,
            int value,
            string source,
            string sourceDefId = null,
            BorrowedArmorSyncPhase phase = BorrowedArmorSyncPhase.Grant)
        {
            TargetUid = targetUid;
            SourceUid = sourceUid;
            Value = Math.Max(0, value);
            Source = source ?? string.Empty;
            SourceDefId = sourceDefId ?? string.Empty;
            Phase = phase;
        }

        public int TargetUid { get; private set; }
        public int SourceUid { get; private set; }
        public int Value { get; private set; }
        public string Source { get; private set; }
        public string SourceDefId { get; private set; }
        public BorrowedArmorSyncPhase Phase { get; private set; }
        public override string ActionName { get { return "SyncAdjacentBorrowedArmor"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            if (Value <= 0)
            {
                return GameActionResult.Empty;
            }

            var registry = context.GetModel<CardRegistry>();
            CardInstance target;
            CardInstance source;
            if (!registry.TryGet(TargetUid, out target)
                || target == null
                || !registry.TryGet(SourceUid, out source)
                || source == null)
            {
                return GameActionResult.Empty;
            }

            return Phase == BorrowedArmorSyncPhase.Settle
                ? ApplySettle(context, target)
                : ApplyGrant(context, target, source);
        }

        private GameActionResult ApplySettle(GameActionContext context, CardInstance target)
        {
            var counterKey = BorrowedArmorAuraKeys.BaselineKey(SourceUid);
            if (!BorrowedArmorAuraKeys.IsTracking(target, SourceUid))
            {
                return GameActionResult.Empty;
            }

            var baselineStored = target.Counters.Get(counterKey);
            target.Counters.Remove(counterKey);
            var current = StatArmorUtility.GetCurrentArmor(target);
            var remove = Math.Min(Value, Math.Max(0, current - baselineStored));
            if (remove <= 0)
            {
                return GameActionResult.Empty;
            }

            var reclaimedArmor = current - remove;
            StatArmorUtility.SetCurrentArmor(target, reclaimedArmor);
            return EmitArmorChanged(context, target, -remove, reclaimedArmor, BorrowedArmorAuraKeys.DecayCause);
        }

        private GameActionResult ApplyGrant(GameActionContext context, CardInstance target, CardInstance source)
        {
            var boardSystem = context.GetSystem<IBoardSystem>();
            if (!boardSystem.AreAdjacent(source, target))
            {
                return GameActionResult.Empty;
            }

            var counterKey = BorrowedArmorAuraKeys.BaselineKey(SourceUid);
            var current = StatArmorUtility.GetCurrentArmor(target);
            var isTracking = BorrowedArmorAuraKeys.IsTracking(target, SourceUid);

            // 双重检查：邻接但 token/甲量不一致时强制对齐（兜 settle 漏跑）。
            if (isTracking)
            {
                var baselineStored = target.Counters.Get(counterKey);
                if (current >= baselineStored + Value)
                {
                    return GameActionResult.Empty;
                }

                target.Counters.Remove(counterKey);
            }

            var baseline = current;
            target.Counters.Set(counterKey, baseline);
            var newArmor = current + Value;
            StatArmorUtility.SetCurrentArmor(target, newArmor);
            return EmitArmorChanged(context, target, Value, newArmor, Source);
        }

        public override IEnumerable<TriggerPoint> GetPostTriggerPoints(GameActionContext context, IReadOnlyList<CoreGameEvent> events)
        {
            return sPostTriggers;
        }

        private GameActionResult EmitArmorChanged(GameActionContext context, CardInstance target, int delta, int newArmor, string cause)
        {
            // ArmorChanged 已携带绝对甲；漏发场景由统一对账缝兜住（ADR-0045）。
            var hp = Math.Max(0, (int)Math.Round(target.Stats.GetBase(StatId.Hp)));
            return new GameActionResult()
                .AddEvent(new CoreGameEvent(CoreEventType.ArmorChanged, context.ActionId, ActionName)
                    .WithTarget(target.Uid)
                    .WithCard(target.Uid)
                    .WithDelta(delta)
                    .WithRemaining(hp, newArmor)
                    .WithSource(SourceDefId, cause));
        }
    }

    /// <summary>#111 / ADR-0026：离开技能 — 置清关标志（#113：即 <c>IsNodeCleared</c>）。</summary>
    public sealed class MarkLeaveTrapBrokenAction : GameAction
    {
        public override string ActionName { get { return "MarkLeaveTrapBroken"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetModel<BattleContextModel>().MarkLeaveTrapBroken();
            return GameActionResult.Empty;
        }
    }
}
