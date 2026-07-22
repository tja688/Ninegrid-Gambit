using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 卡牌实体注册查找与手牌/牌库交接的单一 QF 生命周期所有者。
    /// 场景 Manager 由 Controller Bind；跨域解析不再依赖静态 Instance。
    /// </summary>
    public interface ICardEntityLifecycleSystem : ISystem
    {
        bool IsBound { get; }

        CardManagerSingleton Cards { get; }

        CardHandManagerSingleton Hand { get; }

        CardDeckManagerSingleton Deck { get; }

        void Bind(
            CardManagerSingleton cards,
            CardHandManagerSingleton hand,
            CardDeckManagerSingleton deck);

        void Unbind();

        bool TryGet(int uid, out ManagedCard card);

        void NotifyCardReleased(int uid);

        HandoffState EvictFromHand(ManagedCard card);

        void AdmitToHand(ManagedCard card, in HandoffState state);

        HandoffState EvictFromDeck(ManagedCard card);

        void AdmitToDeck(ManagedCard card, in HandoffState state);
    }
}
