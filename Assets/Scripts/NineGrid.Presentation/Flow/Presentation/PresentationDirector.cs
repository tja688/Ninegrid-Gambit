using System;
using NineGrid.Flow.Diagnostics;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 表演导演：时间线唯一所有者与出口；主线在跑是唯一输入互斥真相。
    /// 诊断连锁根 <see cref="DirectorTrace.CurrentChainId"/> 绑一次 InputIntent 脚本生命周期。
    /// </summary>
    public sealed class PresentationDirector
    {
        private readonly BattleTimeline mMainline;
        private readonly BattleTimeline mBypass;
        private readonly IIntentScriptFactory mScriptFactory;
        private readonly IUiPickPreviewSink mUiPickPreview;
        private bool mExternalHoldReleased = true;
        private int mExternalHoldNestDepth;

        public PresentationDirector(
            IIntentScriptFactory scriptFactory,
            IUiPickPreviewSink uiPickPreview = null,
            ITimelineDiagnosticSink timelineDiagnostics = null,
            IBufferedIntentLegality bufferedIntentLegality = null)
        {
            if (scriptFactory == null)
            {
                throw new ArgumentNullException("scriptFactory");
            }

            // bufferedIntentLegality 已随 ADR-0004 strict-drop 退役；保留参数以兼容装配签名。
            _ = bufferedIntentLegality;

            mScriptFactory = scriptFactory;
            mUiPickPreview = uiPickPreview;
            var diag = timelineDiagnostics ?? DirectorTrace.TimelineSink;
            mMainline = new BattleTimeline(diag, DirectorTimelineLane.Mainline);
            mBypass = new BattleTimeline(diag, DirectorTimelineLane.Bypass);
        }

        /// <summary>主线在跑 = 唯一 busy 真相（旁路装饰道不计入）。</summary>
        public bool IsMainlineBusy
        {
            get { return mMainline.IsBusy; }
        }

        /// <summary>外部薄适配租约仍持有（含嵌套未清）。</summary>
        public bool HasExternalHold
        {
            get { return !mExternalHoldReleased || mExternalHoldNestDepth > 0; }
        }

        public bool IsBypassBusy
        {
            get { return mBypass.IsBusy; }
        }

        public bool HasBufferedIntent
        {
            get { return false; }
        }

        /// <summary>
        /// 提交输入意图。主线忙时拒收（ADR-0004 strict-drop，不缓冲）。
        /// </summary>
        public bool TrySubmitIntent(InputIntent intent, out bool uiPickPreview)
        {
            uiPickPreview = false;

            if (!IsMainlineBusy)
            {
                DirectorTrace.BeginChain();
                TriggerPulseChainDedup.Reset();
                mScriptFactory.BuildScript(intent, mMainline);
                DirectorTrace.IntentAccepted(intent.Kind, intent.TargetId);
                PublishBusy();
                return true;
            }

            DirectorTrace.IntentRejected(intent.Kind, intent.TargetId, "mainlineBusy");
            PublishBusy();
            return false;
        }

        public IntentClearReason? LastClearReason { get; private set; }

        public void HardClearIntents(IntentClearReason reason)
        {
            LastClearReason = reason;
            // Phase / 战败 / 换层使当前剧本上下文失效，一并中止主线与旁路。
            mMainline.Clear();
            mBypass.Clear();
            mExternalHoldReleased = true;
            mExternalHoldNestDepth = 0;
            TriggerPulseChainDedup.Reset();
            // IntentHardClear 内 ClearChain；此处再 PublishBusy。
            DirectorTrace.IntentHardClear(reason.ToString());
            PublishBusy();
        }

        public void EnqueueMainline(ITimelineStep step)
        {
            mMainline.Enqueue(step);
            PublishBusy();
        }

        /// <summary>向主线追加多步（如 BoardStabilization Resolve+Present）。</summary>
        public void MutateMainline(Action<BattleTimeline> mutate)
        {
            if (mutate == null)
            {
                throw new ArgumentNullException("mutate");
            }

            mutate(mMainline);
            PublishBusy();
        }

        /// <summary>
        /// 外部薄适配挂主线租约：未持有时一律入队 Hold step（主线已有其它 Step 时接在后面），
        /// 保证 Apply/Present step 结束后表演窗口仍保持 <see cref="IsMainlineBusy"/>。
        /// 已持有时拒绝重入（嵌套由 <see cref="PresentationMainlineHold"/> 在已有 Hold 上跳过 Begin）。
        /// </summary>
        public bool TryBeginExternalHold(string reason = null)
        {
            if (!mExternalHoldReleased)
            {
                return false;
            }

            mExternalHoldReleased = false;
            mMainline.Enqueue(new ExternalMainlineHoldStep(() => mExternalHoldReleased));
            PublishBusy();
            return true;
        }

        /// <summary>释放 <see cref="TryBeginExternalHold"/> 租约。</summary>
        public void EndExternalHold(string reason = null)
        {
            if (mExternalHoldNestDepth > 0)
            {
                mExternalHoldNestDepth--;
                return;
            }

            mExternalHoldReleased = true;
            PublishBusy();
        }

        /// <summary>清场：强制结束外部租约（含嵌套）。</summary>
        public void ForceEndExternalHold(string reason = null)
        {
            mExternalHoldReleased = true;
            mExternalHoldNestDepth = 0;
            PublishBusy();
        }

        /// <summary>旁路装饰道：与主线并行，不占输入互斥。</summary>
        public void StartBypass(ITimelineStep step)
        {
            var stepName = step != null ? step.GetType().Name : string.Empty;
            mBypass.Enqueue(step);
            DirectorTrace.BypassStart(stepName);
            PublishBusy();
        }

        public void Tick(float deltaTime)
        {
            mMainline.Tick(deltaTime);
            mBypass.Tick(deltaTime);

            if (!IsMainlineBusy)
            {
                // defer 补牌等兄弟 batch 仍在同一脚本内；仅整条主线跑空时清连锁根。
                DirectorTrace.ClearChain();
                // 触发脉冲链级去重窗口与连锁根同生命周期（ADR-0048）。
                TriggerPulseChainDedup.Reset();
            }

            PublishBusy();
        }

        private void PublishBusy()
        {
            DirectorTrace.PublishBusyState(
                IsMainlineBusy,
                IsBypassBusy,
                hasBufferedIntent: false,
                bufferedIntentKind: string.Empty);
        }
    }
}
