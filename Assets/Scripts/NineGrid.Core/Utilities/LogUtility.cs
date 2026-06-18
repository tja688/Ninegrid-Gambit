using System.Collections.Generic;
using QFramework;

namespace NineGrid.Core.Utilities
{
    public enum CoreLogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error
    }

    public struct CoreLogEntry
    {
        public readonly long Sequence;
        public readonly CoreLogLevel Level;
        public readonly string Category;
        public readonly string Message;
        public readonly IReadOnlyDictionary<string, string> Fields;

        public CoreLogEntry(long sequence, CoreLogLevel level, string category, string message, IReadOnlyDictionary<string, string> fields)
        {
            Sequence = sequence;
            Level = level;
            Category = category ?? string.Empty;
            Message = message ?? string.Empty;
            Fields = fields;
        }
    }

    public interface ILogUtility : IUtility
    {
        IReadOnlyList<CoreLogEntry> Entries { get; }
        void Log(CoreLogLevel level, string category, string message);
        void Log(CoreLogLevel level, string category, string message, IReadOnlyDictionary<string, string> fields);
        void Clear();
    }

    public sealed class InMemoryLogUtility : ILogUtility
    {
        private readonly List<CoreLogEntry> mEntries = new List<CoreLogEntry>();
        private long mNextSequence;

        public IReadOnlyList<CoreLogEntry> Entries
        {
            get { return mEntries; }
        }

        public void Log(CoreLogLevel level, string category, string message)
        {
            Log(level, category, message, null);
        }

        public void Log(CoreLogLevel level, string category, string message, IReadOnlyDictionary<string, string> fields)
        {
            var copiedFields = fields == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(fields);

            mEntries.Add(new CoreLogEntry(mNextSequence++, level, category, message, copiedFields));
        }

        public void Clear()
        {
            mEntries.Clear();
            mNextSequence = 0L;
        }
    }
}
