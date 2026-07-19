using System.Collections.Generic;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 洗回 sink 成组：同 ActionId 的新生/随机洗入且 N≥2 → 炸牌组；其余单条保留。
    /// </summary>
    public readonly struct ShuffleBurstGroup
    {
        public ShuffleBurstGroup(int actionId, IReadOnlyList<ShuffleIntoDeckPresentationEntry> entries)
        {
            ActionId = actionId;
            Entries = entries ?? System.Array.Empty<ShuffleIntoDeckPresentationEntry>();
        }

        public int ActionId { get; }
        public IReadOnlyList<ShuffleIntoDeckPresentationEntry> Entries { get; }
    }

    public static class ShuffleBurstGrouper
    {
        public static void Partition(
            IReadOnlyList<ShuffleIntoDeckPresentationEntry> pending,
            List<ShuffleBurstGroup> burstGroups,
            List<ShuffleIntoDeckPresentationEntry> leftovers)
        {
            if (burstGroups == null || leftovers == null)
            {
                return;
            }

            burstGroups.Clear();
            leftovers.Clear();
            if (pending == null || pending.Count == 0)
            {
                return;
            }

            var buckets = new Dictionary<int, List<ShuffleIntoDeckPresentationEntry>>();
            var bucketOrder = new List<int>();

            for (var i = 0; i < pending.Count; i++)
            {
                var entry = pending[i];
                if (!CanBurst(entry))
                {
                    leftovers.Add(entry);
                    continue;
                }

                if (!buckets.TryGetValue(entry.ActionId, out var bucket))
                {
                    bucket = new List<ShuffleIntoDeckPresentationEntry>(4);
                    buckets[entry.ActionId] = bucket;
                    bucketOrder.Add(entry.ActionId);
                }

                bucket.Add(entry);
            }

            for (var i = 0; i < bucketOrder.Count; i++)
            {
                var actionId = bucketOrder[i];
                var bucket = buckets[actionId];
                if (bucket.Count >= 2)
                {
                    burstGroups.Add(new ShuffleBurstGroup(actionId, bucket));
                }
                else
                {
                    leftovers.AddRange(bucket);
                }
            }
        }

        private static bool CanBurst(ShuffleIntoDeckPresentationEntry entry)
        {
            if (entry.ActionId <= 0 || entry.Uid <= 0)
            {
                return false;
            }

            return entry.Kind == ShuffleIntoDeckEventKind.NewCard
                || entry.Kind == ShuffleIntoDeckEventKind.RandomCard;
        }
    }
}
