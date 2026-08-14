using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 开局等非锁步路径：从事件日志重放生成类与开局批内卡面数值指令（与 Handler 同一出口）。
    /// 生成/翻面仍限 Settled；Impact 数值（如 OnNodeStart GainArmor）按日志顺序一并重放，避免只套 AvatarAppeared 钉回旧甲。
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

            var statHandler = new CardFaceStatHandler();
            var faceFlipHandler = new CardFaceFlipBeatHandler();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                var isGeneration = IsGenerationFaceEvent(entry);
                var isOpeningStat = IsOpeningBootstrapStatEvent(entry);
                if (!isGeneration && !isOpeningStat)
                {
                    continue;
                }

                var map = PresentationEventMap.Get(entry.Type);
                if (map.Beat == PresentationBeat.None)
                {
                    continue;
                }

                // 生成/翻面仍走 Settled；Impact 甲血等数值指令不得被此处过滤（复合盔甲等 OnNodeStart GainArmor）。
                if (isGeneration && map.Beat != PresentationBeat.Settled)
                {
                    continue;
                }

                var instruction = new PresentationInstruction(entry, map);
                if (instruction.Kind == PresentationInstructionKind.UpdateFaceUp)
                {
                    // 开局批内翻面（刺客领袖等 OnDeal）：发牌表演收束后再重放，走 CardFaceFlipBeatHandler
                    // 提交镜像并交 FlipPlaybackCoordinator 串行播翻（ADR-0016 时序门控）。
                    faceFlipHandler.TryApply(instruction);
                }
                else
                {
                    statHandler.Apply(instruction);
                }
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
                || entry.Type == CoreEventType.EffectCountdownChanged
                || entry.Type == CoreEventType.EffectCountdownCleared
                || entry.Type == CoreEventType.CardFaceChanged;
        }

        /// <summary>
        /// 开局切片内除生成类外的卡面数值事件（含 Impact 的 ArmorChanged 等）。
        /// 与 <see cref="ApplyFaceHistoryForUid"/> 的数值集合对齐，供 Opening 按序重放。
        /// </summary>
        internal static bool IsOpeningBootstrapStatEvent(CoreGameEvent entry)
        {
            if (entry == null || (entry.CardUid <= 0 && entry.TargetUid <= 0))
            {
                return false;
            }

            return entry.Type == CoreEventType.HpChanged
                || entry.Type == CoreEventType.Healed
                || entry.Type == CoreEventType.ArmorChanged
                || entry.Type == CoreEventType.BaseStatModified
                || entry.Type == CoreEventType.CardKilled;
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
                || type == CoreEventType.EffectCountdownChanged
                || type == CoreEventType.EffectCountdownCleared;
        }

        private static bool BelongsToUid(CoreGameEvent entry, int uid)
        {
            return entry != null
                && (entry.CardUid == uid || entry.TargetUid == uid);
        }
    }
}
