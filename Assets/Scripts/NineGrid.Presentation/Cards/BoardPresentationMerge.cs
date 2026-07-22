using System;
using System.Collections.Generic;

namespace NineGrid.Cards
{
    /// <summary>
    /// 将致死命中 delta 与 PostKill 盘面步骤合并为单一有序 Drain 批次。
    /// 交战主目标尸体由 FieldBattle Vacate 路径处理，不得进入 Remove/RemovedUids。
    /// </summary>
    public static class BoardPresentationMerge
    {
        public static PostKillBoardPresentationResult MergeLethalHitAndPostKill(
            CombatHitPresentationResult hit,
            PostKillBoardPresentationResult postKill,
            int combatVictimUid)
        {
            var merged = new PostKillBoardPresentationResult
            {
                Accepted = hit.Accepted || postKill.Accepted,
                AvatarDefeated = hit.AvatarDefeated || postKill.AvatarDefeated,
                NodeClearedOrRewardPhase =
                    hit.NodeClearedOrRewardPhase || postKill.NodeClearedOrRewardPhase,
                DamagePopups = MergePopups(hit.DamagePopups, postKill.DamagePopups),
            };

            var postKillHasSteps = postKill.Steps != null && postKill.Steps.Length > 0;
            if (hit.HasOrderedSteps || postKillHasSteps)
            {
                var steps = new List<BoardPresentationStep>(8);
                if (hit.HasOrderedSteps)
                {
                    AppendFilteredSteps(steps, hit.Steps, combatVictimUid);
                }

                if (postKillHasSteps)
                {
                    AppendFilteredSteps(steps, postKill.Steps, combatVictimUid);
                }

                merged.Steps = steps.Count > 0 ? steps.ToArray() : Array.Empty<BoardPresentationStep>();
                var removed = MergeRemovedUids(hit.RemovedUids, postKill.RemovedUids, combatVictimUid);
                if (merged.Steps.Length > 0)
                {
                    removed = MergeRemovedUids(
                        removed,
                        CollectRemovedUidsExcludingVictim(merged.Steps, combatVictimUid),
                        combatVictimUid);
                }

                merged.RemovedUids = removed;
            }
            else
            {
                merged.Moves = MergeArrays(hit.Moves, postKill.Moves);
                merged.Deals = MergeArrays(hit.Deals, postKill.Deals);
                merged.RemovedUids = MergeRemovedUids(
                    hit.RemovedUids,
                    postKill.RemovedUids,
                    combatVictimUid);
            }

            return merged;
        }

        private static void AppendFilteredSteps(
            List<BoardPresentationStep> target,
            BoardPresentationStep[] source,
            int combatVictimUid)
        {
            if (source == null || source.Length == 0)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var step = source[i];
                if (step.Kind == BoardPresentationStepKind.Remove)
                {
                    var filtered = FilterRemovedUids(step.RemovedUids, combatVictimUid);
                    if (filtered == null || filtered.Length == 0)
                    {
                        continue;
                    }

                    step.RemovedUids = filtered;
                }

                target.Add(step);
            }
        }

        private static int[] MergeRemovedUids(int[] hitRemoved, int[] postKillRemoved, int combatVictimUid)
        {
            var merged = new List<int>(4);
            AppendRemovedUids(merged, hitRemoved, combatVictimUid);
            AppendRemovedUids(merged, postKillRemoved, combatVictimUid);
            return merged.Count > 0 ? merged.ToArray() : Array.Empty<int>();
        }

        private static void AppendRemovedUids(List<int> target, int[] source, int combatVictimUid)
        {
            if (source == null)
            {
                return;
            }

            for (var i = 0; i < source.Length; i++)
            {
                var uid = source[i];
                if (uid <= 0 || uid == combatVictimUid || target.Contains(uid))
                {
                    continue;
                }

                target.Add(uid);
            }
        }

        private static int[] FilterRemovedUids(int[] source, int combatVictimUid)
        {
            if (source == null || source.Length == 0)
            {
                return Array.Empty<int>();
            }

            var filtered = new List<int>(source.Length);
            for (var i = 0; i < source.Length; i++)
            {
                var uid = source[i];
                if (uid > 0 && uid != combatVictimUid)
                {
                    filtered.Add(uid);
                }
            }

            return filtered.Count > 0 ? filtered.ToArray() : Array.Empty<int>();
        }

        private static int[] CollectRemovedUidsExcludingVictim(
            BoardPresentationStep[] steps,
            int combatVictimUid)
        {
            if (steps == null || steps.Length == 0)
            {
                return Array.Empty<int>();
            }

            var merged = new List<int>(4);
            for (var i = 0; i < steps.Length; i++)
            {
                AppendRemovedUids(merged, steps[i].RemovedUids, combatVictimUid);
            }

            return merged.Count > 0 ? merged.ToArray() : Array.Empty<int>();
        }

        private static CombatDamagePopup[] MergePopups(
            CombatDamagePopup[] hitPopups,
            CombatDamagePopup[] postKillPopups)
        {
            if ((hitPopups == null || hitPopups.Length == 0)
                && (postKillPopups == null || postKillPopups.Length == 0))
            {
                return Array.Empty<CombatDamagePopup>();
            }

            var merged = new List<CombatDamagePopup>(4);
            if (hitPopups != null)
            {
                merged.AddRange(hitPopups);
            }

            if (postKillPopups != null)
            {
                merged.AddRange(postKillPopups);
            }

            return merged.ToArray();
        }

        private static PostKillCardMove[] MergeArrays(
            PostKillCardMove[] first,
            PostKillCardMove[] second)
        {
            if ((first == null || first.Length == 0) && (second == null || second.Length == 0))
            {
                return Array.Empty<PostKillCardMove>();
            }

            if (first == null || first.Length == 0)
            {
                return second;
            }

            if (second == null || second.Length == 0)
            {
                return first;
            }

            var merged = new PostKillCardMove[first.Length + second.Length];
            Array.Copy(first, 0, merged, 0, first.Length);
            Array.Copy(second, 0, merged, first.Length, second.Length);
            return merged;
        }

        private static PostKillCardDeal[] MergeArrays(PostKillCardDeal[] first, PostKillCardDeal[] second)
        {
            if ((first == null || first.Length == 0) && (second == null || second.Length == 0))
            {
                return Array.Empty<PostKillCardDeal>();
            }

            if (first == null || first.Length == 0)
            {
                return second;
            }

            if (second == null || second.Length == 0)
            {
                return first;
            }

            var merged = new PostKillCardDeal[first.Length + second.Length];
            Array.Copy(first, 0, merged, 0, first.Length);
            Array.Copy(second, 0, merged, first.Length, second.Length);
            return merged;
        }
    }
}
