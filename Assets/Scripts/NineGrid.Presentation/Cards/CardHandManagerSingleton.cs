using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;

namespace NineGrid.Cards
{
    /// <summary>
    /// 手牌管理器单例：最多 5 张，CardHandAnchors 布局，hover / 拖拽 / 回手 / 场地抓取。
    /// 净土域：内部布局黑盒；仅暴露 C 阶段 Evict/Admit（速度恒 0）。
    /// V6 compat shell — Cards/Deck 解析优先 <see cref="CardEntityLifecycleHook"/>。
    /// ExecutionOrder -50：先于 <see cref="NineGrid.Flow.PointerHitRouter"/> 刷新 hover，保证按下拖的是本帧悬停牌。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class CardHandManagerSingleton : MonoBehaviour, IHandoffEndpoint
    {
        private sealed class DragSession
        {
            public ManagedCard Card;
            public int OriginHandSlot = -1;
            public bool WasHovering;
            public bool PointerReleasedInZone;
            public bool PointerReleasedInRecycleZone;
        }


        [Header("Scene Anchors")]
        [Tooltip("场景 Anchors/CardHandAnchors。留空时 Awake 按名称 CardHandAnchors 查找。")]
        [SerializeField] private Transform handAnchorsRoot;

        [Tooltip("场景 CardHandAnchors/HandcardApplyZone 的 Collider。留空时作为 handAnchorsRoot 子节点查找。")]
        [SerializeField] private Collider applyZoneCollider;

        [Tooltip("场景 CardHandAnchors/HandcardRecycleZone 的 Collider。拖动时激活；留空时按子节点名查找。")]
        [SerializeField] private Collider recycleZoneCollider;

        [Tooltip("场景 CardHandAnchors/CardRecycleNotice。拖动时激活；留空时按子节点名查找。")]
        [SerializeField] private GameObject recycleNotice;

        [Header("Layout")]
        [Tooltip("手牌布局与动效参数。")]
        [SerializeField] private CardHandLayoutSettings layoutSettings = new();

        private readonly List<Transform> _handAnchors = new();
        private CardHandSlotContainer _slotContainer;
        private DragSession _dragSession;
        private ManagedCard _hoveredCard;
        private readonly List<HandHoverCandidate> _hoverCandidates = new();
        private bool _isBusy;
        private CancellationTokenSource _dragLoopCts;
        private CancellationTokenSource _handWorkCts;

        private readonly struct HandHoverCandidate
        {
            public readonly int SlotIndex;
            public readonly ManagedCard Card;
            public readonly float LayoutX;
            public readonly float LayoutY;

            public HandHoverCandidate(int slotIndex, ManagedCard card, float layoutX, float layoutY)
            {
                SlotIndex = slotIndex;
                Card = card;
                LayoutX = layoutX;
                LayoutY = layoutY;
            }
        }

        /// <summary>
        /// TODO: Core 逻辑层注入 — 校验手牌释放（目标格位、费用、效果等）。返回 true 表示释放成功。
        /// </summary>
        public event Func<ManagedCard, int?, UniTask<bool>> DragApplyValidator;

        public CardHandLayoutSettings LayoutSettings => layoutSettings;

        public int HandCount => _slotContainer?.Count ?? 0;

        public int MaxHandSlots => layoutSettings.maxSlots;

        public bool IsBusy
        {
            get
            {
                // #51：不再聚合场地忙旗；须阻塞的场地表演应持主线（MainlineBusy）。
                return _isBusy
                    || PresentationInputGates.ChoiceOverlayActive
                    || PresentationInputGates.MainlineBusy
                    || PresentationInputGates.BoardSelectModeActive;
            }
        }

        /// <summary>仅手牌自身忙碌（不含场地/导演聚合），供跨关 WaitPresentationIdle 使用。</summary>
        public bool IsSelfBusy => _isBusy;

        public bool IsDragging => _dragSession != null;

        public bool CanAcceptCard => !IsBusy && !IsDragging && HandCount < layoutSettings.maxSlots;

        /// <summary>
        /// 净土域 C 阶段交接：速度恒填 0。域级快照；卡级见 <see cref="EvictCard"/>。
        /// </summary>
        public HandoffState Evict() => HandoffState.AtRest(Vector3.zero);

        /// <summary>净土域 C 阶段：承接位置，忽略速度。</summary>
        public void Admit(in HandoffState state)
        {
            // 手牌布局黑盒内部不动；C 阶段仅接受接口契约。
        }

        /// <summary>单卡离开手牌域：C 阶段速度恒 0。</summary>
        public HandoffState EvictCard(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return HandoffState.AtRest(Vector3.zero);
            }

            return HandoffState.AtRest(card.Transform.localPosition);
        }

