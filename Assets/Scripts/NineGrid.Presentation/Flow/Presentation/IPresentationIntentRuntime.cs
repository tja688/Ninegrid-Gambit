using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 表现意图运行时窄接口：供 Command / System 提交 InputIntent，不依赖具体场景宿主。
    /// 生产与测试统一经 <c>IPresentationRuntimeSystem</c> 实现。
    /// </summary>
    public interface IPresentationIntentRuntime : ISystem
    {
        bool IsStarted { get; }
        IReadonlyBindableProperty<bool> MainlineBusy { get; }
        /// <summary>导演 external-hold 租约是否仍持有（含嵌套计数归零前）。</summary>
        bool HasExternalHold { get; }
        /// <summary>是否有缓存的意图等待主线空闲后提交。</summary>
        bool HasBufferedIntent { get; }
        bool TrySubmitIntent(InputIntent intent, out bool uiPickPreview);
        void Tick(float deltaTime);
        bool TryBeginExternalHold(string reason = null);
        void EndExternalHold(string reason = null);
        void ForceEndExternalHold(string reason = null);
        /// <summary>向主线 timeline 追加步骤（旁路融合补牌等 defer 入队）。</summary>
        void MutateMainline(System.Action<BattleTimeline> mutate);
    }
}
