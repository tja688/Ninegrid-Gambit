using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 场地交战表现的单一 QF 所有者：busy / CTS / Present 编排与取消后终态。
    /// 场景 View 仅提供 Adapter 与 Catalog；读取走 Query，写入走 Present / Command。
    /// </summary>
    public interface IFieldBattlePresentationSystem : ISystem
    {
        bool IsBound { get; }

        void Bind(IFieldBattleView view);

        void Unbind();

        void UnbindIfView(IFieldBattleView view);

        bool IsBusy { get; }

        void CancelBattleWork();

        void ArmNextLethalAttack(bool armed = true);

        bool TryHandleBattleClick(ManagedCard card);

        UniTask RequestBasicAttackAtSlotAsync(
            int victimSlot,
            bool? lethalOverride = null,
            CancellationToken cancellationToken = default);

        UniTask PlayDirectorAttackHitPresentAsync(
            int clickedSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult hitProjection,
            CancellationToken cancellationToken = default);

        UniTask PlayDirectorCounterPresentAsync(
            int attackerSlot,
            int attackerUid,
            PostKillBoardPresentationResult counterProjection,
            CancellationToken cancellationToken = default);

        bool TryBeginAvatarDefeatPresentation(CancellationToken cancellationToken = default);

        bool TryBeginLethalVictimPresentation(
            ManagedCard victim,
            CancellationToken cancellationToken = default);

        UniTask PresentRemovedFieldCardAsync(
            ManagedCard victim,
            CancellationToken cancellationToken = default);
    }
}
