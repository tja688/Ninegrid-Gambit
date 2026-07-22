using System;
using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Flow
{
    public readonly struct SkeletonFusionPresentationEntry
    {
        public SkeletonFusionPresentationEntry(
            int actionId,
            string skillId,
            int triggerCardUid,
            int[] participantUids,
            int resultUid,
            string resultDefId)
        {
            ActionId = actionId;
            SkillId = skillId ?? string.Empty;
            TriggerCardUid = triggerCardUid;
            ParticipantUids = participantUids ?? Array.Empty<int>();
            ResultUid = resultUid;
            ResultDefId = resultDefId ?? string.Empty;
        }

        public int ActionId { get; }
        public string SkillId { get; }
        public int TriggerCardUid { get; }
        public int[] ParticipantUids { get; }
        public int ResultUid { get; }
        public string ResultDefId { get; }
    }

    /// <summary>
    /// 从 EventLog 扫描骷髅合体批次：重组头/身、强力组合等。
    /// </summary>
    public static class SkeletonFusionPresentationScanner
    {
        private const string RecombineHeadSkillId = "skill.recombine_head";
        private const string RecombineBodySkillId = "skill.recombine_body";
        private const string StrongComboSkillId = "skill.strong_combo";

        public static bool IsFusionSkillId(string skillId)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                return false;
            }

            return string.Equals(skillId, RecombineHeadSkillId, StringComparison.Ordinal)
                   || string.Equals(skillId, RecombineBodySkillId, StringComparison.Ordinal)
                   || string.Equals(skillId, StrongComboSkillId, StringComparison.Ordinal);
        }

        public static IReadOnlyList<SkeletonFusionPresentationEntry> Collect(
            IReadOnlyList<CoreGameEvent> entries,
            int startIndex)
        {
            if (entries == null || startIndex < 0 || startIndex >= entries.Count)
            {
                return Array.Empty<SkeletonFusionPresentationEntry>();
            }

            var results = new List<SkeletonFusionPresentationEntry>(2);
            var seenActionIds = new HashSet<int>();
            for (var i = startIndex; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Type != CoreEventType.EffectTriggered
                    || entry.CardUid <= 0
                    || !IsFusionSkillId(entry.SourceDefId)
                    || !seenActionIds.Add(entry.ActionId))
                {
                    continue;
                }

                if (!TryBuildFusionEntry(entries, i, entry, out var fusion))
                {
                    continue;
                }

                results.Add(fusion);
            }

            return results;
        }

        public static Dictionary<int, SkeletonFusionPresentationEntry> BuildParticipantIndex(
            IReadOnlyList<SkeletonFusionPresentationEntry> fusions)
        {
            var map = new Dictionary<int, SkeletonFusionPresentationEntry>();
            if (fusions == null)
            {
                return map;
            }

            for (var i = 0; i < fusions.Count; i++)
            {
                var fusion = fusions[i];
                var participants = fusion.ParticipantUids;
                for (var j = 0; j < participants.Length; j++)
                {
                    var uid = participants[j];
                    if (uid > 0)
                    {
                        map[uid] = fusion;
                    }
                }
            }

            return map;
        }

        private static bool TryBuildFusionEntry(
            IReadOnlyList<CoreGameEvent> entries,
            int triggerIndex,
            CoreGameEvent triggerEvent,
            out SkeletonFusionPresentationEntry fusion)
        {
            fusion = default;
            var removedUids = new List<int>(4);
            var resultUid = 0;
            var resultDefId = string.Empty;

            // ExecuteEffect 与 RemoveCard / ShuffleInto 各自拥有独立 ActionId，不能按 actionId 截断。
            for (var i = triggerIndex + 1; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (IsFusionTriggerEvent(entry))
                {
                    break;
                }

                if (entry.Type == CoreEventType.CardRemoved
                    || entry.Type == CoreEventType.CardKilled)
                {
                    if (entry.CardUid > 0 && !removedUids.Contains(entry.CardUid))
                    {
                        removedUids.Add(entry.CardUid);
                    }
                }

                if (ShuffleIntoDeckPresentationScanner.TryParseShuffleIntoEvent(
                        entry,
                        out _,
                        out var defId))
                {
                    resultUid = entry.CardUid;
                    resultDefId = defId;
                    // 融合批次在「≥2 移除 + 首条洗入结果」即闭合；勿吞掉后续 fall_apart 等无关 ShuffleInto。
                    if (removedUids.Count >= 2 && resultUid > 0 && !string.IsNullOrEmpty(resultDefId))
                    {
                        break;
                    }
                }
            }

            if (removedUids.Count < 2 || resultUid <= 0 || string.IsNullOrEmpty(resultDefId))
            {
                return false;
            }

            fusion = new SkeletonFusionPresentationEntry(
                triggerEvent.ActionId,
                triggerEvent.SourceDefId,
                triggerEvent.CardUid,
                removedUids.ToArray(),
                resultUid,
                resultDefId);
            return true;
        }

        private static bool IsFusionTriggerEvent(CoreGameEvent entry)
        {
            return entry != null
                   && entry.Type == CoreEventType.EffectTriggered
                   && entry.CardUid > 0
                   && IsFusionSkillId(entry.SourceDefId);
        }
    }
}
