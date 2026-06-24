using System;
using System.Collections.Generic;
using System.Text;

namespace NineGrid.Presentation.Diagnostics
{
    public sealed class PresentationTraceRingBuffer
    {
        private readonly PresentationTraceEntry[] mEntries;
        private int mWriteIndex;
        private int mCount;

        public PresentationTraceRingBuffer(int capacity)
        {
            capacity = Math.Max(16, capacity);
            mEntries = new PresentationTraceEntry[capacity];
        }

        public int Capacity => mEntries.Length;
        public int Count => mCount;

        public void Add(PresentationTraceEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            mEntries[mWriteIndex] = entry;
            mWriteIndex = (mWriteIndex + 1) % mEntries.Length;
            if (mCount < mEntries.Length)
            {
                mCount++;
            }
        }

        public void CopyTo(List<PresentationTraceEntry> destination)
        {
            if (destination == null)
            {
                return;
            }

            destination.Clear();
            if (mCount == 0)
            {
                return;
            }

            var start = mCount < mEntries.Length ? 0 : mWriteIndex;
            for (var i = 0; i < mCount; i++)
            {
                var index = (start + i) % mEntries.Length;
                destination.Add(mEntries[index]);
            }
        }

        public string ToJsonArray()
        {
            var builder = new StringBuilder(mCount * 96);
            builder.Append('[');
            var start = mCount < mEntries.Length ? 0 : mWriteIndex;
            for (var i = 0; i < mCount; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var index = (start + i) % mEntries.Length;
                mEntries[index].AppendJson(builder, trailingComma: false);
            }

            builder.Append(']');
            return builder.ToString();
        }
    }
}
