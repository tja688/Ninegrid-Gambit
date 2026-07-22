using System;
using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    public sealed class PresentationRuntimeSystem : AbstractSystem, IPresentationRuntimeSystem
    {
        private readonly BindableProperty<bool> mMainlineBusy = new BindableProperty<bool>(false);
        private PresentationDirector mDirector;

        public bool IsStarted { get { return mDirector != null; } }
        public IReadonlyBindableProperty<bool> MainlineBusy { get { return mMainlineBusy; } }

        public void Start(IIntentScriptFactory scriptFactory, IUiPickPreviewSink uiPickPreview = null,
            ITimelineDiagnosticSink timelineDiagnostics = null)
        {
            if (scriptFactory == null) throw new ArgumentNullException("scriptFactory");
            if (mDirector != null) throw new InvalidOperationException("Presentation runtime is already started.");
            mDirector = new PresentationDirector(scriptFactory, uiPickPreview, timelineDiagnostics);
            PublishBusy();
        }

        public void Stop(IntentClearReason reason)
        {
            if (mDirector != null)
            {
                mDirector.ForceEndExternalHold(reason.ToString());
                mDirector.HardClearIntents(reason);
                mDirector = null;
            }
            PublishBusy();
        }

        public bool TrySubmitIntent(InputIntent intent, out bool uiPickPreview)
        {
            EnsureStarted();
            var accepted = mDirector.TrySubmitIntent(intent, out uiPickPreview);
            PublishBusy();
            return accepted;
        }

        public void Tick(float deltaTime)
        {
            EnsureStarted();
            mDirector.Tick(deltaTime);
            PublishBusy();
        }

        public bool TryBeginExternalHold(string reason = null)
        {
            EnsureStarted();
            var accepted = mDirector.TryBeginExternalHold(reason);
            PublishBusy();
            return accepted;
        }

        public void EndExternalHold(string reason = null)
        {
            EnsureStarted();
            mDirector.EndExternalHold(reason);
            PublishBusy();
        }

        public void ForceEndExternalHold(string reason = null)
        {
            EnsureStarted();
            mDirector.ForceEndExternalHold(reason);
            PublishBusy();
        }

        public void HardClearIntents(IntentClearReason reason)
        {
            if (mDirector == null)
            {
                PublishBusy();
                return;
            }

            mDirector.ForceEndExternalHold(reason.ToString());
            mDirector.HardClearIntents(reason);
            PublishBusy();
        }

        protected override void OnInit() { PublishBusy(); }
        protected override void OnDeinit() { Stop(IntentClearReason.LayerChange); }

        private void EnsureStarted()
        {
            if (mDirector == null) throw new InvalidOperationException("Presentation runtime is not started.");
        }

        private void PublishBusy()
        {
            var busy = mDirector != null && mDirector.IsMainlineBusy;
            mMainlineBusy.Value = busy;
            // expand-contract：Cards IsBusy 仍读 Sink；V2+ 改只读投影后删除。
            CombatHitSink.DirectorMainlineBusy = busy;
        }
    }
}
