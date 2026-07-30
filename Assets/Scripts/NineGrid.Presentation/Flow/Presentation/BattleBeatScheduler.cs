using System.Collections.Generic;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 战斗锚点排期器：一批结算指令的唯一分发出口，按报点交给已注册的表演处理器。
    /// Settled 后仍有已装载未消费指令：只报不改（无强制对账）。
    /// </summary>
    public sealed class BattleBeatScheduler
    {
        private readonly IBattleBeatHandler[] mHandlers;
        private readonly List<PresentationInstruction> mPending = new List<PresentationInstruction>(16);
        private int mActiveBatchId;

        public BattleBeatScheduler(params IBattleBeatHandler[] handlers)
        {
            if (handlers == null || handlers.Length == 0)
            {
                mHandlers = new IBattleBeatHandler[] { new CardFaceStatHandler() };
            }
            else
            {
                mHandlers = handlers;
            }
        }

        public void OnBatchOpened(PresentationBatch batch)
        {
            mPending.Clear();
            mActiveBatchId = batch != null ? batch.BatchId : 0;
            if (batch?.Instructions == null)
            {
                return;
            }

            for (var i = 0; i < batch.Instructions.Count; i++)
            {
                var instruction = batch.Instructions[i];
                if (instruction?.MapEntry == null)
                {
                    continue;
                }

                if (instruction.MapEntry.Beat == PresentationBeat.None)
                {
                    continue;
                }

                mPending.Add(instruction);
            }
        }

        public void ReportBeat(PresentationBeat beat)
        {
            if (beat == PresentationBeat.None)
            {
                return;
            }

            for (var i = 0; i < mPending.Count;)
            {
                var instruction = mPending[i];
                if (instruction.MapEntry.Beat != beat)
                {
                    i++;
                    continue;
                }

                if (!TryDispatch(instruction))
                {
                    i++;
                    continue;
                }

                mPending.RemoveAt(i);
            }

            if (beat == PresentationBeat.Settled)
            {
                DiagnoseUnconsumed();
            }
        }

        /// <summary>
        /// 只消费 UpdateFaceUp：供 PresentStep 在 hop 通道 Begin 前先翻牌（ADR-0016）。
        /// 不报 Impact/Settled，也不做 Settled 未消费诊断。
        /// </summary>
        public void FlushUpdateFaceUp()
        {
            for (var i = 0; i < mPending.Count;)
            {
                var instruction = mPending[i];
                if (instruction == null || instruction.Kind != PresentationInstructionKind.UpdateFaceUp)
                {
                    i++;
                    continue;
                }

                if (!TryDispatch(instruction))
                {
                    i++;
                    continue;
                }

                mPending.RemoveAt(i);
            }
        }

        /// <summary>
        /// 非锁步旁路：临时装载一批并冲刷 Impact→Settled，恢复原先 pending（不搅乱锁步当批）。
        /// </summary>
        public void PresentStandalone(PresentationBatch batch)
        {
            var savedPending = new List<PresentationInstruction>(mPending);
            var savedBatchId = mActiveBatchId;
            try
            {
                OnBatchOpened(batch);
                ReportBeat(PresentationBeat.Impact);
                ReportBeat(PresentationBeat.Settled);
            }
            finally
            {
                mPending.Clear();
                mPending.AddRange(savedPending);
                mActiveBatchId = savedBatchId;
            }
        }

        private bool TryDispatch(PresentationInstruction instruction)
        {
            for (var h = 0; h < mHandlers.Length; h++)
            {
                var handler = mHandlers[h];
                if (handler != null && handler.TryApply(instruction))
                {
                    return true;
                }
            }

            return false;
        }

        private void DiagnoseUnconsumed()
        {
            if (mPending.Count == 0)
            {
                return;
            }

            for (var i = 0; i < mPending.Count; i++)
            {
                var instruction = mPending[i];
                var type = instruction.Event != null ? instruction.Event.Type.ToString() : "?";
                var message =
                    "[BattleBeatScheduler] Unconsumed presentation instruction after Settled"
                    + " batchId=" + mActiveBatchId
                    + " type=" + type
                    + " beat=" + instruction.MapEntry.Beat
                    + " seq=" + instruction.Sequence;
                Debug.LogError(message);
                Debug.Assert(false, message);
            }
        }
    }
}
