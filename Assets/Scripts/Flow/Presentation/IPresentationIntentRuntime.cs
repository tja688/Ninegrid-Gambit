using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 表现意图运行时窄接口：供 QF Command 提交 InputIntent，不依赖具体宿主（InBattle / CompositionRoot）。
    /// </summary>
    public interface IPresentationIntentRuntime : ISystem
    {
        bool IsStarted { get; }
        IReadonlyBindableProperty<bool> MainlineBusy { get; }
        bool TrySubmitIntent(InputIntent intent, out bool uiPickPreview);
        void Tick(float deltaTime);
        bool TryBeginExternalHold(string reason = null);
        void EndExternalHold(string reason = null);
        void ForceEndExternalHold(string reason = null);
    }
}
