using System;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 表演导演：时间线唯一所有者与出口；主线在跑是唯一输入互斥真相。
    /// </summary>
    public sealed class PresentationDirector
    {
        private readonly BattleTimeline mMainline = new BattleTimeline();
        private readonly BattleTimeline mBypass = new BattleTimeline();
        private readonly IIntentScriptFactory mScriptFactory;
        private readonly IUiPickPreviewSink mUiPickPreview;
        private bool mHasBufferedIntent;
        private InputIntent mBufferedIntent;

        public PresentationDirector(
            IIntentScriptFactory scriptFactory,
            IUiPickPreviewSink uiPickPreview = null)
        {
            if (scriptFactory == null)
            {
                throw new ArgumentNullException("scriptFactory");
            }

            mScriptFactory = scriptFactory;
            mUiPickPreview = uiPickPreview;
        }

        /// <summary>主线在跑 = 唯一 busy 真相（旁路装饰道不计入）。</summary>
        public bool IsMainlineBusy
        {
            get { return mMainline.IsBusy; }
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
                mScriptFactory.BuildScript(intent, mMainline);
                return true;
            }

            if (mHasBufferedIntent)
            {
                return false;
            }

            mBufferedIntent = intent;
            mHasBufferedIntent = true;
            uiPickPreview = true;
            if (mUiPickPreview != null)
            {
                mUiPickPreview.Preview(intent);
            }

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
        }

        public void EnqueueMainline(ITimelineStep step)
        {
            mMainline.Enqueue(step);
        }

        /// <summary>旁路装饰道：与主线并行，不占输入互斥。</summary>
        public void StartBypass(ITimelineStep step)
        {
            mBypass.Enqueue(step);
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
                mScriptFactory.BuildScript(intent, mMainline);
            }
        }
    }
}
