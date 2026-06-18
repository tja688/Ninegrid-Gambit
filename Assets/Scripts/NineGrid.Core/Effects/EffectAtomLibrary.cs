using System;
using System.Collections.Generic;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core.Effects
{
    public abstract class TriggerAtomBase : ITrigger
    {
        private TriggerTiming mTiming = TriggerTiming.Post;

        public abstract TriggerPoint Point { get; }

        public TriggerTiming Timing
        {
            get { return mTiming; }
        }

        public virtual void Configure(EffectDslNode config)
        {
            mTiming = config.Get("timing").AsEnum(TriggerTiming.Post);
        }

        public virtual bool Matches(EffectRuntimeContext context)
        {
            return context != null && context.TriggerContext != null;
        }

        protected static bool HasEvent(EffectRuntimeContext context, CoreEventType type)
        {
            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnBattle", EffectAtomKind.Trigger)]
    public sealed class OnBattleTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnBattle; } }
    }

    [EffectAtom("OnKill", EffectAtomKind.Trigger)]
    public sealed class OnKillTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnKill; } }
        public override bool Matches(EffectRuntimeContext context) { return base.Matches(context) && HasEvent(context, CoreEventType.CardKilled); }
    }

    [EffectAtom("OnRemove", EffectAtomKind.Trigger)]
    public sealed class OnRemoveTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnRemove; } }
        public override bool Matches(EffectRuntimeContext context) { return base.Matches(context) && HasEvent(context, CoreEventType.CardRemoved); }
    }

    [EffectAtom("OnUseHelpCard", EffectAtomKind.Trigger)]
    public sealed class OnUseHelpCardTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnUseHelpCard; } }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.ItemUsed
                    && (context.OwnerUid == 0 || events[i].CardUid == context.OwnerUid))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnNodeStart", EffectAtomKind.Trigger)]
    public sealed class OnNodeStartTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnNodeStart; } }
    }

    [EffectAtom("OnNodeEnd", EffectAtomKind.Trigger)]
    public sealed class OnNodeEndTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnNodeEnd; } }
    }

    [EffectAtom("OnRotate", EffectAtomKind.Trigger)]
    public sealed class OnRotateTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnRotate; } }
    }

    [EffectAtom("OnInteract", EffectAtomKind.Trigger)]
    public sealed class OnInteractTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnInteract; } }
    }

    [EffectAtom("OnSelfMove", EffectAtomKind.Trigger)]
    public sealed class OnSelfMoveTrigger : TriggerAtomBase
    {
        private int mEvery = 1;
        private string mCounterKey = string.Empty;

        public override TriggerPoint Point { get { return TriggerPoint.OnMove; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mEvery = Math.Max(1, config.Get("every").AsInt(1));
            mCounterKey = config.Get("counterKey").AsString(string.Empty);
        }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context) || context.OwnerUid == 0)
            {
                return false;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type != CoreEventType.CardMoved || events[i].CardUid != context.OwnerUid)
                {
                    continue;
                }

                if (mEvery <= 1)
                {
                    return true;
                }

                var owner = context.OwnerCard;
                if (owner == null)
                {
                    return false;
                }

                var key = string.IsNullOrEmpty(mCounterKey)
                    ? CoreCounterKeys.EffectCounterPrefix + context.Instance.InstanceId + ".selfMove"
                    : mCounterKey;
                owner.Counters.Add(key, 1);
                return owner.Counters.Get(key) % mEvery == 0;
            }

            return false;
        }
    }

    [EffectAtom("OnMoveToSlot", EffectAtomKind.Trigger)]
    public sealed class OnMoveToSlotTrigger : TriggerAtomBase
    {
        private SlotId mSlot = SlotId.None;
        private string mTargetRef = "Any";

        public override TriggerPoint Point { get { return TriggerPoint.OnMoveToSlot; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mSlot = SlotId.Board(config.Get("slot").AsInt(1));
            mTargetRef = config.Get("target").AsString("Any");
        }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            var expectedUid = TargetResolver.ResolveSingleCardRef(context, mTargetRef);
            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.CardMoved
                    && events[i].ToSlot == mSlot
                    && (expectedUid == 0 || events[i].CardUid == expectedUid))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnEnter", EffectAtomKind.Trigger)]
    public sealed class OnEnterTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnEnter; } }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.CardDealt
                    && (context.OwnerUid == 0 || events[i].CardUid == context.OwnerUid))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnArmorBreak", EffectAtomKind.Trigger)]
    public sealed class OnArmorBreakTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnArmorBreak; } }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.ArmorChanged && events[i].Delta < 0 && events[i].RemainingArmor <= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnDamageTaken", EffectAtomKind.Trigger)]
    public sealed class OnDamageTakenTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnDamageTaken; } }
        public override bool Matches(EffectRuntimeContext context) { return base.Matches(context) && HasNegativeHpChange(context); }

        private static bool HasNegativeHpChange(EffectRuntimeContext context)
        {
            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.HpChanged && events[i].Delta < 0)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnFatalDamage", EffectAtomKind.Trigger)]
    public sealed class OnFatalDamageTrigger : TriggerAtomBase
    {
        private string mTargetRef = "Any";

        public override TriggerPoint Point { get { return TriggerPoint.OnFatalDamage; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mTargetRef = config.Get("target").AsString("Any");
        }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            var expectedUid = TargetResolver.ResolveSingleCardRef(context, mTargetRef);
            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.HpChanged
                    && events[i].Delta < 0
                    && events[i].RemainingHp <= 0
                    && (expectedUid == 0 || events[i].CardUid == expectedUid))
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnCumulative", EffectAtomKind.Trigger)]
    public sealed class OnCumulativeTrigger : TriggerAtomBase
    {
        private string mMetric = "armorLost";
        private int mThreshold = 1;
        private string mCounterKey = string.Empty;

        public override TriggerPoint Point { get { return TriggerPoint.OnCumulative; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mMetric = config.Get("metric").AsString("armorLost");
            mThreshold = Math.Max(1, config.Get("threshold").AsInt(1));
            mCounterKey = config.Get("counterKey").AsString(string.Empty);
        }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            var delta = MeasureDelta(context);
            if (delta <= 0)
            {
                return false;
            }

            var owner = context.OwnerCard ?? context.AvatarCard;
            if (owner == null)
            {
                return false;
            }

            var key = string.IsNullOrEmpty(mCounterKey)
                ? CoreCounterKeys.EffectCounterPrefix + context.Instance.InstanceId + ".cumulative." + mMetric
                : mCounterKey;
            owner.Counters.Add(key, delta);
            if (owner.Counters.Get(key) < mThreshold)
            {
                return false;
            }

            owner.Counters.Add(key, -mThreshold);
            return true;
        }

        private int MeasureDelta(EffectRuntimeContext context)
        {
            var sum = 0;
            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (Same(mMetric, "armorLost") && events[i].Type == CoreEventType.ArmorChanged && events[i].Delta < 0)
                {
                    sum += -events[i].Delta;
                }
                else if (Same(mMetric, "hpLost") && events[i].Type == CoreEventType.HpChanged && events[i].Delta < 0)
                {
                    sum += -events[i].Delta;
                }
            }

            return sum;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    [EffectAtom("Self", EffectAtomKind.Target)]
    public sealed class SelfTarget : ITarget
    {
        public void Configure(EffectDslNode config) { }
        public IReadOnlyList<int> Resolve(EffectRuntimeContext context) { return TargetResolver.Single(context.OwnerUid); }
    }

    [EffectAtom("Player", EffectAtomKind.Target)]
    public sealed class PlayerTarget : ITarget
    {
        public void Configure(EffectDslNode config) { }
        public IReadOnlyList<int> Resolve(EffectRuntimeContext context) { return TargetResolver.Single(context.AvatarUid); }
    }

    [EffectAtom("EventCard", EffectAtomKind.Target)]
    public sealed class EventCardTarget : ITarget
    {
        public void Configure(EffectDslNode config) { }
        public IReadOnlyList<int> Resolve(EffectRuntimeContext context) { return TargetResolver.Single(context.FirstEventCardUid()); }
    }

    [EffectAtom("RandomMonster", EffectAtomKind.Target)]
    public sealed class RandomMonsterTarget : ITarget
    {
        public void Configure(EffectDslNode config) { }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            var monsters = TargetResolver.MonstersOnBoard(context);
            if (monsters.Count == 0)
            {
                return monsters;
            }

            return TargetResolver.Single(monsters[context.Rng.Range(0, monsters.Count)]);
        }
    }

    [EffectAtom("AllMonsters", EffectAtomKind.Target)]
    public sealed class AllMonstersTarget : ITarget
    {
        public void Configure(EffectDslNode config) { }
        public IReadOnlyList<int> Resolve(EffectRuntimeContext context) { return TargetResolver.MonstersOnBoard(context); }
    }

    [EffectAtom("OrthoAdjacent", EffectAtomKind.Target)]
    public sealed class OrthoAdjacentTarget : ITarget
    {
        private string mOriginRef = "Self";

        public void Configure(EffectDslNode config)
        {
            mOriginRef = config.Get("origin").AsString("Self");
        }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            var uid = TargetResolver.ResolveSingleCardRef(context, mOriginRef);
            CardInstance card;
            if (!context.TryGetCard(uid, out card) || !card.Slot.Value.IsBoardSlot)
            {
                return new int[0];
            }

            var result = new List<int>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (!slot.IsAdjacentTo(card.Slot.Value))
                {
                    continue;
                }

                var cardUid = context.Board.GetCardUid(slot);
                if (cardUid != 0)
                {
                    result.Add(cardUid);
                }
            }

            return result;
        }
    }

    [EffectAtom("SlotCard", EffectAtomKind.Target)]
    public sealed class SlotCardTarget : ITarget
    {
        private SlotId mSlot = SlotId.None;

        public void Configure(EffectDslNode config)
        {
            mSlot = SlotId.Board(config.Get("slot").AsInt(1));
        }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            return TargetResolver.Single(context.Board.GetCardUid(mSlot));
        }
    }

    [EffectAtom("Column", EffectAtomKind.Target)]
    public sealed class ColumnTarget : ITarget
    {
        private int mColumn = 0;

        public void Configure(EffectDslNode config)
        {
            var value = config.Get("column").AsInt(1);
            mColumn = value >= 1 && value <= 3 ? value - 1 : value;
        }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            var result = new List<int>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot.Column != mColumn)
                {
                    continue;
                }

                var uid = context.Board.GetCardUid(slot);
                if (uid != 0)
                {
                    result.Add(uid);
                }
            }

            return result;
        }
    }

    [EffectAtom("AdjacentHasCard", EffectAtomKind.Condition)]
    public sealed class AdjacentHasCardEffectCondition : ICondition
    {
        private string mOriginRef = "Self";
        private string mDefId = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mOriginRef = config.Get("origin").AsString("Self");
            mDefId = config.Get("defId").AsString(string.Empty);
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            return ResolveAdjacentCardUid(context, mOriginRef, mDefId) != 0;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return null;
        }

        internal static int ResolveAdjacentCardUid(EffectRuntimeContext context, string originRef, string defId)
        {
            if (context == null || string.IsNullOrEmpty(defId))
            {
                return 0;
            }

            var originUid = TargetResolver.ResolveSingleCardRef(context, originRef);
            CardInstance origin;
            if (!context.TryGetCard(originUid, out origin) || !origin.Slot.Value.IsBoardSlot)
            {
                return 0;
            }

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (!slot.IsAdjacentTo(origin.Slot.Value))
                {
                    continue;
                }

                var cardUid = context.Board.GetCardUid(slot);
                CardInstance card;
                if (context.TryGetCard(cardUid, out card) && card.DefId == defId)
                {
                    return cardUid;
                }
            }

            return 0;
        }
    }

    [EffectAtom("AdjacentCard", EffectAtomKind.Target)]
    public sealed class AdjacentCardTarget : ITarget
    {
        private string mOriginRef = "Self";
        private string mDefId = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mOriginRef = config.Get("origin").AsString("Self");
            mDefId = config.Get("defId").AsString(string.Empty);
        }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            return TargetResolver.Single(AdjacentHasCardEffectCondition.ResolveAdjacentCardUid(context, mOriginRef, mDefId));
        }
    }

    [EffectAtom("AtSlot", EffectAtomKind.Condition)]
    public sealed class AtSlotEffectCondition : ICondition
    {
        private string mTargetRef = "Self";
        private SlotId mSlot = SlotId.None;

        public void Configure(EffectDslNode config)
        {
            mTargetRef = config.Get("target").AsString("Self");
            mSlot = SlotId.Board(config.Get("slot").AsInt(1));
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            var uid = TargetResolver.ResolveSingleCardRef(context, mTargetRef);
            CardInstance card;
            return context.TryGetCard(uid, out card) && card.Slot.Value == mSlot;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return TargetResolver.IsSelfRef(mTargetRef) ? new AtSlotCondition(mSlot) : null;
        }
    }

    [EffectAtom("Adjacent", EffectAtomKind.Condition)]
    public sealed class AdjacentEffectCondition : ICondition
    {
        private string mLeftRef = "Self";
        private string mRightRef = "EventCard";
        private SlotId mFixedSlot = SlotId.None;

        public void Configure(EffectDslNode config)
        {
            mLeftRef = config.Get("left").AsString("Self");
            mRightRef = config.Get("right").AsString("EventCard");
            if (config.Has("slot"))
            {
                mFixedSlot = SlotId.Board(config.Get("slot").AsInt(1));
            }
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            var left = TargetResolver.ResolveSingleCardRef(context, mLeftRef);
            CardInstance leftCard;
            if (!context.TryGetCard(left, out leftCard))
            {
                return false;
            }

            if (mFixedSlot != SlotId.None)
            {
                return leftCard.Slot.Value.IsAdjacentTo(mFixedSlot);
            }

            var right = TargetResolver.ResolveSingleCardRef(context, mRightRef);
            CardInstance rightCard;
            return context.TryGetCard(right, out rightCard) && leftCard.Slot.Value.IsAdjacentTo(rightCard.Slot.Value);
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return mFixedSlot != SlotId.None && TargetResolver.IsSelfRef(mLeftRef)
                ? new AdjacentCondition(mFixedSlot)
                : null;
        }
    }

    [EffectAtom("HpBelow", EffectAtomKind.Condition)]
    public sealed class HpBelowEffectCondition : ICondition
    {
        private string mTargetRef = "Self";
        private float mPct = 0.5f;

        public void Configure(EffectDslNode config)
        {
            mTargetRef = config.Get("target").AsString("Self");
            mPct = config.Get("pct").AsFloat(0.5f);
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            var uid = TargetResolver.ResolveSingleCardRef(context, mTargetRef);
            CardInstance card;
            if (!context.TryGetCard(uid, out card))
            {
                return false;
            }

            var maxHp = card.Stats.GetBase(StatId.MaxHp);
            return maxHp > 0f && card.Stats.GetBase(StatId.Hp) / maxHp < mPct;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return TargetResolver.IsSelfRef(mTargetRef) ? new HpBelowPctCondition(mPct) : null;
        }
    }

    [EffectAtom("HasCard", EffectAtomKind.Condition)]
    public sealed class HasCardEffectCondition : ICondition
    {
        private string mDefId = string.Empty;
        private CardKind mKind = CardKind.Unknown;
        private ZoneId mZone = ZoneId.None;

        public void Configure(EffectDslNode config)
        {
            mDefId = config.Get("defId").AsString(string.Empty);
            mKind = config.Get("kind").AsEnum(CardKind.Unknown);
            mZone = config.Get("zone").AsEnum(ZoneId.None);
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            foreach (var pair in context.Registry.Cards)
            {
                var card = pair.Value;
                if (!string.IsNullOrEmpty(mDefId) && card.DefId != mDefId)
                {
                    continue;
                }

                if (mKind != CardKind.Unknown && card.Kind != mKind)
                {
                    continue;
                }

                if (mZone != ZoneId.None && card.Zone.Value != mZone)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return new HasCardStatCondition(mDefId, mKind, mZone);
        }
    }

    [EffectAtom("OwnsRelicSet", EffectAtomKind.Condition)]
    public sealed class OwnsRelicSetEffectCondition : ICondition
    {
        private readonly List<string> mDefIds = new List<string>();

        public void Configure(EffectDslNode config)
        {
            var values = config.Get("defIds").AsArray();
            for (var i = 0; i < values.Count; i++)
            {
                var defId = values[i].AsString(string.Empty);
                if (!string.IsNullOrEmpty(defId))
                {
                    mDefIds.Add(defId);
                }
            }

            var single = config.Get("defId").AsString(string.Empty);
            if (!string.IsNullOrEmpty(single))
            {
                mDefIds.Add(single);
            }
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            return OwnsAll(context.Player, mDefIds);
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return new OwnsRelicSetStatCondition(mDefIds);
        }

        internal static bool OwnsAll(PlayerModel player, IReadOnlyList<string> defIds)
        {
            if (player == null)
            {
                return false;
            }

            for (var i = 0; i < defIds.Count; i++)
            {
                var found = false;
                for (var j = 0; j < player.RelicDefIds.Count; j++)
                {
                    if (player.RelicDefIds[j] == defIds[i])
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    return false;
                }
            }

            return defIds.Count > 0;
        }
    }

    [EffectAtom("LevelParity", EffectAtomKind.Condition)]
    public sealed class LevelParityEffectCondition : ICondition
    {
        private string mTargetRef = "Self";
        private string mParity = "Even";

        public void Configure(EffectDslNode config)
        {
            mTargetRef = config.Get("target").AsString("Self");
            mParity = config.Get("parity").AsString("Even");
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            var uid = TargetResolver.ResolveSingleCardRef(context, mTargetRef);
            CardInstance card;
            return context.TryGetCard(uid, out card) && IsExpected(card.Counters.Get(CoreCounterKeys.Level), mParity);
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return TargetResolver.IsSelfRef(mTargetRef) ? new LevelParityStatCondition(mParity) : null;
        }

        internal static bool IsExpected(int value, string parity)
        {
            var even = value % 2 == 0;
            return string.Equals(parity, "Even", StringComparison.OrdinalIgnoreCase) ? even : !even;
        }
    }

    [EffectAtom("Sequence", EffectAtomKind.Action)]
    public sealed class SequenceEffectAction : IAction
    {
        private readonly List<EffectDslNode> mActions = new List<EffectDslNode>();

        public void Configure(EffectDslNode config)
        {
            var actions = config.Get("actions").AsArray();
            for (var i = 0; i < actions.Count; i++)
            {
                mActions.Add(actions[i]);
            }
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            var registry = context.Architecture.GetSystem<IEffectSystem>().AtomRegistry;
            for (var i = 0; i < mActions.Count; i++)
            {
                var actionTargets = ActionTargetResolver.Resolve(context, registry, mActions[i], targets);
                var action = registry.CreateAction(mActions[i]);
                var actions = action.BuildActions(context, actionTargets);
                for (var j = 0; j < actions.Count; j++)
                {
                    result.Add(actions[j]);
                }
            }

            return result;
        }
    }

    [EffectAtom("WeightedRandom", EffectAtomKind.Action)]
    public sealed class WeightedRandomEffectAction : IAction
    {
        private readonly List<WeightedAction> mChoices = new List<WeightedAction>();

        public void Configure(EffectDslNode config)
        {
            var choices = config.Get("choices").AsArray();
            for (var i = 0; i < choices.Count; i++)
            {
                mChoices.Add(new WeightedAction(
                    Math.Max(0, choices[i].Get("weight").AsInt(1)),
                    choices[i].Get("action")));
            }
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var total = 0;
            for (var i = 0; i < mChoices.Count; i++)
            {
                total += mChoices[i].Weight;
            }

            if (total <= 0)
            {
                return new GameAction[0];
            }

            var roll = context.Rng.Range(0, total);
            var cursor = 0;
            for (var i = 0; i < mChoices.Count; i++)
            {
                cursor += mChoices[i].Weight;
                if (roll < cursor)
                {
                    var action = context.Architecture.GetSystem<IEffectSystem>().AtomRegistry.CreateAction(mChoices[i].Action);
                    var actionTargets = ActionTargetResolver.Resolve(context, context.Architecture.GetSystem<IEffectSystem>().AtomRegistry, mChoices[i].Action, targets);
                    return action.BuildActions(context, actionTargets);
                }
            }

            return new GameAction[0];
        }

        private sealed class WeightedAction
        {
            public WeightedAction(int weight, EffectDslNode action)
            {
                Weight = weight;
                Action = action;
            }

            public int Weight { get; private set; }
            public EffectDslNode Action { get; private set; }
        }
    }

    [EffectAtom("Repeat", EffectAtomKind.Action)]
    public sealed class RepeatEffectAction : IAction
    {
        private int mCount = 1;
        private EffectDslNode mAction;

        public void Configure(EffectDslNode config)
        {
            mCount = Math.Max(0, config.Get("count").AsInt(1));
            mAction = config.Get("action");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            var registry = context.Architecture.GetSystem<IEffectSystem>().AtomRegistry;
            for (var i = 0; i < mCount; i++)
            {
                var actionTargets = ActionTargetResolver.Resolve(context, registry, mAction, targets);
                var action = registry.CreateAction(mAction);
                var actions = action.BuildActions(context, actionTargets);
                for (var j = 0; j < actions.Count; j++)
                {
                    result.Add(actions[j]);
                }
            }

            return result;
        }
    }

    [EffectAtom("Conditional", EffectAtomKind.Action)]
    public sealed class ConditionalEffectAction : IAction
    {
        private EffectDslNode mCondition;
        private EffectDslNode mAction;
        private EffectDslNode mElseAction;

        public void Configure(EffectDslNode config)
        {
            mCondition = config.Get("condition");
            mAction = config.Get("action");
            mElseAction = config.Get("elseAction");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var registry = context.Architecture.GetSystem<IEffectSystem>().AtomRegistry;
            var condition = registry.CreateCondition(mCondition);
            var selected = condition.IsMet(context) ? mAction : mElseAction;
            if (selected == null || selected.IsNull)
            {
                return new GameAction[0];
            }

            var actionTargets = ActionTargetResolver.Resolve(context, registry, selected, targets);
            return registry.CreateAction(selected).BuildActions(context, actionTargets);
        }
    }

    [EffectAtom("DealDamage", EffectAtomKind.Action)]
    public sealed class DealDamageEffectAction : IAction
    {
        private int mAmount;
        private string mActorRef = "Player";

        public void Configure(EffectDslNode config)
        {
            mAmount = config.Get("amount").AsInt(0);
            mActorRef = config.Get("actor").AsString("Player");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            var actorUid = TargetResolver.ResolveSingleCardRef(context, mActorRef);
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new DealDamageAction(actorUid, targets[i], mAmount));
                }
            }

            return result;
        }
    }

    [EffectAtom("Heal", EffectAtomKind.Action)]
    public sealed class HealEffectAction : IAction
    {
        private int mAmount;
        private string mActorRef = "Self";

        public void Configure(EffectDslNode config)
        {
            mAmount = config.Get("amount").AsInt(0);
            mActorRef = config.Get("actor").AsString("Self");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            var actorUid = TargetResolver.ResolveSingleCardRef(context, mActorRef);
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new HealAction(actorUid, targets[i], mAmount));
                }
            }

            return result;
        }
    }

    [EffectAtom("GainArmor", EffectAtomKind.Action)]
    public sealed class GainArmorEffectAction : IAction
    {
        private int mAmount;

        public void Configure(EffectDslNode config)
        {
            mAmount = config.Get("amount").AsInt(0);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new GainArmorAction(targets[i], mAmount));
                }
            }

            return result;
        }
    }

    [EffectAtom("ModifyGold", EffectAtomKind.Action)]
    public sealed class ModifyGoldEffectAction : IAction
    {
        private int mDelta;
        private string mReason = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mDelta = config.Get("delta").AsInt(0);
            mReason = config.Get("reason").AsString("effect");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[] { new ModifyGoldAction(mDelta, mReason) };
        }
    }

    [EffectAtom("Move", EffectAtomKind.Action)]
    public sealed class MoveEffectAction : IAction
    {
        private SlotId mToSlot = SlotId.None;

        public void Configure(EffectDslNode config)
        {
            mToSlot = SlotId.Board(config.Get("toSlot").AsInt(1));
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new MoveCardAction(targets[i], mToSlot));
                }
            }

            return result;
        }
    }

    [EffectAtom("Swap", EffectAtomKind.Action)]
    public sealed class SwapEffectAction : IAction
    {
        private SlotId mLeft = SlotId.None;
        private SlotId mRight = SlotId.None;

        public void Configure(EffectDslNode config)
        {
            if (config.Has("leftSlot"))
            {
                mLeft = SlotId.Board(config.Get("leftSlot").AsInt(1));
            }

            if (config.Has("rightSlot"))
            {
                mRight = SlotId.Board(config.Get("rightSlot").AsInt(1));
            }
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            if (mLeft != SlotId.None && mRight != SlotId.None)
            {
                return new[] { new SwapBoardSlotsAction(mLeft, mRight) };
            }

            if (targets.Count < 2)
            {
                return new GameAction[0];
            }

            var left = context.Registry.Get(targets[0]).Slot.Value;
            var right = context.Registry.Get(targets[1]).Slot.Value;
            return new[] { new SwapBoardSlotsAction(left, right) };
        }
    }

    [EffectAtom("Rotate", EffectAtomKind.Action)]
    public sealed class RotateEffectAction : IAction
    {
        private int mCount = 1;

        public void Configure(EffectDslNode config)
        {
            mCount = Math.Max(0, config.Get("count").AsInt(1));
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < mCount; i++)
            {
                result.Add(new RotateBoardClockwiseAction());
            }

            return result;
        }
    }

    [EffectAtom("ShuffleInto", EffectAtomKind.Action)]
    public sealed class ShuffleIntoEffectAction : IAction
    {
        private string mDefId = string.Empty;
        private CardKind mKind = CardKind.Monster;
        private int mCount = 1;
        private bool mTop;

        public void Configure(EffectDslNode config)
        {
            mDefId = config.Get("defId").AsString(string.Empty);
            mKind = config.Get("kind").AsEnum(CardKind.Monster);
            mCount = Math.Max(0, config.Get("count").AsInt(1));
            mTop = config.Get("top").AsBool(false);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[] { new ShuffleIntoDrawPileAction(mDefId, mKind, mCount, mTop) };
        }
    }

    [EffectAtom("Spawn", EffectAtomKind.Action)]
    public sealed class SpawnEffectAction : IAction
    {
        private string mDefId = string.Empty;
        private CardKind mKind = CardKind.Monster;
        private ZoneId mZone = ZoneId.DrawPile;
        private SlotId mSlot = SlotId.None;
        private int mCount = 1;

        public void Configure(EffectDslNode config)
        {
            mDefId = config.Get("defId").AsString(string.Empty);
            mKind = config.Get("kind").AsEnum(CardKind.Monster);
            mZone = config.Get("zone").AsEnum(ZoneId.DrawPile);
            mCount = Math.Max(0, config.Get("count").AsInt(1));
            if (config.Has("slot"))
            {
                mSlot = SlotId.Board(config.Get("slot").AsInt(1));
            }
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[] { new SpawnCardAction(mDefId, mKind, mZone, mSlot, mCount) };
        }
    }

    [EffectAtom("AddModifier", EffectAtomKind.Action)]
    public sealed class AddModifierEffectAction : IAction
    {
        private StatId mStat = StatId.Attack;
        private ModifierOp mOp = ModifierOp.Add;
        private float mValue;
        private ModifierLayer mLayer = ModifierLayer.Temporary;
        private ModifierScope mScope = ModifierScope.UntilBattleEnds;
        private string mSource = "effect.action";

        public void Configure(EffectDslNode config)
        {
            mStat = config.Get("stat").AsEnum(StatId.Attack);
            mOp = config.Get("op").AsEnum(ModifierOp.Add);
            mValue = config.Get("value").AsFloat(0f);
            mLayer = config.Get("layer").AsEnum(ModifierLayer.Temporary);
            mScope = config.Get("scope").AsEnum(ModifierScope.UntilBattleEnds);
            mSource = config.Get("source").AsString("effect.action");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new AddStatModifierAction(targets[i], mStat, mOp, mValue, mLayer, mScope, mSource));
                }
            }

            return result;
        }
    }

    [EffectAtom("GrantSkill", EffectAtomKind.Action)]
    public sealed class GrantSkillEffectAction : IAction
    {
        private string mSkillDefId = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mSkillDefId = config.Get("skillDefId").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[] { new GrantSkillAction(mSkillDefId) };
        }
    }

    [EffectAtom("RemoveCard", EffectAtomKind.Action)]
    public sealed class RemoveCardEffectAction : IAction
    {
        private ZoneId mDestination = ZoneId.Removed;
        private string mReason = "effect";

        public void Configure(EffectDslNode config)
        {
            mDestination = config.Get("destination").AsEnum(ZoneId.Removed);
            mReason = config.Get("reason").AsString("effect");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new RemoveCardAction(targets[i], mDestination, mReason));
                }
            }

            return result;
        }
    }

    [EffectAtom("DeactivateSelfEffect", EffectAtomKind.Action)]
    public sealed class DeactivateSelfEffectActionAtom : IAction
    {
        public void Configure(EffectDslNode config) { }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[] { new DeactivateEffectAction(context.Instance.InstanceId) };
        }
    }

    internal static class ActionTargetResolver
    {
        public static IReadOnlyList<int> Resolve(
            EffectRuntimeContext context,
            EffectAtomRegistry registry,
            EffectDslNode actionNode,
            IReadOnlyList<int> defaultTargets)
        {
            if (actionNode == null || actionNode.IsNull || !actionNode.Has("target"))
            {
                return defaultTargets;
            }

            return registry.CreateTarget(actionNode.Get("target")).Resolve(context);
        }
    }

    internal static class TargetResolver
    {
        public static IReadOnlyList<int> Single(int uid)
        {
            return uid == 0 ? new int[0] : new[] { uid };
        }

        public static List<int> MonstersOnBoard(EffectRuntimeContext context)
        {
            var result = new List<int>();
            foreach (var uid in context.Board.BoardCardUids())
            {
                CardInstance card;
                if (context.TryGetCard(uid, out card) && card.Kind == CardKind.Monster)
                {
                    result.Add(uid);
                }
            }

            return result;
        }

        public static int ResolveSingleCardRef(EffectRuntimeContext context, string reference)
        {
            if (context == null)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(reference) || Same(reference, "Any"))
            {
                return 0;
            }

            if (Same(reference, "Self"))
            {
                return context.OwnerUid;
            }

            if (Same(reference, "Player"))
            {
                return context.AvatarUid;
            }

            if (Same(reference, "EventCard"))
            {
                return context.FirstEventCardUid();
            }

            if (Same(reference, "EventTarget"))
            {
                return context.FirstEventTargetUid();
            }

            if (Same(reference, "Actor"))
            {
                var events = context.Events;
                for (var i = 0; i < events.Count; i++)
                {
                    if (events[i].ActorUid != 0)
                    {
                        return events[i].ActorUid;
                    }
                }
            }

            return 0;
        }

        public static bool IsSelfRef(string reference)
        {
            return string.IsNullOrEmpty(reference) || Same(reference, "Self");
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal sealed class HasCardStatCondition : IStatCondition
    {
        private readonly string mDefId;
        private readonly CardKind mKind;
        private readonly ZoneId mZone;

        public HasCardStatCondition(string defId, CardKind kind, ZoneId zone)
        {
            mDefId = defId ?? string.Empty;
            mKind = kind;
            mZone = zone;
        }

        public bool IsMet(StatEvaluationContext context)
        {
            if (context == null || context.Registry == null)
            {
                return false;
            }

            foreach (var pair in context.Registry.Cards)
            {
                var card = pair.Value;
                if (!string.IsNullOrEmpty(mDefId) && card.DefId != mDefId)
                {
                    continue;
                }

                if (mKind != CardKind.Unknown && card.Kind != mKind)
                {
                    continue;
                }

                if (mZone != ZoneId.None && card.Zone.Value != mZone)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }

    internal sealed class OwnsRelicSetStatCondition : IStatCondition
    {
        private readonly IReadOnlyList<string> mDefIds;

        public OwnsRelicSetStatCondition(IReadOnlyList<string> defIds)
        {
            mDefIds = defIds;
        }

        public bool IsMet(StatEvaluationContext context)
        {
            return context != null && OwnsRelicSetEffectCondition.OwnsAll(context.Player, mDefIds);
        }
    }

    internal sealed class LevelParityStatCondition : IStatCondition
    {
        private readonly string mParity;

        public LevelParityStatCondition(string parity)
        {
            mParity = parity ?? "Even";
        }

        public bool IsMet(StatEvaluationContext context)
        {
            return context != null
                && context.Owner != null
                && LevelParityEffectCondition.IsExpected(context.Owner.Counters.Get(CoreCounterKeys.Level), mParity);
        }
    }
}
