using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Adaptors
{
    /// <summary>
    /// 批内去重：旋转/击杀/拾取等多发事件时，适配器只演一次。
    /// </summary>
    public sealed class BoardBatchPlan
    {
        private readonly HashSet<int> mRotationActionIds = new();
        private readonly HashSet<int> mSwapActionIds = new();
        private readonly HashSet<long> mKillTargets = new();
        private readonly HashSet<long> mPickupCards = new();

        public static BoardBatchPlan Build(IReadOnlyList<PresentationInstruction> instructions)
        {
            var plan = new BoardBatchPlan();
            if (instructions == null)
            {
                return plan;
            }

            for (var i = 0; i < instructions.Count; i++)
            {
                PresentationInstruction instruction = instructions[i];
                CoreGameEvent evt = instruction.Event;
                if (evt == null)
                {
                    continue;
                }

                switch (instruction.Kind)
                {
                    case PresentationInstructionKind.RotateBoard:
                        plan.mRotationActionIds.Add(evt.ActionId);
                        break;
                    case PresentationInstructionKind.SwapCards:
                        plan.mSwapActionIds.Add(evt.ActionId);
                        break;
                    case PresentationInstructionKind.KillCard:
                        plan.mKillTargets.Add(Pack(evt.ActionId, evt.TargetUid));
                        break;
                    case PresentationInstructionKind.PickItem:
                        plan.mPickupCards.Add(Pack(evt.ActionId, evt.CardUid));
                        break;
                }
            }

            return plan;
        }

        public bool ShouldSkipMove(CoreGameEvent evt)
        {
            return evt != null
                && (mRotationActionIds.Contains(evt.ActionId) || mSwapActionIds.Contains(evt.ActionId));
        }

        public bool ShouldSkipDamage(CoreGameEvent evt)
        {
            return evt != null && mKillTargets.Contains(Pack(evt.ActionId, evt.TargetUid));
        }

        public bool ShouldSkipRemove(CoreGameEvent evt)
        {
            if (evt == null)
            {
                return false;
            }

            if (mKillTargets.Contains(Pack(evt.ActionId, evt.CardUid)))
            {
                return true;
            }

            return mPickupCards.Contains(Pack(evt.ActionId, evt.CardUid));
        }

        private static long Pack(int actionId, int uid)
        {
            return ((long)actionId << 32) | (uint)uid;
        }
    }
}
