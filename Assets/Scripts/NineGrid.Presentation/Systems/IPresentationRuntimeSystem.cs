using NineGrid.Flow.Presentation;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 唯一生产/测试表现运行时：在意图窄接口上增加 Start/Stop 与 HardClear。
    /// </summary>
    public interface IPresentationRuntimeSystem : IPresentationIntentRuntime
    {
        void Start(IIntentScriptFactory scriptFactory, IUiPickPreviewSink uiPickPreview = null,
            ITimelineDiagnosticSink timelineDiagnostics = null);
        void Stop(IntentClearReason reason);
        void HardClearIntents(IntentClearReason reason);
    }
}
