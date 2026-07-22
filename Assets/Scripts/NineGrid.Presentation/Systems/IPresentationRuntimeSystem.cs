using NineGrid.Flow.Presentation;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// CompositionRoot 用的完整运行时：在意图窄接口上增加 Start/Stop 工厂生命周期。
    /// </summary>
    public interface IPresentationRuntimeSystem : IPresentationIntentRuntime
    {
        void Start(IIntentScriptFactory scriptFactory, IUiPickPreviewSink uiPickPreview = null,
            ITimelineDiagnosticSink timelineDiagnostics = null);
        void Stop(IntentClearReason reason);
    }
}
