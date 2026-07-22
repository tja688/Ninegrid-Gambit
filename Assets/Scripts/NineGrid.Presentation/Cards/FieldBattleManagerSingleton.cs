using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地交战场景视图：Adapter + Encounter Catalog。
    /// busy / CTS / Present 由 <see cref="FieldBattlePresentationSystem"/> 持有。
    /// 保留薄 forwarder 供 HitProxy / DevTest / InBattle 过渡。
    /// </summary>
    public sealed class FieldBattleManagerSingleton : MonoBehaviour, IFieldBattleView
    {
        [Header("Battle Presentation")]
        [Tooltip("CardAttack 节点上的基础交战适配器；留空时 Awake 在本节点子树查找。")]
        [SerializeField] private CardAttackBasicAdapter attackAdapter;

        [Tooltip("战斗编排 Catalog（Intent×参战双方 → Encounter Profile）。必填；留空时仅走 SafeFallback 绑参。")]
        [SerializeField] private BattleEncounterCatalogSO encounterCatalog;

        private FieldBattlePresentationSystem _owner;

        public CardAttackBasicAdapter AttackAdapter => attackAdapter;

        public BattleEncounterCatalogSO EncounterCatalog => encounterCatalog;

        public bool IsBusy => Battle != null && Battle.IsBusy;

        internal FieldBattlePresentationSystem BattleOwnerOrNull => _owner;

        private IFieldBattlePresentationSystem Battle =>
            _owner
            ?? NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>();

        internal void AttachBattleOwner(FieldBattlePresentationSystem owner)
        {
            _owner = owner;
        }

        private void Awake()
        {
            EnsureAttackAdapter();
            FieldBattlePresentationHook.RequestWire(this);
            AttackInputHook.RequestWire(this);
        }

        private void OnDestroy()
        {
            _owner?.UnbindIfView(this);
            CancelBattleWork();
        }

        public void EnsureAttackAdapter()
        {
            if (attackAdapter != null)
            {
                return;
            }

            attackAdapter = GetComponentInChildren<CardAttackBasicAdapter>(true);
        }

        public void ArmNextLethalAttack(bool armed = true)
        {
            Battle?.ArmNextLethalAttack(armed);
        }

        public void CancelBattleWork()
        {
            Battle?.CancelBattleWork();
        }

        public bool TryHandleBattleClick(ManagedCard card)
        {
            return Battle != null && Battle.TryHandleBattleClick(card);
        }

        public UniTask RequestBasicAttackAtSlotAsync(
            int victimSlot,
            bool? lethalOverride = null,
            CancellationToken cancellationToken = default)
        {
            return Battle != null
                ? Battle.RequestBasicAttackAtSlotAsync(victimSlot, lethalOverride, cancellationToken)
                : UniTask.CompletedTask;
        }

        public UniTask PlayDirectorAttackHitPresentAsync(
            int clickedSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult hitProjection,
            CancellationToken cancellationToken = default)
        {
            return Battle != null
                ? Battle.PlayDirectorAttackHitPresentAsync(
                    clickedSlot,
                    resolvedCombatUid,
                    hitProjection,
                    cancellationToken)
                : UniTask.CompletedTask;
        }

        public UniTask PlayDirectorCounterPresentAsync(
            int attackerSlot,
            int attackerUid,
            PostKillBoardPresentationResult counterProjection,
            CancellationToken cancellationToken = default)
        {
            return Battle != null
                ? Battle.PlayDirectorCounterPresentAsync(
                    attackerSlot,
                    attackerUid,
                    counterProjection,
                    cancellationToken)
                : UniTask.CompletedTask;
        }

        public bool TryBeginAvatarDefeatPresentation(CancellationToken cancellationToken = default)
        {
            return Battle != null && Battle.TryBeginAvatarDefeatPresentation(cancellationToken);
        }

        public bool TryBeginLethalVictimPresentation(
            ManagedCard victim,
            CancellationToken cancellationToken = default)
        {
            return Battle != null && Battle.TryBeginLethalVictimPresentation(victim, cancellationToken);
        }

        public UniTask PresentRemovedFieldCardAsync(
            ManagedCard victim,
            CancellationToken cancellationToken = default)
        {
            return Battle != null
                ? Battle.PresentRemovedFieldCardAsync(victim, cancellationToken)
                : UniTask.CompletedTask;
        }
    }
}
