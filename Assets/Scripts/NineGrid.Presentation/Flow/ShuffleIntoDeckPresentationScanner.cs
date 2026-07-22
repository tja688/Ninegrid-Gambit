using System;
using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Flow
{
    public enum ShuffleIntoDeckEventKind
    {
        NewCard,
        RandomCard,
        ExistingCard,
    }

    public readonly struct ShuffleIntoDeckPresentationEntry
    {
        public ShuffleIntoDeckPresentationEntry(
            int uid,
            string defId,
            string cause,
            int actionId,
            long eventSequence,
            ShuffleIntoDeckEventKind kind,
            int triggerCardUid,
            int fromBoardSlot)
        {
            Uid = uid;
            DefId = defId ?? string.Empty;
            Cause = cause ?? string.Empty;
            ActionId = actionId;
            EventSequence = eventSequence;
            Kind = kind;
            TriggerCardUid = triggerCardUid;
            FromBoardSlot = fromBoardSlot;
        }

        public int Uid { get; }
        public string DefId { get; }
        public string Cause { get; }
        public int ActionId { get; }
        public long EventSequence { get; }
        public ShuffleIntoDeckEventKind Kind { get; }
        public int TriggerCardUid { get; }
        public int FromBoardSlot { get; }
    }

    /// <summary>
    /// 从 EventLog 扫描局内洗入事件，供表现层即时入组与单测去重。
    /// </summary>
    public static class ShuffleIntoDeckPresentationScanner
    {
        private const string ShuffleIntoPrefix = "shuffleInto:";
        private const string ShuffleRandomPrefix = "shuffleRandom:";
        private const string ShuffleExistingPrefix = "shuffleExisting:";

        public static bool TryParseShuffleIntoEvent(
            CoreGameEvent entry,
            out ShuffleIntoDeckEventKind kind,
            out string defId)
        {
            kind = default;
            defId = string.Empty;
            if (entry == null || entry.Type != CoreEventType.CardDealt || entry.CardUid <= 0)
            {
                return false;
            }

            var message = entry.Message ?? string.Empty;
            if (entry.ActionName == "ShuffleIntoDrawPile" && message.StartsWith(ShuffleIntoPrefix, StringComparison.Ordinal))
            {
                kind = ShuffleIntoDeckEventKind.NewCard;
                defId = message.Substring(ShuffleIntoPrefix.Length);
                return !string.IsNullOrEmpty(defId);
            }

            if (entry.ActionName == "ShuffleRandomContentIntoDrawPile"
                && message.StartsWith(ShuffleRandomPrefix, StringComparison.Ordinal))
            {
                kind = ShuffleIntoDeckEventKind.RandomCard;
                defId = message.Substring(ShuffleRandomPrefix.Length);
                return !string.IsNullOrEmpty(defId);
            }

            if (entry.ActionName == "ShuffleCardIntoDrawPile"
                && message.StartsWith(ShuffleExistingPrefix, StringComparison.Ordinal))
            {
                kind = ShuffleIntoDeckEventKind.ExistingCard;
                defId = message.Substring(ShuffleExistingPrefix.Length);
                return !string.IsNullOrEmpty(defId);
            }

            return false;
        }

        public static IReadOnlyList<ShuffleIntoDeckPresentationEntry> Collect(
            IReadOnlyList<CoreGameEvent> entries,
            int startIndex,
            Func<int, bool> isAlreadyInDeck)
        {
            if (entries == null || startIndex < 0 || startIndex >= entries.Count)
            {
                return Array.Empty<ShuffleIntoDeckPresentationEntry>();
            }

            isAlreadyInDeck ??= _ => false;

            var actionTriggerCards = new Dictionary<int, int>();
            var removedBoardSlots = new Dictionary<int, int>();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.EffectTriggered && entry.CardUid > 0)
                {
                    actionTriggerCards[entry.ActionId] = entry.CardUid;
                }

                if ((entry.Type == CoreEventType.CardKilled || entry.Type == CoreEventType.CardRemoved)
                    && entry.CardUid > 0
                    && entry.FromSlot.IsBoardSlot)
                {
                    removedBoardSlots[entry.CardUid] = entry.FromSlot.Index;
                }
            }

            var results = new List<ShuffleIntoDeckPresentationEntry>();
            var seenUids = new HashSet<int>();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!TryParseShuffleIntoEvent(entry, out var kind, out var defId))
                {
                    continue;
                }

                if (!seenUids.Add(entry.CardUid) || isAlreadyInDeck(entry.CardUid))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(defId) && !string.IsNullOrEmpty(entry.SourceDefId))
                {
                    defId = entry.SourceDefId;
                }

                var triggerCardUid = ResolveTriggerCardUid(entries, startIndex, i, entry.ActionId, actionTriggerCards);
                var fromBoardSlot = entry.FromSlot.IsBoardSlot
                    ? entry.FromSlot.Index
                    : ResolveRemovedBoardSlot(triggerCardUid, removedBoardSlots);

                results.Add(new ShuffleIntoDeckPresentationEntry(
                    entry.CardUid,
                    defId,
                    entry.Cause,
                    entry.ActionId,
                    entry.Sequence,
                    kind,
                    triggerCardUid,
                    fromBoardSlot));
            }

            return results;
        }

        private static int ResolveTriggerCardUid(
            IReadOnlyList<CoreGameEvent> entries,
            int startIndex,
            int shuffleEventIndex,
            int actionId,
            IReadOnlyDictionary<int, int> actionTriggerCards)
        {
            if (actionTriggerCards != null
                && actionTriggerCards.TryGetValue(actionId, out var mappedUid)
                && mappedUid > 0)
            {
                return mappedUid;
            }

            for (var i = shuffleEventIndex; i >= startIndex; i--)
            {
                var entry = entries[i];
                if (entry.Type == CoreEventType.EffectTriggered && entry.CardUid > 0)
                {
                    return entry.CardUid;
                }
            }

            return 0;
        }

        private static int ResolveRemovedBoardSlot(
            int triggerCardUid,
            IReadOnlyDictionary<int, int> removedBoardSlots)
        {
            if (triggerCardUid <= 0 || removedBoardSlots == null)
            {
                return 0;
            }

            return removedBoardSlots.TryGetValue(triggerCardUid, out var slot) ? slot : 0;
        }
    }
}
