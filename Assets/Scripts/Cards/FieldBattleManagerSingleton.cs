using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地交战管理器单例：Intent → Catalog 路由 → Adapter 播 Rig → 终态 Guard。
    /// 点击默认编排为「玩家进攻 →（未击杀则）怪物反击」，复用四项基础 Profile，不另建组合 Intent。
    /// 场地占用与旋转仍由 GroundFieldManagerSingleton 负责。
    /// LEGACY：仅击杀后外圈旋转；正式规则以未来 Core Batch 为准。
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

        /// <summary>
        /// 玩家进攻编排：播 Attack(/Lethal) Profile；未击杀则接播 CounterAttack Profile；仅击杀后外圈旋转。
        /// </summary>
        public UniTask RequestBasicAttackAtSlotAsync(
            int victimSlot,
            bool? lethalOverride = null,
            CancellationToken cancellationToken = default)
        {
            return RequestBasicAttackInternalAsync(victimSlot, lethalOverride, cancellationToken);
        }

        /// <summary>
        /// 仅怪物反击（DevTest Keypad1 等）。点击交战请走 <see cref="RequestBasicAttackAtSlotAsync"/>。
        /// </summary>
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
            var attackIntent = BattleIntentUtility.FromFlags(counter: false, lethal);
            var attackBind = ResolveBindParams(attackIntent, victim, out _);

            _isBusy = true;
            try
            {
                await attackAdapter.PlayBasicAttackAsync(victim, attackBind, cancellationToken);

                if (lethal)
                {
                    CardManagerSingleton.Instance.MarkFieldDead(victim);
                    fieldManager.VacateSlotForExplore(victimSlot, victim, playRemoveAnim: false, skipBusyGuard: true);
                    FinalizeLethalVictimAsync(victim, cancellationToken).Forget();

                    // LEGACY：仅击杀后旋转腾格；未击杀保留场上卡，正式规则待 Core Batch。
                    await fieldManager.RotateOuterRingClockwiseWhileBusyAsync(cancellationToken);
                    return;
                }

                // 复用独立 Counter Profile：后续调反击手感/变体时，点击交战自动吃到。
                await PlayCounterAttackCoreAsync(victimSlot, lethal: false, cancellationToken);
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

            if (!TryValidateCounterParticipants(attackerSlot, out _))
            {
                return;
            }

            var lethal = lethalOverride ?? false;

            _isBusy = true;
            try
            {
                await PlayCounterAttackCoreAsync(attackerSlot, lethal, cancellationToken);
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>
        /// 播反击 Rig；调用方负责忙碌锁。进攻编排在未击杀分支内复用本方法。
        /// </summary>
        private async UniTask PlayCounterAttackCoreAsync(
            int attackerSlot,
            bool lethal,
            CancellationToken cancellationToken)
        {
            if (!TryValidateCounterParticipants(attackerSlot, out var attacker))
            {
                return;
            }

            var intent = BattleIntentUtility.FromFlags(counter: true, lethal);
            var bind = ResolveBindParams(intent, attacker, out _);
            await attackAdapter.PlayBasicCounterAttackAsync(attacker, bind, cancellationToken);
        }

        private bool TryValidateCounterParticipants(int attackerSlot, out ManagedCard attacker)
        {
            attacker = null;
            ResolveFieldManager();
            ResolveAttackAdapter();

            if (attackAdapter == null)
            {
                Debug.LogWarning("[FieldBattleManager] 未配置 CardAttackBasicAdapter。");
                return false;
            }

            if (fieldManager == null)
            {
                Debug.LogWarning("[FieldBattleManager] 未找到 GroundFieldManagerSingleton。");
                return false;
            }

            if (!fieldManager.IsAvatarOrthogonalBattleSlot(attackerSlot)
                || !fieldManager.TryGetCardAt(attackerSlot, out attacker))
            {
                Debug.LogWarning($"[FieldBattleManager] 格位 {attackerSlot} 不可触发怪物反击。");
                return false;
            }

            if (attacker.IsFieldDead)
            {
                Debug.LogWarning($"[FieldBattleManager] 格位 {attackerSlot} 卡牌已死亡。");
                return false;
            }

            if (!fieldManager.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar.IsFieldDead)
            {
                Debug.LogWarning("[FieldBattleManager] Avatar 不可用，无法触发反击。");
                return false;
            }

            return true;
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
