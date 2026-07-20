using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地交战管理器单例：Intent → Catalog 路由 → Adapter 播 Rig；
    /// 命中帧经 CombatHitSink 写 Core，再抓 Model 刷血/飘字；
    /// 击杀后 Core 一次结算，表现缓冲按 Moved/Dealt 缓释。
    /// 净土域（纯战斗黑盒）：仅暴露 C 阶段 Evict/Admit（速度恒 0）。
    /// </summary>
    public sealed class FieldBattleManagerSingleton : MonoBehaviour, IHandoffEndpoint
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

        /// <summary>
        /// 净土域（纯战斗）C 阶段交接：速度恒填 0。
        /// </summary>
        public HandoffState Evict() => HandoffState.AtRest(Vector3.zero);

        /// <summary>净土域 C 阶段：承接位置，忽略速度；内部战斗黑盒不动。</summary>
        public void Admit(in HandoffState state)
        {
        }

        /// <summary>单卡离开纯战斗域：C 阶段速度恒 0。</summary>
        public HandoffState EvictCard(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return HandoffState.AtRest(Vector3.zero);
            }

            return HandoffState.AtRest(card.Transform.localPosition);
        }

        /// <summary>单卡进入纯战斗域：C 阶段忽略速度。</summary>
        public void AdmitCard(ManagedCard card, in HandoffState state)
        {
            if (card?.Transform == null)
            {
                return;
            }

            card.Transform.localPosition = state.LocalPosition;
        }

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
                || CombatHitSink.BoardSelectModeActive
                || CombatHitSink.DirectorMainlineBusy
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

            RegistryTraceSink.NotifyUserInteraction?.Invoke("BattleClick");
            // 已迁导演：提交攻击意图（忙时由导演缓冲）；不再走旧 RequestBasicAttack 编排。
            return CombatHitSink.RequestAttackIntent(slot);
        }

        /// <summary>
        /// #11 硬切：旧进攻编排已删。提交导演攻击意图后立即返回（不等待主线播完）。
        /// 若需等表演结束，请轮询 <see cref="CombatHitSink.DirectorMainlineBusy"/>。
        /// <paramref name="lethalOverride"/> 仍经 <see cref="ArmNextLethalAttack"/> 注入下一击。
        /// </summary>
        public UniTask RequestBasicAttackAtSlotAsync(
            int victimSlot,
            bool? lethalOverride = null,
            CancellationToken cancellationToken = default)
        {
            if (lethalOverride.HasValue)
            {
                ArmNextLethalAttack(lethalOverride.Value);
            }

            CombatHitSink.RequestAttackIntent(victimSlot);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 导演命中批 Present：Core 已 CombatHit；此处只播 lunge/受击/飘字，击杀则 Vacate（不含 Fill/Rotate）。
        /// <paramref name="resolvedCombatUid"/> 必须来自 Resolve 批捕获值，禁止在 Hit 后再 Resolve（致死会卸嘲讽规则）。
        /// </summary>
        public async UniTask PlayDirectorAttackHitPresentAsync(
            int clickedSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult hitProjection,
            CancellationToken cancellationToken = default)
        {
            ResolveFieldManager();
            ResolveAttackAdapter();

            if (attackAdapter == null || fieldManager == null)
            {
                Debug.LogWarning("[FieldBattleManager] 导演命中 Present：缺 adapter/field。");
                return;
            }

            if (!fieldManager.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar == null)
            {
                Debug.LogWarning("[FieldBattleManager] 导演命中 Present：Avatar 不可用。");
                return;
            }

            if (!fieldManager.TryGetCardAt(clickedSlot, out var clickedVictim) || clickedVictim == null)
            {
                // 点击格已空：优先用 Resolve 批捕获的战斗目标（嘲讽致死常见），否则搜投影死尸。
                if (resolvedCombatUid > 0
                    && CardManagerSingleton.Instance != null
                    && CardManagerSingleton.Instance.TryGet(resolvedCombatUid, out var resolvedVictim)
                    && resolvedVictim != null)
                {
                    var resolvedSlot = clickedSlot;
                    fieldManager.TryGetSlotOf(resolvedVictim.Uid, out resolvedSlot);
                    await PlayDirectorAttackHitCoreAsync(
                        resolvedVictim,
                        resolvedVictim,
                        resolvedSlot,
                        avatar,
                        hitProjection,
                        useTauntRedirect: false,
                        cancellationToken);
                    return;
                }

                if (!TryResolveDirectorCombatVictim(clickedSlot, hitProjection, out clickedVictim, out var combatSlotFallback))
                {
                    Debug.LogWarning($"[FieldBattleManager] 导演命中 Present：格位 {clickedSlot} 无目标。");
                    return;
                }

                await PlayDirectorAttackHitCoreAsync(
                    clickedVictim,
                    clickedVictim,
                    combatSlotFallback,
                    avatar,
                    hitProjection,
                    useTauntRedirect: false,
                    cancellationToken);
                return;
            }

            DirectorAttackPresentTargeting.Decide(
                clickedVictim.Uid,
                resolvedCombatUid,
                out var combatUid,
                out var useTauntRedirect);
            ManagedCard combatVictim = clickedVictim;
            var combatSlot = clickedSlot;
            if (useTauntRedirect)
            {
                if (CardManagerSingleton.Instance == null
                    || !CardManagerSingleton.Instance.TryGet(combatUid, out combatVictim)
                    || combatVictim == null)
                {
                    Debug.LogWarning($"[FieldBattleManager] 导演命中 Present：嘲讽目标 uid={combatUid} 不可用。");
                    return;
                }

                if (!fieldManager.TryGetSlotOf(combatVictim.Uid, out combatSlot))
                {
                    // Core 已清格时表现层卡可能仍在；尽量用投影 RemovedUids 找回槽位。
                    if (!TryResolveDirectorCombatVictim(clickedSlot, hitProjection, out var corpse, out combatSlot)
                        || corpse == null
                        || corpse.Uid != combatUid)
                    {
                        Debug.LogWarning($"[FieldBattleManager] 导演命中 Present：嘲讽目标不在场地 uid={combatUid}。");
                        return;
                    }

                    combatVictim = corpse;
                }
            }

            await PlayDirectorAttackHitCoreAsync(
                clickedVictim,
                combatVictim,
                combatSlot,
                avatar,
                hitProjection,
                useTauntRedirect,
                cancellationToken);
        }

        /// <summary>
        /// 导演反击 Present：只播 Rig/Sync/飘字/盘面 delta；Core 命中已在 ResolveBatch 完成。
        /// </summary>
        public async UniTask PlayDirectorCounterPresentAsync(
            int attackerSlot,
            int attackerUid,
            PostKillBoardPresentationResult counterProjection,
            CancellationToken cancellationToken = default)
        {
            ResolveFieldManager();
            if (attackAdapter == null || fieldManager == null)
            {
                Debug.LogWarning("[FieldBattleManager] 导演反击 Present：未装配 attackAdapter/fieldManager。");
                return;
            }

            if (!counterProjection.Accepted)
            {
                return;
            }

            if (!fieldManager.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar == null)
            {
                Debug.LogWarning("[FieldBattleManager] 导演反击 Present：无 Avatar。");
                return;
            }

            ManagedCard attacker = null;
            var resolvedFromUid = CardManagerSingleton.Instance != null
                && CardManagerSingleton.Instance.TryGet(attackerUid, out attacker)
                && attacker != null;
            if (!resolvedFromUid && !TryValidateCounterParticipants(attackerSlot, out attacker))
            {
                Debug.LogWarning($"[FieldBattleManager] 导演反击 Present：攻击方不可用 slot={attackerSlot} uid={attackerUid}。");
                return;
            }

            var linkedCts = CreateLinkedBattleCts(cancellationToken);
            var ct = linkedCts.Token;
            var willKill = counterProjection.AvatarDefeated
                || HasRemovedUid(counterProjection, avatar.Uid)
                || avatar.IsFieldDead;
            var intent = BattleIntentUtility.FromFlags(counter: true, willKill);
            var bind = ResolveBindParams(intent, attacker, out var profile);
            LogBattleBindResolve(attacker.Uid, avatar.Uid, bind, profile, willKill, isCounter: true);

            var hitFrameApplied = false;
            void ApplyHitFrameVisuals()
            {
                if (hitFrameApplied)
                {
                    return;
                }

                hitFrameApplied = true;
                CombatHitSink.RequestSyncCard(avatar);
                SpawnDamagePopups(counterProjection.DamagePopups, avatar, 0);
            }

            _isBusy = true;
            try
            {
                try
                {
                    PerfTraceSink.OpenBeat?.Invoke("CombatHit", 0);
                    PerfTraceSink.SetCombatants?.Invoke(attacker.Uid, avatar.Uid);
                }
                catch
                {
                    // ignore
                }

                await attackAdapter.PlayBasicCounterAttackAsync(
                    attacker,
                    bind,
                    ApplyHitFrameVisuals,
                    ct);

                if (!hitFrameApplied)
                {
                    ApplyHitFrameVisuals();
                }

                await DrainCombatHitBoardDeltaFromProjectionAsync(counterProjection, ct);
                await CombatHitSink.RequestFlushPendingShuffleIntoPresentation(ct);

                if (counterProjection.AvatarDefeated || willKill)
                {
                    TryBeginAvatarDefeatPresentation(ct);
                    CombatHitSink.RequestBattleEnded(victory: false);
                }
            }
            catch (System.OperationCanceledException)
            {
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
                SyncAfterCombatRound();
                DisposeBattleCts(linkedCts);
            }
        }

        private async UniTask PlayDirectorAttackHitCoreAsync(
            ManagedCard clickedVictim,
            ManagedCard combatVictim,
            int combatSlot,
            ManagedCard avatar,
            PostKillBoardPresentationResult hitProjection,
            bool useTauntRedirect,
            CancellationToken cancellationToken)
        {
            var linkedCts = CreateLinkedBattleCts(cancellationToken);
            var ct = linkedCts.Token;

            var willKill = HasRemovedUid(hitProjection, combatVictim.Uid)
                || combatVictim.IsFieldDead
                || hitProjection.NodeClearedOrRewardPhase;
            var attackIntent = BattleIntentUtility.FromFlags(counter: false, willKill);
            var attackBind = ResolveBindParams(attackIntent, combatVictim, out var attackProfile);
            LogBattleBindResolve(avatar.Uid, combatVictim.Uid, attackBind, attackProfile, willKill, isCounter: false);

            var hitFrameApplied = false;
            void ApplyHitFrameVisuals()
            {
                if (hitFrameApplied)
                {
                    return;
                }

                hitFrameApplied = true;
                CombatHitSink.RequestSyncCard(combatVictim);
                SpawnDamagePopups(hitProjection.DamagePopups, combatVictim, 0);
            }

            _isBusy = true;
            try
            {
                try
                {
                    PerfTraceSink.OpenBeat?.Invoke("CombatHit", 0);
                    PerfTraceSink.SetCombatants?.Invoke(avatar.Uid, combatVictim.Uid);
                }
                catch
                {
                    // ignore
                }

                if (useTauntRedirect)
                {
                    await attackAdapter.PlayTauntRedirectAttackAsync(
                        clickedVictim,
                        combatVictim,
                        attackBind,
                        ApplyHitFrameVisuals,
                        ct);
                }
                else
                {
                    await attackAdapter.PlayBasicAttackAsync(
                        clickedVictim,
                        attackBind,
                        ApplyHitFrameVisuals,
                        ct);
                }

                if (!hitFrameApplied)
                {
                    ApplyHitFrameVisuals();
                }

                if (hitProjection.AvatarDefeated)
                {
                    await DrainCombatHitBoardDeltaFromProjectionAsync(hitProjection, ct);
                    await CombatHitSink.RequestFlushPendingShuffleIntoPresentation(ct);
                    TryBeginAvatarDefeatPresentation(ct);
                    CombatHitSink.RequestBattleEnded(victory: false);
                    return;
                }

                var killed = HasRemovedUid(hitProjection, combatVictim.Uid) || combatVictim.IsFieldDead;
                if (killed)
                {
                    // 导演路径：击杀只做 lunge / 尸体离锚；不再嵌套 DrainPostKillBoard。
                    // Fill/Rotate 由导演后续 Present 驱动，避免 Hit+板面双重 Drain 挂死主线。
                    CardManagerSingleton.Instance?.MarkFieldDead(combatVictim);
                    fieldManager.VacateSlotForExplore(
                        combatSlot,
                        combatVictim,
                        playRemoveAnim: false,
                        skipBusyGuard: true,
                        startExplore: false);
                    CardManagerSingleton.Instance?.StageFieldDeadCorpseOffAnchor(combatVictim);
                    FinalizeLethalVictimAsync(combatVictim, ct).Forget();
                }

                // 未击杀也不在 Hit 内嵌套盘面 Drain；盘面 delta 走后续 Fill/Rotate Present。
                // 洗回（含散架爆开）与击杀同拍，不等到 Fill。
                await CombatHitSink.RequestFlushPendingShuffleIntoPresentation(ct);
            }
            catch (System.OperationCanceledException)
            {
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
                SyncAfterCombatRound();
                DisposeBattleCts(linkedCts);
            }
        }

        private bool TryResolveDirectorCombatVictim(
            int clickedSlot,
            PostKillBoardPresentationResult hitProjection,
            out ManagedCard victim,
            out int combatSlot)
        {
            victim = null;
            combatSlot = clickedSlot;
            var cards = CardManagerSingleton.Instance;
            if (cards == null || hitProjection.RemovedUids == null)
            {
                return false;
            }

            for (var i = 0; i < hitProjection.RemovedUids.Length; i++)
            {
                var uid = hitProjection.RemovedUids[i];
                if (uid <= 0 || !cards.TryGet(uid, out victim) || victim == null)
                {
                    continue;
                }

                if (fieldManager != null && fieldManager.TryGetSlotOf(uid, out combatSlot))
                {
                    return true;
                }

                combatSlot = clickedSlot;
                return true;
            }

            return false;
        }

        private static bool HasRemovedUid(PostKillBoardPresentationResult result, int uid)
        {
            if (uid <= 0 || result.RemovedUids == null)
            {
                return false;
            }

            for (var i = 0; i < result.RemovedUids.Length; i++)
            {
                if (result.RemovedUids[i] == uid)
                {
                    return true;
                }
            }

            return false;
        }

        private async UniTask DrainCombatHitBoardDeltaFromProjectionAsync(
            PostKillBoardPresentationResult projection,
            CancellationToken cancellationToken)
        {
            if (!projection.Accepted || IsEmptyBoardProjection(projection))
            {
                return;
            }

            await CombatHitSink.RequestDrainPostKillBoard(projection, cancellationToken);
        }

        private static bool IsEmptyBoardProjection(PostKillBoardPresentationResult result)
        {
            var stepCount = result.Steps != null ? result.Steps.Length : 0;
            var moveCount = result.Moves != null ? result.Moves.Length : 0;
            var dealCount = result.Deals != null ? result.Deals.Length : 0;
            var removeCount = result.RemovedUids != null ? result.RemovedUids.Length : 0;
            return stepCount == 0 && moveCount == 0 && dealCount == 0 && removeCount == 0;
        }

        /// <summary>
        /// #10：交战收尾若几何登记冲突，经 sink 触发对账断言（禁止静默 Sync 修补）。
        /// </summary>
        private void SyncAfterCombatRound()
        {
            ResolveFieldManager();
            if (fieldManager != null && fieldManager.ConsumeOccupancyConflictFlag())
            {
                CombatHitSink.RequestSyncBoardFromCore();
            }
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
        /// 玩家战败：对齐怪物击杀卸尸（MarkFieldDead → Vacate → 播死 → Release）。
        /// </summary>
        public bool TryBeginAvatarDefeatPresentation(CancellationToken cancellationToken = default)
        {
            ResolveFieldManager();
            if (fieldManager == null
                || !fieldManager.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar == null
                || avatar.IsFieldDead)
            {
                return false;
            }

            return TryBeginLethalVictimPresentation(avatar, cancellationToken);
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
                CardManagerSingleton.Instance?.Release(victim.Uid, "Combat.CompleteRemoveVictim");
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
                CardManagerSingleton.Instance.Release(card.Uid, "Combat.FinalizeLethal");
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
