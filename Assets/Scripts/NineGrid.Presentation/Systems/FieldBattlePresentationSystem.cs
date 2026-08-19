using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 场地交战表现权威所有者：busy / CTS / Present；场景 View 仅 Adapter + Catalog。
    /// </summary>
    public sealed class FieldBattlePresentationSystem : AbstractSystem, IFieldBattlePresentationSystem
    {
        private readonly FieldBattlePresentationExecutor mExecutor = new();
        private IFieldBattleView mView;
        private IFieldBattleViewBinding mBoundView;

        public bool IsBound => mView != null;

        public bool IsBusy => mExecutor.IsBusy;

        public void Bind(IFieldBattleView view)
        {
            if (ReferenceEquals(mView, view) && view != null)
            {
                return;
            }

            UnbindInternal();
            mView = view;
            mBoundView = view as IFieldBattleViewBinding;
            mExecutor.Bind(view);
            mBoundView?.AttachBattleOwner(this);
        }

        public void Unbind()
        {
            UnbindInternal();
        }

        public void UnbindIfView(IFieldBattleView view)
        {
            if (ReferenceEquals(mView, view))
            {
                Unbind();
            }
        }

        private void UnbindInternal()
        {
            if (mBoundView != null && ReferenceEquals(mBoundView.BattleOwnerOrNull, this))
            {
                mBoundView.AttachBattleOwner(null);
            }

            mExecutor.Unbind();
            mView = null;
            mBoundView = null;
        }

        public void CancelBattleWork()
        {
            mExecutor.CancelBattleWork();
        }

        public void ArmNextLethalAttack(bool armed = true)
        {
            mExecutor.ArmNextLethalAttack(armed);
        }

        public bool TryHandleBattleClick(ManagedCard card)
        {
            return mExecutor.TryHandleBattleClick(card);
        }

        public UniTask RequestBasicAttackAtSlotAsync(
            int victimSlot,
            bool? lethalOverride = null,
            CancellationToken cancellationToken = default)
        {
            return mExecutor.RequestBasicAttackAtSlotAsync(victimSlot, lethalOverride, cancellationToken);
        }

        public UniTask PlayDirectorAttackHitPresentAsync(
            int clickedSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult hitProjection,
            CancellationToken cancellationToken = default)
        {
            return mExecutor.PlayDirectorAttackHitPresentAsync(
                clickedSlot,
                resolvedCombatUid,
                hitProjection,
                cancellationToken);
        }

        public UniTask PlayDirectorCounterPresentAsync(
            int attackerSlot,
            int attackerUid,
            PostKillBoardPresentationResult counterProjection,
            CancellationToken cancellationToken = default)
        {
            return mExecutor.PlayDirectorCounterPresentAsync(
                attackerSlot,
                attackerUid,
                counterProjection,
                cancellationToken);
        }

        public bool TryBeginAvatarDefeatPresentation(CancellationToken cancellationToken = default)
        {
            return mExecutor.TryBeginAvatarDefeatPresentation(cancellationToken);
        }

        public UniTask PlayAvatarDefeatPresentationAsync(CancellationToken cancellationToken = default)
        {
            return mExecutor.PlayAvatarDefeatPresentationAsync(cancellationToken);
        }

        public bool TryBeginLethalVictimPresentation(
            ManagedCard victim,
            CancellationToken cancellationToken = default)
        {
            return mExecutor.TryBeginLethalVictimPresentation(victim, cancellationToken);
        }

        public UniTask PresentRemovedFieldCardAsync(
            ManagedCard victim,
            CancellationToken cancellationToken = default)
        {
            return mExecutor.PresentRemovedFieldCardAsync(victim, cancellationToken);
        }

        public UniTask<bool> PlayEffectStrikePresentAsync(
            int strikerUid,
            int victimUid,
            bool victimWillBeRemoved,
            System.Action onStrikeHit,
            CancellationToken cancellationToken = default)
        {
            return mExecutor.PlayEffectStrikePresentAsync(
                strikerUid,
                victimUid,
                victimWillBeRemoved,
                onStrikeHit,
                cancellationToken);
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            Unbind();
        }
    }
}
