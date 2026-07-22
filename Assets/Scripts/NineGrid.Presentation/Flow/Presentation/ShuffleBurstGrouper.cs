using System.Collections.Generic;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 洗回 sink 成组：同 TriggerCardUid（&gt;0）的新生/随机洗入且 N≥2 → 炸牌组；
    /// TriggerCardUid==0 时回退同 ActionId；其余单条保留。
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

            // TriggerCardUid>0 用正键；ActionId 回退用负键，避免与 trigger uid 撞桶。
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

                var key = ResolveBucketKey(entry);
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = new List<ShuffleIntoDeckPresentationEntry>(4);
                    buckets[key] = bucket;
                    bucketOrder.Add(key);
                }

                bucket.Add(entry);
            }

            for (var i = 0; i < bucketOrder.Count; i++)
            {
                var key = bucketOrder[i];
                var bucket = buckets[key];
                if (bucket.Count >= 2)
                {
                    burstGroups.Add(new ShuffleBurstGroup(bucket[0].ActionId, bucket));
                }
                else
                {
                    leftovers.AddRange(bucket);
                }
            }
        }

        private static int ResolveBucketKey(ShuffleIntoDeckPresentationEntry entry)
        {
            if (entry.TriggerCardUid > 0)
            {
                return entry.TriggerCardUid;
            }

            return -entry.ActionId;
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
