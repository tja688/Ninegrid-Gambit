using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 导演拥有的洗回牌库表演队列：只入队、由 Present 前缀通道 Flush，禁止 Forget 旁路泵。
    /// </summary>
    public sealed class ShuffleIntoDeckPresentSink
    {
        private readonly Queue<ShuffleIntoDeckPresentationEntry> mPending =
            new Queue<ShuffleIntoDeckPresentationEntry>();

        public int PendingCount
        {
            get { return mPending.Count; }
        }

        public bool HasPending
        {
            get { return mPending.Count > 0; }
        }

        public void Enqueue(ShuffleIntoDeckPresentationEntry entry)
        {
            if (entry.Uid <= 0)
            {
                return;
            }

            mPending.Enqueue(entry);
        }

        public void EnqueueRange(IReadOnlyList<ShuffleIntoDeckPresentationEntry> entries)
        {
            if (entries == null)
            {
                return;
            }

            for (var i = 0; i < entries.Count; i++)
            {
                Enqueue(entries[i]);
            }
        }

        public bool TryDequeue(out ShuffleIntoDeckPresentationEntry entry)
        {
            if (mPending.Count == 0)
            {
                entry = default;
                return false;
            }

            entry = mPending.Dequeue();
            return true;
        }

        public void Clear()
        {
            mPending.Clear();
        }

        public int PurgeUids(ISet<int> uids)
        {
            if (uids == null || uids.Count == 0 || mPending.Count == 0)
            {
                return 0;
            }

            var kept = new Queue<ShuffleIntoDeckPresentationEntry>();
            var removed = 0;
            while (mPending.Count > 0)
            {
                var entry = mPending.Dequeue();
                if (uids.Contains(entry.Uid))
                {
                    removed++;
                    continue;
                }

                kept.Enqueue(entry);
            }

            while (kept.Count > 0)
            {
                mPending.Enqueue(kept.Dequeue());
            }

            return removed;
        }
    }
}
