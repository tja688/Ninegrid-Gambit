using System;
using NineGrid.Flow.Diagnostics;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 琛ㄦ紨瀵兼紨锛氭椂闂寸嚎鍞竴鎵€鏈夎€呬笌鍑哄彛锛涗富绾垮湪璺戞槸鍞竴杈撳叆浜掓枼鐪熺浉銆?    /// 璇婃柇杩為攣鏍?<see cref="DirectorTrace.CurrentChainId"/> 缁戜竴娆?InputIntent 鑴氭湰鐢熷懡鍛ㄦ湡銆?    /// </summary>
    public sealed class PresentationDirector
    {
        private readonly BattleTimeline mMainline;
        private readonly BattleTimeline mBypass;
        private readonly IIntentScriptFactory mScriptFactory;
        private readonly IUiPickPreviewSink mUiPickPreview;
        private readonly IBufferedIntentLegality mBufferedIntentLegality;
        private bool mHasBufferedIntent;
        private InputIntent mBufferedIntent;
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

            mScriptFactory = scriptFactory;
            mUiPickPreview = uiPickPreview;
            mBufferedIntentLegality = bufferedIntentLegality;
            var diag = timelineDiagnostics ?? DirectorTrace.TimelineSink;
            mMainline = new BattleTimeline(diag, DirectorTimelineLane.Mainline);
            mBypass = new BattleTimeline(diag, DirectorTimelineLane.Bypass);
        }

        /// <summary>涓荤嚎鍦ㄨ窇 = 鍞竴 busy 鐪熺浉锛堟梺璺楗伴亾涓嶈鍏ワ級銆?/summary>
        public bool IsMainlineBusy
        {
            get { return mMainline.IsBusy; }
        }

        /// <summary>澶栭儴钖勯€傞厤绉熺害浠嶆寔鏈夛紙鍚祵濂楁湭娓咃級銆?/summary>
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
        /// 鎻愪氦杈撳叆鎰忓浘銆傚繖鏃剁紦鍐叉渶鏃╀竴鏉″苟缁欏嚭 uiPick 棰勫憡锛涘凡鏈夌紦鍐插垯鎷掔粷鍚庢潵鑰呫€?        /// </summary>
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
            // Phase / 鎴樿触 / 鎹㈠眰浣垮綋鍓嶅墽鏈笂涓嬫枃澶辨晥锛屼竴骞朵腑姝富绾夸笌鏃佽矾銆?            mMainline.Clear();
            mBypass.Clear();
            mExternalHoldReleased = true;
            mExternalHoldNestDepth = 0;
            // IntentHardClear 鍐?ClearChain锛涙澶勫啀 PublishBusy銆?            DirectorTrace.IntentHardClear(reason.ToString());
            PublishBusy();
        }

        public void EnqueueMainline(ITimelineStep step)
        {
            mMainline.Enqueue(step);
            PublishBusy();
        }

        /// <summary>鍚戜富绾胯拷鍔犲姝ワ紙濡?FusionRefill Resolve+Present锛夈€?/summary>
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
        /// 澶栭儴钖勯€傞厤鎸備富绾跨绾︼細idle 鏃跺叆闃?Hold锛涗富绾垮凡蹇欏垯宓屽璁℃暟锛堢敱鐜版湁 Present 鎸佸繖锛夈€?        /// </summary>
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

        /// <summary>閲婃斁 <see cref="TryBeginExternalHold"/> 绉熺害銆?/summary>
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

        /// <summary>娓呭満锛氬己鍒剁粨鏉熷閮ㄧ绾︼紙鍚祵濂楋級銆?/summary>
        public void ForceEndExternalHold(string reason = null)
        {
            mExternalHoldReleased = true;
            mExternalHoldNestDepth = 0;
            PublishBusy();
        }

        /// <summary>鏃佽矾瑁呴グ閬擄細涓庝富绾垮苟琛岋紝涓嶅崰杈撳叆浜掓枼銆?/summary>
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

                // flush 前复用提交时 Core 合法性；非法则安静丢弃、不建脚本。
                var stillLegal = mBufferedIntentLegality == null
                    || mBufferedIntentLegality.IsStillLegal(intent);
                if (stillLegal)
                {
                    DirectorTrace.BeginChain();
                    DirectorTrace.IntentFlush(intent.Kind, intent.TargetId);
                    mScriptFactory.BuildScript(intent, mMainline);
                }
                else
                {
                    DirectorTrace.ClearChain();
                }
            }
            else if (!IsMainlineBusy && !mHasBufferedIntent)
            {
                // defer 琛ョ墝绛夊厔寮?batch 浠嶅湪鍚屼竴鑴氭湰鍐咃紱浠呮暣鏉′富绾胯窇绌轰笖鏃犵紦鍐叉椂娓呰繛閿佹牴銆?                DirectorTrace.ClearChain();
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
