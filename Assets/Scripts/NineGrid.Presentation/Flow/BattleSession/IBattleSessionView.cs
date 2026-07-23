using NineGrid.Cards;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;

namespace NineGrid.Flow
{
    /// <summary>
    /// 局内会话场景视图适配：仅暴露卡牌/牌库/场地/手牌/遗物/选择器等场景宿主。
    /// 会话、批次投影、盘面表现、选择与输出职责由对应 QF System / 深 module 持有。
    /// </summary>
    public interface IBattleSessionView
    {
        CardManagerSingleton CardManager { get; }

        CardDeckManagerSingleton DeckManager { get; }

        GroundFieldView FieldManager { get; }

        RelicManagerSingleton RelicManager { get; }

        CardHandManagerSingleton HandManager { get; }

        SelectorManagerSingleton SelectorManager { get; }

        FieldBattleView BattleManager { get; }

        UiPanelRouter PanelRouter { get; }

        /// <summary>由 PresentationSceneRoot 注入的 Runtime Install 回调。</summary>
        void EnsurePresentationRuntimeInstalled();

        /// <summary>由 PresentationSceneRoot 注入的 Runtime Shutdown 回调。</summary>
        void ShutdownPresentationRuntime(IntentClearReason reason);
    }
}