        /// <summary>单卡进入手牌域：C 阶段忽略速度，仅对齐局部位姿。</summary>
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
            _slotContainer = new CardHandSlotContainer(layoutSettings);
            ResolveSceneReferences();
            CacheAnchors();
            InitializeLayoutOrigin();
            UseItemInputHook.RequestWire(this);
            PickupInputHook.RequestWire(this);
            RecycleItemInputHook.RequestWire(this);
            PickupIntentFlushHook.Notify = OnPickupIntentFlushed;
            RecycleItemIntentFlushHook.Notify = OnRecycleItemIntentFlushed;
            SetRecycleUiActive(false);
        }

        private void OnDestroy()
        {
            CancelHandWork();
            if (PickupIntentFlushHook.Notify == (Action<int, PickupItemPresentationResult>)OnPickupIntentFlushed)
            {
                PickupIntentFlushHook.Notify = null;
            }

            if (RecycleItemIntentFlushHook.Notify == (Action<int, CoreCommandResult>)OnRecycleItemIntentFlushed)
            {
                RecycleItemIntentFlushHook.Notify = null;
            }
        }

        /// <summary>
        /// 强制清空手牌槽与拖拽/忙碌态。不销毁卡视图（由 CardManager 统一释放）。
        /// 回主菜单 / 重开局前必须调用，否则幽灵 uid 会挡住拿卡与补牌。
        /// </summary>
        public void ClearHand()
        {
            CancelHandWork();
            ClearDragSession();
            ClearHandHoverState(_hoveredCard);
            _hoveredCard = null;
            _slotContainer?.Clear();
            _isBusy = false;
        }

        private void CancelHandWork()
        {
            if (_dragLoopCts != null)
            {
                _dragLoopCts.Cancel();
                _dragLoopCts.Dispose();
                _dragLoopCts = null;
            }

            if (_handWorkCts != null)
            {
                _handWorkCts.Cancel();
                _handWorkCts.Dispose();
                _handWorkCts = null;
            }
        }

        private CancellationToken RenewHandWorkToken()
        {
            if (_handWorkCts != null)
            {
                _handWorkCts.Cancel();
                _handWorkCts.Dispose();
            }

            _handWorkCts = new CancellationTokenSource();
            return _handWorkCts.Token;
        }

        private void Update()
        {
            TickHandHover();
        }

        /// <summary>
        /// 手牌是否持有该 uid（含拖拽中）。
        /// </summary>
        public bool ContainsUid(int uid)
        {
            if (uid <= 0)
            {
                return false;
            }

            if (_dragSession?.Card != null && _dragSession.Card.Uid == uid)
            {
                return true;
            }

            if (_slotContainer == null)
            {
                return false;
            }

            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (_slotContainer.TryGetCardAt(i, out var card) && card != null && card.Uid == uid)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 从手牌槽移除但不销毁视图（供异常对齐用）。拖拽中不可用。
        /// </summary>
        public bool TryRemoveFromHand(int uid, out ManagedCard card)
        {
            card = null;
            if (uid <= 0 || IsBusy || IsDragging || _slotContainer == null)
            {
                return false;
            }

            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (!_slotContainer.TryGetCardAt(i, out var found) || found == null || found.Uid != uid)
                {
                    continue;
                }

                if (!_slotContainer.TryRemoveAt(i, out card, out var rippleMoves))
                {
                    return false;
                }

                CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration).Forget();
                return card != null;
            }

            return false;
        }

        /// <summary>
        /// 无飞入动画：直接把卡贴入手牌槽（跨节点持续持有重建；ADR-0025）。
        /// </summary>
        public bool TryPlaceInHandImmediate(ManagedCard card, bool skipBusyGuard = false)
        {
            if (card == null
                || IsDragging
                || HandCount >= layoutSettings.maxSlots
                || PresentationInputGates.ChoiceOverlayActive)
            {
                return false;
            }

            if (!skipBusyGuard && _isBusy)
            {
                return false;
            }

            if (ContainsUid(card.Uid))
            {
                EnsureHandLayout(card);
                return true;
            }

            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            if (cardManager == null || _slotContainer == null)
            {
                return false;
            }

            _isBusy = true;
            try
            {
                CardOpacityUtility.ResetAlpha(card);
                CardEntityLifecycleHook.DeckOrNull()?.TryDetachByUid(card.Uid, out _);
                cardManager.SetDisplayMode(card, CardDisplayMode.HandCardMode);

                if (!_slotContainer.TryInsertAt(HandCount, card, out var rippleMoves))
                {
                    return false;
                }

                // 就地贴位：不走 MoveRipple 缓动，直接杀 tween 贴布局。
                if (rippleMoves != null)
                {
                    for (var i = 0; i < rippleMoves.Count; i++)
                    {
                        var move = rippleMoves[i];
                        if (move.Card?.Transform == null)
                        {
                            continue;
                        }

                        CardDeckTween.KillMotion(move.Card.Transform);
                        move.Card.Transform.position = move.TargetPosition;
                    }
                }

                EnsureHandLayout(card);
                try
                {
                    FlowFieldTraceSink.HandLifecycle?.Invoke(
                        card.Uid,
                        "acquire",
                        true,
                        "HandRestoreImmediate");
                }
                catch
                {
                    // ignore
                }

                return true;
            }
            finally
            {
                _isBusy = false;
            }
        }

        public async UniTask<bool> PullFromGroundAsync(
            ManagedCard card,
            int? insertSlot = null,
            bool skipBusyGuard = false,
            CancellationToken cancellationToken = default)
        {
            // PresentationLocked 时本路径已持单输入锁，勿用完整 IsBusy/CanAcceptCard 自拒。
            // 多选反悔回手：End 后卡可能仍为 DragCardMode 且 _isBusy 短暂为 true，须放行。
            var boardSelectAbortRestore = !PresentationInputGates.BoardSelectModeActive
                && card.DisplayMode == CardDisplayMode.DragCardMode;

            if (card == null
                || IsDragging
                || HandCount >= layoutSettings.maxSlots
                || PresentationInputGates.ChoiceOverlayActive)
            {
                return false;
            }

            if (!skipBusyGuard
                && (_isBusy && !boardSelectAbortRestore))
            {
                return false;
            }

            if (!PresentationInputGates.HasExternalHold && !skipBusyGuard)
            {
                var field = GroundFieldGeometryHook.FieldOrNull();
                // 本地场地运动重入保护（非输入门禁）；勿用聚合 IsBusy 轮询 BattleBusy。
                if (field != null && field.IsFieldBusy)
                {
                    return false;
                }
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                RenewHandWorkToken(),
                cancellationToken.CanBeCanceled ? cancellationToken : CancellationToken.None);
            var ct = linkedCts.Token;

            _isBusy = true;
            try
            {
                var cardManager = CardEntityLifecycleHook.CardsOrNull();
                if (cardManager == null)
                {
                    return false;
                }

                CardOpacityUtility.ResetAlpha(card);

                CardEntityLifecycleHook.DeckOrNull()?.TryDetachByUid(card.Uid, out _);

                // 须在 TryInsertAt 之前切 HandCardMode：ApplyDisplayMode → SnapToDisplayMode 会查手牌槽位并贴布局坐标；
                // 若已入槽再切模式，会瞬间贴位，MoveRippleAsync 无法从场地缓动飞入。
                cardManager.SetDisplayMode(card, CardDisplayMode.HandCardMode);

                var slot = insertSlot ?? HandCount;
                if (!_slotContainer.TryInsertAt(slot, card, out var rippleMoves))
                {
                    return false;
                }

                await CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration, ct);
                ct.ThrowIfCancellationRequested();
                EnsureHandLayout(card);
                try
                {
                    FlowFieldTraceSink.HandLifecycle?.Invoke(
                        card.Uid,
                        "acquire",
                        true,
                        card.DisplayMode.ToString());
                }
                catch
                {
                    // ignore
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                _isBusy = false;
            }
        }

        public bool TryBeginDragFromHand(ManagedCard card)
        {
            if (card == null || IsBusy || IsDragging)
            {
                return false;
            }

            if (card.DisplayMode != CardDisplayMode.HandCardMode)
            {
                return false;
            }

            if (!_slotContainer.TryGetSlotOf(card, out var slotIndex))
            {
                return false;
            }

            var driver = card.View?.GetComponent<CardVisualDriver>();
            var wasHovering = driver != null && driver.CurrentTarget == CardVisualTarget.Hover;

            if (!_slotContainer.TryRemoveAt(slotIndex, out var removed, out var rippleMoves))
            {
                return false;
            }

            CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration).Forget();
            ClearHandHoverState(removed);

            _dragSession = new DragSession
            {
                Card = removed,
                OriginHandSlot = slotIndex,
                WasHovering = wasHovering,
            };

            BeginDragLoop();
            return true;
        }

        /// <summary>
        /// 按下时拖起当前 hover 手牌（与槽位带 hover 语义对齐；不依赖 collider Overlap）。
        /// </summary>
        public bool TryBeginDragFromHoveredCard()
        {
            if (BattleUiDimmerOverlay.IsActive || CardInspectOverlayPresenter.IsOpen)
            {
                return false;
            }

            if (_hoveredCard == null || !IsLiveHandCard(_hoveredCard))
            {
                return false;
            }

            return TryBeginDragFromHand(_hoveredCard);
        }

        /// <summary>右键详述：读当前槽位带 hover 手牌（可为 null）。</summary>
        public ManagedCard TryPeekHoveredCardForInspect()
        {
            return _hoveredCard != null && IsLiveHandCard(_hoveredCard) ? _hoveredCard : null;
        }

        /// <summary>
        /// 场地卡点击入手：道具卡 / 帮助卡等不可从场地拖拽，只能点击直接入手牌。
        /// idle：Controller 内 IntentIntake→ExternalHold→Core Apply 后，此处只播表现；
        /// busy：IntentIntake 缓冲，flush 后经 <see cref="OnPickupIntentFlushed"/> 承接。
        /// 互动范围：目前硬编码 Avatar 格5四向正交（IsAvatarOrthogonalBattleSlot）；
        /// 后续职业互动范围应与 Core AreAdjacent / InteractionRange 共用同一判定源，勿再分叉。
        /// </summary>
        public bool TryPickupFromGround(ManagedCard card)
        {
            if (card == null)
            {
                FlowFieldTraceSink.PickupGate?.Invoke(-1, "NullCard", false, null);
                return false;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            var groundSlotForAttempt = -1;
            if (field != null)
            {
                field.TryGetSlotOf(card.Uid, out groundSlotForAttempt);
            }

            FlowFieldTraceSink.PickupAttempt?.Invoke(
                card.Uid,
                groundSlotForAttempt,
                card.DefId,
                card.CoreKind.ToString());

            // 勿用含 MainlineBusy 的 IsBusy/CanAcceptCard：忙时应落到下方 IntentIntake 缓冲。
            if (IsDragging)
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "HandDragging", false, null);
                return false;
            }

            if (HandCount >= layoutSettings.maxSlots)
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "HandFull", false, null);
                return false;
            }

            if (_isBusy
                || PresentationInputGates.ChoiceOverlayActive
                || PresentationInputGates.BoardSelectModeActive)
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "HandBusy", false, null);
                return false;
            }

            if (card.DisplayMode != CardDisplayMode.GroundCardMode)
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "NotGroundMode", false, null);
                return false;
            }

            if (field == null)
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "NoField", false, null);
                return false;
            }

            if (PresentationInputGates.OpeningPresentationActive)
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "OpeningDeal", false, null);
                return false;
            }

            if (!field.TryGetSlotOf(card.Uid, out var groundSlot))
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "NoSlot", false, null);
                return false;
            }

            if (!field.IsAvatarOrthogonalBattleSlot(groundSlot))
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "NotOrtho", false, null);
                return false;
            }

            if (PickupInputHook.TryApplyPickup == null)
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "PickupHookUnwired", false, null);
                Debug.LogWarning("[CardHandManager] PickupInputHook.TryApplyPickup 未装配。");
                return false;
            }

            // IntentIntake→Hold→Apply 在 Controller；busy 时 Reason=buffered（勿先持锁）。
            var pickup = PickupInputHook.TryApplyPickup(groundSlot);
            if (string.Equals(pickup.Reason, "buffered", StringComparison.Ordinal))
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "Buffered", true, null);
                return true;
            }

            if (string.Equals(pickup.Reason, "lockFail", StringComparison.Ordinal))
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "LockFailAbort", false, null);
                return false;
            }

            if (!pickup.Accepted)
            {
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "CoreReject", false, pickup.Reason);
                return false;
            }

            // Controller 已在 Apply 前取得 ExternalHold；此处不得无租约继续，也不得再抢锁。
            return BeginPresentAcceptedPickup(card, pickup, field);
        }

        /// <summary>
        /// Director flush Pickup 后：Core 已在主线 Apply；取表现租约后接手牌/清场表演。
        /// </summary>
        private void OnPickupIntentFlushed(int groundSlot, PickupItemPresentationResult pickup)
        {
            if (!pickup.Accepted)
            {
                return;
            }

            if (!PresentationInputGates.TryBeginExternalHold("PickupFlush"))
            {
                Debug.LogWarning(
                    "[CardHandManager] PickupFlush ExternalHold 失败，跳过表现 slot="
                    + groundSlot
                    + " uid="
                    + pickup.CardUid);
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            ManagedCard card = null;
            if (field != null && pickup.CardUid > 0)
            {
                if (field.TryGetSlotOf(pickup.CardUid, out var occupiedSlot)
                    && field.TryGetCardAt(occupiedSlot, out var atSlot)
                    && atSlot != null
                    && atSlot.Uid == pickup.CardUid)
                {
                    card = atSlot;
                }
                else if (groundSlot >= 0
                    && field.TryGetCardAt(groundSlot, out var atGround)
                    && atGround != null
                    && atGround.Uid == pickup.CardUid)
                {
                    card = atGround;
                }
                else
                {
                    CardEntityLifecycleHook.CardsOrNull()?.TryGet(pickup.CardUid, out card);
                }
            }

            BeginPresentAcceptedPickup(card, pickup, field);
        }

        /// <summary>
        /// 已在有效 ExternalHold 下承接 Pickup 表现。失败时由调用方或本方法释放租约。
        /// </summary>
        private bool BeginPresentAcceptedPickup(
            ManagedCard card,
            PickupItemPresentationResult pickup,
            GroundFieldView field)
        {
            if (pickup.RemovedWithoutHand)
            {
                if (field != null && pickup.CardUid > 0)
                {
                    field.RequestRemoveFromField(
                        pickup.CardUid,
                        animate: true,
                        skipBusyGuard: true,
                        startExplore: false);
                }

                RunPickupDrainAsync(
                    new PostKillBoardPresentationResult
                    {
                        Accepted = true,
                        Steps = pickup.Steps,
                        Moves = pickup.Moves,
                        Deals = pickup.Deals,
                        NodeClearedOrRewardPhase = pickup.NodeClearedOrRewardPhase,
                    }).Forget();
                FlowFieldTraceSink.PickupSuccess?.Invoke(pickup.CardUid, -1);
                return true;
            }

            if (!pickup.AcquiredToHand)
            {
                Debug.LogWarning($"[CardHandManager] Pickup 已接受但未入手 uid={pickup.CardUid}");
                PresentationInputGates.EndExternalHold("Pickup-no-hand");
                FlowFieldTraceSink.PickupGate?.Invoke(pickup.CardUid, "NoAcquire", false, null);
                return false;
            }

            if (card == null || field == null)
            {
                PresentationInputGates.EndExternalHold("Pickup-missing-view");
                FlowFieldTraceSink.PickupGate?.Invoke(pickup.CardUid, "MissingView", false, null);
                return false;
            }

            if (!field.TryTakeCardFromField(card.Uid, out var taken, startExplore: false, skipBusyGuard: true)
                || taken != card)
            {
                PresentationInputGates.EndExternalHold("Pickup-take-failed");
                FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "TakeFail", false, null);
                return false;
            }

            var driver = card.View?.GetComponent<CardVisualDriver>();
            driver?.SetTarget(CardVisualTarget.Base);

            RunPickupFromGroundAsync(card, pickup).Forget();
            FlowFieldTraceSink.PickupGate?.Invoke(card.Uid, "ok", true, null);
            return true;
        }

        private async UniTaskVoid RunPickupDrainAsync(PostKillBoardPresentationResult postKill)
        {
            try
            {
                await BattleSessionSystem.EnsureRegistered().DrainPostKillBoardAsync(postKill);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                PresentationInputGates.EndExternalHold("Pickup-drain");
            }
        }

        private async UniTaskVoid RunPickupFromGroundAsync(
            ManagedCard card,
            PickupItemPresentationResult pickup)
        {
            try
            {
                var success = await PullFromGroundAsync(card);
                if (!success)
                {
                    // 清场取消时卡视图会由 ReleaseAll 统一释放，勿二次 Release。
                    var cm = CardEntityLifecycleHook.CardsOrNull();
                    if (card != null && cm != null && cm.TryGet(card.Uid, out _))
                    {
                        Debug.LogWarning("[CardHandManager] 场地卡点击入手失败，已释放卡牌。");
                        try
                        {
                            FlowFieldTraceSink.HandLifecycle?.Invoke(
                                card.Uid,
                                "release-pickup-fail",
                                false,
                                card.DisplayMode.ToString());
                        }
                        catch
                        {
                            // ignore
                        }

                        cm.Release(card, "Hand.PickupFail");
                    }

                    return;
                }

                FlowFieldTraceSink.PickupSuccess?.Invoke(card.Uid, ResolveHandSlotForTrace(card));
                await BattleSessionSystem.EnsureRegistered().DrainPostKillBoardAsync(
                    new PostKillBoardPresentationResult
                    {
                        Accepted = true,
                        Steps = pickup.Steps,
                        Moves = pickup.Moves,
                        Deals = pickup.Deals,
                        RemovedUids = pickup.RemovedUids,
                        NodeClearedOrRewardPhase = pickup.NodeClearedOrRewardPhase,
                    });

                // #10 / V3：占格权威在 Core；冲突只记诊断，禁止 force-sync heal。
                var field = GroundFieldGeometryHook.FieldOrNull();
                if (field != null && field.ConsumeOccupancyConflictFlag())
                {
                    Debug.LogWarning(
                        "[CardHandManager] Pickup 后占格冲突残留（已禁止 force-sync）uid="
                        + (card != null ? card.Uid : 0));
                }

                if (card != null && ContainsUid(card.Uid))
                {
                    EnsureHandLayout(card);
                }
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                PresentationInputGates.EndExternalHold("Pickup-from-ground");
            }
        }

        private void TickHandHover()
        {
            if (IsBusy || IsDragging || _slotContainer == null)
            {
                return;
            }

            // Unity destroyed：清掉悬空 hover，避免每帧 MissingReferenceException。
            if (_hoveredCard != null && !IsLiveHandCard(_hoveredCard))
            {
                ClearHandHoverState(_hoveredCard);
                _hoveredCard = null;
            }

            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            if (!WorldPointerUtility.TryGetPointerScreen(out var pointerScreen))
            {
                return;
            }

            var pointerWorld = ScreenToWorldOnPlane(
                pointerScreen,
                camera,
                ResolveHandHoverPlaneZ());
            var resolved = ResolveHandHoverTarget(pointerWorld.x, pointerWorld.y);
            ApplyHandHoverTarget(resolved);
        }

        private float ResolveHandHoverPlaneZ()
        {
            if (_handAnchors.Count > 0 && _handAnchors[0] != null)
            {
                return _handAnchors[0].position.z;
            }

            return handAnchorsRoot != null ? handAnchorsRoot.position.z : 0f;
        }

        private ManagedCard ResolveHandHoverTarget(float pointerX, float pointerY)
        {
            CollectHandHoverCandidates(pointerX, pointerY);
            if (_hoverCandidates.Count == 0)
            {
                return null;
            }

            if (_hoverCandidates.Count == 1)
            {
                return _hoverCandidates[0].Card;
            }

            var rawTarget = ResolveHandHoverBySlotBands(pointerX);
            if (_hoveredCard == null || rawTarget == _hoveredCard)
            {
                return rawTarget;
            }

            return ShouldKeepCurrentHover(pointerX, rawTarget) ? _hoveredCard : rawTarget;
        }

        private void CollectHandHoverCandidates(float pointerX, float pointerY)
        {
            _hoverCandidates.Clear();

            var halfHeight = layoutSettings.handHitBoxSize.y * 0.5f;
            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (!_slotContainer.TryGetCardAt(i, out var card) || card == null)
                {
                    continue;
                }

                if (!IsLiveHandCard(card))
                {
                    continue;
                }

                if (card.DisplayMode != CardDisplayMode.HandCardMode)
                {
                    continue;
                }

                var layoutPosition = _slotContainer.GetLayoutPosition(i);
                if (pointerY < layoutPosition.y - halfHeight || pointerY > layoutPosition.y + halfHeight)
                {
                    continue;
                }

                var halfWidth = layoutSettings.handHitBoxSize.x * 0.5f;
                if (pointerX < layoutPosition.x - halfWidth || pointerX > layoutPosition.x + halfWidth)
                {
                    continue;
                }

                _hoverCandidates.Add(new HandHoverCandidate(i, card, layoutPosition.x, layoutPosition.y));
            }

            _hoverCandidates.Sort(static (a, b) => a.LayoutX.CompareTo(b.LayoutX));
        }

        private ManagedCard ResolveHandHoverBySlotBands(float pointerX)
        {
            var count = _hoverCandidates.Count;
            var halfWidth = layoutSettings.handHitBoxSize.x * 0.5f;
            for (var i = 0; i < count; i++)
            {
                var leftBound = i == 0
                    ? _hoverCandidates[i].LayoutX - halfWidth
                    : (_hoverCandidates[i - 1].LayoutX + _hoverCandidates[i].LayoutX) * 0.5f;
                var rightBound = i == count - 1
                    ? _hoverCandidates[i].LayoutX + halfWidth
                    : (_hoverCandidates[i].LayoutX + _hoverCandidates[i + 1].LayoutX) * 0.5f;

                if (pointerX >= leftBound && pointerX < rightBound)
                {
                    return _hoverCandidates[i].Card;
                }
            }

            return null;
        }

        private bool ShouldKeepCurrentHover(float pointerX, ManagedCard rawTarget)
        {
            if (!TryGetHoverCandidate(_hoveredCard, out var current) ||
                !TryGetHoverCandidate(rawTarget, out var next))
            {
                return false;
            }

            if (current.SlotIndex == next.SlotIndex)
            {
                return true;
            }

            var midpoint = (current.LayoutX + next.LayoutX) * 0.5f;
            var hysteresis = Mathf.Max(0f, layoutSettings.hoverSwitchHysteresis);

            if (next.LayoutX > current.LayoutX)
            {
                return pointerX < midpoint + hysteresis;
            }

            return pointerX > midpoint - hysteresis;
        }

        private bool TryGetHoverCandidate(ManagedCard card, out HandHoverCandidate candidate)
        {
            for (var i = 0; i < _hoverCandidates.Count; i++)
            {
                if (_hoverCandidates[i].Card == card)
                {
                    candidate = _hoverCandidates[i];
                    return true;
                }
            }

            candidate = default;
            return false;
        }

        private void ApplyHandHoverTarget(ManagedCard card)
        {
            if (card != null && !IsLiveHandCard(card))
            {
                card = null;
            }

            if (_hoveredCard != null && !IsLiveHandCard(_hoveredCard))
            {
                _hoveredCard = null;
            }

            if (_hoveredCard == card)
            {
                if (card != null)
                {
                    RefreshHandHoverAlphas(card);
                }

                return;
            }

            if (_hoveredCard != null)
            {
                ResetHandCardHoverVisual(_hoveredCard);
            }

            _hoveredCard = card;
            if (card == null)
            {
                ResetAllHandAlphas();
                return;
            }

            var view = card.View;
            if (view == null)
            {
                _hoveredCard = null;
                ResetAllHandAlphas();
                return;
            }

            var driver = view.GetComponent<CardVisualDriver>();
            driver?.SetTarget(CardVisualTarget.Hover);
            BoostHandCardHoverSorting(card);
            RefreshHandHoverAlphas(card);
        }

        /// <summary>
        /// CardManager.Release 后清 hover，避免 Update 仍持已 Destroy 的 View。
        /// </summary>
        public void NotifyCardReleased(int uid)
        {
            if (_hoveredCard == null || _hoveredCard.Uid != uid)
            {
                return;
            }

            ClearHandHoverState(_hoveredCard);
            _hoveredCard = null;
        }

        private static bool IsLiveHandCard(ManagedCard card)
        {
            // Unity 假 null：destroyed View 必须用 == null，不能靠 ?. 短路。
            return card != null && card.View != null;
        }

        /// <summary>
        /// 手牌在槽位中的权威布局世界坐标；hover 基准与回位均以此为准，避免反复触发累积上浮。
        /// </summary>
        internal bool TryGetHandLayoutWorldPosition(ManagedCard card, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (card == null || _slotContainer == null)
            {
                return false;
            }

            if (!_slotContainer.TryGetSlotOf(card, out var slotIndex))
            {
                return false;
            }

            worldPosition = _slotContainer.GetLayoutPosition(slotIndex);
            return true;
        }

        internal void ResetHandCardHoverVisual(ManagedCard card)
        {
            if (!IsLiveHandCard(card))
            {
                return;
            }

            var driver = card.View.GetComponent<CardVisualDriver>();
            driver?.SetTarget(CardVisualTarget.Base);
            RestoreHandCardSorting(card);
        }

        private void RestoreHandCardSorting(ManagedCard card)
        {
            if (card == null || _slotContainer == null)
            {
                return;
            }

            if (_slotContainer.TryGetSlotOf(card, out var slotIndex))
            {
                _slotContainer.ApplySortingOrder(card, slotIndex);
            }
        }

        private void ApplyAllHandSortingOrders()
        {
            _slotContainer?.ApplySortingOrders();
        }

        /// <summary>
        /// 刷新手牌 sorting（左高右低）；供 CardManager 在 HandCardMode 切换时委托。
        /// </summary>
        internal void EnsureHandSorting(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            if (_slotContainer != null && _slotContainer.TryGetSlotOf(card, out var slotIndex))
            {
                _slotContainer.ApplySortingOrder(card, slotIndex);
            }
            else
            {
                ApplyAllHandSortingOrders();
            }
        }

        /// <summary>
        /// 解析手牌域权威 sortingOrder（左高右低）；供 FlightSortingChannel 掉回目标序时委托。
        /// </summary>
        internal bool TryResolveSortingOrder(ManagedCard card, out int sortingOrder)
        {
            sortingOrder = 0;
            if (card == null || _slotContainer == null)
            {
                return false;
            }

            if (!_slotContainer.TryGetSlotOf(card, out var slotIndex))
            {
                return false;
            }

            sortingOrder = _slotContainer.ComputeSortingOrder(slotIndex);
            return true;
        }

        private void EnsureHandLayout(ManagedCard focus = null)
        {
            ApplyAllHandSortingOrders();
            if (focus != null)
            {
                SnapHandCardToLayout(focus);
            }
        }

        private int ResolveHandSlotForTrace(ManagedCard card)
        {
            if (card == null || _slotContainer == null)
            {
                return -1;
            }

            return _slotContainer.TryGetSlotOf(card, out var slot) ? slot : -1;
        }

        private void RefreshHandCardDisplay(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            CardEntityLifecycleHook.CardsOrNull()?.RefreshDisplayMode(card);
            EnsureHandLayout(card);
        }

        private void BoostHandCardHoverSorting(ManagedCard card)
        {
            var sortingGroup = card.View?.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder = layoutSettings.sortingOrderBase + layoutSettings.hoverSortingBoost;
            }
        }

        public bool TryCompleteDragApply()
        {
            if (_dragSession?.Card == null)
            {
                return false;
            }

            if (!_dragSession.PointerReleasedInZone && !_dragSession.PointerReleasedInRecycleZone)
            {
                return false;
            }

            CompleteDragApplyAsync().Forget();
            return true;
        }

        public void CancelDragAndReturnToHand()
        {
            if (_dragSession == null)
            {
                return;
            }

            CancelDragAndReturnInternalAsync().Forget();
        }

        private void BeginDragLoop()
        {
            _dragLoopCts?.Cancel();
            _dragLoopCts?.Dispose();
            _dragLoopCts = new CancellationTokenSource();
            SetRecycleUiActive(true);
            RunDragLoopAsync(_dragLoopCts.Token).Forget();
        }

        private async UniTaskVoid RunDragLoopAsync(CancellationToken cancellationToken)
        {
            var session = _dragSession;
            if (session?.Card?.Transform == null)
            {
                SetRecycleUiActive(false);
                return;
            }

            var card = session.Card;
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            cardManager?.SetDisplayMode(card, CardDisplayMode.DragCardMode);
            BoostDragSorting(card);

            var camera = Camera.main;
            if (camera == null)
            {
                Debug.LogWarning("[CardHandManager] 未找到 Main Camera，无法拖拽。");
                await FinishDragWithReturnAsync(session);
                return;
            }

            var dragZ = card.Transform.position.z;

            try
            {
                while (WorldPointerUtility.IsPrimaryHeld())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (!WorldPointerUtility.TryGetPointerScreen(out var dragScreen))
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                        continue;
                    }

                    var world = ScreenToWorldOnPlane(dragScreen, camera, dragZ);
                    card.Transform.position = world;

                    var inZone = IsPointInApplyZone(world);
                    var overGround = IsOverGroundCard(world);
                    CardOpacityUtility.SetAlpha(
                        card,
                        overGround ? layoutSettings.dragAlphaWhenOverGround : 1f);

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                }

                if (!WorldPointerUtility.TryGetPointerScreen(out var releaseScreen))
                {
                    await FinishDragWithReturnAsync(session);
                    return;
                }

                var releaseWorld = ScreenToWorldOnPlane(releaseScreen, camera, dragZ);
                session.PointerReleasedInRecycleZone = IsPointInRecycleZone(releaseWorld);
                session.PointerReleasedInZone = !session.PointerReleasedInRecycleZone
                    && IsPointInApplyZone(releaseWorld);

                if (!session.PointerReleasedInRecycleZone && !session.PointerReleasedInZone)
                {
                    await FinishDragWithReturnAsync(session);
                    return;
                }

                await CompleteDragApplyInternalAsync(session);
            }
            catch (OperationCanceledException)
            {
                if (_dragSession == session)
                {
                    await FinishDragWithReturnAsync(session);
                }
            }
        }

        private async UniTask CompleteDragApplyInternalAsync(DragSession session)
        {
            var card = session.Card;
            if (card == null)
            {
                ClearDragSession();
                SetRecycleUiActive(false);
                return;
            }

            if (session.PointerReleasedInRecycleZone)
            {
                var recycled = TrySubmitRecycleForDrag(card);
                if (!recycled)
                {
                    await FinishDragWithReturnAsync(session);
                    return;
                }

                RegistryTraceSink.NotifyUserInteraction?.Invoke("HandDragRecycle");
                ClearDragSession();
                CardOpacityUtility.ResetAlpha(card);
                SetRecycleUiActive(false);
                await ShatterCardAfterRecycleAsync(card);
                return;
            }

            var targetSlot = TryResolveGroundSlotUnderPoint(card.Transform.position);
            var validator = DragApplyValidator ?? DefaultDragApplyValidator;
            var approved = await validator(card, targetSlot);

            if (!approved)
            {
                await FinishDragWithReturnAsync(session);
                return;
            }

            RegistryTraceSink.NotifyUserInteraction?.Invoke("HandDragApply");
            ClearDragSession();
            CardOpacityUtility.ResetAlpha(card);
            SetRecycleUiActive(false);

            if (PresentationInputGates.BoardSelectModeActive)
            {
                await ParkCardForBoardSelectAsync(card);
                return;
            }

            await VanishCardAfterApplyAsync(card);
        }

        private async UniTaskVoid CompleteDragApplyAsync()
        {
            var session = _dragSession;
            if (session == null)
            {
                return;
            }

            await CompleteDragApplyInternalAsync(session);
        }

        private static async UniTask<bool> DefaultDragApplyValidator(ManagedCard card, int? targetGroundSlot)
        {
            await UniTask.CompletedTask;
            return true;
        }

        private async UniTask ParkCardForBoardSelectAsync(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            var anchor = field?.GetGroundAnchor(GroundSlotTopology.AvatarReservedSlot);
            if (anchor == null)
            {
                Debug.LogWarning("[CardHandManager] BoardSelect 驻留失败：缺少 Avatar 锚点。");
                BoardCardSelectModeController.RequestAbort("park-no-anchor");
                return;
            }

            _isBusy = true;
            try
            {
                var cardManager = CardEntityLifecycleHook.CardsOrNull();
                cardManager?.SetDisplayMode(card, CardDisplayMode.DragCardMode);
                BoostDragSorting(card);

                var target = anchor.position;
                target.z = card.Transform.position.z;

                var tween = CardDeckTween.MoveToWorld(
                    card.Transform,
                    target,
                    layoutSettings.moveDuration,
                    uid: card.Uid,
                    reason: "BoardSelect.Park");
                if (tween != null && tween.IsActive())
                {
                    var tcs = new UniTaskCompletionSource();
                    tween.OnComplete(() => tcs.TrySetResult());
                    tween.OnKill(() => tcs.TrySetResult());
                    await tcs.Task;
                }

                ArmBoardSelectParkedHitProxy(card);
                BoardCardSelectModeController.SetParkedItem(card.Uid);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                BoardCardSelectModeController.RequestAbort("park-exception");
            }
            finally
            {
                _isBusy = false;
            }
        }

        private static void ArmBoardSelectParkedHitProxy(ManagedCard card)
        {
            if (card?.View == null)
            {
                return;
            }

            var proxy = card.View.GetComponent<BoardSelectParkedCardHitProxy>();
            if (proxy == null)
            {
                proxy = card.View.gameObject.AddComponent<BoardSelectParkedCardHitProxy>();
            }

            proxy.SetArmed(true);
        }

        public static void DisarmBoardSelectParkedHitProxy(ManagedCard card)
        {
            if (card?.View == null)
            {
                return;
            }

            var proxy = card.View.GetComponent<BoardSelectParkedCardHitProxy>();
            proxy?.SetArmed(false);
        }

        /// <summary>多选提交成功后播放退场 effect 并释放视图。</summary>
        public async UniTask VanishParkedBoardSelectItemAsync(ManagedCard card)
        {
            DisarmBoardSelectParkedHitProxy(card);
            BoardCardSelectModeController.ClearParkedItem();
            await VanishCardAfterApplyAsync(card);
        }

        private async UniTask VanishCardAfterApplyAsync(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                CardEntityLifecycleHook.CardsOrNull()?.Release(card, "Hand.VanishNoTransform");
                return;
            }

            _isBusy = true;
            try
            {
                // Choice 路径可能先把手牌 scale 置 0；
                // SetDisplayMode(RemovedMode) 会把缩放弹回 1.08，造成「用完后又闪一下」。
                var alreadyHidden = card.Transform.localScale.sqrMagnitude <= 0.0001f;
                if (!alreadyHidden)
                {
                    CardEntityLifecycleHook.CardsOrNull()?.SetDisplayMode(card, CardDisplayMode.RemovedMode);
                    if (card.TryGetEffectManager(out var effectManager))
                    {
                        await effectManager.PlayUseAsync(CardBoardDirection.None, CancellationToken.None);
                    }
                    else
                    {
                        var initialScale = card.Transform.localScale;
                        await RunViewTweenAsync(
                            CardViewTween.ScaleDisappear(
                                card.Transform,
                                initialScale,
                                layoutSettings.applyVanishDuration),
                            CancellationToken.None);
                    }
                }

                var field = GroundFieldGeometryHook.FieldOrNull();
                if (field != null
                    && field.TryGetSlotOf(card.Uid, out var groundSlot)
                    && !GroundSlotTopology.IsAvatarReserved(groundSlot))
                {
                    RegistryTraceSink.RecordSuspectGroundRelease?.Invoke(
                        card.Uid,
                        "Hand.VanishAfterApply",
                        nameof(VanishCardAfterApplyAsync),
                        groundSlot);
                }

                CardOpacityUtility.ClearCache(card.Uid);
                CardEntityLifecycleHook.CardsOrNull()?.Release(card, "Hand.VanishAfterApply");
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>
        /// 回收兑金退场：走 Death 碎裂通道，不用 Use/缩小消失。
        /// </summary>
        private async UniTask ShatterCardAfterRecycleAsync(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                CardEntityLifecycleHook.CardsOrNull()?.Release(card, "Hand.RecycleShatterNoTransform");
                return;
            }

            _isBusy = true;
            try
            {
                CardEntityLifecycleHook.CardsOrNull()?.SetDisplayMode(card, CardDisplayMode.RemovedMode);
                if (card.TryGetEffectManager(out var effectManager))
                {
                    await effectManager.PlayDeathAsync(
                        selfSlot: 0,
                        selfDirection: CardBoardDirection.None,
                        cancellationToken: CancellationToken.None);
                }
                else
                {
                    // 无 EffectManager 时仍避免「缩小」手感：直接离手销毁。
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                CardOpacityUtility.ClearCache(card.Uid);
                CardEntityLifecycleHook.CardsOrNull()?.Release(card, "Hand.RecycleShatter");
            }
            finally
            {
                _isBusy = false;
            }
        }

        private async UniTask FinishDragWithReturnAsync(DragSession session)
        {
            var card = session?.Card;
            if (card == null)
            {
                ClearDragSession();
                return;
            }

            _isBusy = true;
            try
            {
                CardOpacityUtility.ResetAlpha(card);
                CardEntityLifecycleHook.CardsOrNull()?.SetDisplayMode(card, CardDisplayMode.HandCardMode);

                var slot = session.OriginHandSlot >= 0 ? session.OriginHandSlot : HandCount;
                if (!_slotContainer.TryInsertAt(slot, card, out var rippleMoves))
                {
                    Debug.LogWarning("[CardHandManager] 回手失败，手牌已满。");
                    CardEntityLifecycleHook.CardsOrNull()?.Release(card, "Hand.DragReturnFail");
                    ClearDragSession();
                    return;
                }

                await CardDeckTween.MoveRippleAsync(rippleMoves, layoutSettings.moveDuration);
                RefreshHandCardDisplay(card);
                SnapHandCardToLayout(card);
            }
            finally
            {
                _isBusy = false;
                ClearDragSession();
                SetRecycleUiActive(false);
            }
        }

        private void SnapHandCardToLayout(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return;
            }

            if (!TryGetHandLayoutWorldPosition(card, out var layoutPosition))
            {
                return;
            }

            SlotFrameConvergence.SanitizeForSanctuary(card, "Hand.SnapLayout.Sanitize");
            CardDeckTween.KillMotion(card.Transform);
            card.Transform.position = layoutPosition;

            var driver = card.View.GetComponent<CardVisualDriver>();
            driver?.SnapToDisplayMode();
        }

        private async UniTask CancelDragAndReturnInternalAsync()
        {
            var session = _dragSession;
            if (session == null)
            {
                return;
            }

            _dragLoopCts?.Cancel();
            await FinishDragWithReturnAsync(session);
        }

        private void ClearDragSession()
        {
            _dragSession = null;
        }

        private void ClearHandHoverState(ManagedCard card)
        {
            if (card != null)
            {
                ResetHandCardHoverVisual(card);
            }

            if (_hoveredCard == card)
            {
                _hoveredCard = null;
            }

            ResetAllHandAlphas();
        }

        private void RefreshHandHoverAlphas(ManagedCard hovered)
        {
            CardOpacityUtility.ResetAlpha(hovered);

            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (!_slotContainer.TryGetCardAt(i, out var card) || card == null || card == hovered)
                {
                    continue;
                }

                CardOpacityUtility.SetAlpha(card, layoutSettings.nonHoveredAlpha);
            }
        }

        private void ResetAllHandAlphas()
        {
            for (var i = 0; i < layoutSettings.maxSlots; i++)
            {
                if (_slotContainer.TryGetCardAt(i, out var card) && card != null)
                {
                    CardOpacityUtility.ResetAlpha(card);
                }
            }
        }

        private void BoostDragSorting(ManagedCard card)
        {
            var sortingGroup = card.View?.GetComponent<SortingGroup>();
            if (sortingGroup != null)
            {
                sortingGroup.sortingOrder =
                    CardDisplayModeVisuals.GetSortingOrder(CardDisplayMode.DragCardMode)
                    + layoutSettings.dragSortingBoost;
            }
        }

        private bool IsPointInApplyZone(Vector3 worldPoint)
        {
            if (applyZoneCollider == null)
            {
                return false;
            }

            return applyZoneCollider.bounds.Contains(worldPoint);
        }

        private bool IsPointInRecycleZone(Vector3 worldPoint)
        {
            if (recycleZoneCollider == null
                || !recycleZoneCollider.enabled
                || !recycleZoneCollider.gameObject.activeInHierarchy)
            {
                return false;
            }

            return recycleZoneCollider.bounds.Contains(worldPoint);
        }

        private void SetRecycleUiActive(bool active)
        {
            if (recycleZoneCollider != null)
            {
                recycleZoneCollider.gameObject.SetActive(active);
            }

            if (recycleNotice != null)
            {
                recycleNotice.SetActive(active);
            }
        }

        private static bool TrySubmitRecycleForDrag(ManagedCard card)
        {
            if (card == null || card.Uid <= 0)
            {
                return false;
            }

            if (RecycleItemInputHook.TrySubmitRecycleItem == null)
            {
                Debug.LogWarning("[CardHandManager] RecycleItemInputHook.TrySubmitRecycleItem 未装配。");
                return false;
            }

            return RecycleItemInputHook.TrySubmitRecycleItem(card.Uid);
        }

        /// <summary>
        /// Director flush Recycle 后：Core 已 Apply。若手牌仍持有该卡（缓冲路径未先 Vanish），此处离手。
        /// </summary>
        private void OnRecycleItemIntentFlushed(int itemUid, CoreCommandResult result)
        {
            if (result == null || !result.Accepted || itemUid <= 0)
            {
                return;
            }

            if (!TryRemoveFromHand(itemUid, out var removed) || removed == null)
            {
                return;
            }

            ShatterCardAfterRecycleAsync(removed).Forget();
        }

        private static bool IsOverGroundCard(Vector3 worldPoint)
        {
            return TryResolveGroundSlotUnderPoint(worldPoint, out var slot)
                   && GroundFieldGeometryHook.FieldOrNull() is { } field
                   && field.TryGetCardAt(slot, out _);
        }

        private static int? TryResolveGroundSlotUnderPoint(Vector3 worldPoint)
        {
            return TryResolveGroundSlotUnderPoint(worldPoint, out var slot) ? slot : null;
        }

        private static bool TryResolveGroundSlotUnderPoint(Vector3 worldPoint, out int slot)
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null
                || !field.TryResolveSlotAtWorld(new Vector2(worldPoint.x, worldPoint.y), out slot))
            {
                slot = 0;
                return false;
            }

            return true;
        }

        private static Vector3 ScreenToWorldOnPlane(Vector3 screenPosition, Camera camera, float worldZ)
        {
            var point = camera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, camera.WorldToScreenPoint(new Vector3(0f, 0f, worldZ)).z));
            point.z = worldZ;
            return point;
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

        private void ResolveSceneReferences()
        {
            if (handAnchorsRoot == null)
            {
                var anchors = GameObject.Find("Anchors");
                if (anchors != null)
                {
                    handAnchorsRoot = anchors.transform.Find("CardHandAnchors");
                }
            }

            if (applyZoneCollider == null && handAnchorsRoot != null)
            {
                var zone = handAnchorsRoot.Find("HandcardApplyZone");
                if (zone != null)
                {
                    applyZoneCollider = zone.GetComponent<Collider>();
                }
            }

            if (handAnchorsRoot != null)
            {
                if (recycleZoneCollider == null)
                {
                    var recycleZone = handAnchorsRoot.Find("HandcardRecycleZone");
                    if (recycleZone != null)
                    {
                        recycleZoneCollider = recycleZone.GetComponent<Collider>();
                    }
                }

                if (recycleNotice == null)
                {
                    var notice = handAnchorsRoot.Find("CardRecycleNotice");
                    if (notice != null)
                    {
                        recycleNotice = notice.gameObject;
                    }
                }
            }
        }

        private void CacheAnchors()
        {
            _handAnchors.Clear();
            _handAnchors.AddRange(CardHandAnchorUtility.GetSortedHandAnchors(handAnchorsRoot, layoutSettings.maxSlots));
            _slotContainer.SetLayoutAnchorPositions(_handAnchors);
        }

        private void InitializeLayoutOrigin()
        {
            var leftX = 0f;
            var baseY = 0f;
            var baseZ = 0f;

            if (_handAnchors.Count > 0 && _handAnchors[0] != null)
            {
                var first = _handAnchors[0].position;
                leftX = first.x;
                baseY = first.y;
                baseZ = first.z;
            }
            else if (handAnchorsRoot != null)
            {
                leftX = handAnchorsRoot.position.x;
                baseY = handAnchorsRoot.position.y;
                baseZ = handAnchorsRoot.position.z;
            }

            _slotContainer.SetLayoutOrigin(leftX, baseY, baseZ);
        }
    }
}
