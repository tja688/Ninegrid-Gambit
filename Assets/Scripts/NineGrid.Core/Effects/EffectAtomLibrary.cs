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
        private string mSourceAction = string.Empty;
        private CardKind mTargetKind = CardKind.Unknown;
        private int mMaxActionDepth = -1;

        public override TriggerPoint Point { get { return TriggerPoint.OnBattle; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mSourceAction = config.Get("sourceAction").AsString(string.Empty);
            mTargetKind = config.Get("targetKind").AsEnum(CardKind.Unknown);
            mMaxActionDepth = config.Get("maxActionDepth").AsInt(-1);
        }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mSourceAction)
                && (context.TriggerContext.Action == null
                    || !Same(context.TriggerContext.Action.ActionName, mSourceAction)))
            {
                return false;
            }

            if (mMaxActionDepth >= 0
                && (context.ActionContext == null || context.ActionContext.Depth > mMaxActionDepth))
            {
                return false;
            }

            if (mTargetKind != CardKind.Unknown)
            {
                CardInstance target;
                if (!context.TryGetCard(context.FirstEventTargetUid(), out target) || target.Kind != mTargetKind)
                {
                    return false;
                }
            }

            return HasEvent(context, CoreEventType.DamageDealt);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    [EffectAtom("OnKill", EffectAtomKind.Trigger)]
    public sealed class OnKillTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnKill; } }
        public override bool Matches(EffectRuntimeContext context) { return base.Matches(context) && HasEvent(context, CoreEventType.CardKilled); }
    }

    [EffectAtom("OnDeal", EffectAtomKind.Trigger)]
    public sealed class OnDealTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnDeal; } }
        public override bool Matches(EffectRuntimeContext context) { return base.Matches(context) && HasEvent(context, CoreEventType.CardDealt); }
    }

    [EffectAtom("OnEvent", EffectAtomKind.Trigger)]
    public sealed class OnEventTrigger : TriggerAtomBase
    {
        private CoreEventType mEventType = CoreEventType.ActionStarted;
        private bool mHasEventType;

        public override TriggerPoint Point { get { return TriggerPoint.AfterAction; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mHasEventType = config.Has("eventType");
            mEventType = config.Get("eventType").AsEnum(CoreEventType.ActionStarted);
        }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context) || context.Events.Count == 0)
            {
                return false;
            }

            return !mHasEventType || HasEvent(context, mEventType);
        }
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

    [EffectAtom("OnMoveToBoardMark", EffectAtomKind.Trigger)]
    public sealed class OnMoveToBoardMarkTrigger : TriggerAtomBase
    {
        private BoardMarkId mMark = BoardMarkId.Blessed;
        private CardKind mTargetKind = CardKind.Unknown;

        public override TriggerPoint Point { get { return TriggerPoint.OnMoveToSlot; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mMark = config.Get("mark").AsEnum(BoardMarkId.Blessed);
            mTargetKind = config.Get("targetKind").AsEnum(CardKind.Unknown);
        }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context) || mMark == BoardMarkId.None)
            {
                return false;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type != CoreEventType.CardMoved || !events[i].ToSlot.IsBoardSlot)
                {
                    continue;
                }

                if (!context.Board.IsMarked(events[i].ToSlot, mMark))
                {
                    continue;
                }

                if (mTargetKind == CardKind.Unknown)
                {
                    return true;
                }

                CardInstance card;
                if (context.TryGetCard(events[i].CardUid, out card) && card.Kind == mTargetKind)
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

    [EffectAtom("EventTarget", EffectAtomKind.Target)]
    public sealed class EventTargetTarget : ITarget
    {
        public void Configure(EffectDslNode config) { }
        public IReadOnlyList<int> Resolve(EffectRuntimeContext context) { return TargetResolver.Single(context.FirstEventTargetUid()); }
    }

    [EffectAtom("BoardMarkEventCard", EffectAtomKind.Target)]
    public sealed class BoardMarkEventCardTarget : ITarget
    {
        private BoardMarkId mMark = BoardMarkId.Blessed;
        private CardKind mTargetKind = CardKind.Unknown;

        public void Configure(EffectDslNode config)
        {
            mMark = config.Get("mark").AsEnum(BoardMarkId.Blessed);
            mTargetKind = config.Get("targetKind").AsEnum(CardKind.Unknown);
        }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            var result = new List<int>();
            if (context == null || mMark == BoardMarkId.None)
            {
                return result;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type != CoreEventType.CardMoved || !events[i].ToSlot.IsBoardSlot)
                {
                    continue;
                }

                if (!context.Board.IsMarked(events[i].ToSlot, mMark))
                {
                    continue;
                }

                CardInstance card;
                if (!context.TryGetCard(events[i].CardUid, out card))
                {
                    continue;
                }

                if (mTargetKind != CardKind.Unknown && card.Kind != mTargetKind)
                {
                    continue;
                }

                AddUnique(result, card.Uid);
            }

            return result;
        }

        private static void AddUnique(List<int> values, int uid)
        {
            if (uid == 0)
            {
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == uid)
                {
                    return;
                }
            }

            values.Add(uid);
        }
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

    [EffectAtom("FilteredCards", EffectAtomKind.Target)]
    public sealed class FilteredCardsTarget : ITarget
    {
        private readonly List<string> mIncludeRefs = new List<string>();
        private readonly List<string> mExcludeRefs = new List<string>();
        private CardKind mKind = CardKind.Unknown;
        private ZoneId mZone = ZoneId.None;
        private string mAdjacentToRef = string.Empty;
        private bool mRandom;
        private int mCount;

        public void Configure(EffectDslNode config)
        {
            mKind = config.Get("kind").AsEnum(CardKind.Unknown);
            mZone = config.Get("zone").AsEnum(ZoneId.None);
            mAdjacentToRef = config.Get("adjacentTo").AsString(string.Empty);
            mRandom = config.Get("random").AsBool(false);
            mCount = Math.Max(0, config.Get("count").AsInt(mRandom ? 1 : 0));
            AddRefs(config.Get("include"), mIncludeRefs);
            AddRefs(config.Get("exclude"), mExcludeRefs);
        }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            var result = new List<int>();
            for (var i = 0; i < mIncludeRefs.Count; i++)
            {
                AddUnique(result, TargetResolver.ResolveSingleCardRef(context, mIncludeRefs[i]));
            }

            var candidates = TargetResolver.FilteredCards(context, mKind, mZone, mAdjacentToRef, mExcludeRefs);
            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                if (Contains(result, candidates[i]))
                {
                    candidates.RemoveAt(i);
                }
            }

            if (mRandom)
            {
                var take = mCount <= 0 ? 1 : Math.Min(mCount, candidates.Count);
                for (var i = 0; i < take; i++)
                {
                    var index = context.Rng.Range(0, candidates.Count);
                    AddUnique(result, candidates[index]);
                    candidates.RemoveAt(index);
                }

                return result;
            }

            var limit = mCount <= 0 ? candidates.Count : Math.Min(mCount, candidates.Count);
            for (var i = 0; i < limit; i++)
            {
                AddUnique(result, candidates[i]);
            }

            return result;
        }

        private static void AddRefs(EffectDslNode node, List<string> refs)
        {
            var values = node.AsArray();
            if (values.Count > 0)
            {
                for (var i = 0; i < values.Count; i++)
                {
                    AddRef(refs, values[i].AsString(string.Empty));
                }

                return;
            }

            AddRef(refs, node.AsString(string.Empty));
        }

        private static void AddRef(List<string> refs, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                refs.Add(value);
            }
        }

        private static void AddUnique(List<int> values, int uid)
        {
            if (uid != 0 && !Contains(values, uid))
            {
                values.Add(uid);
            }
        }

        private static bool Contains(IReadOnlyList<int> values, int uid)
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == uid)
                {
                    return true;
                }
            }

            return false;
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

    [EffectAtom("CardCounter", EffectAtomKind.Condition)]
    public sealed class CardCounterEffectCondition : ICondition
    {
        private string mTargetRef = "EventCard";
        private string mKey = string.Empty;
        private int mMin = 1;

        public void Configure(EffectDslNode config)
        {
            mTargetRef = config.Get("target").AsString("EventCard");
            mKey = config.Get("key").AsString(string.Empty);
            mMin = Math.Max(1, config.Get("min").AsInt(1));
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            if (string.IsNullOrEmpty(mKey))
            {
                return false;
            }

            var uid = TargetResolver.ResolveSingleCardRef(context, mTargetRef);
            CardInstance card;
            return context.TryGetCard(uid, out card) && card.Counters.Get(mKey) >= mMin;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return null;
        }
    }

    [EffectAtom("EventFilter", EffectAtomKind.Condition)]
    public sealed class EventFilterEffectCondition : ICondition
    {
        private readonly List<CoreEventType> mEventTypes = new List<CoreEventType>();
        private StatId mStat = StatId.Attack;
        private bool mHasStat;
        private int mMinDelta = int.MinValue;
        private CardKind mTargetKind = CardKind.Unknown;
        private string mTargetNotRef = string.Empty;
        private string mSourceDefId = string.Empty;
        private string mExcludeSourceDefId = string.Empty;
        private string mCause = string.Empty;
        private string mExcludeCause = string.Empty;

        public void Configure(EffectDslNode config)
        {
            AddEventType(config.Get("eventType").AsString(string.Empty));
            var eventTypes = config.Get("eventTypes").AsArray();
            for (var i = 0; i < eventTypes.Count; i++)
            {
                AddEventType(eventTypes[i].AsString(string.Empty));
            }

            mHasStat = config.Has("stat");
            mStat = config.Get("stat").AsEnum(StatId.Attack);
            mMinDelta = config.Has("minDelta") ? config.Get("minDelta").AsInt(0) : int.MinValue;
            mTargetKind = config.Get("targetKind").AsEnum(CardKind.Unknown);
            mTargetNotRef = config.Get("targetNot").AsString(string.Empty);
            mSourceDefId = config.Get("sourceDefId").AsString(string.Empty);
            mExcludeSourceDefId = config.Get("excludeSourceDefId").AsString(string.Empty);
            mCause = config.Get("cause").AsString(string.Empty);
            mExcludeCause = config.Get("excludeCause").AsString(string.Empty);
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (Matches(context, events[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return null;
        }

        private bool Matches(EffectRuntimeContext context, CoreGameEvent gameEvent)
        {
            if (mEventTypes.Count > 0 && !ContainsEventType(gameEvent.Type))
            {
                return false;
            }

            if (mHasStat && gameEvent.Amount != (int)mStat)
            {
                return false;
            }

            if (mMinDelta != int.MinValue && gameEvent.Delta < mMinDelta)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mSourceDefId) && !Same(gameEvent.SourceDefId, mSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mExcludeSourceDefId) && Same(gameEvent.SourceDefId, mExcludeSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mCause) && !Same(gameEvent.Cause, mCause))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mExcludeCause) && Same(gameEvent.Cause, mExcludeCause))
            {
                return false;
            }

            var eventTargetUid = gameEvent.TargetUid != 0 ? gameEvent.TargetUid : gameEvent.CardUid;
            if (mTargetKind != CardKind.Unknown)
            {
                CardInstance target;
                if (!context.TryGetCard(eventTargetUid, out target) || target.Kind != mTargetKind)
                {
                    return false;
                }
            }

            var forbiddenUid = TargetResolver.ResolveSingleCardRef(context, mTargetNotRef);
            if (forbiddenUid != 0 && eventTargetUid == forbiddenUid)
            {
                return false;
            }

            return true;
        }

        private void AddEventType(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
            {
                return;
            }

            CoreEventType parsed;
            if (Enum.TryParse(eventType, true, out parsed) && !ContainsEventType(parsed))
            {
                mEventTypes.Add(parsed);
            }
        }

        private bool ContainsEventType(CoreEventType eventType)
        {
            for (var i = 0; i < mEventTypes.Count; i++)
            {
                if (mEventTypes[i] == eventType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
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
        private EffectValueExpression mAmount;
        private string mActorRef = "Player";
        private string mCause = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mAmount = EffectValueExpression.FromActionAmount(config);
            mActorRef = config.Get("actor").AsString("Player");
            mCause = config.Get("cause").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            var actorUid = TargetResolver.ResolveSingleCardRef(context, mActorRef);
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new DealDamageAction(actorUid, targets[i], mAmount.Evaluate(context, targets[i]), context.SourceDefId, EffectActionSource.CauseOrEffect(context, mCause)));
                }
            }

            return result;
        }
    }

    [EffectAtom("Heal", EffectAtomKind.Action)]
    public sealed class HealEffectAction : IAction
    {
        private EffectValueExpression mAmount;
        private string mActorRef = "Self";
        private string mCause = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mAmount = EffectValueExpression.FromActionAmount(config);
            mActorRef = config.Get("actor").AsString("Self");
            mCause = config.Get("cause").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            var actorUid = TargetResolver.ResolveSingleCardRef(context, mActorRef);
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new HealAction(actorUid, targets[i], mAmount.Evaluate(context, targets[i]), context.SourceDefId, EffectActionSource.CauseOrEffect(context, mCause)));
                }
            }

            return result;
        }
    }

    [EffectAtom("GainArmor", EffectAtomKind.Action)]
    public sealed class GainArmorEffectAction : IAction
    {
        private EffectValueExpression mAmount;
        private string mCause = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mAmount = EffectValueExpression.FromActionAmount(config);
            mCause = config.Get("cause").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new GainArmorAction(targets[i], mAmount.Evaluate(context, targets[i]), context.SourceDefId, EffectActionSource.CauseOrEffect(context, mCause)));
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
            return new[] { new ModifyGoldAction(mDelta, mReason, context.SourceDefId) };
        }
    }

    [EffectAtom("ModifyBaseStat", EffectAtomKind.Action)]
    public sealed class ModifyBaseStatEffectAction : IAction
    {
        private StatId mStat = StatId.Attack;
        private int mDelta;
        private string mReason = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mStat = config.Get("stat").AsEnum(StatId.Attack);
            mDelta = config.Get("delta").AsInt(0);
            mReason = config.Get("reason").AsString("effect");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new ModifyBaseStatAction(targets[i], mStat, mDelta, mReason, context.SourceDefId));
                }
            }

            return result;
        }
    }

    [EffectAtom("OfferRewardChoice", EffectAtomKind.Action)]
    public sealed class OfferRewardChoiceEffectAction : IAction
    {
        private string mPoolId = string.Empty;
        private int mOptionCount;

        public void Configure(EffectDslNode config)
        {
            mPoolId = config.Get("poolId").AsString(string.Empty);
            mOptionCount = Math.Max(0, config.Get("optionCount").AsInt(0));
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[] { new OfferRewardChoiceAction(mPoolId, mOptionCount) };
        }
    }

    [EffectAtom("GrantRewardFromPool", EffectAtomKind.Action)]
    public sealed class GrantRewardFromPoolEffectAction : IAction
    {
        private string mPoolId = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mPoolId = config.Get("poolId").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[] { new GrantRewardFromPoolAction(mPoolId) };
        }
    }

    [EffectAtom("GrantRelic", EffectAtomKind.Action)]
    public sealed class GrantRelicEffectAction : IAction
    {
        private string mRelicDefId = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mRelicDefId = config.Get("relicDefId").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[] { new GrantRelicAction(mRelicDefId) };
        }
    }

    [EffectAtom("GrantPlayerSkillContent", EffectAtomKind.Action)]
    public sealed class GrantPlayerSkillContentEffectAction : IAction
    {
        private string mSkillDefId = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mSkillDefId = config.Get("skillDefId").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[] { new GrantPlayerSkillContentAction(mSkillDefId) };
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
        private bool mClockwise = true;

        public void Configure(EffectDslNode config)
        {
            mCount = Math.Max(0, config.Get("count").AsInt(1));
            mClockwise = !string.Equals(
                config.Get("direction").AsString("Clockwise"),
                "CounterClockwise",
                StringComparison.OrdinalIgnoreCase);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < mCount; i++)
            {
                result.Add(new RotateBoardClockwiseAction(mClockwise));
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
            return new[] { new ShuffleIntoDrawPileAction(mDefId, mKind, mCount, mTop, context.SourceDefId) };
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
            return new[] { new SpawnCardAction(mDefId, mKind, mZone, mSlot, mCount, context.SourceDefId) };
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
                    result.Add(new AddStatModifierAction(targets[i], mStat, mOp, mValue, mLayer, mScope, mSource, context.SourceDefId));
                }
            }

            return result;
        }
    }

    [EffectAtom("AddRuleModifier", EffectAtomKind.Action)]
    public sealed class AddRuleModifierEffectAction : IAction
    {
        private RuleId mRule = RuleId.DamageMultiplier;
        private ModifierOp mOp = ModifierOp.Add;
        private float mValue;
        private ModifierLayer mLayer = ModifierLayer.Temporary;
        private ModifierScope mScope = ModifierScope.Once;
        private string mSource = "effect.action.rule";

        public void Configure(EffectDslNode config)
        {
            mRule = config.Get("rule").AsEnum(RuleId.DamageMultiplier);
            mOp = config.Get("op").AsEnum(ModifierOp.Add);
            mValue = config.Get("value").AsFloat(0f);
            mLayer = config.Get("layer").AsEnum(ModifierLayer.Temporary);
            mScope = config.Get("scope").AsEnum(ModifierScope.Once);
            mSource = config.Get("source").AsString("effect.action.rule");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new AddRuleModifierAction(targets[i], mRule, mOp, mValue, mLayer, mScope, mSource));
                }
            }

            return result;
        }
    }

    [EffectAtom("SetBoardMark", EffectAtomKind.Action)]
    public sealed class SetBoardMarkEffectAction : IAction
    {
        private readonly List<SlotId> mExcludeSlots = new List<SlotId>();
        private BoardMarkId mMark = BoardMarkId.Blessed;
        private SlotId mSlot = SlotId.None;
        private bool mMarked = true;
        private bool mRandom;
        private bool mOnlyUnmarked = true;
        private int mCount = 1;

        public void Configure(EffectDslNode config)
        {
            mMark = config.Get("mark").AsEnum(BoardMarkId.Blessed);
            mMarked = config.Get("marked").AsBool(true);
            mRandom = config.Get("random").AsBool(false);
            mOnlyUnmarked = config.Get("onlyUnmarked").AsBool(true);
            mCount = Math.Max(0, config.Get("count").AsInt(1));
            mSlot = config.Has("slot") ? SlotId.Board(config.Get("slot").AsInt(1)) : SlotId.None;
            mExcludeSlots.Clear();
            AddExcludedSlots(config.Get("excludeSlots"));
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var slots = ResolveSlots(context);
            var result = new List<GameAction>();
            for (var i = 0; i < slots.Count; i++)
            {
                result.Add(new SetBoardMarkAction(slots[i], mMark, mMarked, context.SourceDefId, context.EffectId));
            }

            return result;
        }

        private List<SlotId> ResolveSlots(EffectRuntimeContext context)
        {
            var result = new List<SlotId>();
            if (context == null || mMark == BoardMarkId.None || mCount <= 0)
            {
                return result;
            }

            if (!mRandom)
            {
                if (mSlot.IsBoardSlot)
                {
                    result.Add(mSlot);
                }

                return result;
            }

            var candidates = new List<SlotId>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (IsExcluded(slot))
                {
                    continue;
                }

                if (mOnlyUnmarked && context.Board.IsMarked(slot, mMark) == mMarked)
                {
                    continue;
                }

                candidates.Add(slot);
            }

            var take = Math.Min(mCount, candidates.Count);
            for (var i = 0; i < take; i++)
            {
                var index = context.Rng.Range(0, candidates.Count);
                result.Add(candidates[index]);
                candidates.RemoveAt(index);
            }

            return result;
        }

        private void AddExcludedSlots(EffectDslNode node)
        {
            var values = node.AsArray();
            if (values.Count == 0)
            {
                AddExcludedSlot(node.AsInt(0));
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                AddExcludedSlot(values[i].AsInt(0));
            }
        }

        private void AddExcludedSlot(int index)
        {
            if (index >= SlotId.MinBoardIndex && index <= SlotId.MaxBoardIndex)
            {
                mExcludeSlots.Add(SlotId.Board(index));
            }
        }

        private bool IsExcluded(SlotId slot)
        {
            for (var i = 0; i < mExcludeSlots.Count; i++)
            {
                if (mExcludeSlots[i] == slot)
                {
                    return true;
                }
            }

            return false;
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
                    result.Add(new RemoveCardAction(targets[i], mDestination, mReason, context.SourceDefId));
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

    internal static class EffectActionSource
    {
        public static string CauseOrEffect(EffectRuntimeContext context, string configuredCause)
        {
            return string.IsNullOrEmpty(configuredCause) ? context.EffectId : configuredCause;
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

        public static List<int> FilteredCards(
            EffectRuntimeContext context,
            CardKind kind,
            ZoneId zone,
            string adjacentToRef,
            IReadOnlyList<string> excludeRefs)
        {
            var result = new List<int>();
            if (context == null)
            {
                return result;
            }

            var adjacentToUid = ResolveSingleCardRef(context, adjacentToRef);
            CardInstance adjacentToCard = null;
            var requiresAdjacency = !string.IsNullOrEmpty(adjacentToRef);
            var hasAdjacentOrigin = !requiresAdjacency || context.TryGetCard(adjacentToUid, out adjacentToCard);
            if (!hasAdjacentOrigin)
            {
                return result;
            }

            if (zone == ZoneId.None || zone == ZoneId.Board)
            {
                foreach (var uid in context.Board.BoardCardUids())
                {
                    AddIfMatches(context, result, uid, kind, ZoneId.Board, requiresAdjacency, adjacentToCard, excludeRefs);
                }

                return result;
            }

            foreach (var pair in context.Registry.Cards)
            {
                AddIfMatches(context, result, pair.Key, kind, zone, requiresAdjacency, adjacentToCard, excludeRefs);
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

        private static void AddIfMatches(
            EffectRuntimeContext context,
            List<int> result,
            int uid,
            CardKind kind,
            ZoneId zone,
            bool requiresAdjacency,
            CardInstance adjacentToCard,
            IReadOnlyList<string> excludeRefs)
        {
            CardInstance card;
            if (!context.TryGetCard(uid, out card))
            {
                return;
            }

            if (kind != CardKind.Unknown && card.Kind != kind)
            {
                return;
            }

            if (zone != ZoneId.None && card.Zone.Value != zone)
            {
                return;
            }

            if (requiresAdjacency && (adjacentToCard == null || !card.Slot.Value.IsAdjacentTo(adjacentToCard.Slot.Value)))
            {
                return;
            }

            for (var i = 0; i < excludeRefs.Count; i++)
            {
                if (uid == ResolveSingleCardRef(context, excludeRefs[i]))
                {
                    return;
                }
            }

            result.Add(uid);
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

    internal sealed class EffectValueExpression
    {
        private readonly EffectDslNode mNode;
        private readonly int mFallback;
        private readonly bool mUseNode;

        private EffectValueExpression(EffectDslNode node, int fallback, bool useNode)
        {
            mNode = node;
            mFallback = fallback;
            mUseNode = useNode;
        }

        public static EffectValueExpression FromActionAmount(EffectDslNode config)
        {
            if (config != null && config.Has("value"))
            {
                return new EffectValueExpression(config.Get("value"), 0, true);
            }

            return new EffectValueExpression(null, config == null ? 0 : config.Get("amount").AsInt(0), false);
        }

        public int Evaluate(EffectRuntimeContext context, int targetUid)
        {
            return Math.Max(0, (int)Math.Round(mUseNode ? EvaluateNode(mNode, context, targetUid) : mFallback));
        }

        private static float EvaluateNode(EffectDslNode node, EffectRuntimeContext context, int targetUid)
        {
            if (node == null || node.IsNull)
            {
                return 0f;
            }

            if (!node.IsObject)
            {
                return node.AsFloat(0f);
            }

            if (node.Has("constant"))
            {
                return node.Get("constant").AsFloat(0f);
            }

            if (node.Has("op"))
            {
                return EvaluateOp(node, context, targetUid);
            }

            return EvaluateSource(node, context, targetUid);
        }

        private static float EvaluateOp(EffectDslNode node, EffectRuntimeContext context, int targetUid)
        {
            var values = node.Get("values").AsArray();
            var op = node.Get("op").AsString(string.Empty);
            if (values.Count == 0)
            {
                return 0f;
            }

            var current = EvaluateNode(values[0], context, targetUid);
            if (Same(op, "Negate"))
            {
                return -current;
            }

            for (var i = 1; i < values.Count; i++)
            {
                var next = EvaluateNode(values[i], context, targetUid);
                if (Same(op, "Add"))
                {
                    current += next;
                }
                else if (Same(op, "Subtract"))
                {
                    current -= next;
                }
                else if (Same(op, "Multiply"))
                {
                    current *= next;
                }
                else if (Same(op, "Min"))
                {
                    current = Math.Min(current, next);
                }
                else if (Same(op, "Max"))
                {
                    current = Math.Max(current, next);
                }
            }

            return current;
        }

        private static float EvaluateSource(EffectDslNode node, EffectRuntimeContext context, int targetUid)
        {
            var source = node.Get("source").AsString(string.Empty);
            if (Same(source, "Event"))
            {
                return EvaluateEventField(context, node.Get("field").AsString("Amount"));
            }

            var card = ResolveCard(context, targetUid, source);
            if (card == null)
            {
                return 0f;
            }

            var stat = node.Get("stat").AsEnum(StatId.Attack);
            if (stat == StatId.Hp || stat == StatId.Armor)
            {
                return card.Stats.GetBase(stat);
            }

            return context.Architecture.GetSystem<IStatSystem>().GetEffectiveValue(card, stat);
        }

        private static float EvaluateEventField(EffectRuntimeContext context, string field)
        {
            if (context == null)
            {
                return 0f;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (Same(field, "Amount") && events[i].Amount != 0)
                {
                    return events[i].Amount;
                }

                if (Same(field, "Delta") && events[i].Delta != 0)
                {
                    return events[i].Delta;
                }

                if (Same(field, "RemainingHp"))
                {
                    return events[i].RemainingHp;
                }

                if (Same(field, "RemainingArmor"))
                {
                    return events[i].RemainingArmor;
                }
            }

            return 0f;
        }

        private static CardInstance ResolveCard(EffectRuntimeContext context, int targetUid, string source)
        {
            if (context == null)
            {
                return null;
            }

            if (Same(source, "Player"))
            {
                return context.AvatarCard;
            }

            if (Same(source, "Target"))
            {
                return context.GetCard(targetUid);
            }

            if (Same(source, "Owner") || Same(source, "Self"))
            {
                return context.OwnerCard;
            }

            if (Same(source, "EventTarget"))
            {
                return context.GetCard(context.FirstEventTargetUid());
            }

            if (Same(source, "EventCard"))
            {
                return context.GetCard(context.FirstEventCardUid());
            }

            if (Same(source, "Actor"))
            {
                return context.GetCard(TargetResolver.ResolveSingleCardRef(context, "Actor"));
            }

            return null;
        }

        public static void Validate(EffectDslNode node, string path, EffectValidationResult result)
        {
            if (node == null || node.IsNull || result == null)
            {
                return;
            }

            if (!node.IsObject)
            {
                if (node.AsFloat(0f) < 0f)
                {
                    result.Add("schema.range.value", path + " must be >= 0.");
                }

                return;
            }

            if (node.Has("constant"))
            {
                if (node.Get("constant").AsFloat(0f) < 0f)
                {
                    result.Add("schema.range.value", path + ".constant must be >= 0.");
                }

                return;
            }

            if (node.Has("op"))
            {
                ValidateOp(node, path, result);
                return;
            }

            ValidateSource(node, path, result);
        }

        private static void ValidateOp(EffectDslNode node, string path, EffectValidationResult result)
        {
            var op = node.Get("op").AsString(string.Empty);
            if (!IsSupportedOp(op))
            {
                result.Add("schema.value.op", path + ".op is not supported.");
            }

            var values = node.Get("values").AsArray();
            if (values.Count == 0)
            {
                result.Add("schema.value.values", path + ".values must not be empty.");
            }

            for (var i = 0; i < values.Count; i++)
            {
                Validate(values[i], path + ".values[" + i + "]", result);
            }
        }

        private static void ValidateSource(EffectDslNode node, string path, EffectValidationResult result)
        {
            var source = node.Get("source").AsString(string.Empty);
            if (!IsSupportedSource(source))
            {
                result.Add("schema.value.source", path + ".source is not supported.");
                return;
            }

            if (Same(source, "Event"))
            {
                if (!IsSupportedEventField(node.Get("field").AsString(string.Empty)))
                {
                    result.Add("schema.value.field", path + ".field is not supported.");
                }

                return;
            }

            if (!node.Has("stat"))
            {
                result.Add("schema.value.stat", path + ".stat is required.");
                return;
            }

            if (!IsSupportedStat(node.Get("stat").AsString(string.Empty)))
            {
                result.Add("schema.value.stat", path + ".stat is not supported.");
            }
        }

        private static bool IsSupportedSource(string source)
        {
            return Same(source, "Player")
                || Same(source, "Target")
                || Same(source, "Owner")
                || Same(source, "Self")
                || Same(source, "EventTarget")
                || Same(source, "EventCard")
                || Same(source, "Actor")
                || Same(source, "Event");
        }

        private static bool IsSupportedStat(string stat)
        {
            if (string.IsNullOrEmpty(stat))
            {
                return false;
            }

            StatId ignored;
            return Enum.TryParse(stat, true, out ignored);
        }

        private static bool IsSupportedEventField(string field)
        {
            return Same(field, "Amount")
                || Same(field, "Delta")
                || Same(field, "RemainingHp")
                || Same(field, "RemainingArmor");
        }

        private static bool IsSupportedOp(string op)
        {
            return Same(op, "Add")
                || Same(op, "Subtract")
                || Same(op, "Multiply")
                || Same(op, "Min")
                || Same(op, "Max")
                || Same(op, "Negate");
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
