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
        private bool mHasBufferedIntent;
        private InputIntent mBufferedIntent;
        private bool mExternalHoldReleased = true;
        private int mExternalHoldNestDepth;

        public PresentationDirector(
            IIntentScriptFactory scriptFactory,
            IUiPickPreviewSink uiPickPreview = null,
            ITimelineDiagnosticSink timelineDiagnostics = null)
        {
            if (scriptFactory == null)
            {
                throw new ArgumentNullException("scriptFactory");
            }

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
            get { return mHasBufferedIntent; }
        }

        public InputIntent BufferedIntent
        {
            get { return mBufferedIntent; }
        }

        /// <summary>
        /// 提交输入意图。忙时缓冲最早一条并给出 uiPick 预告；已有缓冲则拒绝后来者。
        /// </summary>
        public bool TrySubmitIntent(InputIntent intent, out bool uiPickPreview)
        {
            uiPickPreview = false;

            if (!IsMainlineBusy)
            {
                DirectorTrace.BeginChain();
                mScriptFactory.BuildScript(intent, mMainline);
                DirectorTrace.IntentAccepted(intent.Kind, intent.TargetId);
                PublishBusy();
                return true;
            }

            if (mHasBufferedIntent)
            {
                DirectorTrace.IntentRejected(intent.Kind, intent.TargetId);
                return false;
            }

            mBufferedIntent = intent;
            mHasBufferedIntent = true;
            uiPickPreview = true;
            if (mUiPickPreview != null)
            {
                mUiPickPreview.Preview(intent);
            }

            DirectorTrace.IntentBuffered(intent.Kind, intent.TargetId, uiPick: true);
            PublishBusy();
            return true;
        }

        public IntentClearReason? LastClearReason { get; private set; }

        public void HardClearIntents(IntentClearReason reason)
        {
            LastClearReason = reason;
            mHasBufferedIntent = false;
            mBufferedIntent = default(InputIntent);
            // Phase / 战败 / 换层使当前剧本上下文失效，一并中止主线与旁路。
            mMainline.Clear();
            mBypass.Clear();
            mExternalHoldReleased = true;
            mExternalHoldNestDepth = 0;
            // IntentHardClear 内 ClearChain；此处再 PublishBusy。
            DirectorTrace.IntentHardClear(reason.ToString());
            PublishBusy();
        }

        public void EnqueueMainline(ITimelineStep step)
        {
            mMainline.Enqueue(step);
            PublishBusy();
        }

        /// <summary>向主线追加多步（如 FusionRefill Resolve+Present）。</summary>
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
        /// 外部薄适配挂主线租约：idle 时入队 Hold；主线已忙则嵌套计数（由现有 Present 持忙）。
        /// </summary>
        public bool TryBeginExternalHold(string reason = null)
        {
            if (!mExternalHoldReleased && mExternalHoldNestDepth == 0)
            {
                return false;
            }

            if (IsMainlineBusy)
            {
                mExternalHoldNestDepth++;
                return true;
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

            if (!IsMainlineBusy && mHasBufferedIntent)
            {
                var intent = mBufferedIntent;
                mHasBufferedIntent = false;
                mBufferedIntent = default(InputIntent);
                DirectorTrace.BeginChain();
                DirectorTrace.IntentFlush(intent.Kind, intent.TargetId);
                mScriptFactory.BuildScript(intent, mMainline);
            }
            else if (!IsMainlineBusy && !mHasBufferedIntent)
            {
                // defer 补牌等兄弟 batch 仍在同一脚本内；仅整条主线跑空且无缓冲时清连锁根。
                DirectorTrace.ClearChain();
            }

            PublishBusy();
        }

        private void PublishBusy()
        {
            DirectorTrace.PublishBusyState(
                IsMainlineBusy,
                IsBypassBusy,
                mHasBufferedIntent,
                mHasBufferedIntent ? mBufferedIntent.Kind : string.Empty);
        }
    }
}
