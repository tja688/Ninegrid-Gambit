using System.Collections.Generic;

namespace NineGrid.Flow.BattleLog
{
    public enum BattleLogEntryKind
    {
        /// <summary>效果 / 技能 / 遗物触发的聚合头，其下缩进挂它造成的数值变动。</summary>
        EffectHeader,
        Damage,
        Heal,
        Armor,
        Gold,
        BaseStat,
        Kill,
        Relic,
        Note
    }

    /// <summary>已着色的一行日志。<see cref="Depth"/> 为 1 表示归属上方的 <see cref="BattleLogEntryKind.EffectHeader"/>。</summary>
    public sealed class BattleLogEntry
    {
        public BattleLogEntry(long sequence, BattleLogEntryKind kind, int depth, string richText)
        {
            Sequence = sequence;
            Kind = kind;
            Depth = depth;
            RichText = richText ?? string.Empty;
        }

        public long Sequence { get; }
        public BattleLogEntryKind Kind { get; }
        public int Depth { get; }
        public string RichText { get; }
    }

    /// <summary>
    /// 一个房间（节点）的日志分段。面板默认只渲染最后一段，往回翻才逐段载入更早的房间。
    /// </summary>
    public sealed class BattleLogSection
    {
        private readonly List<BattleLogEntry> mEntries = new List<BattleLogEntry>();

        public BattleLogSection(int id, string title)
        {
            Id = id;
            Title = title ?? string.Empty;
        }

        public int Id { get; }

        /// <summary>如「第 1 层 · 精英」；开局未进房间时为空串。</summary>
        public string Title { get; private set; }

        public IReadOnlyList<BattleLogEntry> Entries => mEntries;

        public void Retitle(string title)
        {
            if (!string.IsNullOrEmpty(title))
            {
                Title = title;
            }
        }

        public void Add(BattleLogEntry entry)
        {
            if (entry != null)
            {
                mEntries.Add(entry);
            }
        }

        /// <summary>丢弃最旧条目，给整局长跑封顶。</summary>
        public void TrimOldest(int count)
        {
            if (count > 0)
            {
                mEntries.RemoveRange(0, count > mEntries.Count ? mEntries.Count : count);
            }
        }
    }
}
