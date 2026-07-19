using System;
using System.Collections.Generic;
using NineGrid.Flow.Diagnostics;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 解算批次门：打开一批 / 就位回执关闭。未 ack 不得解算下一批。
    /// </summary>
    public interface IPresentationBatchGate
    {
        bool HasOpenBatch { get; }
        int ActiveBatchId { get; }

        /// <summary>请 Core 解算下一批并 OpenBatch。若仍有未 ack 批次则拒绝。</summary>
        bool TryOpenNextBatch(out int batchId);

        /// <summary>表演就位回执：FinishBatch。batchId 不匹配则拒绝。</summary>
        bool TryAcknowledge(int batchId);
    }

    /// <summary>
    /// ResolveBatch：瞬时打开一批后 Finished；被门拒绝则驻留 Continue（等待 ack 后重试）。
    /// </summary>
    public sealed class ResolveBatchStep : ITimelineStep
    {
        private readonly IPresentationBatchGate mGate;
        private int mOpenedBatchId;

        public ResolveBatchStep(IPresentationBatchGate gate)
        {
            if (gate == null)
            {
                throw new ArgumentNullException("gate");
            }

            mGate = gate;
        }

        public int OpenedBatchId
        {
            get { return mOpenedBatchId; }
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            if (mOpenedBatchId > 0)
            {
                return TimelineStepStatus.Finished;
            }

            int batchId;
            if (!mGate.TryOpenNextBatch(out batchId))
            {
                return TimelineStepStatus.Continue;
            }

            mOpenedBatchId = batchId;
            return TimelineStepStatus.Finished;
        }
    }

    /// <summary>
    /// 表演通道：开始播一批，按 Tick 推进，完成后可查询是否就位。
    /// </summary>
    public interface IPresentChannel
    {
        void Begin(int batchId);
        void Tick(float deltaTime);
        bool IsComplete { get; }
        int ActiveBatchId { get; }
    }

    /// <summary>
    /// Present：播当前已打开批次，完成后 ack 关闭。batchId 取自门上 ActiveBatchId。
    /// </summary>
    public sealed class PresentStep : ITimelineStep
    {
        private readonly IPresentationBatchGate mGate;
        private readonly IPresentChannel mChannel;
        private int mBatchId;
        private bool mStarted;
        private bool mAcknowledged;

        public PresentStep(IPresentationBatchGate gate, IPresentChannel channel)
        {
            if (gate == null)
            {
                throw new ArgumentNullException("gate");
            }

            if (channel == null)
            {
                throw new ArgumentNullException("channel");
            }

            mGate = gate;
            mChannel = channel;
        }

        public TimelineStepStatus Tick(float deltaTime)
        {
            if (mAcknowledged)
            {
                return TimelineStepStatus.Finished;
            }

            if (!mStarted)
            {
                if (!mGate.HasOpenBatch)
                {
                    return TimelineStepStatus.Continue;
                }

                mBatchId = mGate.ActiveBatchId;
                mChannel.Begin(mBatchId);
                mStarted = true;
            }

            mChannel.Tick(deltaTime);

            if (!mChannel.IsComplete)
            {
                return TimelineStepStatus.Continue;
            }

            if (!mGate.TryAcknowledge(mBatchId))
            {
                return TimelineStepStatus.Continue;
            }

            PerfTraceRecorder.Record(
                "DirectorPresentAck",
                mBatchId,
                "PresentStep",
                new Dictionary<string, string>
                {
                    ["batchId"] = mBatchId.ToString(),
                });
            mAcknowledged = true;
            return TimelineStepStatus.Finished;
        }
    }
}
