using System;
using System.Collections.Generic;

namespace NineGrid.Flow.BattleLog
{
    /// <summary>
    /// 人读战斗日志的唯一存放处：按房间分段（<see cref="BattleLogSection"/>），
    /// 段内是已聚合、已着色的成品行。只被 <c>BattleLogRecorder</c> 写、被面板读。
    /// 与 <c>Flow/Diagnostics</c> 那套 AI 排查用的五轨落盘日志互不相干。
    /// </summary>
    public static class BattleLogStore
    {
        /// <summary>整轮保留的房间段上限；超出丢最旧（一轮通关远小于此数）。</summary>
        private const int MaxSections = 64;

        /// <summary>单段条目上限，防病态循环效果撑爆内存。</summary>
        private const int MaxEntriesPerSection = 600;

        private static readonly List<BattleLogSection> sSections = new List<BattleLogSection>(16);
        private static int sNextSectionId = 1;

        /// <summary>内容有变（新段 / 新行 / 清空）。面板据此重绘。</summary>
        public static event Action Changed;

        public static IReadOnlyList<BattleLogSection> Sections => sSections;

        public static BattleLogSection Current => sSections.Count > 0 ? sSections[sSections.Count - 1] : null;

        public static void ResetAll()
        {
            if (sSections.Count == 0)
            {
                return;
            }

            sSections.Clear();
            sNextSectionId = 1;
            RaiseChanged();
        }

        /// <summary>开新房间段。标题重复（同一房间被重复通知）时只改标题，不新开段。</summary>
        public static BattleLogSection BeginSection(string title)
        {
            var current = Current;
            if (current != null && current.Entries.Count == 0)
            {
                current.Retitle(title);
                RaiseChanged();
                return current;
            }

            var section = new BattleLogSection(sNextSectionId++, title);
            sSections.Add(section);
            if (sSections.Count > MaxSections)
            {
                sSections.RemoveRange(0, sSections.Count - MaxSections);
            }

            RaiseChanged();
            return section;
        }

        public static void Append(BattleLogEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var section = Current ?? BeginSection(string.Empty);
            section.Add(entry);
            if (section.Entries.Count > MaxEntriesPerSection)
            {
                section.TrimOldest(section.Entries.Count - MaxEntriesPerSection);
            }

            RaiseChanged();
        }

        /// <summary>批量写入期间攒一次通知，避免一帧内几十次重绘。</summary>
        public static void AppendRange(List<BattleLogEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return;
            }

            var section = Current ?? BeginSectionSilent(string.Empty);
            for (var i = 0; i < entries.Count; i++)
            {
                section.Add(entries[i]);
            }

            if (section.Entries.Count > MaxEntriesPerSection)
            {
                section.TrimOldest(section.Entries.Count - MaxEntriesPerSection);
            }

            RaiseChanged();
        }

        private static BattleLogSection BeginSectionSilent(string title)
        {
            var section = new BattleLogSection(sNextSectionId++, title);
            sSections.Add(section);
            return section;
        }

        private static void RaiseChanged()
        {
            var handler = Changed;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler();
            }
            catch
            {
                // 面板异常不得反噬记录路径。
            }
        }
    }
}
