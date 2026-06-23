using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 批内发牌编排：同 ActionId 的多张 <see cref="PresentationInstructionKind.DealCard"/> 合并为一次开局发牌。
    /// </summary>
    public sealed class DeckBatchPlan
    {
        public readonly struct DealEntry
        {
            public DealEntry(int cardUid, SlotId toSlot, int instructionIndex)
            {
                CardUid = cardUid;
                ToSlot = toSlot;
                InstructionIndex = instructionIndex;
            }

            public int CardUid { get; }
            public SlotId ToSlot { get; }
            public int InstructionIndex { get; }
        }

        private readonly Dictionary<int, List<DealEntry>> mDealGroups = new();
        private readonly HashSet<int> mBatchDealActionIds = new();
        private readonly Dictionary<int, int> mFirstDealInstructionIndex = new();

        public static DeckBatchPlan Build(IReadOnlyList<PresentationInstruction> instructions)
        {
            var plan = new DeckBatchPlan();
            if (instructions == null)
            {
                return plan;
            }

            for (var i = 0; i < instructions.Count; i++)
            {
                PresentationInstruction instruction = instructions[i];
                if (instruction == null || instruction.Kind != PresentationInstructionKind.DealCard)
                {
                    continue;
                }

                CoreGameEvent evt = instruction.Event;
                if (evt == null || evt.CardUid <= 0)
                {
                    continue;
                }

                if (!plan.mDealGroups.TryGetValue(evt.ActionId, out List<DealEntry> group))
                {
                    group = new List<DealEntry>();
                    plan.mDealGroups.Add(evt.ActionId, group);
                }

                group.Add(new DealEntry(evt.CardUid, evt.ToSlot, i));
            }

            foreach (KeyValuePair<int, List<DealEntry>> pair in plan.mDealGroups)
            {
                if (pair.Value.Count > 1)
                {
                    plan.mBatchDealActionIds.Add(pair.Key);
                    plan.mFirstDealInstructionIndex[pair.Key] = pair.Value[0].InstructionIndex;
                }
            }

            return plan;
        }

        public static bool IsOpeningDeal(CoreGameEvent evt)
        {
            return evt != null
                && evt.Type == CoreEventType.CardDealt
                && evt.CardUid <= 0
                && evt.Message == "opening";
        }

        public bool IsBatchDealAction(int actionId)
        {
            return mBatchDealActionIds.Contains(actionId);
        }

        public bool ShouldSkipDeal(CoreGameEvent evt, int instructionIndex)
        {
            if (evt == null || evt.CardUid <= 0)
            {
                return false;
            }

            if (!mBatchDealActionIds.Contains(evt.ActionId))
            {
                return false;
            }

            return mFirstDealInstructionIndex.TryGetValue(evt.ActionId, out int firstIndex)
                && instructionIndex != firstIndex;
        }

        public IReadOnlyList<DealEntry> GetDealGroup(int actionId)
        {
            return mDealGroups.TryGetValue(actionId, out List<DealEntry> group)
                ? group
                : null;
        }

        public void CollectDealtUidsInOrder(IReadOnlyList<PresentationInstruction> instructions, IList<int> results)
        {
            results.Clear();
            if (instructions == null)
            {
                return;
            }

            for (var i = 0; i < instructions.Count; i++)
            {
                PresentationInstruction instruction = instructions[i];
                if (instruction == null || instruction.Kind != PresentationInstructionKind.DealCard)
                {
                    continue;
                }

                CoreGameEvent evt = instruction.Event;
                if (evt == null || evt.CardUid <= 0)
                {
                    continue;
                }

                results.Add(evt.CardUid);
            }
        }
    }
}
