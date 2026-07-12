using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地交战管理器单例：Intent → Catalog 路由 → Adapter 播 Rig；
    /// 命中帧经 CombatHitSink 写 Core，再抓 Model 刷血/飘字；
    /// 击杀后 Core 一次结算，表现缓冲按 Moved/Dealt 缓释。
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
        private CancellationTokenSource _battleCts;

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
            CancelBattleWork();
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 取消进行中的交战编排（回主菜单等生命周期清理）。
        /// </summary>
        public void CancelBattleWork()
        {
            if (_battleCts != null)
            {
                _battleCts.Cancel();
                _battleCts.Dispose();
                _battleCts = null;
            }

            _isBusy = false;
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
                || CombatHitSink.ChoiceOverlayActive
                || CombatHitSink.PresentationLocked
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
        /// 玩家进攻编排：命中帧写 Core；击杀则清格+Core 旋转；未击杀则接播反击。
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

            if (_isBusy
                || CombatHitSink.ChoiceOverlayActive
                || CombatHitSink.PresentationLocked
                || (fieldManager != null && fieldManager.IsFieldBusy))
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

            if (!fieldManager.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar == null)
            {
                Debug.LogWarning("[FieldBattleManager] Avatar 不可用，无法进攻。");
                return;
            }

            var linkedCts = CreateLinkedBattleCts(cancellationToken);
            var ct = linkedCts.Token;

            var willKill = lethalOverride
                ?? attackAdapter.ConsumeNextLethalArmed()
                || CombatHitSink.RequestEstimateWillKill(avatar.Uid, victim.Uid);
            var attackIntent = BattleIntentUtility.FromFlags(counter: false, willKill);
            var attackBind = ResolveBindParams(attackIntent, victim, out var attackProfile);
            LogBattleBindResolve(avatar.Uid, victim.Uid, attackBind, attackProfile, willKill, isCounter: false);

            CombatHitPresentationResult hitResult = default;
            var hitApplied = false;

            _isBusy = true;
            try
            {
                try
                {
                    PerfTraceSink.OpenBeat?.Invoke("CombatHit", 0);
                    PerfTraceSink.SetCombatants?.Invoke(avatar.Uid, victim.Uid);
                }
                catch
                {
                    // ignore
                }

                await attackAdapter.PlayBasicAttackAsync(
                    victim,
                    attackBind,
                    () =>
                    {
                        if (hitApplied)
                        {
                            return;
                        }

                        hitApplied = true;
                        hitResult = ApplyHitPresentation(avatar, victim, "PlayerAttack");
                    },
                    ct);

                if (!hitApplied)
                {
                    // Timeline 未打到命中回调时兜底结算，避免动画播完无数据。
                    hitResult = ApplyHitPresentation(avatar, victim, "PlayerAttack");
                    hitApplied = true;
                }

                if (hitResult.TargetKilled)
                {
                    CardManagerSingleton.Instance.MarkFieldDead(victim);

                    // Core 须在 Vacate 之前结算：否则 FieldMaybeClearSignal 会在
                    // OfferReward 前用 IsNodeCleared 抢跑主循环。
                    var postKill = CombatHitSink.RequestPostKillBoard();

                    // 真交战击杀后由 Core 旋转补牌，不走空槽探求。
                    fieldManager.VacateSlotForExplore(
                        victimSlot,
                        victim,
                        playRemoveAnim: false,
                        skipBusyGuard: true,
                        startExplore: false);
                    // Drain 前必须离锚点，否则 hop 会与尸体叠位闪现。
                    CardManagerSingleton.Instance.StageFieldDeadCorpseOffAnchor(victim);
                    FinalizeLethalVictimAsync(victim, ct).Forget();

                    await CombatHitSink.RequestDrainPostKillBoard(postKill, ct);

                    // 只认 PostKill 结果，不用 hitResult.NodeCleared（击杀当下即可 true）。
                    if (postKill.NodeClearedOrRewardPhase)
                    {
                        // 节点通关 → 主循环结算（奖励/房间），非整局胜利。
                        CombatHitSink.RequestNodeSettlement();
                    }

                    return;
                }

                if (attackBind.BindDeathCallback || attackBind.IsLethal)
                {
                    RecoverSurvivingVictimAfterLethalMismatch(
                        avatar.Uid,
                        victim,
                        victimSlot,
                        attackBind,
                        willKill,
                        hitResult.TargetKilled);
                }

                await PlayCounterAttackCoreAsync(victimSlot, lethal: false, ct);
            }
            catch (System.OperationCanceledException)
            {
                // 回主菜单等取消路径
            }
            finally
            {
                try
                {
                    PerfTraceSink.CloseBeat?.Invoke();
                }
                catch
                {
                    // ignore
                }

                _isBusy = false;
                DisposeBattleCts(linkedCts);
            }
        }

        private async UniTask RequestBasicCounterAttackInternalAsync(
            int attackerSlot,
            bool? lethalOverride,
            CancellationToken cancellationToken)
        {
            ResolveFieldManager();
            ResolveAttackAdapter();

            if (_isBusy
                || CombatHitSink.ChoiceOverlayActive
                || CombatHitSink.PresentationLocked
                || (fieldManager != null && fieldManager.IsFieldBusy))
            {
                Debug.LogWarning("[FieldBattleManager] 当前忙碌，无法触发反击。");
                return;
            }

            if (!TryValidateCounterParticipants(attackerSlot, out _))
            {
                return;
            }

            var lethal = lethalOverride ?? false;
            var linkedCts = CreateLinkedBattleCts(cancellationToken);
            var ct = linkedCts.Token;

            _isBusy = true;
            try
            {
                await PlayCounterAttackCoreAsync(attackerSlot, lethal, ct);
            }
            catch (System.OperationCanceledException)
            {
            }
            finally
            {
                _isBusy = false;
                DisposeBattleCts(linkedCts);
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

            if (!fieldManager.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar == null)
            {
                return;
            }

            var willKill = lethal || CombatHitSink.RequestEstimateWillKill(attacker.Uid, avatar.Uid);
            var intent = BattleIntentUtility.FromFlags(counter: true, willKill);
            var bind = ResolveBindParams(intent, attacker, out var profile);
            LogBattleBindResolve(attacker.Uid, avatar.Uid, bind, profile, willKill, isCounter: true);

            CombatHitPresentationResult hitResult = default;
            var hitApplied = false;

            try
            {
                PerfTraceSink.OpenBeat?.Invoke("CombatHit", 0);
                PerfTraceSink.SetCombatants?.Invoke(attacker.Uid, avatar.Uid);

                await attackAdapter.PlayBasicCounterAttackAsync(
                    attacker,
                    bind,
                    () =>
                    {
                        if (hitApplied)
                        {
                            return;
                        }

                        hitApplied = true;
                        hitResult = ApplyHitPresentation(attacker, avatar, "CounterAttack");
                    },
                    cancellationToken);

                if (!hitApplied)
                {
                    hitResult = ApplyHitPresentation(attacker, avatar, "CounterAttack");
                }

                if (hitResult.AvatarDefeated)
                {
                    CombatHitSink.RequestBattleEnded(victory: false);
                }
            }
            finally
            {
                try
                {
                    PerfTraceSink.CloseBeat?.Invoke();
                }
                catch
                {
                    // ignore
                }
            }
        }

        private static CombatHitPresentationResult ApplyHitPresentation(
            ManagedCard attacker,
            ManagedCard victim,
            string traceReason)
        {
            if (attacker == null || victim == null)
            {
                return default;
            }

            if (!string.IsNullOrEmpty(traceReason))
            {
                CombatHitSink.PendingTraceReason = traceReason;
            }

            var result = CombatHitSink.RequestCombatHit(attacker.Uid, victim.Uid);
            if (!result.Accepted)
            {
                return result;
            }

            CombatHitSink.RequestSyncCard(victim);
            SpawnDamagePopups(result.DamagePopups, victim, result.DamageAmount);
            return result;
        }

        /// <summary>
        /// 按 DamageDealt 事件序分别飘字；反伤等可落在非主目标上。无 popups 时回退主目标 DamageAmount。
        /// </summary>
        private static void SpawnDamagePopups(
            CombatDamagePopup[] popups,
            ManagedCard fallbackVictim,
            int fallbackAmount)
        {
            var cards = CardManagerSingleton.Instance;
            if (popups != null && popups.Length > 0)
            {
                for (var i = 0; i < popups.Length; i++)
                {
                    var popup = popups[i];
                    if (popup.Amount <= 0)
                    {
                        continue;
                    }

                    Vector3? pos = null;
                    if (cards != null
                        && cards.TryGet(popup.TargetUid, out var target)
                        && target?.Transform != null)
                    {
                        pos = target.Transform.position;
                        if (fallbackVictim == null || target.Uid != fallbackVictim.Uid)
                        {
                            CombatHitSink.RequestSyncCard(target);
                        }
                    }
                    else if (fallbackVictim != null
                             && fallbackVictim.Uid == popup.TargetUid
                             && fallbackVictim.Transform != null)
                    {
                        pos = fallbackVictim.Transform.position;
                    }

                    if (pos.HasValue)
                    {
                        CombatHitSink.RequestDamageNumber(pos.Value, popup.Amount);
                    }
                }

                return;
            }

            if (fallbackAmount > 0 && fallbackVictim?.Transform != null)
            {
                CombatHitSink.RequestDamageNumber(fallbackVictim.Transform.position, fallbackAmount);
            }
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

        private static void LogBattleBindResolve(
            int attackerUid,
            int victimUid,
            BattleBindParams bind,
            BattleEncounterProfileSO profile,
            bool estimatedWillKill,
            bool isCounter)
        {
            var profileId = profile != null ? profile.ProfileId : bind.ProfileId;
            CardPresentationProbe.BattleBindResolve(
                attackerUid,
                victimUid,
                "Combat.BindResolve",
                bind.Intent.ToString(),
                profileId,
                bind.BindDeathCallback,
                estimatedWillKill,
                isCounter);

            Debug.Log(
                $"[FieldBattleManager] 路由 intent={bind.Intent} profile={profileId}"
                + $" bindDeath={bind.BindDeathCallback} estKill={estimatedWillKill}"
                + $" counter={isCounter} attacker={attackerUid} victim={victimUid}");
        }

        /// <summary>
        /// Lethal Profile 预估击杀但实际存活：清死亡残留、强制回锚，避免接反击时「尸体隐身打人」。
        /// </summary>
        private void RecoverSurvivingVictimAfterLethalMismatch(
            int attackerUid,
            ManagedCard victim,
            int victimSlot,
            BattleBindParams bind,
            bool estimatedWillKill,
            bool actualKilled)
        {
            if (actualKilled || victim == null)
            {
                return;
            }

            if (!estimatedWillKill && !bind.BindDeathCallback)
            {
                return;
            }

            Debug.LogWarning(
                $"[FieldBattleManager] Lethal 预估偏差：uid={victim.Uid} estKill={estimatedWillKill}"
                + $" actualKill={actualKilled} profile={bind.ProfileId}，清理死亡残留后接反击。");

            CardPresentationProbe.DeathCallback(
                victim.Uid,
                "Combat.LethalMismatchRecover",
                "recover",
                armed: bind.BindDeathCallback,
                confirmedKill: false,
                attackerUid: attackerUid);

            if (victim.TryGetEffectManager(out var effectManager))
            {
                effectManager.StopCurrent();
            }

            CardManagerSingleton.Instance?.RefreshDisplayMode(victim);

            ResolveFieldManager();
            if (fieldManager != null && victim.Transform != null)
            {
                var anchor = fieldManager.GetGroundAnchor(victimSlot);
                if (anchor != null)
                {
                    BattleFinalStateGuard.SnapImmediate(victim.Transform, anchor.position);
                }
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
        /// UseItem / 非交战击杀：对齐真交战卸尸契约。
        /// MarkFieldDead → Vacate（不探求）→ 异步播死并 Release。必须在 Drain/Sync 之前调用 Vacate。
        /// </summary>
        public bool TryBeginLethalVictimPresentation(
            ManagedCard victim,
            CancellationToken cancellationToken = default)
        {
            ResolveFieldManager();
            if (victim == null || fieldManager == null)
            {
                return false;
            }

            var hadSlot = fieldManager.TryGetSlotOf(victim.Uid, out var victimSlot);
            CardManagerSingleton.Instance?.MarkFieldDead(victim);

            if (hadSlot)
            {
                fieldManager.VacateSlotForExplore(
                    victimSlot,
                    victim,
                    playRemoveAnim: false,
                    skipBusyGuard: true,
                    startExplore: false);
            }

            // 与真交战一致：Vacate 后立刻离锚，避免后续 Drain/Sync 叠位。
            CardManagerSingleton.Instance?.StageFieldDeadCorpseOffAnchor(victim);

            // UseItem 无交战时间轴：若尚未在播死亡，主动开播，再由 Finalize 等闲后 Release。
            if (victim.TryGetEffectManager(out var effectManager) && !effectManager.IsPlaying)
            {
                effectManager.PlayDeathAsync(
                    hadSlot ? victimSlot : 0,
                    cancellationToken: cancellationToken).Forget();
            }

            FinalizeLethalVictimAsync(victim, cancellationToken).Forget();
            return true;
        }

        /// <summary>
        /// 技能/效果 RemoveCard：在原地播默认死亡退场并 Release（不等离锚），供 Drain 等待完成后再 hop/补牌。
        /// </summary>
        public async UniTask PresentRemovedFieldCardAsync(
            ManagedCard victim,
            CancellationToken cancellationToken = default)
        {
            ResolveFieldManager();
            if (victim == null)
            {
                return;
            }

            var victimSlot = 0;
            var hadSlot = fieldManager != null
                && fieldManager.TryGetSlotOf(victim.Uid, out victimSlot);

            CardManagerSingleton.Instance?.MarkFieldDead(victim);

            if (hadSlot)
            {
                fieldManager.VacateSlotForExplore(
                    victimSlot,
                    victim,
                    playRemoveAnim: false,
                    skipBusyGuard: true,
                    startExplore: false);
            }

            if (victim.TryGetEffectManager(out var effectManager))
            {
                if (!effectManager.IsPlaying)
                {
                    await effectManager.PlayDeathAsync(
                        victimSlot,
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await WaitForEffectIdleAsync(effectManager, cancellationToken);
                }
            }
            else if (victim.Transform != null)
            {
                var removeDuration = fieldManager != null
                    ? fieldManager.LayoutSettings.removeDisappearDuration
                    : 0.25f;
                var initialScale = victim.Transform.localScale;
                await RunViewTweenAsync(
                    CardViewTween.ScaleDisappear(
                        victim.Transform,
                        initialScale,
                        removeDuration),
                    cancellationToken);
            }

            if (victim.Transform != null)
            {
                CardManagerSingleton.Instance?.Release(victim.Uid);
            }
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

        private CancellationTokenSource CreateLinkedBattleCts(CancellationToken external)
        {
            if (_battleCts != null)
            {
                _battleCts.Cancel();
                _battleCts.Dispose();
                _battleCts = null;
            }

            _battleCts = new CancellationTokenSource();
            if (external.CanBeCanceled)
            {
                return CancellationTokenSource.CreateLinkedTokenSource(_battleCts.Token, external);
            }

            return CancellationTokenSource.CreateLinkedTokenSource(_battleCts.Token);
        }

        private void DisposeBattleCts(CancellationTokenSource linked)
        {
            linked?.Dispose();
            if (_battleCts != null)
            {
                _battleCts.Dispose();
                _battleCts = null;
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
