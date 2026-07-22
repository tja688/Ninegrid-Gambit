using NineGrid.Core;

namespace NineGrid.Presentation.Tests.Fixtures
{
    public static class EventLogAssert
    {
        public static bool ContainsTypeSince(EventLog log, int startIndex, CoreEventType type)
        {
            return IndexOfTypeSince(log, startIndex, type) >= 0;
        }

        public static int IndexOfTypeSince(EventLog log, int startIndex, CoreEventType type)
        {
            if (log == null)
            {
                return -1;
            }

            var entries = log.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
