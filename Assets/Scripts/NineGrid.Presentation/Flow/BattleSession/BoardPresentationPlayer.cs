using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// 盘面表现执行：Drain / Deal / Move / Rotate / Fusion / Shuffle / 技能移除卡的收口。
    /// 持有 drain-in-flight、洗回 sink/scheduler 与融合状态；场景宿主经会话视图/Hook 解析。
    /// </summary>
    internal sealed class BoardPresentationPlayer
    {
        private readonly BattleSessionExecutor _session;

        private int _boardPresentationRequestId;
        private bool _pendingSyncFromCore;
        private Transform _shuffleOriginScratch;
        private readonly ShuffleIntoDeckPresentSink _shuffleIntoSink = new ShuffleIntoDeckPresentSink();
        private readonly ShuffleIntoDeckScheduler _shuffleIntoScheduler = new ShuffleIntoDeckScheduler();
        private readonly Dictionary<int, HashSet<int>> _pendingFusionRemoves = new();
        private readonly HashSet<int> _completedFusionActionIds = new();

        public BoardPresentationPlayer(BattleSessionExecutor session)
        {
            _session = session;
        }

        private CardManagerSingleton Cards => _session.Cards;
        private CardDeckManagerSingleton Deck => _session.Deck;
        private GroundFieldView Field => _session.Field;
        private CardHandManagerSingleton Hand => _session.Hand;

        public void ClearShuffleSink()
        {
            _shuffleIntoSink.Clear();
        }

        /// <summary>
        /// #11 硬切后的 Present 薄适配：直接播盘面 delta，不再经 BoardPresentationQueue 泵。
        /// 导演主线已串行 Present；外部薄适配经主线租约持忙。
        /// </summary>
        public async UniTask DrainPostKillBoardAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken)
        {
            if (!result.Accepted)
            {
                return;
            }

            while (_session.DrainInFlight)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }

            var acquiredHere = false;
            if (!PresentationInputGates.HasExternalHold)
            {
                if (!PresentationInputGates.TryBeginExternalHold("BoardPresentDrain"))
                {
                    Debug.LogWarning("[InBattleManager] 盘面 Present 无法获取表现锁，跳过。");
                    return;
                }

                acquiredHere = true;
            }

            _session.DrainInFlight = true;
            ChoreoTraceContext.DrainInFlight = true;
            ChoreoTraceContext.PumpRunning = false;
            ChoreoTraceContext.BoardQueueDepth = 0;
            try
            {
                await DrainPostKillBoardCoreAsync(result, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[InBattleManager] 盘面 Present 失败: " + ex.Message);
                _pendingSyncFromCore = true;
            }
            finally
            {
                _session.DrainInFlight = false;
                ChoreoTraceContext.DrainInFlight = false;
                if (acquiredHere)
                {
                    PresentationInputGates.EndExternalHold("BoardPresentDrain");
                }

                FlushDeferredBoardSync(force: true);
            }
        }

        /// <summary>
        /// 单条盘面 delta 缓释：保序步骤流逐步 await，或回退扁平 Deals/Moves。
        /// 位移类 Step 经 <see cref="BoardMotionStepScheduler"/> 供给执行层；就位由五次收敛 + 栅栏保证，无硬 snap。
        /// 锁由队列泵或外层交战流程持有，本方法不再重复加解锁。
        /// </summary>
        private async UniTask DrainPostKillBoardCoreAsync(
            PostKillBoardPresentationResult result,
            CancellationToken cancellationToken)
        {
            if (!result.Accepted)
            {
                return;
            }

            var stepCount = result.Steps?.Length ?? 0;
            var moveCount = stepCount > 0 ? stepCount : (result.Moves?.Length ?? 0);
            var dealCount = result.Deals?.Length ?? 0;
            var requestId = ++_boardPresentationRequestId;
            var drainNode = 0;
            int.TryParse(FieldTraceHelper.ResolveNodeIndex(), out drainNode);
            PerfTraceRecorder.OpenBeat(DiagBeatKinds.PostKillDrain, drainNode);
            OperationCanceledException pendingCancel = null;
            var ranDrainBody = false;
            try
            {
                // 生命周期清场会 CancelPresentationWork；未传可取消 token 时挂到局内 CTS。
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    _session.EnsurePresentationToken(),
                    cancellationToken.CanBeCanceled ? cancellationToken : CancellationToken.None);
                var ct = linkedCts.Token;

                var fieldManager = Field;
                if (fieldManager == null || Cards == null || Deck == null)
                {
                    Debug.LogError("[InBattleManager] DrainPostKillBoard 缺少 Card/Deck/Field 管理器。");
                }
                else
                {
                    ranDrainBody = true;
                    try
                    {
                        var fusionState = BuildFusionDrainState(result, _session.NodeEventLogStart);
                        PurgeFusionResultsFromShuffleQueue(fusionState);
                        await FlushPendingShuffleIntoPresentationAsync(ct);
                        FieldTraceHelper.RecordDrainBegin(
                            moveCount,
                            dealCount,
                            drainInFlight: true,
                            fieldBusy: fieldManager.IsBusy,
                            presentationLocked: PresentationInputGates.HasExternalHold,
                            stepCount: stepCount,
                            requestId: requestId);
                        FieldTraceHelper.RecordOccupancySnapshot("drainBefore");
                        fieldManager.ClearOccupancyConflictFlag();

                        if (stepCount > 0)
                        {
                            await DrainBoardStepsAsync(result.Steps, requestId, fusionState, ct);
                        }
                        else
                        {
                            FieldTraceHelper.RecordDrainLegacyFallback(
                                requestId,
                                moveCount,
                                dealCount,
                                result.RemovedUids != null ? result.RemovedUids.Length : 0);
                            await DrainLegacyBoardDeltaAsync(result, ct);
                        }

                        // #7 / #9 / V4：融合与 drain 补牌只经导演 Scheduler 离散批次；
                        // 禁止 Present 内 presentAdapter in-flight FillEmptySlots。
                        PresentationOutputProjector.SpawnDamagePopups(result.DamagePopups, fallbackVictim: null, fallbackAmount: 0);
                        PresentationOutputProjector.UpdateAvatarDebugText();

                        if (result.NodeClearedOrRewardPhase)
                        {
                            _session.TryEnterNodeSettlement();
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        ChoreoTraceContext.ForceCloseOpenChoreos("boardDrainCancelled");
                        pendingCancel = ex;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("[InBattleManager] DrainPostKillBoard 主体失败: " + ex.Message);
                    }
                }
            }
            finally
            {
                PerfTraceRecorder.CloseBeat();
            }

            // #10：Drain 尾部不再 force Sync 自愈；冲突只断言+诊断。就位栅栏仍等齐飞牌。
            if (ranDrainBody)
            {
                try
                {
                    var fieldManager = Field;
                    if (fieldManager != null)
                    {
                        await fieldManager.WaitAllActiveDealFlightsAsync(CancellationToken.None);
                        if (fieldManager.HasOccupancyConflictSinceClear)
                        {
                            BattleSessionExecutor.AssertOccupancySyncForbidden("drainOccupancyConflict", "force");
                            await fieldManager.WaitAllActiveDealFlightsAsync(CancellationToken.None);
                        }

                        fieldManager.RefreshSlotHitColliders();
                        FieldTraceHelper.RecordOccupancySnapshot("drainAfter");
                        FieldTraceHelper.RecordDrainEnd(
                            moveCount,
                            dealCount,
                            drainInFlight: true,
                            fieldBusy: fieldManager.IsBusy,
                            presentationLocked: PresentationInputGates.HasExternalHold,
                            stepCount: stepCount,
                            requestId: requestId);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[InBattleManager] Drain 尾部 DrainEnd 失败: " + ex.Message);
                    BattleSessionExecutor.AssertOccupancySyncForbidden("drainTailFailure", ex.Message);
                }
            }

            if (pendingCancel != null)
            {
                throw pendingCancel;
            }
        }

        private async UniTask DrainLegacyBoardDeltaAsync(
            PostKillBoardPresentationResult result,
            CancellationToken ct)
        {
            if (result.Deals != null && result.Deals.Length > 0)
            {
                await DrainDealsAsync(result.Deals, ct);
            }

            if (result.RemovedUids != null && result.RemovedUids.Length > 0)
            {
                await PresentSkillRemovedCardsAsync(result.RemovedUids, ct);
            }

            if (result.Moves != null && result.Moves.Length > 0)
            {
                await Field.ApplyBoardMovesAndHopAsync(
                    result.Moves,
                    ct,
                    skipBusyGuard: true);
            }
        }

        private async UniTask DrainBoardStepsAsync(
            BoardPresentationStep[] steps,
            int requestId,
            FusionDrainState fusionState,
            CancellationToken ct)
        {
            _pendingFusionRemoves.Clear();
            _completedFusionActionIds.Clear();

            var fieldManager = Field;
            var rotateCount = 0;
            var choreoRotateCount = 0;
            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                if (step.Kind == BoardPresentationStepKind.Rotate)
                {
                    rotateCount++;
                }

                FieldTraceHelper.RecordBoardStepBegin(
                    requestId,
                    i,
                    steps.Length,
                    step);

                try
                {
                    ct.ThrowIfCancellationRequested();
                    switch (step.Kind)
                    {
                        case BoardPresentationStepKind.Rotate:
                            var choreoBefore = ChoreoTraceContext.LastChoreoSeqId;
                            await BoardMotionStepScheduler.ExecuteMotionStepAsync(fieldManager, step, ct);
                            if (ChoreoTraceContext.LastChoreoSeqId != choreoBefore)
                            {
                                choreoRotateCount++;
                            }

                            break;

                        case BoardPresentationStepKind.Swap:
                        case BoardPresentationStepKind.Move:
                            if (step.Moves != null && step.Moves.Length > 0)
                            {
                                if (step.Kind == BoardPresentationStepKind.Move
                                    && step.Moves.Length >= GroundSlotTopology.ClockwiseRing.Count)
                                {
                                    FieldTraceHelper.RecordBoardStepFallbackGeneralHop(
                                        requestId,
                                        i,
                                        step.Moves.Length);
                                }

                                await BoardMotionStepScheduler.ExecuteMotionStepAsync(fieldManager, step, ct);
                            }

                            break;

                        case BoardPresentationStepKind.Deal:
                            if (step.Deals != null && step.Deals.Length > 0)
                            {
                                await DrainDealsAsync(step.Deals, ct, steps, i + 1);
                            }

                            break;

                        case BoardPresentationStepKind.Remove:
                            if (step.RemovedUids != null && step.RemovedUids.Length > 0)
                            {
                                if (!await TryHandleFusionRemoveStepAsync(fusionState, step, ct))
                                {
                                    await PresentSkillRemovedCardsAsync(step.RemovedUids, ct);
                                }
                            }

                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        $"[InBattleManager] BoardStep 失败 request={requestId} i={i} kind={step.Kind}: {ex.Message}");
                    FieldTraceHelper.RecordBoardStepFail(requestId, i, step, ex);
                }
                finally
                {
                    FieldTraceHelper.RecordBoardStepEnd(
                        requestId,
                        i,
                        steps.Length,
                        step,
                        ChoreoTraceContext.CurrentSeqId);
                }
            }

            if (rotateCount > 0 && rotateCount != choreoRotateCount)
            {
                FieldTraceHelper.RecordBoardRotateChoreoMismatch(
                    requestId,
                    rotateCount,
                    choreoRotateCount);
            }
        }

        private async UniTask DrainDealsAsync(
            PostKillCardDeal[] deals,
            CancellationToken ct,
            BoardPresentationStep[] steps = null,
            int rotateScanStart = 0)
        {
            await FlushPendingShuffleIntoPresentationAsync(ct);

            var deckManager = Deck;
            var fieldManager = Field;
            var cardManager = Cards;
            var dealInterval = deckManager != null && deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.dealInterval
                : 0.05f;
            var flightHandles = new List<DealFlightHandle>();
            var rotateDirs = new List<bool>(4);
            DealVisualTargetResolver.CollectPendingRotateDirections(steps, rotateScanStart, rotateDirs);
            var pendingRotateSteps = rotateDirs.Count;

            for (var i = 0; i < deals.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                var deal = deals[i];
                if (deal.Uid <= 0 || deal.Slot <= 0)
                {
                    continue;
                }

                var visualTargetSlot = DealVisualTargetResolver.ApplyRingSteps(deal.Slot, rotateDirs);

                if (fieldManager.TryGetCardAt(deal.Slot, out var already)
                    && already != null
                    && already.Uid == deal.Uid)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(already);
                    continue;
                }

                if (visualTargetSlot != deal.Slot
                    && fieldManager.TryGetCardAt(visualTargetSlot, out already)
                    && already != null
                    && already.Uid == deal.Uid)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(already);
                    continue;
                }

                var hand = Hand;
                if (hand != null && hand.ContainsUid(deal.Uid))
                {
                    Debug.LogWarning(
                        $"[InBattleManager] Drain 跳过补牌：uid={deal.Uid} 已在手牌，目标格 {deal.Slot}");
                    continue;
                }

                var ensureCard = ResolveOrSpawnDeckCardForDeal(deal);
                var flightContext = new DealFlightContext(
                    fieldManager.IsFieldBusy,
                    fieldManager.ActiveDealFlightCount + flightHandles.Count + 1,
                    pendingRotateSteps,
                    visualTargetSlot);
                var (ok, handle) = await deckManager.DealCardByUidWithFlightAsync(
                    deal.Uid,
                    deal.Slot,
                    ensureCard: ensureCard,
                    skipBusyGuard: true,
                    flightContext: flightContext,
                    cancellationToken: ct);
                if (ok)
                {
                    if (handle != null)
                    {
                        flightHandles.Add(handle);
                    }

                    if (cardManager.TryGet(deal.Uid, out var dealt))
                    {
                        CoreCardPresentationMapper.ApplyToManagedCard(dealt);
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"[InBattleManager] 补牌发牌失败 uid={deal.Uid} slot={deal.Slot}，留给安全网对齐。");
                }

                if (i < deals.Length - 1 && dealInterval > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(dealInterval),
                        cancellationToken: ct);
                }
            }
        }

        /// <summary>
        /// 为补牌准备可入组的视图：已在卡组则返回 null；否则复用游离视图或 Spawn 新视图（CardDeckMode）。
        /// </summary>
        private ManagedCard ResolveOrSpawnDeckCardForDeal(PostKillCardDeal deal)
        {
            var deckManager = Deck;
            if (deckManager != null && deckManager.ContainsUid(deal.Uid))
            {
                return null;
            }

            var hand = Hand;
            if (hand != null && hand.ContainsUid(deal.Uid))
            {
                return null;
            }

            var cardManager = Cards;
            if (cardManager != null && cardManager.TryGet(deal.Uid, out var existing) && existing != null)
            {
                if (existing.DisplayMode == CardDisplayMode.HandCardMode
                    || existing.DisplayMode == CardDisplayMode.DragCardMode)
                {
                    return null;
                }

                // 僵尸句柄（无 View）不可入组发牌；Release 后强制 SpawnView。
                if (existing.View == null || existing.Transform == null)
                {
                    Debug.LogWarning(
                        $"[InBattleManager] ResolveOrSpawnDeckCardForDeal uid={deal.Uid} View 为空，Release 后重 Spawn。");
                    cardManager.Release(existing, "Deal.NullViewRespawn");
                }
                else
                {
                    return existing;
                }
            }

            if (cardManager == null)
            {
                return null;
            }

            var defId = string.IsNullOrEmpty(deal.DefId)
                ? CardManagerSingleton.StandardDefId
                : deal.DefId;
            var view = cardManager.SpawnView(
                deal.Uid,
                defId,
                initialMode: CardDisplayMode.CardDeckMode,
                kind: CoreCardPresentationMapper.ResolvePresentationKind(deal.Uid, defId));
            if (view != null)
            {
                CoreCardPresentationMapper.ApplyToManagedCard(view);
            }

            return view;
        }

        /// <summary>
        /// 扫描局内洗入事件并入导演 sink；仅由 Present 前缀 / Drain Flush 播出，禁止 Forget 旁路泵。
        /// </summary>
        public void PresentShuffleIntoDeckFromEventLog(int startIndex)
        {
            if (startIndex < 0)
            {
                return;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return;
            }

            var deckManager = Deck;
            if (deckManager == null || Cards == null || deckManager.CurrentMode != CardDeckMode.InGame)
            {
                return;
            }

            _shuffleIntoScheduler.EnqueueFromEventLog(
                _shuffleIntoSink,
                arch,
                startIndex,
                uid => deckManager.ContainsUid(uid));
        }

        public async UniTask FlushPendingShuffleIntoPresentationAsync(CancellationToken ct)
        {
            if (!_shuffleIntoSink.HasPending)
            {
                return;
            }

            await DrainPendingShuffleIntoPresentationCoreAsync(ct);
        }

        private async UniTask DrainPendingShuffleIntoPresentationCoreAsync(CancellationToken ct)
        {
            var deckManager = Deck;
            if (deckManager == null || Cards == null)
            {
                _shuffleIntoSink.Clear();
                return;
            }

            var pending = new List<ShuffleIntoDeckPresentationEntry>(_shuffleIntoSink.PendingCount);
            while (_shuffleIntoSink.TryDequeue(out var queued))
            {
                pending.Add(queued);
            }

            if (pending.Count == 0)
            {
                return;
            }

            var burstGroups = new List<ShuffleBurstGroup>();
            var leftovers = new List<ShuffleIntoDeckPresentationEntry>();
            ShuffleBurstGrouper.Partition(pending, burstGroups, leftovers);

            var dealInterval = deckManager.LayoutSettings != null
                ? deckManager.LayoutSettings.dealInterval
                : 0.05f;
            var startedCount = pending.Count;
            _shuffleIntoScheduler.RecordPresentBegin(startedCount);

            try
            {
                for (var i = 0; i < burstGroups.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    while (deckManager.IsBusy)
                    {
                        ct.ThrowIfCancellationRequested();
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    await PresentBurstScatterShuffleGroupAsync(burstGroups[i], ct);
                }

                for (var i = 0; i < leftovers.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    while (deckManager.IsBusy)
                    {
                        ct.ThrowIfCancellationRequested();
                        await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    }

                    await PresentOneShuffleIntoDeckAsync(leftovers[i], ct);

                    if (i + 1 < leftovers.Count && dealInterval > 0f)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(dealInterval), cancellationToken: ct);
                    }
                }
            }
            finally
            {
                _shuffleIntoScheduler.RecordPresentEnd(startedCount);
            }
        }

        private async UniTask PresentBurstScatterShuffleGroupAsync(
            ShuffleBurstGroup group,
            CancellationToken ct)
        {
            if (group.Entries == null || group.Entries.Count == 0)
            {
                return;
            }

            _shuffleIntoScheduler.RecordBurstScatterBegin(group.ActionId, group.Entries.Count);
            try
            {
                var deckManager = Deck;
                var fieldManager = Field;
                if (deckManager == null || Cards == null)
                {
                    return;
                }

                var originEntry = group.Entries[0];
                if (!TryResolveShuffleIntoOrigin(originEntry, out var originTransform)
                    || originTransform == null)
                {
                    if (!deckManager.TryGetDefaultDealOrigin(out originTransform)
                        || originTransform == null)
                    {
                        Debug.LogWarning(
                            $"[InBattleManager] BurstScatter action={group.ActionId} 无炸开原点，回退单条洗回。");
                        for (var i = 0; i < group.Entries.Count; i++)
                        {
                            await PresentOneShuffleIntoDeckAsync(group.Entries[i], ct);
                        }

                        return;
                    }
                }

                var origin = originTransform.position;
                var cards = new List<ManagedCard>(group.Entries.Count);
                for (var i = 0; i < group.Entries.Count; i++)
                {
                    var entry = group.Entries[i];
                    if (deckManager.ContainsUid(entry.Uid))
                    {
                        continue;
                    }

                    var card = EnsureShuffleIntoCardView(entry, CardDisplayMode.GroundCardMode);
                    if (card != null)
                    {
                        cards.Add(card);
                    }
                }

                if (cards.Count == 0)
                {
                    return;
                }

                var fieldLayout = fieldManager != null ? fieldManager.LayoutSettings : null;
                var radius = fieldLayout != null ? fieldLayout.burstScatterRadius : 1.1f;
                var burstDuration = fieldLayout != null ? fieldLayout.burstScatterDuration : 0.28f;
                var holdDuration = fieldLayout != null ? fieldLayout.burstScatterHoldDuration : 0.06f;
                var exitDuration = fieldLayout != null ? fieldLayout.fieldExitDuration : 0.35f;

                await CardBurstScatterIntoDeckPresenter.PresentAsync(
                    cards,
                    origin,
                    radius,
                    burstDuration,
                    holdDuration,
                    exitDuration,
                    deckManager,
                    ct);
            }
            finally
            {
                _shuffleIntoScheduler.RecordBurstScatterEnd(group.ActionId, group.Entries.Count);
            }
        }

        private ManagedCard EnsureShuffleIntoCardView(
            ShuffleIntoDeckPresentationEntry entry,
            CardDisplayMode initialMode)
        {
            var cardManager = Cards;
            if (cardManager == null || entry.Uid <= 0)
            {
                return null;
            }

            var arch = NineGridArchitecture.Current;
            var defId = entry.DefId;
            if (string.IsNullOrEmpty(defId)
                && arch != null
                && arch.GetModel<CardRegistry>().TryGet(entry.Uid, out var coreCard))
            {
                defId = coreCard.DefId;
            }

            if (string.IsNullOrEmpty(defId))
            {
                defId = CardManagerSingleton.StandardDefId;
            }

            cardManager.TryGet(entry.Uid, out var ensureCard);
            if (ensureCard != null
                && !string.IsNullOrEmpty(defId)
                && !string.Equals(ensureCard.DefId, defId, StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[InBattleManager] ShuffleInto uid={entry.Uid} 视图 def 不符 expect={defId} actual={ensureCard.DefId}，重建。");
                cardManager.Release(ensureCard, "ShuffleInto.DefIdMismatch");
                ensureCard = null;
            }

            if (ensureCard == null)
            {
                ensureCard = cardManager.SpawnView(
                    entry.Uid,
                    defId,
                    initialMode: initialMode,
                    kind: CoreCardPresentationMapper.ResolvePresentationKind(entry.Uid, defId));
                if (ensureCard != null)
                {
                    CoreCardPresentationMapper.ApplyToManagedCard(ensureCard);
                }
            }

            return ensureCard;
        }

        private async UniTask PresentOneShuffleIntoDeckAsync(
            ShuffleIntoDeckPresentationEntry entry,
            CancellationToken ct)
        {
            var deckManager = Deck;
            var cardManager = Cards;
            if (deckManager == null || cardManager == null || entry.Uid <= 0)
            {
                return;
            }

            if (deckManager.ContainsUid(entry.Uid))
            {
                return;
            }

            if (entry.Kind == ShuffleIntoDeckEventKind.ExistingCard)
            {
                if (cardManager.TryGet(entry.Uid, out var existing) && existing != null)
                {
                    deckManager.LaunchReturnFieldCardToDeck(existing);
                }

                return;
            }

            var ensureCard = EnsureShuffleIntoCardView(entry, CardDisplayMode.CardDeckMode);
            if (ensureCard == null)
            {
                Debug.LogWarning($"[InBattleManager] ShuffleInto 无法生成视图 uid={entry.Uid} def={entry.DefId}。");
                return;
            }

            if (!TryResolveShuffleIntoOrigin(entry, out var origin))
            {
                if (!deckManager.TryGetDefaultDealOrigin(out origin))
                {
                    Debug.LogWarning(
                        $"[InBattleManager] ShuffleInto uid={entry.Uid} 无飞入起点，fallback 直接入组。");
                }
            }

            var deckOk = await deckManager.AddCardAtFromOriginAsync(
                CardDeckManagerSingleton.RandomInsertIndex,
                ensureCard,
                origin,
                ct);
            if (!deckOk)
            {
                Debug.LogWarning($"[InBattleManager] ShuffleInto 入组失败 uid={entry.Uid} def={entry.DefId}。");
            }
        }

        private bool TryResolveShuffleIntoOrigin(
            ShuffleIntoDeckPresentationEntry entry,
            out Transform origin)
        {
            origin = null;
            var cardManager = Cards;

            // 优先死亡格锚点；禁止 StageFieldDead 后的尸体 live transform（y-80 → 炸开全程离屏）。
            if (ShuffleBurstOriginResolver.TryResolveWorld(
                    entry,
                    ResolveBurstBoardSlotWorld,
                    uid =>
                    {
                        if (cardManager == null
                            || !cardManager.TryGet(uid, out var trigger)
                            || !ShuffleBurstOriginResolver.IsUsableLiveTrigger(trigger))
                        {
                            return null;
                        }

                        return trigger;
                    },
                    PresentationOutputProjector.ResolveCardWorldPosition,
                    out var world)
                && TryGetShuffleOriginScratch(world, out origin))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(entry.Cause) && _session.TryResolveHandDealOrigin(entry.Cause, out origin))
            {
                return true;
            }

            var deckManager = Deck;
            return deckManager != null && deckManager.TryGetDefaultDealOrigin(out origin);
        }

        private Vector3? ResolveBurstBoardSlotWorld(int groundSlot)
        {
            if (groundSlot <= 0)
            {
                return null;
            }

            var field = Field;
            if (field != null)
            {
                // 直接取格锚，避免 ResolveBoardSlotWorldPosition 仍读到 staged 尸体位。
                var anchor = field.GetGroundAnchor(groundSlot);
                if (anchor != null)
                {
                    return anchor.position;
                }
            }

            return PresentationOutputProjector.ResolveBoardSlotWorldPosition(groundSlot);
        }

        private bool TryGetShuffleOriginScratch(Vector3 worldPosition, out Transform origin)
        {
            origin = EnsureShuffleOriginScratch();
            if (origin == null)
            {
                return false;
            }

            origin.position = worldPosition;
            return true;
        }

        private Transform EnsureShuffleOriginScratch()
        {
            if (_shuffleOriginScratch == null)
            {
                var scratchGo = new GameObject("ShuffleIntoOriginScratch");
                scratchGo.hideFlags = HideFlags.HideAndDontSave;
                _shuffleOriginScratch = scratchGo.transform;
            }

            return _shuffleOriginScratch;
        }

        private async UniTask PresentSkillRemovedCardsAsync(int[] removedUids, CancellationToken ct)
        {
            var cardManager = Cards;
            var fieldManager = Field;
            if (removedUids == null || removedUids.Length == 0 || cardManager == null)
            {
                return;
            }

            var battle = BattleSessionExecutor.ResolveBattlePresentation();
            if (battle == null)
            {
                return;
            }

            var tasks = new List<UniTask>(removedUids.Length);
            for (var i = 0; i < removedUids.Length; i++)
            {
                var uid = removedUids[i];
                if (uid <= 0 || !cardManager.TryGet(uid, out var card) || card == null)
                {
                    continue;
                }

                // 交战主目标尸体已 MarkFieldDead + 异步 Finalize，勿重复播死。
                if (card.IsFieldDead && card.DisplayMode == CardDisplayMode.RemovedMode)
                {
                    continue;
                }

                if (card.DisplayMode == CardDisplayMode.RemovedMode
                    && !fieldManager.TryGetSlotOf(uid, out _))
                {
                    continue;
                }

                tasks.Add(battle.PresentRemovedFieldCardAsync(card, ct));
            }

            if (tasks.Count > 0)
            {
                await UniTask.WhenAll(tasks);
            }
        }

        private sealed class FusionDrainState
        {
            public Dictionary<int, SkeletonFusionPresentationEntry> ParticipantIndex = new();
            public HashSet<int> ResultUids = new();
        }

        private static FusionDrainState BuildFusionDrainState(
            PostKillBoardPresentationResult result,
            int eventLogStartIndex)
        {
            var state = new FusionDrainState();
            var removedInBatch = CollectRemovedUidsFromSteps(result.Steps);
            if (removedInBatch.Count == 0 && result.RemovedUids != null)
            {
                for (var i = 0; i < result.RemovedUids.Length; i++)
                {
                    var uid = result.RemovedUids[i];
                    if (uid > 0)
                    {
                        removedInBatch.Add(uid);
                    }
                }
            }

            if (removedInBatch.Count == 0)
            {
                return state;
            }

            var arch = NineGridArchitecture.Current;
            if (arch == null)
            {
                return state;
            }

            var entries = arch.GetSystem<IActionPipelineSystem>().EventLog.Entries;
            // 仅扫本节点 EventLog 窗口，禁止全历史 Collect(0) 把旧融合 ResultUid 误 purge 进 sink。
            var startIndex = eventLogStartIndex < 0 ? 0 : eventLogStartIndex;
            var fusions = SkeletonFusionPresentationScanner.Collect(entries, startIndex);
            for (var i = 0; i < fusions.Count; i++)
            {
                var fusion = fusions[i];
                if (!FusionIntersectsBatch(fusion, removedInBatch))
                {
                    continue;
                }

                // 只 purge 与本批移除相交的融合结果；ResultUid 须为正。
                if (fusion.ResultUid > 0)
                {
                    state.ResultUids.Add(fusion.ResultUid);
                }

                var participants = fusion.ParticipantUids;
                for (var j = 0; j < participants.Length; j++)
                {
                    var uid = participants[j];
                    if (uid > 0 && removedInBatch.Contains(uid))
                    {
                        state.ParticipantIndex[uid] = fusion;
                    }
                }
            }

            RecordSkeletonFusionTrace(
                "DrainStateBuilt",
                -1,
                new Dictionary<string, string>
                {
                    ["removedInBatch"] = removedInBatch.Count.ToString(),
                    ["eventLogStart"] = startIndex.ToString(),
                    ["fusionCandidates"] = fusions.Count.ToString(),
                    ["participantMapped"] = state.ParticipantIndex.Count.ToString(),
                    ["resultUids"] = state.ResultUids.Count.ToString(),
                });

            return state;
        }

        private static HashSet<int> CollectRemovedUidsFromSteps(BoardPresentationStep[] steps)
        {
            var removed = new HashSet<int>();
            if (steps == null)
            {
                return removed;
            }

            for (var i = 0; i < steps.Length; i++)
            {
                var step = steps[i];
                if (step.Kind != BoardPresentationStepKind.Remove || step.RemovedUids == null)
                {
                    continue;
                }

                for (var j = 0; j < step.RemovedUids.Length; j++)
                {
                    var uid = step.RemovedUids[j];
                    if (uid > 0)
                    {
                        removed.Add(uid);
                    }
                }
            }

            return removed;
        }

        private static bool FusionIntersectsBatch(
            SkeletonFusionPresentationEntry fusion,
            HashSet<int> removedInBatch)
        {
            var participants = fusion.ParticipantUids;
            for (var i = 0; i < participants.Length; i++)
            {
                if (removedInBatch.Contains(participants[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static void RecordSkeletonFusionTrace(
            string site,
            int uid,
            Dictionary<string, string> payload = null)
        {
            payload = payload ?? new Dictionary<string, string>();
            if (!payload.ContainsKey("chainId"))
            {
                payload["chainId"] = DirectorTrace.CurrentChainId.ToString();
            }

            if (!payload.ContainsKey("choreoSeqId"))
            {
                payload["choreoSeqId"] = ChoreoTraceContext.CurrentSeqId.ToString();
            }

            if (!payload.ContainsKey("refillScheduled"))
            {
                payload["refillScheduled"] = FusionRefillScheduler.IsRefillScheduled ? "1" : "0";
            }

            if (!payload.ContainsKey("refillGateArmed"))
            {
                payload["refillGateArmed"] = FusionRefillScheduler.IsRefillGateArmed ? "1" : "0";
            }

            PerfTraceRecorder.Record("SkeletonFusion", uid, site, payload);
            if (payload.Count == 0)
            {
                Debug.Log($"[SkeletonFusion] {site} uid={uid}");
                return;
            }

            var summary = new StringBuilder(site);
            summary.Append(" uid=").Append(uid);
            foreach (var pair in payload)
            {
                summary.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
            }

            Debug.Log("[SkeletonFusion] " + summary);
        }

        private void PurgeFusionResultsFromShuffleQueue(FusionDrainState fusionState)
        {
            if (fusionState == null || fusionState.ResultUids.Count == 0)
            {
                return;
            }

            _shuffleIntoSink.PurgeUids(fusionState.ResultUids);
        }

        private async UniTask<bool> TryHandleFusionRemoveStepAsync(
            FusionDrainState fusionState,
            BoardPresentationStep step,
            CancellationToken ct)
        {
            if (fusionState == null
                || step.RemovedUids == null
                || step.RemovedUids.Length != 1)
            {
                return false;
            }

            var uid = step.RemovedUids[0];
            if (!fusionState.ParticipantIndex.TryGetValue(uid, out var fusion))
            {
                RecordSkeletonFusionTrace(
                    "RemoveStepMiss",
                    uid,
                    new Dictionary<string, string>
                    {
                        ["stepActionId"] = step.ActionId.ToString(),
                        ["participantMapped"] = fusionState.ParticipantIndex.Count.ToString(),
                    });
                return false;
            }

            if (!_pendingFusionRemoves.TryGetValue(fusion.ActionId, out var pending))
            {
                pending = new HashSet<int>();
                _pendingFusionRemoves[fusion.ActionId] = pending;
            }

            pending.Add(uid);
            if (pending.Count < fusion.ParticipantUids.Length)
            {
                RecordSkeletonFusionTrace(
                    "RemoveStepPending",
                    uid,
                    new Dictionary<string, string>
                    {
                        ["skillId"] = fusion.SkillId,
                        ["pending"] = pending.Count.ToString(),
                        ["required"] = fusion.ParticipantUids.Length.ToString(),
                    });
                return true;
            }

            if (_completedFusionActionIds.Contains(fusion.ActionId))
            {
                RecordSkeletonFusionTrace(
                    "RemoveStepAlreadyCompleted",
                    uid,
                    new Dictionary<string, string> { ["skillId"] = fusion.SkillId });
                return true;
            }

            _completedFusionActionIds.Add(fusion.ActionId);
            _pendingFusionRemoves.Remove(fusion.ActionId);

            var fieldManager = Field;
            if (fieldManager == null)
            {
                RecordSkeletonFusionTrace("PresentBlockedNoFieldManager", uid);
                return false;
            }

            var request = ToFusionRequest(fusion);
            EnsureFusionResultView(fusion);
            RecordSkeletonFusionTrace(
                "PresentBegin",
                uid,
                new Dictionary<string, string>
                {
                    ["skillId"] = fusion.SkillId,
                    ["resultUid"] = fusion.ResultUid.ToString(),
                    ["resultDefId"] = fusion.ResultDefId,
                    ["participants"] = string.Join(",", fusion.ParticipantUids),
                });
            await fieldManager.PresentSkeletonFusionAsync(
                request,
                onFusionStarted: null,
                ct);
            var gateArmed = FusionRefillScheduler.IsRefillGateArmed;
            var alreadyScheduled = FusionRefillScheduler.IsRefillScheduled;
            RecordSkeletonFusionTrace(
                "PresentEnd",
                uid,
                new Dictionary<string, string>
                {
                    ["skillId"] = fusion.SkillId,
                    ["refillDeferredToDirector"] = PresentationInputGates.MainlineBusy ? "1" : "0",
                    // refillScheduled 只信真实入队标记；gateArmed 仅表示剧本挂了分支，不等于已补牌。
                    ["refillScheduled"] = alreadyScheduled ? "1" : "0",
                    ["refillGateArmed"] = gateArmed ? "1" : "0",
                });
            if (!alreadyScheduled)
            {
                var exclude = new List<int>(1);
                if (fusion.ResultUid > 0)
                {
                    exclude.Add(fusion.ResultUid);
                }

                FusionRefillAftermath.TrySchedule(exclude);
            }

            return true;
        }

        private static SkeletonFusionPresentationRequest ToFusionRequest(SkeletonFusionPresentationEntry entry)
        {
            return new SkeletonFusionPresentationRequest(
                entry.ActionId,
                entry.SkillId,
                entry.TriggerCardUid,
                entry.ParticipantUids,
                entry.ResultUid,
                entry.ResultDefId);
        }

        private void EnsureFusionResultView(SkeletonFusionPresentationEntry fusion)
        {
            var cardManager = Cards;
            if (cardManager == null || fusion.ResultUid <= 0)
            {
                return;
            }

            if (!cardManager.TryGet(fusion.ResultUid, out var resultCard) || resultCard == null)
            {
                resultCard = cardManager.SpawnView(
                    fusion.ResultUid,
                    fusion.ResultDefId,
                    initialMode: CardDisplayMode.GroundCardMode,
                    kind: CoreCardPresentationMapper.ResolvePresentationKind(
                        fusion.ResultUid, fusion.ResultDefId));
            }

            if (resultCard != null)
            {
                CoreCardPresentationMapper.ApplyToManagedCard(resultCard);
                // 预热 Spawn 不可提前显露：Reveal 前 scale=0，避免融合旁路「凭空多一张」。
                if (resultCard.Transform != null)
                {
                    resultCard.Transform.localScale = Vector3.zero;
                }
            }
        }

        private static bool ShouldDeferBoardTimelineSync()
        {
            return ChoreoTraceContext.PumpRunning || ChoreoTraceContext.DrainInFlight;
        }

        public void FlushDeferredBoardSync(bool force = false)
        {
            if (!force && ShouldDeferBoardTimelineSync())
            {
                return;
            }

            if (_pendingSyncFromCore)
            {
                _pendingSyncFromCore = false;
                // #10：延迟对账降级为断言，禁止静默修补。
                BattleSessionExecutor.AssertOccupancySyncForbidden("flushDeferredBoardSync", force ? "force" : "soft");
            }
        }
    }
}
