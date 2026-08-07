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
                if (!IsGenerationFaceEvent(entry))
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

        /// <summary>
        /// 手牌等视图重 Spawn：按事件日志顺序重放该 uid 的卡面数值指令，避免只套生成绝对值钉回战斗中变化。
        /// </summary>
        public static void ApplyFaceHistoryForUid(IArchitecture architecture, int uid)
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

            var handler = new CardFaceStatHandler();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (!BelongsToUid(entry, uid) || !IsCardFaceStatEvent(entry.Type))
                {
                    continue;
                }

                var map = PresentationEventMap.Get(entry.Type);
                if (map.Beat == PresentationBeat.None)
                {
                    continue;
                }

                handler.Apply(new PresentationInstruction(entry, map));
            }
        }

        private static bool IsGenerationFaceEvent(CoreGameEvent entry)
        {
            if (entry == null || (entry.CardUid <= 0 && entry.TargetUid <= 0))
            {
                return false;
            }

            return entry.Type == CoreEventType.CardSpawned
                || entry.Type == CoreEventType.CardDealt
                || entry.Type == CoreEventType.AvatarAppeared
                || entry.Type == CoreEventType.ActionCountdownChanged
                || entry.Type == CoreEventType.EffectCountdownChanged;
        }

        private static bool IsCardFaceStatEvent(CoreEventType type)
        {
            return type == CoreEventType.CardSpawned
                || type == CoreEventType.CardDealt
                || type == CoreEventType.AvatarAppeared
                || type == CoreEventType.HpChanged
                || type == CoreEventType.Healed
                || type == CoreEventType.ArmorChanged
                || type == CoreEventType.BaseStatModified
                || type == CoreEventType.CardKilled
                || type == CoreEventType.ActionCountdownChanged
                || type == CoreEventType.EffectCountdownChanged;
        }

        private static bool BelongsToUid(CoreGameEvent entry, int uid)
        {
            return entry != null
                && (entry.CardUid == uid || entry.TargetUid == uid);
        }
    }
}
