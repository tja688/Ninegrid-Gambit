using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地交战表现执行缝：busy / CTS / 目标解析 / 攻击·反击·致死 Present。
    /// 由 <see cref="NineGrid.Presentation.Systems.FieldBattlePresentationSystem"/> 拥有。
    /// </summary>
    public sealed class FieldBattlePresentationExecutor
    {
        private IFieldBattleView _view;
        private bool _isBusy;
        private CancellationTokenSource _battleCts;

        public bool IsBusy => _isBusy;

        public void Bind(IFieldBattleView view)
        {
            _view = view;
        }

        public void Unbind()
        {
            CancelBattleWork();
            _view = null;
        }

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
            EnsureAdapter()?.ArmNextLethalAttack(armed);
        }

        public bool TryHandleBattleClick(ManagedCard card)
        {
            var geometry = ResolveGeometry();
            var adapter = EnsureAdapter();
            if (card == null || adapter == null || geometry == null)
            {
                LogBattleClickReject(card, "missing-adapter-or-geometry");
                return false;
            }

            // 轴二：攻击目标表面为受保护场地。轴一互斥只认 MainlineBusy（IntentIntake/#51）。
            if (!PresentationInputGates.OwnsProtectedField)
            {
                LogBattleClickReject(card, "not-protected-field-owner=" + PresentationInputGates.CurrentOwner);
                return false;
            }

            if (card.IsFieldDead)
            {
                LogBattleClickReject(card, "field-dead");
                return false;
            }

            if (!geometry.TryGetSlotOf(card.Uid, out var slot)
                || !geometry.IsAvatarOrthogonalBattleSlot(slot))
            {
                LogBattleClickReject(card, "not-ortho-slot");
                return false;
            }

            RegistryTraceSink.NotifyUserInteraction?.Invoke("BattleClick");
            if (AttackInputHook.TrySubmitAttack == null)
            {
                LogBattleClickReject(card, "attack-hook-unwired");
                Debug.LogWarning("[FieldBattle] AttackInputHook.TrySubmitAttack 未装配，交战点击不可用。");
                return false;
            }

            if (!AttackInputHook.TrySubmitAttack(slot))
            {
                LogBattleClickReject(card, "attack-submit-false slot=" + slot);
                return false;
            }

            return true;
        }

        private static void LogBattleClickReject(ManagedCard card, string detail)
        {
            var uid = card != null ? card.Uid : 0;
            var defId = card != null ? card.DefId : "?";
            BoardIntentGateDiagnostics.LogConsole(
                "BattleClick",
                "reject uid=" + uid + " defId=" + defId + " detail=" + detail,
                NineGridArchitecture.Current,
                GameCommandKind.Attack);
        }

        public UniTask RequestBasicAttackAtSlotAsync(
            int victimSlot,
            bool? lethalOverride = null,
            CancellationToken cancellationToken = default)
        {
            if (lethalOverride.HasValue)
            {
                ArmNextLethalAttack(lethalOverride.Value);
            }

            if (AttackInputHook.TrySubmitAttack == null)
            {
                Debug.LogWarning("[FieldBattle] AttackInputHook.TrySubmitAttack 未装配。");
                return UniTask.CompletedTask;
            }

            AttackInputHook.TrySubmitAttack(victimSlot);
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 导演命中批 Present：Core 已 CombatHit；播 lunge/受击/飘字，
        /// 再 Drain 同批 OnBattle 盘面 delta（如逃避 Swap；主目标尸体除外），击杀则 Vacate（不含 Fill/Rotate）。
        /// <paramref name="resolvedCombatUid"/> 必须来自 Resolve 批捕获值，禁止在 Hit 后再 Resolve。
        /// </summary>
        public async UniTask PlayDirectorAttackHitPresentAsync(
            int clickedSlot,
            int resolvedCombatUid,
            PostKillBoardPresentationResult hitProjection,
            CancellationToken cancellationToken = default)
        {
            var geometry = ResolveGeometry();
            var adapter = EnsureAdapter();
            if (adapter == null || geometry == null)
            {
                Debug.LogWarning("[FieldBattle] 导演命中 Present：缺 adapter/geometry。");
                return;
            }

            if (!geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar == null)
            {
                Debug.LogWarning("[FieldBattle] 导演命中 Present：Avatar 不可用。");
                return;
            }

            if (!geometry.TryGetCardAt(clickedSlot, out var clickedVictim) || clickedVictim == null)
            {
                if (resolvedCombatUid > 0
                    && CardEntityLifecycleHook.CardsOrNull() != null
                    && CardEntityLifecycleHook.CardsOrNull().TryGet(resolvedCombatUid, out var resolvedVictim)
                    && resolvedVictim != null)
                {
                    var resolvedSlot = clickedSlot;
                    geometry.TryGetSlotOf(resolvedVictim.Uid, out resolvedSlot);
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
                    Debug.LogWarning($"[FieldBattle] 导演命中 Present：格位 {clickedSlot} 无目标。");
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
                if (CardEntityLifecycleHook.CardsOrNull() == null
                    || !CardEntityLifecycleHook.CardsOrNull().TryGet(combatUid, out combatVictim)
                    || combatVictim == null)
                {
                    Debug.LogWarning($"[FieldBattle] 导演命中 Present：嘲讽目标 uid={combatUid} 不可用。");
                    return;
                }

                if (!geometry.TryGetSlotOf(combatVictim.Uid, out combatSlot))
                {
                    if (!TryResolveDirectorCombatVictim(clickedSlot, hitProjection, out var corpse, out combatSlot)
                        || corpse == null
                        || corpse.Uid != combatUid)
                    {
                        Debug.LogWarning($"[FieldBattle] 导演命中 Present：嘲讽目标不在场地 uid={combatUid}。");
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

        public async UniTask PlayDirectorCounterPresentAsync(
            int attackerSlot,
            int attackerUid,
            PostKillBoardPresentationResult counterProjection,
            CancellationToken cancellationToken = default)
        {
            var geometry = ResolveGeometry();
            var adapter = EnsureAdapter();
            if (adapter == null || geometry == null)
            {
                Debug.LogWarning("[FieldBattle] 导演反击 Present：未装配 adapter/geometry。");
                ResolveBattleSession()?.EnsureBattleEndedIfAvatarDefeated(
                    counterProjection, cancellationToken);
                return;
            }

            if (!counterProjection.Accepted)
            {
                ResolveBattleSession()?.EnsureBattleEndedIfAvatarDefeated(
                    counterProjection, cancellationToken);
                return;
            }

            if (!geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar == null)
            {
                Debug.LogWarning("[FieldBattle] 导演反击 Present：无 Avatar。");
                ResolveBattleSession()?.EnsureBattleEndedIfAvatarDefeated(
                    counterProjection, cancellationToken);
                return;
            }

            ManagedCard attacker = null;
            var resolvedFromUid = CardEntityLifecycleHook.CardsOrNull() != null
                && CardEntityLifecycleHook.CardsOrNull().TryGet(attackerUid, out attacker)
                && attacker != null;
            if (!resolvedFromUid && !TryValidateCounterParticipants(attackerSlot, out attacker))
            {
                Debug.LogWarning($"[FieldBattle] 导演反击 Present：攻击方不可用 slot={attackerSlot} uid={attackerUid}。");
                ResolveBattleSession()?.EnsureBattleEndedIfAvatarDefeated(
                    counterProjection, cancellationToken);
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
                // ADR-0048：命中帧只冲飘字/血甲（触发条件先可见，ADR-0018）；
                // TriggerEffect 留到本批盘面运动落地后由 PresentStep 两相冲刷统一演出。
                BattleBeatHook.NotifyFlushImpactExcept(PresentationInstructionKind.TriggerEffect);
            }

            var holdAcquired = false;
            if (!PresentationMainlineHold.TryAcquire("FieldBattlePresent", out holdAcquired))
            {
                Debug.LogWarning("[FieldBattle] 导演反击 Present：无法获取主线租约，本地 busy 仍继续。");
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

                await adapter.PlayBasicCounterAttackAsync(
                    attacker,
                    bind,
                    ApplyHitFrameVisuals,
                    ct);

                if (!hitFrameApplied)
                {
                    ApplyHitFrameVisuals();
                }

                await DrainCombatHitBoardDeltaFromProjectionAsync(counterProjection, ct);
                await FlushPendingShuffleAsync(ct);
            }
            catch (OperationCanceledException)
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

                if (counterProjection.AvatarDefeated || willKill)
                {
                    ResolveBattleSession()?.EnsureBattleEndedIfAvatarDefeated(
                        counterProjection.AvatarDefeated
                            ? counterProjection
                            : new PostKillBoardPresentationResult
                            {
                                Accepted = true,
                                AvatarDefeated = true,
                                DamagePopups = counterProjection.DamagePopups,
                                Steps = counterProjection.Steps,
                                Moves = counterProjection.Moves,
                                Deals = counterProjection.Deals,
                                RemovedUids = counterProjection.RemovedUids,
                            },
                        ct);
                }

                _isBusy = false;
                PresentationMainlineHold.Release(holdAcquired, "FieldBattlePresent");
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
            var geometry = ResolveGeometry();
            var adapter = EnsureAdapter();
            if (adapter == null || geometry == null)
            {
                return;
            }

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
                // ADR-0048：命中帧只冲飘字/血甲（触发条件先可见，ADR-0018）；
                // TriggerEffect 留到本批盘面运动落地后由 PresentStep 两相冲刷统一演出。
                BattleBeatHook.NotifyFlushImpactExcept(PresentationInstructionKind.TriggerEffect);
            }

            // 神圣决斗惩罚：把本批全部「决斗者脉冲 + 对玩家伤害」指令从当批暂挂隔离，
            // 待各决斗者攻击表演命中帧再按持有者逐个放回报点。
            var duelPunishments = hitProjection.HolyDuelPunishments;
            var duelQuarantined = duelPunishments != null && duelPunishments.Length > 0;
            if (duelQuarantined)
            {
                var avatarUidForDuel = avatar.Uid;
                BattleBeatHook.NotifyQuarantineImpactWhere(
                    instruction => IsHolyDuelPunishmentInstruction(instruction, avatarUidForDuel));
            }

            var holdAcquired = false;
            if (!PresentationMainlineHold.TryAcquire("FieldBattlePresent", out holdAcquired))
            {
                Debug.LogWarning("[FieldBattle] 导演攻击 Present：无法获取主线租约，本地 busy 仍继续。");
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
                    await adapter.PlayTauntRedirectAttackAsync(
                        clickedVictim,
                        combatVictim,
                        attackBind,
                        ApplyHitFrameVisuals,
                        ct);
                }
                else
                {
                    await adapter.PlayBasicAttackAsync(
                        clickedVictim,
                        attackBind,
                        ApplyHitFrameVisuals,
                        ct);
                }

                if (!hitFrameApplied)
                {
                    ApplyHitFrameVisuals();
                }

                // 决斗者攻击表演：惩罚伤害在决斗者命中帧逐个落地（隔离指令按持有者释放）。
                if (duelQuarantined)
                {
                    for (var duelIndex = 0; duelIndex < duelPunishments.Length; duelIndex++)
                    {
                        await PlayDuelPunishmentRigAsync(
                            duelPunishments[duelIndex],
                            avatar,
                            hitProjection,
                            ct);
                    }
                }

                // OnBattle（逃避 Swap 等）与技能移除写在同一 CombatHit EventLog 窗。
                // 主目标存活：正常在 Drain 里重放其位移（Core 已换位而表现占格仍旧 → OccupancyDesync）。
                // 主目标已死：Core 在击杀落地（KillIfDead FollowUp）前，OnBattle 旋转/换位遗物
                // 会真实移动尸体一格——表现侧不重放尸体位移，否则死亡表演中的卡被 hop 拽走
                // （「尸体跟着转」闪烁）。改为先 MarkFieldDead + Vacate + 离锚，再 Drain：
                // Rotate 步只带方向，旋转「无尸体的环」；Move/Swap 步中尸体位移已在
                // ForHitPresentDrain 剥离。终态占格与 Core 击杀后一致。
                var killed = HasRemovedUid(hitProjection, combatVictim.Uid) || combatVictim.IsFieldDead;
                if (killed)
                {
                    // ADR-0050：主目标退场前先打完涉及它的效果打击组（反伤等），
                    // 保证打击与飘字在尸体离锚之前可见。
                    await EffectStrikeHook.NotifyPlayStrikesInvolvingAsync(combatVictim.Uid, ct);
                }

                var hitBoardDelta = BoardPresentationMerge.ForHitPresentDrain(
                    hitProjection,
                    combatVictim.Uid,
                    stripVictimMotion: killed);
                var pendingVacateUids = killed ? new[] { combatVictim.Uid } : null;
                var victimStagedBeforeDrain = false;
                if (!hitProjection.AvatarDefeated && killed)
                {
                    // 尸体位移已剥离：以 Drain 前的表现占格定尸体格（找不到时回退交战格）。
                    if (geometry.TryGetSlotOf(combatVictim.Uid, out var corpseSlot)
                        && corpseSlot > 0)
                    {
                        combatSlot = corpseSlot;
                    }

                    CardEntityLifecycleHook.CardsOrNull()?.MarkFieldDead(combatVictim);
                    geometry.VacateSlotForExplore(
                        combatSlot,
                        combatVictim,
                        playRemoveAnim: false,
                        skipBusyGuard: true,
                        startExplore: false);
                    CardEntityLifecycleHook.CardsOrNull()?.StageFieldDeadCorpseOffAnchor(combatVictim);
                    victimStagedBeforeDrain = true;
                }

                await DrainCombatHitBoardDeltaFromProjectionAsync(
                    hitBoardDelta,
                    ct,
                    pendingVacateUids);

                if (victimStagedBeforeDrain)
                {
                    FinalizeLethalVictimAsync(combatVictim, ct).Forget();
                    ResolveBattleSession()?.AssertHitPresentOccupancySync();
                }

                await FlushPendingShuffleAsync(ct);
            }
            catch (OperationCanceledException)
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

                ResolveBattleSession()?.EnsureBattleEndedIfAvatarDefeated(hitProjection, ct);

                _isBusy = false;
                PresentationMainlineHold.Release(holdAcquired, "FieldBattlePresent");
                SyncAfterCombatRound();
                DisposeBattleCts(linkedCts);
            }
        }

        /// <summary>
        /// 效果打击 Present（ADR-0050）：场上打击者对受击者播一段「攻击动作 + 受击反馈」，
        /// 命中帧回调 <paramref name="onStrikeHit"/>（由调用方冲刷该组暂扣的伤害/血甲指令）。
        /// 打击者不在场 / 参与者不可用时返回 false，调用方降级为普通冲刷。
        /// 注意：本方法可能在攻击/反击 Present 内部（Drain 中）被调用，
        /// 不得创建新的战斗 CTS（会取消外层交战表演），只透传调用方 token。
        /// </summary>
        public async UniTask<bool> PlayEffectStrikePresentAsync(
            int strikerUid,
            int victimUid,
            bool victimWillBeRemoved,
            Action onStrikeHit,
            CancellationToken cancellationToken = default)
        {
            var geometry = ResolveGeometry();
            var adapter = EnsureAdapter();
            var cards = CardEntityLifecycleHook.CardsOrNull();
            if (adapter == null || geometry == null || cards == null)
            {
                return false;
            }

            if (strikerUid <= 0
                || !cards.TryGet(strikerUid, out var striker)
                || striker == null
                || striker.Transform == null
                || striker.IsFieldDead
                || !geometry.TryGetSlotOf(strikerUid, out _))
            {
                return false;
            }

            if (victimUid <= 0
                || !cards.TryGet(victimUid, out var victim)
                || victim == null
                || victim.Transform == null)
            {
                return false;
            }

            var strikerIsAvatar = strikerUid == ResolveAvatarUid(geometry);
            var intent = BattleIntentUtility.FromFlags(counter: !strikerIsAvatar, lethal: false);
            var profileCard = strikerIsAvatar ? victim : striker;
            var resolved = ResolveBindParams(intent, profileCard, out var profile);
            // 效果打击定制：不绑死亡回调（退场由移除/击杀呈现接手）；
            // 受击者将被移除时不回锚，避免「打飞 → 拽回 → 再碎裂」。
            var bind = new BattleBindParams(
                intent,
                resolved.ProfileId,
                resolved.RigFamily,
                useRelativeAttackerMotion: true,
                useRelativeVictimKnockback: true,
                resolved.VictimKnockbackCoefficient,
                bindDeathCallback: false,
                resolved.HitFlashTimingPolicy,
                resolved.HitFlashCallbackDelay,
                deathCallbackDelay: 0f,
                restoreAttackerToSlot: true,
                restoreVictimToSlot: !victimWillBeRemoved,
                requireFinalStateGuard: resolved.RequireFinalStateGuard);
            LogBattleBindResolve(strikerUid, victimUid, bind, profile, victimWillBeRemoved, isCounter: !strikerIsAvatar);

            // adapter 内部可能因参与者/rig 缺失静默跳过；把结果如实上抛，
            // 让编排器按「降级为普通冲刷」处理而不是误当已表演（ADR-0050 补丁）。
            return await adapter.PlayEffectStrikeAsync(striker, victim, bind, onStrikeHit, cancellationToken);
        }

        private static int ResolveAvatarUid(IGroundFieldGeometrySystem geometry)
        {
            return geometry != null
                && geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                && avatar != null
                    ? avatar.Uid
                    : 0;
        }

        /// <summary>
        /// 神圣决斗惩罚攻击表演：决斗持有者以反击 rig 向玩家卡打出惩罚伤害。
        /// 命中帧放回隔离指令并报 Impact（脉冲 + 伤害飘字 + 玩家血条同步落地）。
        /// 决斗者不可用/不邻接等任何失败路径都会放回隔离区兜底（惩罚伤害不得丢失）。
        /// </summary>
        private async UniTask PlayDuelPunishmentRigAsync(
            HolyDuelPunishmentEntry duelPunishment,
            ManagedCard avatar,
            PostKillBoardPresentationResult hitProjection,
            CancellationToken cancellationToken)
        {
            if (duelPunishment.HolderUid <= 0 || duelPunishment.Amount <= 0)
            {
                return;
            }

            var geometry = ResolveGeometry();
            var adapter = EnsureAdapter();
            if (adapter == null || geometry == null || avatar == null || avatar.Transform == null)
            {
                ReleaseDuelQuarantineFallback(duelPunishment.HolderUid, avatar != null ? avatar.Uid : 0);
                return;
            }

            var cards = CardEntityLifecycleHook.CardsOrNull();
            ManagedCard holder;
            if (cards == null
                || !cards.TryGet(duelPunishment.HolderUid, out holder)
                || holder == null
                || holder.Transform == null
                || holder.IsFieldDead
                || !geometry.TryGetSlotOf(holder.Uid, out var holderSlot)
                || !GroundSlotTopology.AreAdjacentEight(holderSlot, GroundSlotTopology.AvatarReservedSlot))
            {
                ReleaseDuelQuarantineFallback(duelPunishment.HolderUid, avatar.Uid);
                return;
            }

            var duelHolderUid = duelPunishment.HolderUid;
            var avatarUid = avatar.Uid;
            var duelHitApplied = false;
            void ApplyDuelHitFrameVisuals()
            {
                if (duelHitApplied)
                {
                    return;
                }

                duelHitApplied = true;
                BattleBeatHook.NotifyReleaseQuarantinedWhere(
                    instruction => IsHolyDuelPunishmentInstructionForHolder(
                        instruction,
                        duelHolderUid,
                        avatarUid));
                BattleBeatHook.NotifyBeat(PresentationBeat.Impact);
            }

            try
            {
                var willDefeatAvatar = hitProjection.AvatarDefeated;
                var duelIntent = BattleIntentUtility.FromFlags(counter: true, willDefeatAvatar);
                var duelBind = ResolveBindParams(duelIntent, holder, out var duelProfile);
                LogBattleBindResolve(holder.Uid, avatar.Uid, duelBind, duelProfile, willDefeatAvatar, isCounter: true);
                await adapter.PlayBasicCounterAttackAsync(holder, duelBind, ApplyDuelHitFrameVisuals, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FieldBattle] 决斗惩罚攻击表演失败: " + ex.Message);
            }
            finally
            {
                if (!duelHitApplied)
                {
                    // rig 未播或未到命中帧（决斗者对角无 rig 等）：放回该持有者隔离区，由后续 FlushBeats 兜底消费。
                    ReleaseDuelQuarantineFallback(duelHolderUid, avatarUid);
                }
            }
        }

        private static void ReleaseDuelQuarantineFallback(int holderUid, int avatarUid)
        {
            if (holderUid <= 0)
            {
                BattleBeatHook.NotifyReleaseQuarantined();
                return;
            }

            BattleBeatHook.NotifyReleaseQuarantinedWhere(
                instruction => IsHolyDuelPunishmentInstructionForHolder(instruction, holderUid, avatarUid));
        }

        /// <summary>
        /// 神圣决斗惩罚指令判定（全持有者）：EffectTriggered 或打向玩家卡的 Impact。
        /// </summary>
        private static bool IsHolyDuelPunishmentInstruction(
            PresentationInstruction instruction,
            int avatarUid)
        {
            var gameEvent = instruction?.Event;
            if (gameEvent == null
                || instruction.MapEntry == null
                || instruction.MapEntry.Beat != PresentationBeat.Impact)
            {
                return false;
            }

            if (gameEvent.Type == CoreEventType.EffectTriggered)
            {
                return string.Equals(gameEvent.Message, "skill.holy_duel.activate", StringComparison.Ordinal);
            }

            return gameEvent.TargetUid == avatarUid
                && string.Equals(gameEvent.SourceDefId, "skill.holy_duel", StringComparison.Ordinal);
        }

        /// <summary>
        /// 神圣决斗惩罚指令判定（单持有者）：用于按人释放隔离区。
        /// </summary>
        private static bool IsHolyDuelPunishmentInstructionForHolder(
            PresentationInstruction instruction,
            int holderUid,
            int avatarUid)
        {
            var gameEvent = instruction?.Event;
            if (gameEvent == null
                || instruction.MapEntry == null
                || instruction.MapEntry.Beat != PresentationBeat.Impact)
            {
                return false;
            }

            if (gameEvent.Type == CoreEventType.EffectTriggered)
            {
                return gameEvent.CardUid == holderUid
                    && string.Equals(gameEvent.Message, "skill.holy_duel.activate", StringComparison.Ordinal);
            }

            var actorUid = gameEvent.ActorUid > 0 ? gameEvent.ActorUid : gameEvent.CardUid;
            return actorUid == holderUid
                && gameEvent.TargetUid == avatarUid
                && string.Equals(gameEvent.SourceDefId, "skill.holy_duel", StringComparison.Ordinal);
        }

        private bool TryResolveDirectorCombatVictim(
            int clickedSlot,
            PostKillBoardPresentationResult hitProjection,
            out ManagedCard victim,
            out int combatSlot)
        {
            victim = null;
            combatSlot = clickedSlot;
            var cards = CardEntityLifecycleHook.CardsOrNull();
            var geometry = ResolveGeometry();
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

                if (geometry != null && geometry.TryGetSlotOf(uid, out combatSlot))
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

        private static async UniTask DrainCombatHitBoardDeltaFromProjectionAsync(
            PostKillBoardPresentationResult projection,
            CancellationToken cancellationToken,
            int[] occupancyPendingVacateUids = null)
        {
            if (!projection.Accepted || IsEmptyBoardProjection(projection))
            {
                return;
            }

            var session = ResolveBattleSession();
            if (session == null)
            {
                return;
            }

            await session.DrainPostKillBoardAsync(
                projection,
                cancellationToken,
                occupancyPendingVacateUids);
        }

        private static async UniTask FlushPendingShuffleAsync(CancellationToken cancellationToken)
        {
            var session = ResolveBattleSession();
            if (session == null)
            {
                return;
            }

            await session.FlushPendingShuffleIntoPresentationAsync(cancellationToken);
        }

        private static bool IsEmptyBoardProjection(PostKillBoardPresentationResult result)
        {
            var stepCount = result.Steps != null ? result.Steps.Length : 0;
            var moveCount = result.Moves != null ? result.Moves.Length : 0;
            var dealCount = result.Deals != null ? result.Deals.Length : 0;
            var removeCount = result.RemovedUids != null ? result.RemovedUids.Length : 0;
            return stepCount == 0 && moveCount == 0 && dealCount == 0 && removeCount == 0;
        }

        private void SyncAfterCombatRound()
        {
            var geometry = ResolveGeometry();
            if (geometry != null && geometry.ConsumeOccupancyConflictFlag())
            {
                ResolveBattleSession()?.RequestSyncBoardFromCore();
            }
        }

        private bool TryValidateCounterParticipants(int attackerSlot, out ManagedCard attacker)
        {
            attacker = null;
            var geometry = ResolveGeometry();
            var adapter = EnsureAdapter();

            if (adapter == null)
            {
                Debug.LogWarning("[FieldBattle] 未配置 CardAttackBasicAdapter。");
                return false;
            }

            if (geometry == null)
            {
                Debug.LogWarning("[FieldBattle] 未绑定 GroundFieldGeometrySystem。");
                return false;
            }

            if (!GroundSlotTopology.AreAdjacentEight(attackerSlot, GroundSlotTopology.AvatarReservedSlot)
                || !geometry.TryGetCardAt(attackerSlot, out attacker))
            {
                Debug.LogWarning($"[FieldBattle] 格位 {attackerSlot} 不可触发怪物反击。");
                return false;
            }

            if (attacker.IsFieldDead)
            {
                Debug.LogWarning($"[FieldBattle] 格位 {attackerSlot} 卡牌已死亡。");
                return false;
            }

            if (!geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar.IsFieldDead)
            {
                Debug.LogWarning("[FieldBattle] Avatar 不可用，无法触发反击。");
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
                $"[FieldBattle] 路由 intent={bind.Intent} profile={profileId}"
                + $" bindDeath={bind.BindDeathCallback} estKill={estimatedWillKill}"
                + $" counter={isCounter} attacker={attackerUid} victim={victimUid}");
        }

        private BattleBindParams ResolveBindParams(
            BattleIntent intent,
            ManagedCard monsterCard,
            out BattleEncounterProfileSO profile)
        {
            var playerId = ResolveAvatarDefId();
            var monsterId = monsterCard != null ? monsterCard.DefId : BattleParticipantIds.Wildcard;
            return BattlePresentationRouter.ResolveBindParams(
                _view?.EncounterCatalog,
                intent,
                playerId,
                monsterId,
                out profile,
                out _);
        }

        private string ResolveAvatarDefId()
        {
            var geometry = ResolveGeometry();
            if (geometry != null
                && geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                && avatar != null
                && !string.IsNullOrWhiteSpace(avatar.DefId))
            {
                return avatar.DefId;
            }

            return BattleParticipantIds.Wildcard;
        }

        public bool TryBeginAvatarDefeatPresentation(CancellationToken cancellationToken = default)
        {
            var geometry = ResolveGeometry();
            if (geometry == null
                || !geometry.TryGetCardAt(GroundSlotTopology.AvatarReservedSlot, out var avatar)
                || avatar == null
                || avatar.IsFieldDead)
            {
                return false;
            }

            return TryBeginLethalVictimPresentation(avatar, cancellationToken);
        }

        public bool TryBeginLethalVictimPresentation(
            ManagedCard victim,
            CancellationToken cancellationToken = default)
        {
            var geometry = ResolveGeometry();
            if (victim == null || geometry == null)
            {
                return false;
            }

            var hadSlot = geometry.TryGetSlotOf(victim.Uid, out var victimSlot);
            CardEntityLifecycleHook.CardsOrNull()?.MarkFieldDead(victim);

            if (hadSlot)
            {
                geometry.VacateSlotForExplore(
                    victimSlot,
                    victim,
                    playRemoveAnim: false,
                    skipBusyGuard: true,
                    startExplore: false);
            }

            CardEntityLifecycleHook.CardsOrNull()?.StageFieldDeadCorpseOffAnchor(victim);

            // Death 必须播：PlayInternal 会 StopCurrent 顶掉未完成 Hit；
            // 旧的 !IsPlaying 守卫会在受击特效未结束时整段跳过退场 FX。
            if (victim.TryGetEffectManager(out var effectManager))
            {
                effectManager.PlayDeathAsync(
                    hadSlot ? victimSlot : 0,
                    cancellationToken: cancellationToken).Forget();
            }

            FinalizeLethalVictimAsync(victim, cancellationToken).Forget();
            return true;
        }

        public async UniTask PresentRemovedFieldCardAsync(
            ManagedCard victim,
            CancellationToken cancellationToken = default)
        {
            var geometry = ResolveGeometry();
            if (victim == null)
            {
                return;
            }

            // 捕熊等同批效果：刚起飞的补牌可能立刻被 Remove；须先取消 dealFlight。
            geometry?.CancelDealFlightForUid(victim.Uid, "PresentRemovedFieldCard");

            var victimSlot = 0;
            var hadSlot = geometry != null
                && geometry.TryGetSlotOf(victim.Uid, out victimSlot);

            CardEntityLifecycleHook.CardsOrNull()?.MarkFieldDead(victim);

            if (hadSlot)
            {
                geometry.VacateSlotForExplore(
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
                    // 击杀同步路径可能已 Forget 播 Death；此处只等到主通道空闲。
                    await WaitForEffectIdleAsync(effectManager, cancellationToken);
                }
            }
            else if (victim.Transform != null)
            {
                CardLifecycleAudioCues.Pulse(
                    CardLifecycleAudioCues.Exit,
                    "FieldBattlePresentationExecutor.PresentRemovedFieldCardAsync",
                    victim.DefId);
                var removeDuration = geometry != null
                    ? geometry.LayoutSettings.removeDisappearDuration
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
                // 引用身份保护：禁止 await 后仅凭可复用 uid 释放新实体。
                CardEntityLifecycleHook.CardsOrNull()?.Release(victim, "Combat.CompleteRemoveVictim");
            }
        }

        private async UniTask FinalizeLethalVictimAsync(ManagedCard card, CancellationToken cancellationToken)
        {
            if (card == null)
            {
                return;
            }

            var geometry = ResolveGeometry();
            if (card.TryGetEffectManager(out var effectManager))
            {
                await WaitForEffectIdleAsync(effectManager, cancellationToken);
            }
            else if (card.Transform != null)
            {
                var removeDuration = geometry != null
                    ? geometry.LayoutSettings.removeDisappearDuration
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
                // 引用身份保护：禁止 await 后仅凭可复用 uid 释放新实体。
                CardEntityLifecycleHook.CardsOrNull()?.Release(card, "Combat.FinalizeLethal");
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

        private CardAttackBasicAdapter EnsureAdapter()
        {
            _view?.EnsureAttackAdapter();
            return _view?.AttackAdapter;
        }

        private static IGroundFieldGeometrySystem ResolveGeometry()
        {
            return NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>();
        }

        private static IBattleSessionSystem ResolveBattleSession()
        {
            return NineGridArchitecture.Interface?.GetSystem<IBattleSessionSystem>()
                   ?? NineGridArchitecture.Current?.GetSystem<IBattleSessionSystem>();
        }
    }
}
