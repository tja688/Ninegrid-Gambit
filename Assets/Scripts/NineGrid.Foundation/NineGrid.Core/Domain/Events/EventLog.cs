using System.Collections.Generic;

namespace NineGrid.Core
{
    public sealed class EventLog
    {
        private readonly List<CoreGameEvent> mEntries = new List<CoreGameEvent>();
        private long mNextSequence;

        public IReadOnlyList<CoreGameEvent> Entries
        {
            get { return mEntries; }
        }

        public CoreGameEvent Append(CoreGameEvent gameEvent)
        {
            if (gameEvent == null)
            {
                return null;
            }

            gameEvent.AssignSequence(mNextSequence++);
            mEntries.Add(gameEvent);
            return gameEvent;
        }

        public void AppendRange(IEnumerable<CoreGameEvent> events)
        {
            if (events == null)
            {
                return;
            }

            foreach (var gameEvent in events)
            {
                Append(gameEvent);
            }
        }

        public bool Contains(CoreEventType type)
        {
            for (var i = 0; i < mEntries.Count; i++)
            {
                if (mEntries[i].Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        public void Clear()
        {
            mEntries.Clear();
            mNextSequence = 0L;
        }
    }
}
