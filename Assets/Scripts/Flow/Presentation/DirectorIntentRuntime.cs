using System;
using NineGrid.Cards;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 将已有 <see cref="PresentationDirector"/> 暴露为 <see cref="IPresentationIntentRuntime"/>，
    /// 供生产路径（InBattle 装配导演）与 QF Command 共用同一实例。
    /// </summary>
    public sealed class DirectorIntentRuntime : AbstractSystem, IPresentationIntentRuntime
    {
        private readonly BindableProperty<bool> mMainlineBusy = new BindableProperty<bool>(false);
        private PresentationDirector mDirector;

        public bool IsStarted
        {
            get { return mDirector != null; }
        }

        public IReadonlyBindableProperty<bool> MainlineBusy
        {
            get { return mMainlineBusy; }
        }

        public void Bind(PresentationDirector director)
        {
            mDirector = director;
            PublishBusy();
        }

        public void Unbind()
        {
            mDirector = null;
            PublishBusy();
        }

        public bool TrySubmitIntent(InputIntent intent, out bool uiPickPreview)
        {
            EnsureBound();
            var accepted = mDirector.TrySubmitIntent(intent, out uiPickPreview);
            PublishBusy();
            return accepted;
        }

        public void Tick(float deltaTime)
        {
            EnsureBound();
            mDirector.Tick(deltaTime);
            PublishBusy();
        }

        public bool TryBeginExternalHold(string reason = null)
        {
            EnsureBound();
            var accepted = mDirector.TryBeginExternalHold(reason);
            PublishBusy();
            return accepted;
        }

        public void EndExternalHold(string reason = null)
        {
            EnsureBound();
            mDirector.EndExternalHold(reason);
            PublishBusy();
        }

        public void ForceEndExternalHold(string reason = null)
        {
            EnsureBound();
            mDirector.ForceEndExternalHold(reason);
            PublishBusy();
        }

        protected override void OnInit()
        {
            PublishBusy();
        }

        protected override void OnDeinit()
        {
            Unbind();
        }

        private void EnsureBound()
        {
            if (mDirector == null)
            {
                throw new InvalidOperationException("Presentation director is not bound.");
            }
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
