using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    public interface IPresentationRuntimeSystem : ISystem
    {
        bool IsStarted { get; }
        IReadonlyBindableProperty<bool> MainlineBusy { get; }
        void Start(IIntentScriptFactory scriptFactory, IUiPickPreviewSink uiPickPreview = null,
            ITimelineDiagnosticSink timelineDiagnostics = null);
        void Stop(IntentClearReason reason);
        bool TrySubmitIntent(InputIntent intent, out bool uiPickPreview);
        void Tick(float deltaTime);
        bool TryBeginExternalHold(string reason = null);
        void EndExternalHold(string reason = null);
        void ForceEndExternalHold(string reason = null);
    }
}
