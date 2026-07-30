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

            return HasEvent(context, CoreEventType.DamageDealt)
                || HasEvent(context, CoreEventType.ArmorChanged);
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
    [EffectAtom("OnSelfRemoved", EffectAtomKind.Trigger)]
    public sealed class OnSelfRemovedTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnRemove; } }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type != CoreEventType.CardRemoved)
                {
                    continue;
                }

                if (context.OwnerUid == 0 || events[i].CardUid == context.OwnerUid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnAnyCardRemoved", EffectAtomKind.Trigger)]
    public sealed class OnAnyCardRemovedTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnRemove; } }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.CardRemoved)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnUseHelpCard", EffectAtomKind.Trigger)]
    [EffectAtom("OnSelfUsed", EffectAtomKind.Trigger)]
    public sealed class OnSelfUsedTrigger : TriggerAtomBase
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

    [EffectAtom("OnOtherHelpCardUsed", EffectAtomKind.Trigger)]
    public sealed class OnOtherHelpCardUsedTrigger : TriggerAtomBase
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
                    && events[i].CardUid != context.OwnerUid)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnAnyHelpCardUsed", EffectAtomKind.Trigger)]
    public sealed class OnAnyHelpCardUsedTrigger : TriggerAtomBase
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
                if (events[i].Type == CoreEventType.ItemUsed)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [EffectAtom("OnActivate", EffectAtomKind.Trigger)]
    public sealed class OnActivateTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnActivate; } }

        public override bool Matches(EffectRuntimeContext context)
        {
            return context != null;
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
        private int mEvery = 1;
        private string mCounterKey = string.Empty;

        public override TriggerPoint Point { get { return TriggerPoint.OnInteract; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mEvery = Math.Max(1, config.Get("every").AsInt(1));
            mCounterKey = config.Get("counterKey").AsString(string.Empty);
        }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context))
            {
                return false;
            }

            if (mEvery <= 1)
            {
                return true;
            }

            var owner = context.OwnerCard ?? context.AvatarCard;
            if (owner == null)
            {
                return false;
            }

            var key = string.IsNullOrEmpty(mCounterKey)
                ? CoreCounterKeys.EffectCounterPrefix + context.Instance.InstanceId + ".interact"
                : mCounterKey;
            return ActionCountdown.TickOnce(owner.Counters, key, mEvery);
        }
    }

    [EffectAtom("OnSelfMove", EffectAtomKind.Trigger)]
    public sealed class OnSelfMoveTrigger : TriggerAtomBase
    {
        private int mEvery = 1;
        private string mCounterKey = string.Empty;
        private string mRequireAdjacentToRef = string.Empty;
        private string mSourceDefId = string.Empty;
        private string mExcludeSourceDefId = string.Empty;
        private string mSourcePrefix = string.Empty;

        public override TriggerPoint Point { get { return TriggerPoint.OnMove; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mEvery = Math.Max(1, config.Get("every").AsInt(1));
            mCounterKey = config.Get("counterKey").AsString(string.Empty);
            mRequireAdjacentToRef = config.Get("requireAdjacentTo").AsString(string.Empty);
            mSourceDefId = config.Get("sourceDefId").AsString(string.Empty);
            mExcludeSourceDefId = config.Get("excludeSourceDefId").AsString(string.Empty);
            mSourcePrefix = config.Get("sourcePrefix").AsString(string.Empty);
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
                if (events[i].Type != CoreEventType.CardMoved
                    || events[i].CardUid != context.OwnerUid
                    || !MatchesSource(context, events[i]))
                {
                    continue;
                }

                if (!IsOwnerAdjacentToRequiredRef(context))
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
                return ActionCountdown.TickOnce(owner.Counters, key, mEvery);
            }

            return false;
        }

        private bool IsOwnerAdjacentToRequiredRef(EffectRuntimeContext context)
        {
            if (string.IsNullOrEmpty(mRequireAdjacentToRef))
            {
                return true;
            }

            var otherUid = TargetResolver.ResolveSingleCardRef(context, mRequireAdjacentToRef);
            CardInstance owner;
            CardInstance other;
            return context.TryGetCard(context.OwnerUid, out owner)
                && context.TryGetCard(otherUid, out other)
                && owner.Slot.Value.IsAdjacentTo(other.Slot.Value);
        }

        private bool MatchesSource(EffectRuntimeContext context, CoreGameEvent gameEvent)
        {
            var sourceDefId = gameEvent.SourceDefId;
            if (!string.IsNullOrEmpty(context.EffectId) && Same(gameEvent.Cause, context.EffectId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mSourceDefId) && !Same(sourceDefId, mSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mExcludeSourceDefId) && Same(sourceDefId, mExcludeSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mSourcePrefix) && !StartsWith(sourceDefId, mSourcePrefix))
            {
                return false;
            }

            return true;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWith(string value, string prefix)
        {
            return value != null && prefix != null && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
    }

    [EffectAtom("OnMoveToSlot", EffectAtomKind.Trigger)]
    public sealed class OnMoveToSlotTrigger : TriggerAtomBase
    {
        private readonly List<SlotId> mSlots = new List<SlotId>();
        private SlotId mSlot = SlotId.None;
        private string mTargetRef = "Any";
        private string mSourceDefId = string.Empty;
        private string mExcludeSourceDefId = string.Empty;
        private string mSourcePrefix = string.Empty;
        private string mCounterKey = string.Empty;

        public override TriggerPoint Point { get { return TriggerPoint.OnMoveToSlot; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mSlot = SlotId.Board(config.Get("slot").AsInt(1));
            AddSlots(config.Get("slots"));
            mTargetRef = config.Get("target").AsString("Any");
            mSourceDefId = config.Get("sourceDefId").AsString(string.Empty);
            mExcludeSourceDefId = config.Get("excludeSourceDefId").AsString(string.Empty);
            mSourcePrefix = config.Get("sourcePrefix").AsString(string.Empty);
            mCounterKey = config.Get("counterKey").AsString(string.Empty);
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
                    && MatchesSlot(events[i].ToSlot)
                    && (expectedUid == 0 || events[i].CardUid == expectedUid)
                    && MatchesSource(events[i].SourceDefId))
                {
                    IncrementCounter(context);
                    return true;
                }
            }

            return false;
        }

        private void AddSlots(EffectDslNode node)
        {
            mSlots.Clear();
            var values = node.AsArray();
            for (var i = 0; i < values.Count; i++)
            {
                var slot = SlotId.Board(values[i].AsInt(0));
                if (slot.IsBoardSlot && !ContainsSlot(slot))
                {
                    mSlots.Add(slot);
                }
            }
        }

        private bool MatchesSlot(SlotId slot)
        {
            if (mSlots.Count == 0)
            {
                return slot == mSlot;
            }

            return ContainsSlot(slot);
        }

        private bool ContainsSlot(SlotId slot)
        {
            for (var i = 0; i < mSlots.Count; i++)
            {
                if (mSlots[i] == slot)
                {
                    return true;
                }
            }

            return false;
        }

        private void IncrementCounter(EffectRuntimeContext context)
        {
            if (string.IsNullOrEmpty(mCounterKey))
            {
                return;
            }

            var owner = context.OwnerCard ?? context.AvatarCard;
            if (owner != null)
            {
                owner.Counters.Add(mCounterKey, 1);
            }
        }

        private bool MatchesSource(string sourceDefId)
        {
            if (!string.IsNullOrEmpty(mSourceDefId) && !Same(sourceDefId, mSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mExcludeSourceDefId) && Same(sourceDefId, mExcludeSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mSourcePrefix) && !StartsWith(sourceDefId, mSourcePrefix))
            {
                return false;
            }

            return true;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWith(string value, string prefix)
        {
            return value != null && prefix != null && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
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

    [EffectAtom("OnFlip", EffectAtomKind.Trigger)]
    public sealed class OnFlipTrigger : TriggerAtomBase
    {
        public override TriggerPoint Point { get { return TriggerPoint.OnFlip; } }

        public override bool Matches(EffectRuntimeContext context)
        {
            if (!base.Matches(context) || context.OwnerUid == 0)
            {
                return false;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.CardFaceChanged
                    && events[i].CardUid == context.OwnerUid)
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
            // 仅本卡护甲归零：场上其他卡碎甲 / 反伤打掉玩家甲 不得误触发。
            if (!base.Matches(context) || context.OwnerUid == 0)
            {
                return false;
            }

            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.ArmorChanged
                    && events[i].CardUid == context.OwnerUid
                    && events[i].Delta < 0
                    && events[i].RemainingArmor <= 0)
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
    public class OnCumulativeTrigger : TriggerAtomBase
    {
        private string mMetric = "armorLost";
        private int mThreshold = 1;
        private string mCounterKey = string.Empty;
        private CoreEventType mEventType = CoreEventType.ActionStarted;
        private bool mHasEventType;
        private string mSourceDefId = string.Empty;
        private string mExcludeSourceDefId = string.Empty;
        private string mCause = string.Empty;
        private bool mTargetIsSelf;
        private bool mActorIsSelf;
        private bool mTargetIsPlayer;
        private bool mActorIsPlayer;

        public override TriggerPoint Point { get { return TriggerPoint.OnCumulative; } }

        public override void Configure(EffectDslNode config)
        {
            base.Configure(config);
            mMetric = config.Get("metric").AsString("armorLost");
            mThreshold = Math.Max(1, config.Get("threshold").AsInt(1));
            mCounterKey = config.Get("counterKey").AsString(string.Empty);
            mHasEventType = config.Has("eventType");
            mEventType = config.Get("eventType").AsEnum(CoreEventType.ActionStarted);
            mSourceDefId = config.Get("sourceDefId").AsString(string.Empty);
            mExcludeSourceDefId = config.Get("excludeSourceDefId").AsString(string.Empty);
            mCause = config.Get("cause").AsString(string.Empty);
            ConfigureMorphology(config);
        }

        protected virtual void ConfigureMorphology(EffectDslNode config)
        {
            mTargetIsSelf = false;
            mActorIsSelf = false;
            mTargetIsPlayer = false;
            mActorIsPlayer = false;
        }

        protected void SetSelfArmorLostMorphology()
        {
            mMetric = "armorLost";
            mTargetIsSelf = true;
        }

        protected void SetSelfDamageDealtToPlayerMorphology()
        {
            mMetric = "damageDealt";
            mActorIsSelf = true;
            mTargetIsPlayer = true;
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
            return ActionCountdown.Tick(owner.Counters, key, mThreshold, delta);
        }

        private int MeasureDelta(EffectRuntimeContext context)
        {
            var sum = 0;
            var events = context.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (!MatchesEvent(context, events[i]))
                {
                    continue;
                }

                if (Same(mMetric, "armorLost") && events[i].Type == CoreEventType.ArmorChanged && events[i].Delta < 0)
                {
                    sum += -events[i].Delta;
                }
                else if (Same(mMetric, "hpLost") && events[i].Type == CoreEventType.HpChanged && events[i].Delta < 0)
                {
                    sum += -events[i].Delta;
                }
                else if (Same(mMetric, "damageDealt") && events[i].Type == CoreEventType.DamageDealt && events[i].Delta > 0)
                {
                    sum += events[i].Delta;
                }
            }

            return sum;
        }

        private bool MatchesEvent(EffectRuntimeContext context, CoreGameEvent gameEvent)
        {
            if (mHasEventType && gameEvent.Type != mEventType)
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

            var eventTargetUid = gameEvent.TargetUid != 0 ? gameEvent.TargetUid : gameEvent.CardUid;
            if (mTargetIsSelf && (context.OwnerUid == 0 || eventTargetUid != context.OwnerUid))
            {
                return false;
            }

            if (mTargetIsPlayer && (context.AvatarUid == 0 || eventTargetUid != context.AvatarUid))
            {
                return false;
            }

            if (mActorIsSelf && (context.OwnerUid == 0 || gameEvent.ActorUid != context.OwnerUid))
            {
                return false;
            }

            if (mActorIsPlayer && (context.AvatarUid == 0 || gameEvent.ActorUid != context.AvatarUid))
            {
                return false;
            }

            return true;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    [EffectAtom("OnSelfArmorLostCumulative", EffectAtomKind.Trigger)]
    public sealed class OnSelfArmorLostCumulativeTrigger : OnCumulativeTrigger
    {
        protected override void ConfigureMorphology(EffectDslNode config)
        {
            SetSelfArmorLostMorphology();
        }
    }

    [EffectAtom("OnSelfDamageDealtToPlayerCumulative", EffectAtomKind.Trigger)]
    public sealed class OnSelfDamageDealtToPlayerCumulativeTrigger : OnCumulativeTrigger
    {
        protected override void ConfigureMorphology(EffectDslNode config)
        {
            SetSelfDamageDealtToPlayerMorphology();
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
        private readonly List<ZoneId> mZones = new List<ZoneId>();
        private string mDefId = string.Empty;
        private CardKind mKind = CardKind.Unknown;
        private ZoneId mZone = ZoneId.None;
        private string mAdjacentToRef = string.Empty;
        private int mMinLevel;
        private int mMaxLevel;
        private bool mExcludeElite;
        private bool mExcludeBoss;
        private bool mRandom;
        private int mCount;

        public void Configure(EffectDslNode config)
        {
            mDefId = config.Get("defId").AsString(string.Empty);
            mKind = config.Get("kind").AsEnum(CardKind.Unknown);
            mZone = config.Get("zone").AsEnum(ZoneId.None);
            mAdjacentToRef = config.Get("adjacentTo").AsString(string.Empty);
            mMinLevel = Math.Max(0, config.Get("minLevel").AsInt(0));
            mMaxLevel = Math.Max(0, config.Get("maxLevel").AsInt(0));
            mExcludeElite = config.Get("excludeElite").AsBool(false);
            mExcludeBoss = config.Get("excludeBoss").AsBool(false);
            mRandom = config.Get("random").AsBool(false);
            mCount = Math.Max(0, config.Get("count").AsInt(mRandom ? 1 : 0));
            AddZones(config.Get("zones"));
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

            var candidates = TargetResolver.FilteredCards(
                context,
                mDefId,
                mKind,
                mZone,
                mZones,
                mAdjacentToRef,
                mMinLevel,
                mMaxLevel,
                mExcludeElite,
                mExcludeBoss,
                mExcludeRefs);
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

        private void AddZones(EffectDslNode node)
        {
            mZones.Clear();
            var values = node.AsArray();
            for (var i = 0; i < values.Count; i++)
            {
                AddZone(values[i].AsString(string.Empty));
            }
        }

        private void AddZone(string value)
        {
            ZoneId zone;
            if (!string.IsNullOrEmpty(value) && Enum.TryParse(value, true, out zone) && zone != ZoneId.None)
            {
                mZones.Add(zone);
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

    [EffectAtom("SelectedCards", EffectAtomKind.Target)]
    public sealed class SelectedCardsTarget : ITarget
    {
        private CardKind mKind = CardKind.Unknown;
        private ZoneId mZone = ZoneId.None;
        private int mMinLevel;
        private int mMaxLevel;
        private bool mExcludeElite;
        private bool mExcludeBoss;
        private int mCount;
        private bool mAllowAvatar;

        public void Configure(EffectDslNode config)
        {
            mKind = config.Get("kind").AsEnum(CardKind.Unknown);
            mZone = config.Get("zone").AsEnum(ZoneId.None);
            mMinLevel = Math.Max(0, config.Get("minLevel").AsInt(0));
            mMaxLevel = Math.Max(0, config.Get("maxLevel").AsInt(0));
            mExcludeElite = config.Get("excludeElite").AsBool(false);
            mExcludeBoss = config.Get("excludeBoss").AsBool(false);
            mCount = Math.Max(0, config.Get("count").AsInt(0));
            mAllowAvatar = config.Get("allowAvatar").AsBool(false);
        }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            var result = new List<int>();
            var useItem = context.TriggerContext == null ? null : context.TriggerContext.Action as UseItemAction;
            if (useItem == null)
            {
                return result;
            }

            for (var i = 0; i < useItem.SelectedCardUids.Count; i++)
            {
                CardInstance card;
                if (!context.TryGetCard(useItem.SelectedCardUids[i], out card))
                {
                    continue;
                }

                if (!mAllowAvatar && card.Kind == CardKind.Avatar)
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

                if (!MatchesLevelAndFlags(card))
                {
                    continue;
                }

                AddUnique(result, card.Uid);
            }

            if (mCount > 0 && result.Count != mCount)
            {
                return new int[0];
            }

            return result;
        }

        private bool MatchesLevelAndFlags(CardInstance card)
        {
            if (card == null)
            {
                return false;
            }

            var level = card.Counters.Get(CoreCounterKeys.Level);
            if (mMinLevel > 0 && level < mMinLevel)
            {
                return false;
            }

            if (mMaxLevel > 0 && level > mMaxLevel)
            {
                return false;
            }

            if (mExcludeElite && card.Counters.Get(CoreCounterKeys.Elite) > 0)
            {
                return false;
            }

            return !mExcludeBoss || card.Counters.Get(CoreCounterKeys.Boss) <= 0;
        }

        private static void AddUnique(List<int> values, int uid)
        {
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
            var boardSystem = context.Architecture.GetSystem<IBoardSystem>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var cardUid = context.Board.GetCardUid(slot);
                if (cardUid != 0 && boardSystem.AreAdjacent(card, slot, cardUid))
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
        private CardKind mKind = CardKind.Unknown;
        private int mMinLevel;
        private int mMaxLevel;
        private bool mExcludeElite;
        private bool mExcludeBoss;

        public void Configure(EffectDslNode config)
        {
            mSlot = SlotId.Board(config.Get("slot").AsInt(1));
            mKind = config.Get("kind").AsEnum(CardKind.Unknown);
            mMinLevel = Math.Max(0, config.Get("minLevel").AsInt(0));
            mMaxLevel = Math.Max(0, config.Get("maxLevel").AsInt(0));
            mExcludeElite = config.Get("excludeElite").AsBool(false);
            mExcludeBoss = config.Get("excludeBoss").AsBool(false);
        }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            var uid = context.Board.GetCardUid(mSlot);
            CardInstance card;
            if (!context.TryGetCard(uid, out card))
            {
                return new int[0];
            }

            if (mKind != CardKind.Unknown && card.Kind != mKind)
            {
                return new int[0];
            }

            var level = card.Counters.Get(CoreCounterKeys.Level);
            if (mMinLevel > 0 && level < mMinLevel)
            {
                return new int[0];
            }

            if (mMaxLevel > 0 && level > mMaxLevel)
            {
                return new int[0];
            }

            if (mExcludeElite && card.Counters.Get(CoreCounterKeys.Elite) > 0)
            {
                return new int[0];
            }

            if (mExcludeBoss && card.Counters.Get(CoreCounterKeys.Boss) > 0)
            {
                return new int[0];
            }

            return TargetResolver.Single(uid);
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
        private CardKind mKind = CardKind.Unknown;

        public void Configure(EffectDslNode config)
        {
            mOriginRef = config.Get("origin").AsString("Self");
            mDefId = config.Get("defId").AsString(string.Empty);
            mKind = config.Get("kind").AsEnum(CardKind.Unknown);
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            return ResolveAdjacentCardUid(context, mOriginRef, mDefId, mKind) != 0;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            if (!TargetResolver.IsSelfRef(mOriginRef)
                || (string.IsNullOrEmpty(mDefId) && mKind == CardKind.Unknown))
            {
                return null;
            }

            return new OwnerAdjacentHasCardCondition(mDefId, mKind);
        }

        internal static int ResolveAdjacentCardUid(EffectRuntimeContext context, string originRef, string defId)
        {
            return ResolveAdjacentCardUid(context, originRef, defId, CardKind.Unknown);
        }

        internal static int ResolveAdjacentCardUid(
            EffectRuntimeContext context,
            string originRef,
            string defId,
            CardKind kind)
        {
            if (context == null
                || (string.IsNullOrEmpty(defId) && kind == CardKind.Unknown))
            {
                return 0;
            }

            var originUid = TargetResolver.ResolveSingleCardRef(context, originRef);
            CardInstance origin;
            if (!context.TryGetCard(originUid, out origin) || !origin.Slot.Value.IsBoardSlot)
            {
                return 0;
            }

            var boardSystem = context.Architecture.GetSystem<IBoardSystem>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var cardUid = context.Board.GetCardUid(slot);
                if (cardUid == 0 || cardUid == originUid)
                {
                    continue;
                }

                CardInstance card;
                if (!context.TryGetCard(cardUid, out card))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(defId) && !string.Equals(card.DefId, defId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (kind != CardKind.Unknown && card.Kind != kind)
                {
                    continue;
                }

                if (boardSystem.AreAdjacent(origin, slot, cardUid))
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
        private CardKind mKind = CardKind.Unknown;

        public void Configure(EffectDslNode config)
        {
            mOriginRef = config.Get("origin").AsString("Self");
            mDefId = config.Get("defId").AsString(string.Empty);
            mKind = config.Get("kind").AsEnum(CardKind.Unknown);
        }

        public IReadOnlyList<int> Resolve(EffectRuntimeContext context)
        {
            return TargetResolver.Single(
                AdjacentHasCardEffectCondition.ResolveAdjacentCardUid(context, mOriginRef, mDefId, mKind));
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
                return context.Architecture.GetSystem<IBoardSystem>().AreAdjacent(leftCard, mFixedSlot, context.Board.GetCardUid(mFixedSlot));
            }

            int rightUid;
            var rightSlot = ResolveRightSlot(context, mRightRef, out rightUid);
            return rightSlot != SlotId.None && context.Architecture.GetSystem<IBoardSystem>().AreAdjacent(leftCard, rightSlot, rightUid);
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            if (TargetResolver.IsSelfRef(mLeftRef) && string.Equals(mRightRef, "Player", StringComparison.OrdinalIgnoreCase))
            {
                var ownerUid = context.Instance == null || context.Instance.Owner == null ? 0 : context.Instance.Owner.OwnerUid;
                return new SourceAdjacentToUidCondition(ownerUid, context.Architecture.GetModel<BoardModel>().AvatarUid.Value);
            }

            return mFixedSlot != SlotId.None && TargetResolver.IsSelfRef(mLeftRef)
                ? new AdjacentCondition(mFixedSlot)
                : null;
        }

        private static SlotId ResolveRightSlot(EffectRuntimeContext context, string rightRef, out int rightUid)
        {
            rightUid = 0;
            if (string.Equals(rightRef, "EventCard", StringComparison.OrdinalIgnoreCase))
            {
                var events = context.Events;
                for (var i = 0; i < events.Count; i++)
                {
                    if (events[i].CardUid != 0 && events[i].FromSlot.IsBoardSlot)
                    {
                        rightUid = events[i].CardUid;
                        return events[i].FromSlot;
                    }
                }
            }

            rightUid = TargetResolver.ResolveSingleCardRef(context, rightRef);
            CardInstance rightCard;
            return context.TryGetCard(rightUid, out rightCard) ? rightCard.Slot.Value : SlotId.None;
        }
    }

    [EffectAtom("CardZone", EffectAtomKind.Condition)]
    public sealed class CardZoneEffectCondition : ICondition
    {
        private string mTargetRef = "Self";
        private ZoneId mZone = ZoneId.None;

        public void Configure(EffectDslNode config)
        {
            mTargetRef = config.Get("target").AsString("Self");
            mZone = config.Get("zone").AsEnum(ZoneId.None);
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            var uid = TargetResolver.ResolveSingleCardRef(context, mTargetRef);
            CardInstance card;
            return context.TryGetCard(uid, out card) && (mZone == ZoneId.None || card.Zone.Value == mZone);
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return TargetResolver.IsSelfRef(mTargetRef) ? new ZoneCondition(mZone) : null;
        }
    }

    [EffectAtom("IsFaceUp", EffectAtomKind.Condition)]
    public sealed class IsFaceUpEffectCondition : ICondition
    {
        private string mTargetRef = "Self";
        private bool mExpected = true;

        public void Configure(EffectDslNode config)
        {
            mTargetRef = config.Get("target").AsString("Self");
            mExpected = config.Get("faceUp").AsBool(config.Get("value").AsBool(true));
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            var uid = TargetResolver.ResolveSingleCardRef(context, mTargetRef);
            CardInstance card;
            if (!context.TryGetCard(uid, out card) || card == null)
            {
                return false;
            }

            return card.FaceUp == mExpected;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return null;
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

    [EffectAtom("StatAtLeast", EffectAtomKind.Condition)]
    public sealed class StatAtLeastEffectCondition : ICondition
    {
        private string mTargetRef = "Self";
        private StatId mStat = StatId.Armor;
        private float mValue;

        public void Configure(EffectDslNode config)
        {
            mTargetRef = config.Get("target").AsString("Self");
            mStat = config.Get("stat").AsEnum(StatId.Armor);
            mValue = config.Get("value").AsFloat(0f);
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            var uid = TargetResolver.ResolveSingleCardRef(context, mTargetRef);
            CardInstance card;
            if (!context.TryGetCard(uid, out card))
            {
                return false;
            }

            var current = mStat == StatId.CurrentArmor || mStat == StatId.Armor
                ? StatArmorUtility.GetCurrentArmor(card)
                : mStat == StatId.Hp
                ? card.Stats.GetBase(mStat)
                : context.Architecture.GetSystem<IStatSystem>().GetEffectiveValue(card, mStat);
            return current >= mValue;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            var uid = ResolveBuildRef(context, mTargetRef);
            return new StatAtLeastStatCondition(uid, mStat, mValue);
        }

        private static int ResolveBuildRef(EffectBuildContext context, string reference)
        {
            if (context == null || string.IsNullOrEmpty(reference))
            {
                return 0;
            }

            if (string.Equals(reference, "Self", StringComparison.OrdinalIgnoreCase)
                || string.Equals(reference, "Owner", StringComparison.OrdinalIgnoreCase))
            {
                return context.Instance == null || context.Instance.Owner == null ? 0 : context.Instance.Owner.OwnerUid;
            }

            if (string.Equals(reference, "Player", StringComparison.OrdinalIgnoreCase))
            {
                return context.Architecture.GetModel<BoardModel>().AvatarUid.Value;
            }

            return 0;
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

    [EffectAtom("TargetCount", EffectAtomKind.Condition)]
    public sealed class TargetCountEffectCondition : ICondition
    {
        private EffectDslNode mTarget;
        private int mMin = 1;

        public void Configure(EffectDslNode config)
        {
            mTarget = config.Get("target");
            mMin = Math.Max(0, config.Get("min").AsInt(1));
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            if (context == null || mTarget == null || mTarget.IsNull)
            {
                return false;
            }

            var registry = context.Architecture.GetSystem<IEffectSystem>().AtomRegistry;
            return registry.CreateTarget(mTarget).Resolve(context).Count >= mMin;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return null;
        }
    }

    [EffectAtom("BoardMarkCount", EffectAtomKind.Condition)]
    public sealed class BoardMarkCountEffectCondition : ICondition
    {
        private BoardMarkId mMark = BoardMarkId.Blessed;
        private int mMin = 1;

        public void Configure(EffectDslNode config)
        {
            mMark = config.Get("mark").AsEnum(BoardMarkId.Blessed);
            mMin = Math.Max(0, config.Get("min").AsInt(1));
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            return context != null && mMark != BoardMarkId.None && context.Board.CountMarkedSlots(mMark) >= mMin;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return null;
        }
    }

    [EffectAtom("SelectedOption", EffectAtomKind.Condition)]
    public sealed class SelectedOptionEffectCondition : ICondition
    {
        private string mOption = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mOption = config.Get("option").AsString(string.Empty);
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            var useItem = context.TriggerContext == null ? null : context.TriggerContext.Action as UseItemAction;
            return useItem != null && string.Equals(useItem.SelectedOption, mOption, StringComparison.OrdinalIgnoreCase);
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return null;
        }
    }

    [EffectAtom("EventFilter", EffectAtomKind.Condition)]
    [EffectAtom("EventFilterActorIsPlayer", EffectAtomKind.Condition)]
    [EffectAtom("EventFilterTargetIsSelf", EffectAtomKind.Condition)]
    [EffectAtom("EventFilterTargetNotSelf", EffectAtomKind.Condition)]
    [EffectAtom("EventFilterActorIsPlayerTargetIsSelf", EffectAtomKind.Condition)]
    [EffectAtom("EventFilterActorIsPlayerTargetNotSelf", EffectAtomKind.Condition)]
    [EffectAtom("EventFilterSourcePrefix", EffectAtomKind.Condition)]
    [EffectAtom("EventFilterExcludeCause", EffectAtomKind.Condition)]
    public sealed class EventFilterEffectCondition : ICondition
    {
        private readonly List<CoreEventType> mEventTypes = new List<CoreEventType>();
        private StatId mStat = StatId.Attack;
        private bool mHasStat;
        private int mMinDelta = int.MinValue;
        private int mMaxDelta = int.MaxValue;
        private CardKind mTargetKind = CardKind.Unknown;
        private string mTargetDefId = string.Empty;
        private string mSourceDefId = string.Empty;
        private string mExcludeSourceDefId = string.Empty;
        private string mCause = string.Empty;
        private string mSourcePrefix = string.Empty;
        private string mExcludeCause = string.Empty;
        private bool mActorIsPlayer;
        private bool mTargetIsSelf;
        private bool mTargetNotSelf;

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
            mMaxDelta = config.Has("maxDelta") ? config.Get("maxDelta").AsInt(0) : int.MaxValue;
            mTargetKind = config.Get("targetKind").AsEnum(CardKind.Unknown);
            mTargetDefId = config.Get("targetDefId").AsString(string.Empty);
            mSourceDefId = config.Get("sourceDefId").AsString(string.Empty);
            mExcludeSourceDefId = config.Get("excludeSourceDefId").AsString(string.Empty);
            var atomName = config.Get("atom").AsString(config.Get("type").AsString(string.Empty));
            if (!Same(atomName, "EventFilterExcludeCause"))
            {
                mCause = config.Get("cause").AsString(string.Empty);
            }

            ApplyMorphology(atomName, config);
        }

        private void ApplyMorphology(string atom, EffectDslNode config)
        {
            mActorIsPlayer = Same(atom, "EventFilterActorIsPlayer")
                || Same(atom, "EventFilterActorIsPlayerTargetIsSelf")
                || Same(atom, "EventFilterActorIsPlayerTargetNotSelf");
            mTargetIsSelf = Same(atom, "EventFilterTargetIsSelf")
                || Same(atom, "EventFilterActorIsPlayerTargetIsSelf");
            mTargetNotSelf = Same(atom, "EventFilterTargetNotSelf")
                || Same(atom, "EventFilterActorIsPlayerTargetNotSelf");

            if (Same(atom, "EventFilterSourcePrefix"))
            {
                mSourcePrefix = config.Get("prefix").AsString(string.Empty);
            }

            if (Same(atom, "EventFilterExcludeCause"))
            {
                mExcludeCause = config.Get("cause").AsString(string.Empty);
            }
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
            var ownerUid = context == null || context.Instance == null || context.Instance.Owner == null
                ? 0
                : context.Instance.Owner.OwnerUid;
            var avatarUid = context == null || context.Architecture == null
                ? 0
                : context.Architecture.GetModel<BoardModel>().AvatarUid.Value;
            return new EventFilterStatCondition(
                mTargetIsSelf ? ownerUid : 0,
                mTargetNotSelf ? ownerUid : 0,
                mActorIsPlayer ? avatarUid : 0,
                mTargetKind,
                mSourceDefId,
                mExcludeSourceDefId,
                mSourcePrefix,
                mCause,
                mExcludeCause);
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

            if (mMaxDelta != int.MaxValue && gameEvent.Delta > mMaxDelta)
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

            if (!string.IsNullOrEmpty(mSourcePrefix)
                && (gameEvent.SourceDefId == null
                    || !gameEvent.SourceDefId.StartsWith(mSourcePrefix, StringComparison.OrdinalIgnoreCase)))
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
            if (mTargetIsSelf && (context.OwnerUid == 0 || eventTargetUid != context.OwnerUid))
            {
                return false;
            }

            if (mTargetNotSelf && context.OwnerUid != 0 && eventTargetUid == context.OwnerUid)
            {
                return false;
            }

            if (mActorIsPlayer && (context.AvatarUid == 0 || gameEvent.ActorUid != context.AvatarUid))
            {
                return false;
            }

            if (mTargetKind != CardKind.Unknown || !string.IsNullOrEmpty(mTargetDefId))
            {
                CardInstance target;
                if (!context.TryGetCard(eventTargetUid, out target))
                {
                    return false;
                }

                if (mTargetKind != CardKind.Unknown && target.Kind != mTargetKind)
                {
                    return false;
                }

                if (!string.IsNullOrEmpty(mTargetDefId)
                    && !Same(target.DefId, mTargetDefId))
                {
                    return false;
                }
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

    [EffectAtom("ActionSource", EffectAtomKind.Condition)]
    public sealed class ActionSourceEffectCondition : ICondition
    {
        private string mActionName = string.Empty;
        private string mSourceDefId = string.Empty;
        private string mExcludeSourceDefId = string.Empty;
        private string mCause = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mActionName = config.Get("action").AsString(string.Empty);
            mSourceDefId = config.Get("sourceDefId").AsString(string.Empty);
            mExcludeSourceDefId = config.Get("excludeSourceDefId").AsString(string.Empty);
            mCause = config.Get("cause").AsString(string.Empty);
        }

        public bool IsMet(EffectRuntimeContext context)
        {
            if (context == null)
            {
                return false;
            }

            var actionName = context.TriggerContext == null || context.TriggerContext.Action == null
                ? string.Empty
                : context.TriggerContext.Action.ActionName;
            if (!MatchesAction(actionName))
            {
                return false;
            }

            var events = context.Events;
            if (events.Count == 0)
            {
                return string.IsNullOrEmpty(mSourceDefId)
                    && string.IsNullOrEmpty(mExcludeSourceDefId)
                    && string.IsNullOrEmpty(mCause);
            }

            for (var i = 0; i < events.Count; i++)
            {
                if (MatchesSource(events[i].SourceDefId, events[i].Cause))
                {
                    return true;
                }
            }

            return false;
        }

        public IStatCondition CreateStatCondition(EffectBuildContext context)
        {
            return new ActionSourceCondition(mActionName, mSourceDefId, mExcludeSourceDefId, mCause, string.Empty);
        }

        private bool MatchesAction(string actionName)
        {
            return string.IsNullOrEmpty(mActionName) || Same(actionName, mActionName);
        }

        private bool MatchesSource(string sourceDefId, string cause)
        {
            if (!string.IsNullOrEmpty(mSourceDefId) && !Same(sourceDefId, mSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mExcludeSourceDefId) && Same(sourceDefId, mExcludeSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mCause) && !Same(cause, mCause))
            {
                return false;
            }

            return true;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
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

    [EffectAtom("TransferArmor", EffectAtomKind.Action)]
    public sealed class TransferArmorEffectAction : IAction
    {
        private int mAmount;
        private bool mAll;
        private string mReceiverRef = "Self";
        private string mCause = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mAmount = Math.Max(0, config.Get("amount").AsInt(0));
            mAll = config.Get("all").AsBool(false);
            mReceiverRef = config.Get("receiver").AsString("Self");
            mCause = config.Get("cause").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            var receiverUid = TargetResolver.ResolveSingleCardRef(context, mReceiverRef);
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new TransferArmorAction(
                        targets[i],
                        receiverUid,
                        mAmount,
                        mAll,
                        context.SourceDefId,
                        EffectActionSource.CauseOrEffect(context, mCause)));
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
        private EffectValueExpression mValue;
        private bool mUseValue;
        private string mReason = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mStat = config.Get("stat").AsEnum(StatId.Attack);
            mDelta = config.Get("delta").AsInt(0);
            mUseValue = config.Has("value");
            mValue = mUseValue ? EffectValueExpression.FromActionAmount(config) : null;
            mReason = config.Get("reason").AsString("effect");
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    var delta = mUseValue ? mValue.Evaluate(context, targets[i]) : mDelta;
                    result.Add(new ModifyBaseStatAction(targets[i], mStat, delta, mReason, context.SourceDefId));
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
                    result.Add(new MoveCardAction(targets[i], mToSlot, context.SourceDefId, context.EffectId));
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
                return new[] { new SwapBoardSlotsAction(mLeft, mRight, context.SourceDefId, context.EffectId) };
            }

            if (targets.Count < 2)
            {
                return new GameAction[0];
            }

            var left = context.Registry.Get(targets[0]).Slot.Value;
            var right = context.Registry.Get(targets[1]).Slot.Value;
            return new[] { new SwapBoardSlotsAction(left, right, context.SourceDefId, context.EffectId) };
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
                result.Add(new RotateBoardClockwiseAction(mClockwise, context.SourceDefId, context.EffectId));
            }

            return result;
        }
    }

    [EffectAtom("Flip", EffectAtomKind.Action)]
    public sealed class FlipEffectAction : IAction
    {
        public void Configure(EffectDslNode config)
        {
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            if (targets == null || targets.Count == 0)
            {
                if (context != null && context.OwnerUid != 0)
                {
                    result.Add(new FlipCardAction(context.OwnerUid, context.SourceDefId, context.EffectId));
                }

                return result;
            }

            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new FlipCardAction(targets[i], context.SourceDefId, context.EffectId));
                }
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
        private bool mPerTarget;

        public void Configure(EffectDslNode config)
        {
            mDefId = config.Get("defId").AsString(string.Empty);
            mKind = config.Get("kind").AsEnum(CardKind.Monster);
            mCount = Math.Max(0, config.Get("count").AsInt(1));
            mTop = config.Get("top").AsBool(false);
            mPerTarget = config.Get("perTarget").AsBool(false);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var count = mPerTarget ? mCount * targets.Count : mCount;
            return new[] { new ShuffleIntoDrawPileAction(mDefId, mKind, count, mTop, context.SourceDefId) };
        }
    }

    [EffectAtom("MoveToDrawPile", EffectAtomKind.Action)]
    public sealed class MoveToDrawPileEffectAction : IAction
    {
        private bool mTop;

        public void Configure(EffectDslNode config)
        {
            mTop = config.Get("top").AsBool(false);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new ShuffleCardIntoDrawPileAction(targets[i], mTop, context.SourceDefId));
                }
            }

            return result;
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
        private bool mPerTarget;

        public void Configure(EffectDslNode config)
        {
            mDefId = config.Get("defId").AsString(string.Empty);
            mKind = config.Get("kind").AsEnum(CardKind.Monster);
            mZone = config.Get("zone").AsEnum(ZoneId.DrawPile);
            mCount = Math.Max(0, config.Get("count").AsInt(1));
            mPerTarget = config.Get("perTarget").AsBool(false);
            if (config.Has("slot"))
            {
                mSlot = SlotId.Board(config.Get("slot").AsInt(1));
            }
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var count = mPerTarget ? mCount * targets.Count : mCount;
            var nodeStartDrawPileGrant = mZone == ZoneId.PlayerCardPool
                && mKind == CardKind.HelpCard
                && context.Instance?.Trigger != null
                && context.Instance.Trigger.Point == TriggerPoint.OnNodeStart;
            return new[]
            {
                new SpawnCardAction(
                    mDefId,
                    mKind,
                    mZone,
                    mSlot,
                    count,
                    context.SourceDefId,
                    nodeStartDrawPileGrant),
            };
        }
    }

    [EffectAtom("ShuffleRandomContent", EffectAtomKind.Action)]
    public sealed class ShuffleRandomContentEffectAction : IAction
    {
        private CardKind mKind = CardKind.Monster;
        private int mCount = 1;
        private bool mTop;
        private int mMinLevel;
        private int mMaxLevel;
        private bool mExcludeElite;
        private bool mExcludeBoss;
        private string mExcludeDeckId = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mKind = config.Get("kind").AsEnum(CardKind.Monster);
            mCount = Math.Max(0, config.Get("count").AsInt(1));
            mTop = config.Get("top").AsBool(false);
            mMinLevel = Math.Max(0, config.Get("minLevel").AsInt(0));
            mMaxLevel = Math.Max(0, config.Get("maxLevel").AsInt(0));
            mExcludeElite = config.Get("excludeElite").AsBool(false);
            mExcludeBoss = config.Get("excludeBoss").AsBool(false);
            mExcludeDeckId = config.Get("excludeDeckId").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            return new[]
            {
                new ShuffleRandomContentIntoDrawPileAction(
                    mKind,
                    mCount,
                    mTop,
                    mMinLevel,
                    mMaxLevel,
                    mExcludeElite,
                    mExcludeBoss,
                    mExcludeDeckId,
                    context.SourceDefId)
            };
        }
    }

    [EffectAtom("ExchangeWithDrawPile", EffectAtomKind.Action)]
    public sealed class ExchangeWithDrawPileEffectAction : IAction
    {
        private CardKind mKind = CardKind.Monster;
        private int mMinLevel;
        private int mMaxLevel;
        private bool mExcludeElite;
        private bool mExcludeBoss;

        public void Configure(EffectDslNode config)
        {
            mKind = config.Get("kind").AsEnum(CardKind.Monster);
            mMinLevel = Math.Max(0, config.Get("minLevel").AsInt(0));
            mMaxLevel = Math.Max(0, config.Get("maxLevel").AsInt(0));
            mExcludeElite = config.Get("excludeElite").AsBool(false);
            mExcludeBoss = config.Get("excludeBoss").AsBool(false);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new ExchangeWithDrawPileAction(
                        targets[i],
                        mKind,
                        mMinLevel,
                        mMaxLevel,
                        mExcludeElite,
                        mExcludeBoss,
                        context.SourceDefId));
                }
            }

            return result;
        }
    }

    [EffectAtom("ForceBattle", EffectAtomKind.Action)]
    public sealed class ForceBattleEffectAction : IAction
    {
        public void Configure(EffectDslNode config) { }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new ForceBattleAction(targets[i], context.SourceDefId, context.EffectId));
                }
            }

            return result;
        }
    }

    [EffectAtom("AddModifier", EffectAtomKind.Action)]
    public sealed class AddModifierEffectAction : IAction
    {
        private StatId mStat = StatId.Attack;
        private ModifierOp mOp = ModifierOp.Add;
        private float mValue;
        private EffectValueExpression mValueExpression;
        private bool mUseValueExpression;
        private ModifierLayer mLayer = ModifierLayer.Temporary;
        private ModifierScope mScope = ModifierScope.UntilBattleEnds;
        private string mSource = "effect.action";
        private string mActiveWhileAdjacentToRef = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mStat = config.Get("stat").AsEnum(StatId.Attack);
            mOp = config.Get("op").AsEnum(ModifierOp.Add);
            mValue = config.Get("value").AsFloat(0f);
            mUseValueExpression = config.Get("value").IsObject;
            mValueExpression = mUseValueExpression ? EffectValueExpression.FromModifierValue(config) : null;
            mLayer = config.Get("layer").AsEnum(ModifierLayer.Temporary);
            mScope = config.Get("scope").AsEnum(ModifierScope.UntilBattleEnds);
            mSource = config.Get("source").AsString("effect.action");
            mActiveWhileAdjacentToRef = config.Get("activeWhileAdjacentTo").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new AddStatModifierAction(
                        targets[i],
                        mStat,
                        mOp,
                        mUseValueExpression ? mValueExpression.Evaluate(context, targets[i]) : mValue,
                        mLayer,
                        mScope,
                        mSource,
                        context.SourceDefId,
                        CreateActionCondition(context, targets[i])));
                }
            }

            return result;
        }

        private IStatCondition CreateActionCondition(EffectRuntimeContext context, int targetUid)
        {
            if (string.IsNullOrEmpty(mActiveWhileAdjacentToRef))
            {
                return null;
            }

            var sourceUid = TargetResolver.ResolveSingleCardRef(context, mActiveWhileAdjacentToRef);
            return sourceUid == 0 ? null : new SourceAdjacentToUidCondition(sourceUid, targetUid);
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
        private string mConditionTarget = "ActionTarget";
        private string mConditionActorRef = string.Empty;
        private CardKind mConditionTargetKind = CardKind.Unknown;
        private string mSourceAction = string.Empty;
        private string mExcludeSourcePrefix = string.Empty;

        public void Configure(EffectDslNode config)
        {
            mRule = config.Get("rule").AsEnum(RuleId.DamageMultiplier);
            mOp = config.Get("op").AsEnum(ModifierOp.Add);
            mValue = config.Get("value").AsFloat(0f);
            mLayer = config.Get("layer").AsEnum(ModifierLayer.Temporary);
            mScope = config.Get("scope").AsEnum(ModifierScope.Once);
            mSource = config.Get("source").AsString("effect.action.rule");
            mConditionTarget = config.Get("conditionTarget").AsString("ActionTarget");
            mConditionActorRef = config.Get("conditionActor").AsString(string.Empty);
            mConditionTargetKind = config.Get("conditionTargetKind").AsEnum(CardKind.Unknown);
            mSourceAction = config.Get("sourceAction").AsString(string.Empty);
            mExcludeSourcePrefix = config.Get("excludeSourcePrefix").AsString(string.Empty);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var result = new List<GameAction>();
            var useTargetCondition = !string.Equals(mConditionTarget, "None", StringComparison.OrdinalIgnoreCase);
            var actorUid = TargetResolver.ResolveSingleCardRef(context, mConditionActorRef);
            for (var i = 0; i < targets.Count; i++)
            {
                if (targets[i] != 0)
                {
                    result.Add(new AddRuleModifierAction(
                        targets[i],
                        useTargetCondition,
                        actorUid,
                        mConditionTargetKind,
                        mRule,
                        mOp,
                        mValue,
                        mLayer,
                        mScope,
                        mSource,
                        mSourceAction,
                        mExcludeSourcePrefix));
                }
            }

            return result;
        }
    }

    [EffectAtom("ReplayHelpCardEffects", EffectAtomKind.Action)]
    public sealed class ReplayHelpCardEffectsEffectAction : IAction
    {
        private CardKind mTargetKind = CardKind.Unknown;
        private bool mDeactivateSelf;

        public void Configure(EffectDslNode config)
        {
            mTargetKind = config.Get("targetKind").AsEnum(CardKind.Unknown);
            mDeactivateSelf = config.Get("deactivateSelf").AsBool(false);
        }

        public IReadOnlyList<GameAction> BuildActions(EffectRuntimeContext context, IReadOnlyList<int> targets)
        {
            var useItem = context.TriggerContext == null ? null : context.TriggerContext.Action as UseItemAction;
            return new[] { new ReplayHelpCardEffectsAction(context.OwnerUid, context.Instance.InstanceId, useItem, mTargetKind, mDeactivateSelf) };
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
            string defId,
            CardKind kind,
            ZoneId zone,
            IReadOnlyList<ZoneId> zones,
            string adjacentToRef,
            int minLevel,
            int maxLevel,
            bool excludeElite,
            bool excludeBoss,
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

            if (zones != null && zones.Count > 0)
            {
                for (var i = 0; i < zones.Count; i++)
                {
                    AddCardsFromZone(context, result, defId, kind, zones[i], requiresAdjacency, adjacentToCard, minLevel, maxLevel, excludeElite, excludeBoss, excludeRefs);
                }

                return result;
            }

            if (zone == ZoneId.None || zone == ZoneId.Board)
            {
                foreach (var uid in context.Board.BoardCardUids())
                {
                    AddIfMatches(context, result, uid, defId, kind, ZoneId.Board, requiresAdjacency, adjacentToCard, minLevel, maxLevel, excludeElite, excludeBoss, excludeRefs);
                }

                return result;
            }

            AddCardsFromZone(context, result, defId, kind, zone, requiresAdjacency, adjacentToCard, minLevel, maxLevel, excludeElite, excludeBoss, excludeRefs);

            return result;
        }

        private static void AddCardsFromZone(
            EffectRuntimeContext context,
            List<int> result,
            string defId,
            CardKind kind,
            ZoneId zone,
            bool requiresAdjacency,
            CardInstance adjacentToCard,
            int minLevel,
            int maxLevel,
            bool excludeElite,
            bool excludeBoss,
            IReadOnlyList<string> excludeRefs)
        {
            if (zone == ZoneId.Board)
            {
                foreach (var uid in context.Board.BoardCardUids())
                {
                    AddIfMatches(context, result, uid, defId, kind, zone, requiresAdjacency, adjacentToCard, minLevel, maxLevel, excludeElite, excludeBoss, excludeRefs);
                }

                return;
            }

            foreach (var pair in context.Registry.Cards)
            {
                AddIfMatches(context, result, pair.Key, defId, kind, zone, requiresAdjacency, adjacentToCard, minLevel, maxLevel, excludeElite, excludeBoss, excludeRefs);
            }
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
            string defId,
            CardKind kind,
            ZoneId zone,
            bool requiresAdjacency,
            CardInstance adjacentToCard,
            int minLevel,
            int maxLevel,
            bool excludeElite,
            bool excludeBoss,
            IReadOnlyList<string> excludeRefs)
        {
            CardInstance card;
            if (!context.TryGetCard(uid, out card))
            {
                return;
            }

            if (!string.IsNullOrEmpty(defId) && card.DefId != defId)
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

            var level = card.Counters.Get(CoreCounterKeys.Level);
            if (minLevel > 0 && level < minLevel)
            {
                return;
            }

            if (maxLevel > 0 && level > maxLevel)
            {
                return;
            }

            if (excludeElite && card.Counters.Get(CoreCounterKeys.Elite) > 0)
            {
                return;
            }

            if (excludeBoss && card.Counters.Get(CoreCounterKeys.Boss) > 0)
            {
                return;
            }

            if (requiresAdjacency && (adjacentToCard == null || !context.Architecture.GetSystem<IBoardSystem>().AreAdjacent(card, adjacentToCard)))
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

    internal sealed class StatAtLeastStatCondition : IStatCondition
    {
        private readonly int mTargetUid;
        private readonly StatId mStat;
        private readonly float mValue;

        public StatAtLeastStatCondition(int targetUid, StatId stat, float value)
        {
            mTargetUid = targetUid;
            mStat = stat;
            mValue = value;
        }

        public bool IsMet(StatEvaluationContext context)
        {
            if (context == null || context.Registry == null)
            {
                return false;
            }

            var card = context.Owner;
            if (mTargetUid != 0 && !context.Registry.TryGet(mTargetUid, out card))
            {
                return false;
            }

            if (card == null)
            {
                return false;
            }

            return mStat == StatId.CurrentArmor || mStat == StatId.Armor
                ? StatArmorUtility.GetCurrentArmor(card) >= mValue
                : card.Stats.GetBase(mStat) >= mValue;
        }
    }

    internal sealed class EventFilterStatCondition : IStatCondition
    {
        private readonly int mTargetIsUid;
        private readonly int mTargetNotUid;
        private readonly int mActorIsUid;
        private readonly CardKind mTargetKind;
        private readonly string mSourceDefId;
        private readonly string mExcludeSourceDefId;
        private readonly string mSourcePrefix;
        private readonly string mCause;
        private readonly string mExcludeCause;

        public EventFilterStatCondition(
            int targetIsUid,
            int targetNotUid,
            int actorIsUid,
            CardKind targetKind,
            string sourceDefId,
            string excludeSourceDefId,
            string sourcePrefix,
            string cause,
            string excludeCause)
        {
            mTargetIsUid = targetIsUid;
            mTargetNotUid = targetNotUid;
            mActorIsUid = actorIsUid;
            mTargetKind = targetKind;
            mSourceDefId = sourceDefId ?? string.Empty;
            mExcludeSourceDefId = excludeSourceDefId ?? string.Empty;
            mSourcePrefix = sourcePrefix ?? string.Empty;
            mCause = cause ?? string.Empty;
            mExcludeCause = excludeCause ?? string.Empty;
        }

        public bool IsMet(StatEvaluationContext context)
        {
            if (context == null || context.Owner == null)
            {
                return false;
            }

            if (mTargetIsUid != 0 && context.Owner.Uid != mTargetIsUid)
            {
                return false;
            }

            if (mTargetNotUid != 0 && context.Owner.Uid == mTargetNotUid)
            {
                return false;
            }

            if (mTargetKind != CardKind.Unknown && context.Owner.Kind != mTargetKind)
            {
                return false;
            }

            if (mActorIsUid != 0 && context.ActorUid != mActorIsUid)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mSourceDefId) && !Same(context.SourceDefId, mSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mExcludeSourceDefId) && Same(context.SourceDefId, mExcludeSourceDefId))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mSourcePrefix) && !StartsWith(context.SourceDefId, mSourcePrefix))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mCause) && !Same(context.Cause, mCause))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(mExcludeCause) && Same(context.Cause, mExcludeCause))
            {
                return false;
            }

            return true;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWith(string value, string prefix)
        {
            return value != null && prefix != null && value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
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

        public static EffectValueExpression FromModifierValue(EffectDslNode config)
        {
            return new EffectValueExpression(config == null ? null : config.Get("value"), 0, config != null && config.Has("value"));
        }

        public int Evaluate(EffectRuntimeContext context, int targetUid)
        {
            return Math.Max(0, (int)Math.Round(mUseNode ? EvaluateNode(mNode, context, targetUid) : mFallback));
        }

        public float Evaluate(StatEvaluationContext context)
        {
            return Math.Max(0f, mUseNode ? EvaluateStatNode(mNode, context) : mFallback);
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

            if (Same(op, "Floor"))
            {
                return (float)Math.Floor(current);
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
            if (Same(source, "CardCount"))
            {
                return CountCards(context == null ? null : context.Registry, context == null ? null : context.Board, node);
            }

            if (Same(source, "BoardMarkCount"))
            {
                var mark = node.Get("mark").AsEnum(BoardMarkId.Blessed);
                return context == null || context.Board == null || mark == BoardMarkId.None ? 0f : context.Board.CountMarkedSlots(mark);
            }

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
            if (stat == StatId.CurrentArmor)
            {
                return StatArmorUtility.GetCurrentArmor(card);
            }

            if (node.Get("effective").AsBool(false))
            {
                return context.Architecture.GetSystem<IStatSystem>().GetEffectiveValue(card, stat);
            }

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

                if (Same(field, "RemovedAttack"))
                {
                    return events[i].RemovedAttack;
                }

                if (Same(field, "RemovedArmor"))
                {
                    return events[i].RemovedArmor;
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

        private static float EvaluateStatNode(EffectDslNode node, StatEvaluationContext context)
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
                return EvaluateStatOp(node, context);
            }

            return EvaluateStatSource(node, context);
        }

        private static float EvaluateStatOp(EffectDslNode node, StatEvaluationContext context)
        {
            var values = node.Get("values").AsArray();
            var op = node.Get("op").AsString(string.Empty);
            if (values.Count == 0)
            {
                return 0f;
            }

            var current = EvaluateStatNode(values[0], context);
            if (Same(op, "Negate"))
            {
                return -current;
            }

            if (Same(op, "Floor"))
            {
                return (float)Math.Floor(current);
            }

            for (var i = 1; i < values.Count; i++)
            {
                var next = EvaluateStatNode(values[i], context);
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

        private static float EvaluateStatSource(EffectDslNode node, StatEvaluationContext context)
        {
            var source = node.Get("source").AsString(string.Empty);
            if (Same(source, "CardCount"))
            {
                return CountCards(context == null ? null : context.Registry, context == null ? null : context.Board, node);
            }

            if (Same(source, "BoardMarkCount"))
            {
                var mark = node.Get("mark").AsEnum(BoardMarkId.Blessed);
                return context == null || context.Board == null || mark == BoardMarkId.None ? 0f : context.Board.CountMarkedSlots(mark);
            }

            var card = ResolveStatCard(context, source);
            if (card == null)
            {
                return 0f;
            }

            var stat = node.Get("stat").AsEnum(StatId.Attack);
            return card.Stats.GetBase(stat);
        }

        private static CardInstance ResolveStatCard(StatEvaluationContext context, string source)
        {
            if (context == null)
            {
                return null;
            }

            if (Same(source, "Owner") || Same(source, "Self") || Same(source, "Target"))
            {
                return context.Owner;
            }

            if (Same(source, "Player") && context.Board != null && context.Registry != null)
            {
                CardInstance avatar;
                return context.Registry.TryGet(context.Board.AvatarUid.Value, out avatar) ? avatar : null;
            }

            return null;
        }

        private static float CountCards(CardRegistry registry, BoardModel board, EffectDslNode node)
        {
            if (registry == null || board == null)
            {
                return 0f;
            }

            var defId = node.Get("defId").AsString(string.Empty);
            var kind = node.Get("kind").AsEnum(CardKind.Unknown);
            var zone = node.Get("zone").AsEnum(ZoneId.None);
            var zones = node.Get("zones").AsArray();
            if (zones.Count > 0)
            {
                var total = 0;
                for (var i = 0; i < zones.Count; i++)
                {
                    total += CountCardsInZone(registry, board, defId, kind, zones[i].AsEnum(ZoneId.None));
                }

                return total;
            }

            return CountCardsInZone(registry, board, defId, kind, zone);
        }

        private static int CountCardsInZone(CardRegistry registry, BoardModel board, string defId, CardKind kind, ZoneId zone)
        {
            var count = 0;
            if (zone == ZoneId.None || zone == ZoneId.Board)
            {
                foreach (var uid in board.BoardCardUids())
                {
                    if (MatchesCard(registry, uid, defId, kind, ZoneId.Board))
                    {
                        count++;
                    }
                }

                return count;
            }

            foreach (var pair in registry.Cards)
            {
                if (MatchesCard(registry, pair.Key, defId, kind, zone))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool MatchesCard(CardRegistry registry, int uid, string defId, CardKind kind, ZoneId zone)
        {
            CardInstance card;
            if (!registry.TryGet(uid, out card))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(defId) && card.DefId != defId)
            {
                return false;
            }

            if (kind != CardKind.Unknown && card.Kind != kind)
            {
                return false;
            }

            return zone == ZoneId.None || card.Zone.Value == zone;
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

            if (Same(source, "CardCount"))
            {
                if (!node.Has("defId") && !node.Has("kind"))
                {
                    result.Add("schema.value.cardCount", path + " requires defId or kind for CardCount.");
                }

                ValidateZones(node, path, result);
                return;
            }

            if (Same(source, "BoardMarkCount"))
            {
                if (node.Has("mark"))
                {
                    BoardMarkId parsed;
                    if (!Enum.TryParse(node.Get("mark").AsString(string.Empty), true, out parsed) || parsed == BoardMarkId.None)
                    {
                        result.Add("schema.value.mark", path + ".mark is not supported.");
                    }
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
                || Same(source, "Event")
                || Same(source, "CardCount")
                || Same(source, "BoardMarkCount");
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
                || Same(field, "RemainingArmor")
                || Same(field, "RemovedAttack")
                || Same(field, "RemovedArmor");
        }

        private static void ValidateZones(EffectDslNode node, string path, EffectValidationResult result)
        {
            if (node.Has("zone") && !IsSupportedZone(node.Get("zone").AsString(string.Empty)))
            {
                result.Add("schema.value.zone", path + ".zone is not supported.");
            }

            var zones = node.Get("zones").AsArray();
            for (var i = 0; i < zones.Count; i++)
            {
                if (!IsSupportedZone(zones[i].AsString(string.Empty)))
                {
                    result.Add("schema.value.zone", path + ".zones[" + i + "] is not supported.");
                }
            }
        }

        private static bool IsSupportedZone(string zone)
        {
            if (string.IsNullOrEmpty(zone))
            {
                return false;
            }

            ZoneId ignored;
            return Enum.TryParse(zone, true, out ignored);
        }

        private static bool IsSupportedOp(string op)
        {
            return Same(op, "Add")
                || Same(op, "Subtract")
                || Same(op, "Multiply")
                || Same(op, "Min")
                || Same(op, "Max")
                || Same(op, "Floor")
                || Same(op, "Negate");
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
