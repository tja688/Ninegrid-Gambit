using NineGrid.Cards;

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

        GroundFieldManagerSingleton FieldManager { get; }

        RelicManagerSingleton RelicManager { get; }

        CardHandManagerSingleton HandManager { get; }

        SelectorManagerSingleton SelectorManager { get; }
    }
}
