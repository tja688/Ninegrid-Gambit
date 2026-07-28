using System;
using System.Collections.Generic;
using NineGrid.Core;
using UnityEngine;
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

        /// <summary>
        /// 请 Core 解算下一批并 OpenBatch。
        /// WaitHasOpen=仍有未 ack 批次（可重试）；Failed=解算/开门终态失败（须中止剧本）。
        /// </summary>
        BatchOpenResult TryOpenNextBatch(out int batchId);

        /// <summary>表演就位回执：FinishBatch。batchId 不匹配则拒绝。</summary>
        bool TryAcknowledge(int batchId);
    }

    /// <summary>
    /// ResolveBatch：瞬时打开一批后 Finished；仅 WaitHasOpen 驻留 Continue；Failed 则 Aborted。
    /// </summary>
    public sealed class ResolveBatchStep : ITimelineStep
    {
        private readonly IPresentationBatchGate mGate;
        private int mOpenedBatchId;
        private bool mAborted;

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
            if (mAborted)
            {
                return TimelineStepStatus.Aborted;
            }

            if (mOpenedBatchId > 0)
            {
                return TimelineStepStatus.Finished;
            }

            int batchId;
            var open = mGate.TryOpenNextBatch(out batchId);
            if (open == BatchOpenResult.WaitHasOpen)
            {
                return TimelineStepStatus.Continue;
            }

            if (open == BatchOpenResult.Failed)
            {
                mAborted = true;
                DirectorTrace.ScriptAborted("batchOpenFailed");
                return TimelineStepStatus.Aborted;
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
        private readonly string mChannelName;
        private readonly string mChoreoKind;
        private int mBatchId;
        private bool mStarted;
        private bool mAcknowledged;
        private bool mChoreoOpen;
        private float mWaitStartRealtime = -1f;
        private float mLastStallRealtime = -1f;

        public PresentStep(
            IPresentationBatchGate gate,
            IPresentChannel channel,
            string channelName = null,
            string choreoKind = null)
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
            mChannelName = channelName ?? channel.GetType().Name;
            mChoreoKind = choreoKind;
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
                mWaitStartRealtime = Time.realtimeSinceStartup;
                mLastStallRealtime = -1f;
                DirectorTrace.PresentBegin(mBatchId, mChannelName);
                TryBeginChoreo();
            }

            mChannel.Tick(deltaTime);

            if (!mChannel.IsComplete)
            {
                MaybeStall(DirectorTrace.StallPhaseNotComplete);
                return TimelineStepStatus.Continue;
            }

            // 表演通道完成后、就位回执前：与非锁步路径同构的统一冲刷报点。
            BattleBeatFlush.FlushBeats();

            if (!mGate.TryAcknowledge(mBatchId))
            {
                DirectorTrace.PresentAckRejected(mBatchId);
                MaybeStall(DirectorTrace.StallPhaseNotAck);
                return TimelineStepStatus.Continue;
            }

            DirectorTrace.PresentAck(mBatchId);
            TryEndChoreo("ok");
            mAcknowledged = true;
            return TimelineStepStatus.Finished;
        }

        private void TryBeginChoreo()
        {
            if (string.IsNullOrEmpty(mChoreoKind) || mChoreoOpen)
            {
                return;
            }

            ChoreoTraceContext.BeginChoreo(
                mChoreoKind,
                new Dictionary<string, string>
                {
                    { "batchId", mBatchId.ToString() },
                    { "channel", mChannelName },
                });
            mChoreoOpen = true;
        }

        private void TryEndChoreo(string outcome)
        {
            if (!mChoreoOpen)
            {
                return;
            }

            ChoreoTraceContext.EndChoreo(outcome);
            mChoreoOpen = false;
        }

        private void MaybeStall(string phase)
        {
            if (mWaitStartRealtime < 0f)
            {
                return;
            }

            var now = Time.realtimeSinceStartup;
            var waitSec = now - mWaitStartRealtime;
            if (waitSec < DirectorTrace.PresentStallThresholdSec)
            {
                return;
            }

            if (mLastStallRealtime >= 0f
                && now - mLastStallRealtime < DirectorTrace.PresentStallRepeatSec)
            {
                return;
            }

            mLastStallRealtime = now;
            DirectorTrace.PresentStall(mBatchId, waitSec, phase);
        }
    }
}
