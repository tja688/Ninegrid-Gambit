using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 开局等非锁步路径：从事件日志重放生成类指令到卡面数值处理器（与 Settled 同一出口）。
    /// </summary>
    public static class CardFaceGenerationBootstrap
    {
        public static void ApplyFromEventLog(IArchitecture architecture, int startIndex)
        {
            if (architecture == null || startIndex < 0)
            {
                return;
            }

            var entries = architecture.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            if (entries == null || startIndex >= entries.Count)
            {
                return;
            }

            var handler = new CardFaceStatHandler();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.CardUid <= 0
                    && entry.TargetUid <= 0)
                {
                    continue;
                }

                if (entry.Type != CoreEventType.CardSpawned
                    && entry.Type != CoreEventType.CardDealt
                    && entry.Type != CoreEventType.AvatarAppeared)
                {
                    continue;
                }

                var map = PresentationEventMap.Get(entry.Type);
                if (map.Beat != PresentationBeat.Settled)
                {
                    continue;
                }

                handler.Apply(new PresentationInstruction(entry, map));
            }
        }

        public static void ApplyLatestForUid(IArchitecture architecture, int uid)
        {
            if (architecture == null || uid <= 0)
            {
                return;
            }

            var entries = architecture.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            if (entries == null)
            {
                return;
            }

            CoreGameEvent latest = null;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type != CoreEventType.CardSpawned
                    && entry.Type != CoreEventType.CardDealt
                    && entry.Type != CoreEventType.AvatarAppeared)
                {
                    continue;
                }

                var eventUid = entry.CardUid > 0 ? entry.CardUid : entry.TargetUid;
                if (eventUid != uid)
                {
                    continue;
                }

                latest = entry;
            }

            if (latest == null)
            {
                return;
            }

            var map = PresentationEventMap.Get(latest.Type);
            new CardFaceStatHandler().Apply(new PresentationInstruction(latest, map));
        }
    }
}
