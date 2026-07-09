using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地交战管理器单例：Intent → Catalog 路由 → Adapter 播 Rig → 终态 Guard。
    /// 场地占用与旋转仍由 GroundFieldManagerSingleton 负责。
    /// LEGACY：攻击后旋转等伪规则行为本轮保留，正式规则以未来 Core Batch 为准。
    /// </summary>
    public sealed class FieldBattleManagerSingleton : MonoBehaviour
    {
        private static FieldBattleManagerSingleton _instance;

        [Tooltip("运行时自动查找 GroundFieldManagerSingleton.Instance；也可手动拖入覆盖。")]
        [SerializeField] private GroundFieldManagerSingleton fieldManager;

        [Header("Battle Presentation")]
        [Tooltip("CardAttack 节点上的基础交战适配器；留空时 Awake 在场地管理器子节点或场景中自动查找。")]
        [SerializeField] private CardAttackBasicAdapter attackAdapter;

        [Tooltip("战斗编排 Catalog（Intent×参战双方 → Encounter Profile）。必填；留空时仅走 SafeFallback 绑参。")]
        [SerializeField] private BattleEncounterCatalogSO encounterCatalog;

        private bool _isBusy;

        public static FieldBattleManagerSingleton Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<FieldBattleManagerSingleton>();
                }

                return _instance;
            }
        }

        public bool IsBusy => _isBusy;

        public CardAttackBasicAdapter AttackAdapter => attackAdapter;

        public BattleEncounterCatalogSO EncounterCatalog => encounterCatalog;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            ResolveFieldManager();
            ResolveAttackAdapter();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        public void ArmNextLethalAttack(bool armed = true)
        {
            attackAdapter?.ArmNextLethalAttack(armed);
        }

        public bool TryHandleBattleClick(ManagedCard card)
        {
            ResolveFieldManager();
            if (card == null
                || _isBusy
                || attackAdapter == null
                || fieldManager == null
                || fieldManager.IsFieldBusy)
            {
                return false;
            }

            if (card.IsFieldDead)
            {
                return false;
            }

            if (!fieldManager.TryGetSlotOf(card.Uid, out var slot)
                || !fieldManager.IsAvatarOrthogonalBattleSlot(slot))
            {
                return false;
            }

            RequestBasicAttackAtSlotAsync(slot).Forget();
            return true;
        }

        public UniTask RequestBasicAttackAtSlotAsync(
            int victimSlot,
            bool? lethalOverride = null,
            CancellationToken cancellationToken = default)
        {
            return RequestBasicAttackInternalAsync(victimSlot, lethalOverride, cancellationToken);
        }

        public UniTask RequestBasicCounterAttackAtSlotAsync(
            int attackerSlot,
            bool? lethalOverride = null,
            CancellationToken cancellationToken = default)
        {
            return RequestBasicCounterAttackInternalAsync(attackerSlot, lethalOverride, cancellationToken);
        }

        private async UniTask RequestBasicAttackInternalAsync(
            int victimSlot,
            bool? lethalOverride,
            CancellationToken cancellationToken)
        {
            ResolveFieldManager();
            ResolveAttackAdapter();

            if (_isBusy || (fieldManager != null && fieldManager.IsFieldBusy))
            {
                Debug.LogWarning("[FieldBattleManager] 当前忙碌，无法触发交战。");
                return;
            }

            if (attackAdapter == null)
            {
                Debug.LogWarning("[FieldBattleManager] 未配置 CardAttackBasicAdapter。");
                return;
            }

            if (fieldManager == null)
            {
                Debug.LogWarning("[FieldBattleManager] 未找到 GroundFieldManagerSingleton。");
                return;
            }

            if (!fieldManager.IsAvatarOrthogonalBattleSlot(victimSlot)
                || !fieldManager.TryGetCardAt(victimSlot, out var victim))
            {
                Debug.LogWarning($"[FieldBattleManager] 格位 {victimSlot} 不可触发 Avatar 四向交战。");
                return;
            }

            if (victim.IsFieldDead)
            {
                Debug.LogWarning($"[FieldBattleManager] 格位 {victimSlot} 卡牌已死亡。");
                return;
            }

            var lethal = lethalOverride ?? attackAdapter.ConsumeNextLethalArmed();
            var intent = BattleIntentUtility.FromFlags(counter: false, lethal);
            var bind = ResolveBindParams(intent, victim, out _);

            _isBusy = true;
            try
            {
                await attackAdapter.PlayBasicAttackAsync(victim, bind, cancellationToken);
                if (lethal)
                {
                    CardManagerSingleton.Instance.MarkFieldDead(victim);
                    fieldManager.VacateSlotForExplore(victimSlot, victim, playRemoveAnim: false, skipBusyGuard: true);
                    FinalizeLethalVictimAsync(victim, cancellationToken).Forget();
                }

                // LEGACY：伪战斗路径攻击后几乎总旋转；正式规则应由 Core Batch 驱动。
                await fieldManager.RotateOuterRingClockwiseWhileBusyAsync(cancellationToken);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async UniTask RequestBasicCounterAttackInternalAsync(
            int attackerSlot,
            bool? lethalOverride,
            CancellationToken cancellationToken)
        {
            ResolveFieldManager();
            ResolveAttackAdapter();

            if (_isBusy || (fieldManager != null && fieldManager.IsFieldBusy))
            {
                Debug.LogWarning("[FieldBattleManager] 当前忙碌，无法触发反击。");
                return;
            }

            if (attackAdapter == null)
            {
                Debug.LogWarning("[FieldBattleManager] 未配置 CardAttackBasicAdapter。");
                return;
            }

            if (fieldManager == null)
            {
                Debug.LogWarning("[FieldBattleManager] 未找到 GroundFieldManagerSingleton。");
                return;
            }

            if (!fieldManager.IsAvatarOrthogonalBattleSlot(attackerSlot)
                || !fieldManager.TryGetCardAt(attackerSlot, out var attacker))
            {
                Debug.LogWarning($"[FieldBattleManager] 格位 {attackerSlot} 不可触发怪物反击。");
                return;
            }

            if (attacker.IsFieldDead)
            {
                Debug.LogWarning($"[FieldBattleManager] 格位 {attackerSlot} 卡牌已死亡。");
                return;
            }

            if (!fieldManager.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar.IsFieldDead)
            {
                Debug.LogWarning("[FieldBattleManager] Avatar 不可用，无法触发反击。");
                return;
            }

            var lethal = lethalOverride ?? false;
            var intent = BattleIntentUtility.FromFlags(counter: true, lethal);
            var bind = ResolveBindParams(intent, attacker, out _);

            _isBusy = true;
            try
            {
                await attackAdapter.PlayBasicCounterAttackAsync(attacker, bind, cancellationToken);
            }
            finally
            {
                _isBusy = false;
            }
        }

        private BattleBindParams ResolveBindParams(
            BattleIntent intent,
            ManagedCard monsterCard,
            out BattleEncounterProfileSO profile)
        {
            var playerId = ResolveAvatarDefId();
            var monsterId = monsterCard != null ? monsterCard.DefId : BattleParticipantIds.Wildcard;
            return BattlePresentationRouter.ResolveBindParams(
                encounterCatalog,
                intent,
                playerId,
                monsterId,
                out profile,
                out _);
        }

        private string ResolveAvatarDefId()
        {
            ResolveFieldManager();
            if (fieldManager != null
                && fieldManager.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                && avatar != null
                && !string.IsNullOrWhiteSpace(avatar.DefId))
            {
                return avatar.DefId;
            }

            return BattleParticipantIds.Wildcard;
        }

        /// <summary>
        /// 即死交战已在 rig 时间轴内触发死亡特效，此处仅等待播完再 Release，避免重复播死亡或 Refresh 诈尸。
        /// </summary>
        private async UniTask FinalizeLethalVictimAsync(ManagedCard card, CancellationToken cancellationToken)
        {
            if (card == null)
            {
                return;
            }

            if (card.TryGetEffectManager(out var effectManager))
            {
                await WaitForEffectIdleAsync(effectManager, cancellationToken);
            }
            else if (card.Transform != null)
            {
                var removeDuration = fieldManager != null
                    ? fieldManager.LayoutSettings.removeDisappearDuration
                    : 0.25f;
                var initialScale = card.Transform.localScale;
                await RunViewTweenAsync(
                    CardViewTween.ScaleDisappear(
                        card.Transform,
                        initialScale,
                        removeDuration),
                    cancellationToken);
            }

            if (card.Transform != null)
            {
                CardManagerSingleton.Instance.Release(card.Uid);
            }
        }

        private static async UniTask WaitForEffectIdleAsync(
            CardEffectManager effectManager,
            CancellationToken cancellationToken)
        {
            const float startupGraceSeconds = 0.15f;
            var deadline = Time.time + startupGraceSeconds;
            while (!effectManager.IsPlaying && Time.time < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            while (effectManager.IsPlaying)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private static async UniTask RunViewTweenAsync(IEnumerator routine, CancellationToken cancellationToken)
        {
            if (routine == null)
            {
                return;
            }

            while (routine.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        private void ResolveFieldManager()
        {
            if (fieldManager != null)
            {
                return;
            }

            fieldManager = GroundFieldManagerSingleton.Instance;
        }

        private void ResolveAttackAdapter()
        {
            if (attackAdapter != null)
            {
                return;
            }

            ResolveFieldManager();
            if (fieldManager != null)
            {
                attackAdapter = fieldManager.GetComponentInChildren<CardAttackBasicAdapter>(true);
            }

            if (attackAdapter == null)
            {
                attackAdapter = FindFirstObjectByType<CardAttackBasicAdapter>();
            }
        }
    }
}
