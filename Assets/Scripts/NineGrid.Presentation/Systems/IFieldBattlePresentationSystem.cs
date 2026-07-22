using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 场地战斗表现宿主的单一 QF 所有者：忙碌态、净土域 Handoff、取消交战。
    /// 导演 Present 执行仍委托 compat shell 上的既有方法。
    /// </summary>
    public interface IFieldBattlePresentationSystem : ISystem
    {
        bool IsBound { get; }

        FieldBattleManagerSingleton Battle { get; }

        void Bind(FieldBattleManagerSingleton battle);

        void Unbind();

        bool IsBusy { get; }

        void CancelBattleWork();

        HandoffState EvictCard(ManagedCard card);

        void AdmitCard(ManagedCard card, in HandoffState state);
    }
}
