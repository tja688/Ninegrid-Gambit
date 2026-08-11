using System;
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
        private readonly List<PresentationInstruction> mQuarantined = new List<PresentationInstruction>(4);
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
            DiagnoseDiscardedPending();
            // 隔离区只属于当批：开新批即失效丢弃。
            mQuarantined.Clear();
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
        /// 冲刷 Impact 但跳过指定 Kind（ADR-0018）：例如 Vacate 前先认领飘字/血甲，
        /// 把 TriggerEffect 留给盘面运动落地后的 Drain Impact。
        /// </summary>
        public void FlushImpactExcept(PresentationInstructionKind excludedKind)
        {
            for (var i = 0; i < mPending.Count;)
            {
                var instruction = mPending[i];
                if (instruction == null
                    || instruction.MapEntry == null
                    || instruction.MapEntry.Beat != PresentationBeat.Impact)
                {
                    i++;
                    continue;
                }

                if (instruction.Kind == excludedKind)
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
        /// 从当批暂挂移出满足谓词的 Impact 指令进隔离区（神圣决斗：惩罚伤害与持有者脉冲
        /// 留给决斗者攻击表演的命中帧，而不是玩家攻击命中帧就掉血）。
        /// </summary>
        public void QuarantineImpactWhere(Func<PresentationInstruction, bool> predicate)
        {
            if (predicate == null)
            {
                return;
            }

            for (var i = mPending.Count - 1; i >= 0; i--)
            {
                var instruction = mPending[i];
                if (instruction == null
                    || instruction.MapEntry == null
                    || instruction.MapEntry.Beat != PresentationBeat.Impact
                    || !predicate(instruction))
                {
                    continue;
                }

                mPending.RemoveAt(i);
                mQuarantined.Add(instruction);
            }
        }

        /// <summary>
        /// 把隔离区指令放回当批暂挂（决斗者攻击命中帧报点前调用；随后 ReportBeat(Impact) 消费）。
        /// 未放回就开新批会被 OnBatchOpened 丢弃（不该发生；由调用方兜底释放）。
        /// </summary>
        public void ReleaseQuarantined()
        {
            if (mQuarantined.Count == 0)
            {
                return;
            }

            mPending.AddRange(mQuarantined);
            mQuarantined.Clear();
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
                // 所有权先挪到 savedPending，避免 OnBatchOpened 误报「开批丢弃未消费」。
                mPending.Clear();
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

        /// <summary>
        /// 开批时上一批仍有未消费指令：说明该批从未 FlushBeats / Present（非 Settled 未消费）。
        /// 只报不改，避免掩盖漏 Present。
        /// </summary>
        private void DiagnoseDiscardedPending()
        {
            if (mPending.Count == 0)
            {
                return;
            }

            for (var i = 0; i < mPending.Count; i++)
            {
                var instruction = mPending[i];
                var type = instruction.Event != null ? instruction.Event.Type.ToString() : "?";
                var beat = instruction.MapEntry != null ? instruction.MapEntry.Beat.ToString() : "?";
                Debug.LogError(
                    "[BattleBeatScheduler] Discarding unconsumed presentation instruction on new batch"
                    + " previousBatchId=" + mActiveBatchId
                    + " type=" + type
                    + " beat=" + beat
                    + " seq=" + instruction.Sequence
                    + " kind=" + instruction.Kind);
            }
        }
    }
}
