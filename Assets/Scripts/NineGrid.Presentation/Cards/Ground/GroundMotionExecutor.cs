using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Vfx;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using NineGrid.Presentation.Systems;
using QFramework;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地运动执行：Rotate/Hop/Move/Remove/RevealAvatar/Swap 等。
    /// 由 GroundFieldGeometrySystem 持有；不持有场景锚点。
    /// </summary>
    internal sealed class GroundMotionExecutor : IDealFlightHost
    {
        private readonly GroundOccupancyIndex _index;
        private IGroundFieldView _view;
        private DealFlightCoordinator _deal;
        private readonly PresentationClock _presentationClock = new();
        private BeatGrid _beatGrid;
        private LeaseArbiter _leaseArbiter;
        private bool _isBusy;
        private CancellationTokenSource _fieldAnimCts;
        private Action _onFieldMaybeClear;
        private Action<int> _onEmptySlotClicked;

        public GroundMotionExecutor(GroundOccupancyIndex index)
        {
            _index = index ?? throw new ArgumentNullException(nameof(index));
            _index.OccupancyMaybeClear += HandleOccupancyMaybeClear;
        }

        public void BindView(IGroundFieldView view, Action onFieldMaybeClear, Action<int> onEmptySlotClicked = null)
        {
            _view = view;
            _onFieldMaybeClear = onFieldMaybeClear;
            _onEmptySlotClicked = onEmptySlotClicked;
            _beatGrid = new BeatGrid(_presentationClock);
            _leaseArbiter = new LeaseArbiter(OnDisciplineBAlarm);
            var token = view != null ? view.DestroyToken : CancellationToken.None;
            _deal = new DealFlightCoordinator(this, token);
        }

        public void UnbindView()
        {
            CancelFieldAnimations();
            _deal?.CancelAll();
            _deal = null;
            _view = null;
            _onFieldMaybeClear = null;
            _onEmptySlotClicked = null;
            _beatGrid = null;
            _leaseArbiter = null;
            _isBusy = false;
        }

        public GroundOccupancyIndex Index => _index;
        public DealFlightCoordinator Deal => _deal;
        public bool IsFieldBusy => _isBusy;
        public int ActiveDealFlightCount => _deal?.ActiveCount ?? 0;

        public bool IsBusy
        {
            get
            {
                if (PresentationInputGates.ChoiceOverlayActive
                    || PresentationInputGates.MainlineBusy)
                {
                    return true;
                }

                var battle = FieldBattlePresentationHook.BattleOrNull();
                return _isBusy || IsBattlePresentationBusy();
            }
        }

        private static bool IsBattlePresentationBusy()
        {
            var system = NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>();
            return system != null && system.IsBusy;
        }

        GroundFieldLayoutSettings IDealFlightHost.LayoutSettings =>
            _view?.LayoutSettings ?? new GroundFieldLayoutSettings();

        public GroundFieldLayoutSettings LayoutSettings =>
            _view?.LayoutSettings ?? new GroundFieldLayoutSettings();

        private void HandleOccupancyMaybeClear()
        {
            if (HasLivingNonAvatarCard())
            {
                return;
            }

            _onFieldMaybeClear?.Invoke();
        }

        private void OnDisciplineBAlarm(string reason)
        {
            Debug.LogWarning("[GroundMotionExecutor] " + reason);
            var code = reason != null && reason.IndexOf("Preempted", StringComparison.Ordinal) >= 0
                ? "DisciplineBPreemptCommitted"
                : "DisciplineBSyncConflict";
            var uid = TryParseCardIdFromLeaseReason(reason);
            ConvergenceDiagProbe.DisciplineB(
                uid: uid,
                code: code,
                reason: reason,
                layer: TowerLayer.SlotFrame.ToString(),
                verdict: code);
        }

        private static int TryParseCardIdFromLeaseReason(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                return 0;
            }

            const string marker = "card=";
            var idx = reason.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
            {
                return 0;
            }

            var start = idx + marker.Length;
            var end = start;
            while (end < reason.Length && char.IsDigit(reason[end]))
            {
                end++;
            }

            return end > start
                   && int.TryParse(reason.Substring(start, end - start), out var uid)
                ? uid
                : 0;
        }

        private void RefreshSlotHitCollider(int slot)
        {
            // ADR-0023：九框恒开；禁止用启停 collider 表达规则。
            _view?.RefreshSlotHit(slot, hitEnabled: true);
        }

        private void RefreshAllSlotHitColliders()
        {
            _view?.RefreshAllSlotHits(_ => true);
        }

        public void RefreshSlotHitColliders()
        {
            RefreshAllSlotHitColliders();
        }

        private static void SyncGroundCardClaim(ManagedCard card, int slot)
        {
            if (card?.View == null)
            {
                return;
            }

            var proxy = card.View.GetComponent<GroundCardHitProxy>();
            if (proxy == null)
            {
                return;
            }

            if (!proxy.isActiveAndEnabled
                || card.CoreKind == CardPresentationKind.Avatar
                || card.CoreKind == CardPresentationKind.Unknown)
            {
                proxy.ReleaseClaim();
                return;
            }

            proxy.SyncClaimForSlot(slot);
        }

        private static void ReleaseGroundCardClaim(ManagedCard card)
        {
            if (card?.View == null)
            {
                return;
            }

            card.View.GetComponent<GroundCardHitProxy>()?.ReleaseClaim();
        }

        private bool TryGetAnchor(int slot, out Transform anchor)
        {
            if (_view != null)
            {
                return _view.TryGetAnchor(slot, out anchor);
            }

            anchor = null;
            return false;
        }

        public Transform GetGroundAnchor(int slot)
        {
            return _view != null ? _view.GetGroundAnchor(slot) : null;
        }

        private static bool IsValidSlot(int slot)
        {
            return GroundSlotTopology.IsValidSlot(slot);
        }

                public GroundFieldSnapshot GetSnapshot()
        {
            return _index.BuildSnapshot();
        }

        public bool TryGetCardAt(int slot, out ManagedCard card)
        {
            card = null;
            if (!IsValidSlot(slot) || _index.GetUidAt(slot) == 0)
            {
                return false;
            }

            var uid = _index.GetUidAt(slot);
            if (CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out card))
            {
                return true;
            }

            CardPresentationProbe.RegistryMiss(
                uid,
                "Ground.TryGetCardAt",
                "slot=" + slot.ToString(CultureInfo.InvariantCulture));
            CardEntityLifecycleHook.CardsOrNull()?.AuditRegistryIntegrity("Ground.TryGetCardAt");
            return false;
        }

        public bool TryGetSlotOf(int uid, out int slot)
        {
            return _index.TryGetSlotOf(uid, out slot);
        }

        /// <summary>
        /// 查找 uid 占用的场地格（含双向表短暂不一致时的扫描；不 force-sync 写回）。
        /// </summary>
        public bool TryFindOccupiedSlotForUid(int uid, out int slot)
        {
            return _index.TryFindOccupiedSlotForUid(uid, out slot);
        }

        /// <summary>
        /// 按 uid 清占格（不 Release 视图）。Release 前守卫用，避免「占格在、视图无」幽灵格。
        /// </summary>
        public bool TryClearOccupancyForUid(int uid, bool skipBusyGuard = false)
        {
            if (!TryFindOccupiedSlotForUid(uid, out var slot))
            {
                return false;
            }

            return ClearSlotOccupancy(slot, skipBusyGuard);
        }

        /// <summary>#10 几何登记是否为空（命中代理用），非 Core 逻辑占格。</summary>
        public bool IsEmpty(int slot)
        {
            return _index.IsEmpty(slot);
        }

        /// <summary>#10 几何上是否可落视图（非逻辑 Placeable）。</summary>
        public bool IsPlaceable(int slot)
        {
            return _index.IsPlaceable(slot);
        }

        public IReadOnlyList<int> GetEmptyPlaceableSlots()
        {
            return _index.GetEmptyPlaceableSlots();
        }

        public bool HasFullOpeningRing()
        {
            return _index.HasFullOpeningRing();
        }

        public bool TryGetRandomOccupiedCard(out ManagedCard card)
        {
            card = null;
            var candidates = new List<int>();
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (_index.GetUidAt(slot) != 0)
                {
                    candidates.Add(_index.GetUidAt(slot));
                }
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            var uid = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            if (!CardEntityLifecycleHook.CardsOrNull().TryGetTracked(uid, "Ground.TryGetRandomOccupiedCard", out card))
            {
                return false;
            }

            return true;
        }

        public bool RequestPlaceCard(int slot, ManagedCard card, bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法接受放置申请。");
                return false;
            }

            if (card == null)
            {
                return false;
            }

            if (!IsPlaceable(slot))
            {
                Debug.LogWarning($"[GroundMotionExecutor] 格位不可放置: slot={slot}");
                return false;
            }

            if (!_index.TryRegister(slot, card.Uid))
            {
                return false;
            }

            CardEntityLifecycleHook.CardsOrNull().SetDisplayMode(card, CardDisplayMode.GroundCardMode);
            RefreshSlotHitCollider(slot);
            SyncGroundCardClaim(card, slot);
            return true;
        }

        /// <summary>
        /// 登记到格位；可选硬贴锚点（冷启动 spawn）或短预算 L2 收敛（已在复杂域的 reanchor）。
        /// </summary>
        public bool RequestPlaceCardAtAnchor(
            int slot,
            ManagedCard card,
            bool skipBusyGuard = false,
            bool snapToAnchor = true,
            float convergeSourceTime = 0.2f)
        {
            if (!RequestPlaceCard(slot, card, skipBusyGuard))
            {
                return false;
            }

            if (card?.Transform == null
                || !TryGetAnchor(slot, out var anchor)
                || anchor == null)
            {
                return true;
            }

            if (snapToAnchor)
            {
                SlotFrameConvergence.SnapHome(card, anchor.position, "Ground.Place.Snap", card.Uid);
                CardEdgeDustFx.PlayPlace(card);
                CardPresentationProbe.SnapSet(
                    card.Uid,
                    anchor.position,
                    "Ground.Place.Snap",
                    slot: slot,
                    killedTween: true,
                    reason: "placeAtAnchor");
                CardEntityLifecycleHook.CardsOrNull().RefreshDisplayMode(card);
            }
            else
            {
                var duration = convergeSourceTime > 0f ? convergeSourceTime : 0.2f;
                SlotFrameConvergence.ConvergeVisualToWorld(
                    card,
                    anchor.position,
                    duration,
                    CommitmentKind.Async);
                CardPresentationProbe.SnapSet(
                    card.Uid,
                    SlotFrameConvergence.GetVisualWorldPosition(card),
                    "Ground.Place.Converge",
                    slot: slot,
                    killedTween: false,
                    reason: "placeAtAnchorConverge");
                CardEntityLifecycleHook.CardsOrNull().RefreshDisplayMode(card);
            }

            return true;
        }

        /// <summary>
        /// 仅迁移占格（不销毁视图）：从当前格挪到目标格，可选瞬移到锚点。战后 Sync 安全网用。
        /// </summary>
        public bool RequestRelocateOccupancy(
            int uid,
            int toSlot,
            bool snapToAnchor,
            bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法迁移占格。");
                return false;
            }

            if (!IsValidSlot(toSlot) || GroundSlotTopology.IsAvatarReserved(toSlot))
            {
                return false;
            }

            if (!_index.TryGetSlotOf(uid, out var fromSlot))
            {
                return false;
            }

            if (fromSlot == toSlot)
            {
                if (snapToAnchor
                    && CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out var same)
                    && same?.Transform != null
                    && TryGetAnchor(toSlot, out var sameAnchor)
                    && sameAnchor != null)
                {
                    SlotFrameConvergence.SnapHome(same, sameAnchor.position, "Ground.Relocate.Snap", uid);
                    CardPresentationProbe.SnapSet(
                        uid,
                        sameAnchor.position,
                        "Ground.Relocate.Snap",
                        slot: toSlot,
                        killedTween: true,
                        reason: "relocateSameSlot");
                }

                return true;
            }

            if (!IsEmpty(toSlot))
            {
                Debug.LogWarning($"[GroundMotionExecutor] 目标格已占用，无法迁移 uid={uid} → slot={toSlot}");
                return false;
            }

            _index.Unregister(fromSlot);
            if (!_index.TryRegister(toSlot, uid))
            {
                // 回滚：目标格被占时恢复原格，避免 uid 从占格表消失却留下视图。
                _index.TryRegister(fromSlot, uid);
                return false;
            }

            RefreshSlotHitCollider(fromSlot);
            RefreshSlotHitCollider(toSlot);

            if (CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out var relocated))
            {
                SyncGroundCardClaim(relocated, toSlot);
            }

            if (snapToAnchor
                && CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out var card)
                && card?.Transform != null
                && TryGetAnchor(toSlot, out var anchor)
                && anchor != null)
            {
                SlotFrameConvergence.SnapHome(card, anchor.position, "Ground.Relocate.Snap", uid);
                CardPresentationProbe.SnapSet(
                    uid,
                    anchor.position,
                    "Ground.Relocate.Snap",
                    slot: toSlot,
                    killedTween: true,
                    reason: "relocate");
                CardEntityLifecycleHook.CardsOrNull().RefreshDisplayMode(card);
            }

            return true;
        }

        /// <summary>
        /// 按 Core CardMoved 列表更新占格并并行 hop（不整圈盲转）。交战忙碌时可 skipBusyGuard。
        /// </summary>
        /// <param name="commitment">Sync = 租约必达；Async = 无租约后发先至。</param>
        public UniTask ApplyBoardMovesAndHopAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken = default,
            bool skipBusyGuard = false,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            return ApplyBoardMovesAndHopInternalAsync(
                moves,
                cancellationToken,
                skipBusyGuard,
                commitment);
        }

        /// <summary>
        /// Avatar 专用入场：登记到格5并播缩放出现。不走 <see cref="IsPlaceable"/>，不锁 busy，便于与开局外圈发牌并行。
        /// </summary>
        public UniTask RequestRevealAvatarAsync(ManagedCard avatar, CancellationToken cancellationToken = default)
        {
            return RequestRevealAvatarInternalAsync(avatar, cancellationToken);
        }

        private async UniTask RequestRevealAvatarInternalAsync(
            ManagedCard avatar,
            CancellationToken cancellationToken)
        {
            // 开局揭示只门禁场地自身 busy：覆盖层 / PresentationLocked / 交战忙不应挡 Avatar 落格。
            if (IsFieldBusy)
            {
                Debug.LogWarning(
                    "[GroundMotionExecutor] 场地自身忙碌，无法揭示 Avatar。"
                    + $" choiceOverlay={PresentationInputGates.ChoiceOverlayActive}"
                    + $" presentationLocked={PresentationInputGates.HasExternalHold}"
                    + $" fieldSelfBusy={IsFieldBusy}"
                    + $" battleBusy={IsBattlePresentationBusy()}"
                    + $" fieldBusy={IsBusy}");
                return;
            }

            if (avatar == null)
            {
                Debug.LogWarning("[GroundMotionExecutor] Avatar 卡为空，无法揭示。");
                return;
            }

            var slot = GroundSlotTopology.AvatarReservedSlot;
            if (!IsEmpty(slot))
            {
                Debug.LogWarning($"[GroundMotionExecutor] Avatar 格位已被占用: slot={slot}");
                return;
            }

            if (!TryGetAnchor(slot, out var anchor) || anchor == null)
            {
                Debug.LogWarning($"[GroundMotionExecutor] Avatar 锚点缺失: slot={slot}");
                return;
            }

            if (!_index.TryRegister(slot, avatar.Uid))
            {
                Debug.LogWarning($"[GroundMotionExecutor] Avatar 登记失败: slot={slot}");
                return;
            }

            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            cardManager.SetDisplayMode(avatar, CardDisplayMode.GroundCardMode);
            SlotFrameConvergence.SnapHome(avatar, anchor.position, "Ground.AvatarReveal", avatar.Uid);
            RefreshSlotHitCollider(slot);

            var finalScale = CardDisplayModeVisuals.GetBaseLocalScale(
                CardDisplayMode.GroundCardMode,
                avatar.View != null
                    ? (avatar.View.GetComponent<CardVisualDriver>()?.AuthoredBaseScale ?? Vector3.one)
                    : Vector3.one);
            avatar.Transform.localScale = Vector3.zero;

            var duration = LayoutSettings != null ? LayoutSettings.avatarRevealDuration : 0.28f;
            try
            {
                await RunViewTweenAsync(
                    CardViewTween.ScaleAppear(avatar.Transform, finalScale, duration),
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (avatar.Transform != null)
                {
                    avatar.Transform.localScale = finalScale;
                }

                throw;
            }

            if (avatar.Transform != null)
            {
                cardManager.RefreshDisplayMode(avatar);
            }
        }

        public bool RequestMoveCard(int fromSlot, int toSlot, bool animate)
        {
            if (IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法移动卡牌。");
                return false;
            }

            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot) || fromSlot == toSlot)
            {
                return false;
            }

            if (_index.GetUidAt(fromSlot) == 0 || _index.GetUidAt(toSlot) != 0)
            {
                return false;
            }

            var uid = _index.GetUidAt(fromSlot);
            if (!CardEntityLifecycleHook.CardsOrNull().TryGetTracked(
                    uid,
                    "Ground.RequestMoveCard",
                    out var card,
                    "fromSlot=" + fromSlot.ToString(CultureInfo.InvariantCulture))
                || card?.Transform == null)
            {
                return false;
            }

            _index.Unregister(fromSlot);
            if (!_index.TryRegister(toSlot, uid))
            {
                _index.TryRegister(fromSlot, uid);
                return false;
            }

            RefreshSlotHitCollider(fromSlot);
            RefreshSlotHitCollider(toSlot);

            if (animate)
            {
                // ADR-0023：跳跃中不认领；落地后再登记，避免悬停/点击与肉眼卡错位。
                ReleaseGroundCardClaim(card);
                MoveCardAnimatedThenClaimAsync(card, fromSlot, toSlot, EnsureFieldAnimToken())
                    .Forget();
            }
            else
            {
                if (TryGetAnchor(toSlot, out var anchor))
                {
                    SlotFrameConvergence.SnapHome(card, anchor.position, "Ground.Move.Snap", uid);
                    CardEntityLifecycleHook.CardsOrNull().RefreshDisplayMode(card);
                }

                SyncGroundCardClaim(card, toSlot);
            }

            return true;
        }

        /// <summary>
        /// 将卡从场地表移除但不销毁视图，供手牌管理器接管。
        /// </summary>
        public bool TryTakeCardFromField(
            int uid,
            out ManagedCard card,
            bool startExplore = false,
            bool skipBusyGuard = false)
        {
            card = null;
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法取走卡牌。");
                return false;
            }

            if (!_index.TryGetSlotOf(uid, out var slot))
            {
                return false;
            }

            if (!CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out card))
            {
                CardPresentationProbe.RegistryMiss(
                    uid,
                    "Ground.TryTakeCardFromField",
                    "slot=" + slot.ToString(CultureInfo.InvariantCulture) + ",healVacate=1");
                _index.Unregister(slot, "TryTakeCardFromField.missView");
                RefreshSlotHitCollider(slot);
                if (startExplore)
                {
                    _deal?.StartExplore(slot);
                }

                return false;
            }

            VacateSlotForExplore(slot, card, playRemoveAnim: false, skipBusyGuard: true, startExplore: startExplore);
            return true;
        }

        public bool RequestRemoveFromField(
            int uid,
            bool animate,
            bool skipBusyGuard = false,
            bool startExplore = true)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法移除卡牌。");
                return false;
            }

            if (!_index.TryGetSlotOf(uid, out var slot))
            {
                return false;
            }

            if (!CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out var card))
            {
                CardPresentationProbe.RegistryMiss(
                    uid,
                    "Ground.RequestRemoveFromField",
                    "slot=" + slot.ToString(CultureInfo.InvariantCulture) + ",healVacate=1");
                _index.Unregister(slot, "RequestRemoveFromField.missView");
                RefreshSlotHitCollider(slot);
                if (startExplore)
                {
                    _deal?.StartExplore(slot);
                }

                return false;
            }

            VacateSlotForExplore(slot, card, animate, skipBusyGuard, startExplore);
            if (!animate)
            {
                // 引用身份保护：即使同步路径也走 ManagedCard，避免误伤复用 uid 的新实体。
                CardEntityLifecycleHook.CardsOrNull().Release(card, "Ground.RemoveImmediate");
            }

            return true;
        }

        internal void VacateSlotForExplore(
            int slot,
            ManagedCard card,
            bool playRemoveAnim,
            bool skipBusyGuard = false,
            bool startExplore = true)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法清格探求。");
                return;
            }

            if (!IsValidSlot(slot) || _index.GetUidAt(slot) == 0)
            {
                return;
            }

            var occupantUid = _index.GetUidAt(slot);
            if (card != null && occupantUid != card.Uid)
            {
                CardPresentationProbe.Anomaly(
                    card.Uid,
                    "VacateUidMismatch",
                    "slot=" + slot + ";expected=" + card.Uid + ";actual=" + occupantUid,
                    "Ground.VacateSlotForExplore",
                    layer: "L0",
                    verdict: "refused");
                Debug.LogWarning(
                    $"[GroundMotionExecutor] 拒绝卸格：slot={slot} 期望 uid={card.Uid}，实际 occupant={occupantUid}。");
                return;
            }

            // 同批 Deal→Remove：飞行中卡被 Vacate/Release 前必须取消 probe，否则 WaitAll 挂死。
            var flightUid = card != null ? card.Uid : occupantUid;
            if (flightUid > 0)
            {
                CancelDealFlightForUid(flightUid, "VacateSlotForExplore");
            }

            ReleaseGroundCardClaim(card);
            _index.Unregister(slot);
            RefreshSlotHitCollider(slot);
            if (startExplore)
            {
                _deal?.StartExplore(slot);
            }

            if (card == null)
            {
                return;
            }

            if (playRemoveAnim)
            {
                RemoveCardAnimatedAsync(card, slot, EnsureFieldAnimToken()).Forget();
            }
        }

        public bool PlaceForExplore(int slot, ManagedCard card)
        {
            if (card == null || !IsPlaceable(slot))
            {
                return false;
            }

            if (!_index.TryRegister(slot, card.Uid))
            {
                return false;
            }

            CardEntityLifecycleHook.CardsOrNull().SetDisplayMode(card, CardDisplayMode.GroundCardMode);
            RefreshSlotHitCollider(slot);
            // 调用方须已结束 in-flight，否则 Sync 会因 IsDealInFlight 拒领。
            SyncGroundCardClaim(card, slot);
            return true;
        }

        /// <inheritdoc cref="IDealFlightHost.NotifyDealFlightLanded"/>
        public void NotifyDealFlightLanded(ManagedCard card, int slot)
        {
            SyncGroundCardClaim(card, slot);
        }

        public bool TryGetExploreAnchorPosition(int slot, out Vector3 position)
        {
            position = default;
            if (!TryGetAnchor(slot, out var anchor) || anchor == null)
            {
                return false;
            }

            position = anchor.position;
            return true;
        }

        /// <summary>
        /// 启动 Drain 补牌 L2 收敛飞牌（逻辑占格后视觉收敛到格锚）。
        /// </summary>
        internal DealFlightHandle LaunchDrainDealFlight(
            ManagedCard card,
            int targetSlot,
            Vector3 launchPos,
            DealFlightContext context)
        {
            // 无飞牌协调器时不注销认领（否则 fallback 动画路径永远不回登记）。
            if (_deal == null)
            {
                return null;
            }

            // ADR-0023：起飞即注销，落地由 DealFlightCoordinator → NotifyDealFlightLanded 再登记。
            ReleaseGroundCardClaim(card);
            return _deal.LaunchDrainFlight(card, targetSlot, launchPos, context);
        }

        public bool IsDealInFlight(int uid)
        {
            return _deal != null && _deal.IsInFlight(uid);
        }

        /// <summary>
        /// 移除/卸格前取消该 uid 的补牌飞牌，避免 ActiveCount 粘死与 dealFlight choreo 泄漏。
        /// </summary>
        public bool CancelDealFlightForUid(int uid, string reason = null)
        {
            return _deal != null && _deal.CancelFlightForUid(uid, reason ?? "vacate");
        }

        /// <summary>
        /// 等到当前所有 Drain/Explore 补牌飞牌结束（ActiveCount==0）。Sync 前调用，避免 Phase2 误杀在途卡。
        /// </summary>
        public async UniTask WaitAllActiveDealFlightsAsync(CancellationToken cancellationToken)
        {
            if (_deal == null)
            {
                return;
            }

            while (_deal.ActiveCount > 0)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public static async UniTask WaitDealFlightsSettledAsync(
            IReadOnlyList<DealFlightHandle> handles,
            CancellationToken cancellationToken)
        {
            await DealFlightCoordinator.WaitAllSettledAsync(handles, cancellationToken);
        }

        /// <summary>
        /// 清场。生命周期清理（回主菜单/重开）应传 <paramref name="force"/>，
        /// 否则忙碌态会直接放弃，留下占格与 _isBusy 残留。
        /// </summary>
        public void ClearField(bool force = false)
        {
            if (!force && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法清场。");
                return;
            }

            if (force)
            {
                NineGridArchitecture.Interface?.GetSystem<IFieldBattlePresentationSystem>()?.CancelBattleWork();
                CancelFieldAnimations();
                _isBusy = false;
            }

            _deal?.CancelAll();

            // 非 force：先 Vacate 再 Release，与 RequestRemoveFromField 契约一致，避免幽灵占格。
            if (!force)
            {
                var cardManager = CardEntityLifecycleHook.CardsOrNull();
                if (cardManager != null)
                {
                    for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
                    {
                        var uid = _index.GetUidAt(slot);
                        if (uid == 0)
                        {
                            continue;
                        }

                        _index.Unregister(slot, "ClearField");
                        if (cardManager.TryGet(uid, out var card) && card != null)
                        {
                            ReleaseGroundCardClaim(card);
                            cardManager.Release(card, "Ground.ClearField");
                        }
                    }
                }
            }

            _index.Clear();
            RefreshAllSlotHitColliders();
        }

        private void CancelFieldAnimations()
        {
            if (_fieldAnimCts == null)
            {
                return;
            }

            _fieldAnimCts.Cancel();
            _fieldAnimCts.Dispose();
            _fieldAnimCts = null;
        }

        private CancellationToken EnsureFieldAnimToken()
        {
            if (_fieldAnimCts == null)
            {
                _fieldAnimCts = new CancellationTokenSource();
            }

            return _fieldAnimCts.Token;
        }

        public void OnEmptySlotClicked(int slot)
        {
            TryHandleEmptySlotClick(slot);
        }

        public bool TryHandleEmptySlotClick(int slot)
        {
            // 无认领者时的走格/探索提交；几何非空（含 Avatar）不再静默吞掉——合法性交 IntentIntake（ADR-0023）。
            var walkEnabled = BoardWalkInputHook.IsEnabled != null && BoardWalkInputHook.IsEnabled();

            // 轴二所有权快拒；轴一互斥只认 MainlineBusy（IntentIntake），勿再轮询 FieldBusy/BattleBusy。
            // 房内场地板（商店等）不得整段持有 ChoiceOverlay；此处拦截是给局内浮层宝箱保底。
            if (PresentationInputGates.ChoiceOverlayActive
                || PresentationInputGates.OpeningPresentationActive
                || PresentationInputGates.BoardSelectModeActive)
            {
                Debug.Log(
                    "[GroundMotionExecutor] 空槽点击被门禁拦截 slot=" + slot
                    + " walkEnabled=" + walkEnabled
                    + " choiceOverlay=" + PresentationInputGates.ChoiceOverlayActive
                    + " opening=" + PresentationInputGates.OpeningPresentationActive
                    + " boardSelect=" + PresentationInputGates.BoardSelectModeActive
                    + " owner=" + PresentationInputGates.CurrentOwner);
                return false;
            }

            if (walkEnabled)
            {
                if (BoardWalkInputHook.TrySubmitBoardWalk == null)
                {
                    Debug.LogWarning("[GroundMotionExecutor] BoardWalkInputHook.TrySubmitBoardWalk 未装配。");
                    return false;
                }

                Debug.Log($"[GroundMotionExecutor] 未认领格点击 → BoardWalk: slot={slot}");
                _onEmptySlotClicked?.Invoke(slot);
                var accepted = BoardWalkInputHook.TrySubmitBoardWalk(slot);
                if (!accepted)
                {
                    Debug.LogWarning(
                        "[GroundMotionExecutor] BoardWalk submit returned false slot=" + slot
                        + " owner=" + PresentationInputGates.CurrentOwner);
                }

                return accepted;
            }

            if (ExploreInputHook.TrySubmitExplore == null)
            {
                Debug.LogWarning("[GroundMotionExecutor] ExploreInputHook.TrySubmitExplore 未装配，空槽点击不可用。");
                return false;
            }

            Debug.Log($"[GroundMotionExecutor] 未认领格点击 → ExploreInputController: slot={slot}");
            _onEmptySlotClicked?.Invoke(slot);
            return ExploreInputHook.TrySubmitExplore(slot);
        }

        /// <summary>
        /// 仅清占格登记，不销毁视图、不启动探求。Sync 两阶段卸格用。
        /// </summary>
        public bool ClearSlotOccupancy(int slot, bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法清占格。");
                return false;
            }

            if (!IsValidSlot(slot) || _index.GetUidAt(slot) == 0)
            {
                return false;
            }

            var uid = _index.GetUidAt(slot);
            var cards = CardEntityLifecycleHook.CardsOrNull();
            if (cards != null && cards.TryGet(uid, out var occupant))
            {
                ReleaseGroundCardClaim(occupant);
            }

            _index.Unregister(slot);
            RefreshSlotHitCollider(slot);
            return true;
        }

        public bool IsAvatarOrthogonalBattleSlot(int slot)
        {
            return GroundSlotTopology.AreOrthogonal(slot, GroundSlotTopology.AvatarReservedSlot);
        }

        /// <summary>
        /// 在 Avatar 四向相邻格中随机挑选一张存活卡牌（用于怪物反击测试）。
        /// </summary>
        public bool TryGetRandomAvatarOrthogonalMonsterSlot(out int slot, out ManagedCard card)
        {
            slot = 0;
            card = null;

            var avatarSlot = GroundSlotTopology.AvatarReservedSlot;
            var neighbors = GroundSlotTopology.GetNeighbors(avatarSlot, GroundSlotRelation.Orthogonal);
            var candidates = new List<int>(neighbors.Count);
            for (var i = 0; i < neighbors.Count; i++)
            {
                var neighborSlot = neighbors[i];
                if (!TryGetCardAt(neighborSlot, out var neighborCard) || neighborCard.IsFieldDead)
                {
                    continue;
                }

                candidates.Add(neighborSlot);
            }

            if (candidates.Count == 0)
            {
                return false;
            }

            slot = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            return TryGetCardAt(slot, out card);
        }

        public UniTask RotateOuterRingClockwiseAsync(CancellationToken cancellationToken = default)
        {
            return RotateOuterRingInternalAsync(true, cancellationToken);
        }

        /// <summary>
        /// 交战流程内嵌套旋转：跳过场地忙碌门禁（由交战管理器持有外层忙碌锁）。
        /// </summary>
        internal UniTask RotateOuterRingClockwiseWhileBusyAsync(CancellationToken cancellationToken = default)
        {
            return RotateOuterRingInternalAsync(true, cancellationToken, skipBusyGuard: true);
        }

        /// <summary>
        /// 交战流程内嵌套旋转：跳过场地忙碌门禁，可指定顺/逆时针。
        /// </summary>
        public UniTask RotateOuterRingWhileBusyAsync(bool clockwise, CancellationToken cancellationToken = default)
        {
            return RotateOuterRingInternalAsync(clockwise, cancellationToken, skipBusyGuard: true);
        }

        

        /// <summary>
        /// 自上次 <see cref="ClearOccupancyConflictFlag"/> 以来是否发生过几何登记冲突。
        /// #10：不再驱动 force Sync；冲突应由 Flow 断言并诊断失败。
        /// </summary>
        public bool HasOccupancyConflictSinceClear => _index.HasOccupancyConflictSinceClear;

        public void ClearOccupancyConflictFlag()
        {
            _index.ClearConflictFlag();
        }

        public bool ConsumeOccupancyConflictFlag()
        {
            return _index.ConsumeConflictFlag();
        }

        private async UniTask ApplyBoardMovesAndHopInternalAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken,
            bool skipBusyGuard,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法应用盘面移动。");
                return;
            }

            if (moves == null || moves.Count == 0)
            {
                return;
            }

            if (moves.Count == 2
                && TryGetCrossSwapPair(moves, out var swapA, out var swapB))
            {
                await ApplyCrossSwapMovesInternalAsync(
                    swapA,
                    swapB,
                    cancellationToken,
                    skipBusyGuard,
                    commitment);
                return;
            }

            if (TryClassifyOuterRingRotation(moves, out var clockwise))
            {
                await RotateOuterRingWhileBusyAsync(clockwise, cancellationToken);
                return;
            }

            await ApplyGeneralBoardMovesInternalAsync(
                moves,
                cancellationToken,
                skipBusyGuard,
                commitment);
        }

        private bool TryClassifyOuterRingRotation(
            IReadOnlyList<PostKillCardMove> moves,
            out bool clockwise)
        {
            clockwise = true;
            if (moves == null || moves.Count == 0)
            {
                return false;
            }

            var ring = GroundSlotTopology.ClockwiseRing;
            var ringOccupied = 0;
            for (var i = 0; i < ring.Count; i++)
            {
                if (_index.GetUidAt(ring[i]) != 0)
                {
                    ringOccupied++;
                }
            }

            if (ringOccupied == 0 || moves.Count != ringOccupied)
            {
                return false;
            }

            bool? direction = null;
            var fromSlots = new HashSet<int>(moves.Count);
            for (var i = 0; i < moves.Count; i++)
            {
                var move = moves[i];
                if (move.Uid <= 0
                    || !IsValidSlot(move.FromSlot)
                    || !IsValidSlot(move.ToSlot)
                    || move.FromSlot == move.ToSlot)
                {
                    return false;
                }

                if (!GroundSlotTopology.IsOuterRing(move.FromSlot)
                    || !GroundSlotTopology.IsOuterRing(move.ToSlot))
                {
                    return false;
                }

                if (_index.GetUidAt(move.FromSlot) != move.Uid)
                {
                    return false;
                }

                var fromIdx = GroundSlotTopology.GetClockwiseRingIndex(move.FromSlot);
                var toIdx = GroundSlotTopology.GetClockwiseRingIndex(move.ToSlot);
                if (fromIdx < 0 || toIdx < 0)
                {
                    return false;
                }

                var isClockwise = toIdx == (fromIdx + 1) % ring.Count;
                var isCounterClockwise = toIdx == (fromIdx + ring.Count - 1) % ring.Count;
                if (!isClockwise && !isCounterClockwise)
                {
                    return false;
                }

                var moveClockwise = isClockwise;
                if (!direction.HasValue)
                {
                    direction = moveClockwise;
                }
                else if (direction.Value != moveClockwise)
                {
                    return false;
                }

                if (!fromSlots.Add(move.FromSlot))
                {
                    return false;
                }
            }

            for (var i = 0; i < ring.Count; i++)
            {
                var slot = ring[i];
                if (_index.GetUidAt(slot) != 0 && !fromSlots.Contains(slot))
                {
                    return false;
                }
            }

            clockwise = direction ?? true;
            FlowFieldTraceSink.RotateClassify?.Invoke(true, clockwise, moves.Count, ringOccupied);
            return true;
        }

        private async UniTask ApplyGeneralBoardMovesInternalAsync(
            IReadOnlyList<PostKillCardMove> moves,
            CancellationToken cancellationToken,
            bool skipBusyGuard,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法应用盘面移动。");
                return;
            }

            if (moves == null || moves.Count == 0)
            {
                return;
            }

            var holdAcquired = false;
            if (!skipBusyGuard)
            {
                if (!PresentationMainlineHold.TryAcquire("FieldMotion", out holdAcquired))
                {
                    Debug.LogWarning("[GroundMotionExecutor] 盘面移动：无法获取主线租约，本地 field busy 仍继续。");
                }

                _isBusy = true;
            }

            try
            {
                var cardManager = CardEntityLifecycleHook.CardsOrNull();
                if (cardManager == null)
                {
                    Debug.LogWarning("[GroundMotionExecutor] 盘面 hop：CardManager 未装配，占格不变。");
                    return;
                }

                var expectedMoves = new List<(int uid, int fromSlot, int toSlot)>(moves.Count);

                for (var i = 0; i < moves.Count; i++)
                {
                    var move = moves[i];
                    if (move.Uid <= 0
                        || !IsValidSlot(move.FromSlot)
                        || !IsValidSlot(move.ToSlot)
                        || move.FromSlot == move.ToSlot)
                    {
                        continue;
                    }

                    expectedMoves.Add((move.Uid, move.FromSlot, move.ToSlot));
                }

                if (expectedMoves.Count == 0)
                {
                    return;
                }

                for (var i = 0; i < expectedMoves.Count; i++)
                {
                    var expected = expectedMoves[i];
                    if (!cardManager.TryGet(expected.uid, out var card) || card?.Transform == null)
                    {
                        Debug.LogWarning(
                            $"[GroundMotionExecutor] 盘面 hop 前置校验失败：uid={expected.uid} 缺少视图，占格不变。");
                        return;
                    }

                    if (!_index.ContainsUid(expected.uid))
                    {
                        Debug.LogWarning(
                            $"[GroundMotionExecutor] 盘面 hop 前置校验失败：uid={expected.uid} 不在占格索引，占格不变。");
                        return;
                    }
                }

                var batch = new List<(int uid, int toSlot)>(expectedMoves.Count);
                for (var i = 0; i < expectedMoves.Count; i++)
                {
                    batch.Add((expectedMoves[i].uid, expectedMoves[i].toSlot));
                }

                if (!_index.TryCommitPermutation(batch, "General.Hop"))
                {
                    Debug.LogError("[GroundMotionExecutor] 盘面 hop 占格批量置换失败，跳过动画。");
                    return;
                }

                var hopPlans = new List<(ManagedCard card, int fromSlot, int toSlot)>(expectedMoves.Count);
                for (var i = 0; i < expectedMoves.Count; i++)
                {
                    var expected = expectedMoves[i];
                    cardManager.TryGet(expected.uid, out var card);
                    // ADR-0023：起飞卸认领，落地再登记（见下方 WhenAll 后 Sync）。
                    ReleaseGroundCardClaim(card);
                    hopPlans.Add((card, expected.fromSlot, expected.toSlot));
                }

                try
                {
                    var planSb = new System.Text.StringBuilder(hopPlans.Count * 12);
                    for (var i = 0; i < hopPlans.Count; i++)
                    {
                        var p = hopPlans[i];
                        if (planSb.Length > 0)
                        {
                            planSb.Append(';');
                        }

                        planSb.Append(p.card.Uid)
                            .Append(':')
                            .Append(p.fromSlot)
                            .Append('\u2192')
                            .Append(p.toSlot);
                    }

                    FlowFieldTraceSink.HopPlan?.Invoke(planSb.ToString());
                }
                catch
                {
                    // ignore
                }

                for (var i = 0; i < hopPlans.Count; i++)
                {
                    var p = hopPlans[i];
                    var fromPos = p.card.Transform != null ? p.card.Transform.position : Vector3.zero;
                    var toAnchor = GetGroundAnchor(p.toSlot);
                    var toPos = toAnchor != null ? toAnchor.position : fromPos;
                    CardPresentationProbe.MotionPlan(
                        p.card.Uid,
                        p.fromSlot,
                        p.toSlot,
                        fromPos,
                        toPos,
                        "Ground.HopPlan",
                        reason: "hop");
                }

                if (hopPlans.Count > 0)
                {
                    var moveTasks = new List<UniTask>(hopPlans.Count);
                    for (var i = 0; i < hopPlans.Count; i++)
                    {
                        var plan = hopPlans[i];
                        if (!_index.TryGetSlotOf(plan.card.Uid, out var registeredSlot)
                            || registeredSlot != plan.toSlot)
                        {
                            continue;
                        }

                        if (IsDealInFlight(plan.card.Uid))
                        {
                            _deal?.TryRedirectFlightToSlot(
                                plan.card.Uid,
                                plan.toSlot,
                                LayoutSettings.moveDuration);
                        }

                        moveTasks.Add(
                            AnimateCardHopToSlotAsync(
                                plan.card,
                                plan.fromSlot,
                                plan.toSlot,
                                cancellationToken,
                                commitment));
                    }

                    if (moveTasks.Count > 0)
                    {
                        await UniTask.WhenAll(moveTasks);
                    }
                }

                for (var i = 0; i < hopPlans.Count; i++)
                {
                    var plan = hopPlans[i];
                    SyncGroundCardClaim(plan.card, plan.toSlot);
                }

                RefreshAllSlotHitColliders();
            }
            finally
            {
                if (!skipBusyGuard)
                {
                    _isBusy = false;
                    PresentationMainlineHold.Release(holdAcquired, "FieldMotion");
                }
            }
        }

        private async UniTask RotateOuterRingInternalAsync(
            bool clockwise,
            CancellationToken cancellationToken,
            bool skipBusyGuard = false)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法旋转。");
                return;
            }

            var holdAcquired = false;
            if (!skipBusyGuard)
            {
                if (!PresentationMainlineHold.TryAcquire("FieldMotion", out holdAcquired))
                {
                    Debug.LogWarning("[GroundMotionExecutor] 外环旋转：无法获取主线租约，本地 field busy 仍继续。");
                }

                _isBusy = true;
            }

            PerfTraceSink.OpenBeat?.Invoke("BoardChoreo", 0);
            var choreoSeqId = ChoreoTraceSink.SafeBeginChoreo(
                "rotate",
                "skipBusyGuard", skipBusyGuard ? "1" : "0",
                "clockwise", clockwise ? "1" : "0");
            var plannedAnim = 0;
            var actualAnim = 0;
            var outcome = "ok";

            try
            {
                var ring = GroundSlotTopology.ClockwiseRing;
                var uids = new int[ring.Count];
                var planSb = new StringBuilder(ring.Count * 16);
                for (var i = 0; i < ring.Count; i++)
                {
                    uids[i] = _index.GetUidAt(ring[i]);
                    if (uids[i] == 0)
                    {
                        continue;
                    }

                    plannedAnim++;
                    var toIndex = clockwise
                        ? (i + 1) % ring.Count
                        : (i + ring.Count - 1) % ring.Count;
                    if (planSb.Length > 0)
                    {
                        planSb.Append(';');
                    }

                    planSb.Append(uids[i])
                        .Append(':')
                        .Append(ring[i])
                        .Append('\u2192')
                        .Append(ring[toIndex]);
                }

                CardPresentationProbe.RingShift(
                    "plan",
                    planSb.ToString(),
                    "Ground.RingShift",
                    clockwise,
                    skipBusyGuard,
                    plannedAnim,
                    0,
                    choreoSeqId);

                var batch = new List<(int uid, int toSlot)>(plannedAnim);
                for (var i = 0; i < ring.Count; i++)
                {
                    if (uids[i] == 0)
                    {
                        continue;
                    }

                    var toIndex = clockwise
                        ? (i + 1) % ring.Count
                        : (i + ring.Count - 1) % ring.Count;
                    batch.Add((uids[i], ring[toIndex]));
                }

                if (!_index.TryCommitPermutation(batch, "Ring.Shift"))
                {
                    Debug.LogError("[GroundMotionExecutor] 外圈旋转占格批量置换失败，跳过动画。");
                    outcome = "commit_fail";
                    CardPresentationProbe.RingShift(
                        "commit_fail",
                        planSb.ToString(),
                        "Ground.RingShift",
                        clockwise,
                        skipBusyGuard,
                        plannedAnim,
                        0,
                        choreoSeqId);
                    return;
                }

                CardPresentationProbe.RingShift(
                    "registered",
                    planSb.ToString(),
                    "Ground.RingShift",
                    clockwise,
                    skipBusyGuard,
                    plannedAnim,
                    0,
                    choreoSeqId);

                ChoreoTraceSink.SafeExploreTrace(
                    -1,
                    "ringShift",
                    -1,
                    -1,
                    "clockwise", clockwise ? "1" : "0");

                _deal?.OnRingShifted(clockwise);
                // 占格已迁到环移后格：立刻刷新空槽代理；认领须等 hop 落地（ADR-0023）。
                RefreshAllSlotHitColliders();
                for (var i = 0; i < batch.Count; i++)
                {
                    var (uid, _) = batch[i];
                    if (CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out var moved))
                    {
                        ReleaseGroundCardClaim(moved);
                    }
                }

                SyncPresentationClock();
                var sourceTime = LayoutSettings != null ? LayoutSettings.moveDuration : 0.35f;
                var beatId = _beatGrid != null ? _beatGrid.OpenBeat(sourceTime) : -1;
                if (beatId > 0 && _beatGrid.TryGetBeat(beatId, out var beatInfo))
                {
                    ConvergenceDiagProbe.BeatAlign(
                        beatId,
                        beatInfo.SharedSourceTime,
                        beatInfo.StartWallTime);
                    var barrierWall = _presentationClock.Now + sourceTime;
                    _beatGrid.PlaceBarrier(beatId, barrierWall);
                    ConvergenceDiagProbe.BarrierPlace(
                        beatId,
                        barrierWall,
                        beatInfo.SharedSourceTime,
                        beatInfo.StartWallTime,
                        registrationHint: plannedAnim);
                }

                var moveTasks = new List<UniTask>();
                for (var i = 0; i < ring.Count; i++)
                {
                    var uid = uids[i];
                    if (uid == 0)
                    {
                        continue;
                    }

                    if (!CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out var card) || card?.Transform == null)
                    {
                        CardPresentationProbe.RegistryMiss(
                            uid,
                            "Ground.RingShift.AnimateSkip",
                            "fromSlot=" + ring[i].ToString(CultureInfo.InvariantCulture) + ";noCard");
                        continue;
                    }

                    if (!_index.TryGetSlotOf(uid, out var toSlot))
                    {
                        continue;
                    }

                    var fromSlot = ring[i];
                    if (beatId > 0)
                    {
                        if (SlotFrameConvergence.TryGetDriver(card, out var hopDriver)
                            || SlotFrameConvergence.TryEnsureInfrastructure(
                                card,
                                out _,
                                out hopDriver,
                                "Ground.RingShift.Register"))
                        {
                            _beatGrid.Register(beatId, committed: true, hopDriver);
                        }
                        else
                        {
                            CardPresentationProbe.Anomaly(
                                uid,
                                "BarrierRegisterFail",
                                "noDriver",
                                "Ground.RingShift.Register",
                                layer: "L2",
                                verdict: "timeOnly");
                            _beatGrid.Register(beatId, committed: true);
                        }
                    }

                    moveTasks.Add(AnimateCardHopToSlotAsync(card, fromSlot, toSlot, cancellationToken));
                    actualAnim++;
                }

                CardPresentationProbe.RingShift(
                    "animate",
                    planSb.ToString(),
                    "Ground.RingShift",
                    clockwise,
                    skipBusyGuard,
                    plannedAnim,
                    actualAnim,
                    choreoSeqId);

                if (moveTasks.Count > 0)
                {
                    await UniTask.WhenAll(moveTasks);
                    // 旋转落位故意不喷尘雾（发牌落位 / PlaceAtAnchor 仍走 CardEdgeDustFx）。
                }
                else
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(LayoutSettings.emptyRotateDuration),
                        cancellationToken: cancellationToken);
                }

                if (beatId > 0)
                {
                    SyncPresentationClock();
                    var satisfied = _beatGrid.IsBarrierSatisfied(beatId);
                    var regCount = _beatGrid.TryGetBeat(beatId, out var afterInfo)
                        ? afterInfo.RegistrationCount
                        : 0;
                    ConvergenceDiagProbe.BarrierSatisfied(
                        beatId,
                        satisfied,
                        regCount,
                        _presentationClock.Now);
                    if (regCount == 0)
                    {
                        CardPresentationProbe.Anomaly(
                            0,
                            "BarrierUnsatisfied",
                            "beatId=" + beatId.ToString(CultureInfo.InvariantCulture)
                            + ";regs=0",
                            "Ground.RingShift.Barrier",
                            layer: "L2",
                            verdict: "skipEmpty");
                    }
                    else if (!satisfied)
                    {
                        CardPresentationProbe.Anomaly(
                            0,
                            "BarrierUnsatisfied",
                            "beatId=" + beatId.ToString(CultureInfo.InvariantCulture)
                            + ";regs=" + regCount.ToString(CultureInfo.InvariantCulture),
                            "Ground.RingShift.Barrier",
                            layer: "L2",
                            verdict: "fail");
                    }
                }

                for (var i = 0; i < batch.Count; i++)
                {
                    var (uid, toSlot) = batch[i];
                    if (CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out var landed))
                    {
                        SyncGroundCardClaim(landed, toSlot);
                    }
                }

                RefreshAllSlotHitColliders();
                CardEntityLifecycleHook.CardsOrNull()?.AuditRegistryIntegrity("Ground.RingRotate.End");
            }
            catch (OperationCanceledException)
            {
                outcome = "cancel";
                throw;
            }
            catch (Exception)
            {
                outcome = "error";
                throw;
            }
            finally
            {
                ChoreoTraceSink.SafeEndChoreo(outcome, plannedAnim, actualAnim);
                PerfTraceSink.CloseBeat?.Invoke();
                if (!skipBusyGuard)
                {
                    _isBusy = false;
                    PresentationMainlineHold.Release(holdAcquired, "FieldMotion");
                }
            }
        }

        /// <summary>
        /// Avatar 单格 hop：允许落点含格5；短租 FieldMotion 主线以便跳间缓冲改目标。
        /// </summary>
        public async UniTask HopAvatarToSlotAsync(
            int fromSlot,
            int toSlot,
            CancellationToken cancellationToken = default)
        {
            if (!IsValidSlot(fromSlot) || !IsValidSlot(toSlot) || fromSlot == toSlot)
            {
                return;
            }

            var uid = _index.GetUidAt(fromSlot);
            if (uid == 0)
            {
                Debug.LogWarning($"[GroundMotionExecutor] Avatar hop 起点无占格 from={fromSlot}");
                return;
            }

            if (!IsEmpty(toSlot))
            {
                Debug.LogWarning($"[GroundMotionExecutor] Avatar hop 目标已占用 to={toSlot}");
                return;
            }

            if (!CardEntityLifecycleHook.CardsOrNull().TryGet(uid, out var card) || card?.Transform == null)
            {
                Debug.LogWarning($"[GroundMotionExecutor] Avatar hop 无视图 uid={uid}");
                return;
            }

            if (!PresentationMainlineHold.TryAcquire("FieldMotion", out var holdAcquired))
            {
                Debug.LogWarning("[GroundMotionExecutor] Avatar hop 无法取得主线租约。");
                return;
            }

            _isBusy = true;
            try
            {
                _index.Unregister(fromSlot);
                if (!_index.TryRegister(toSlot, uid))
                {
                    _index.TryRegister(fromSlot, uid);
                    Debug.LogWarning($"[GroundMotionExecutor] Avatar hop 登记失败 to={toSlot}");
                    return;
                }

                RefreshSlotHitCollider(fromSlot);
                RefreshSlotHitCollider(toSlot);
                await AnimateCardHopToSlotAsync(card, fromSlot, toSlot, cancellationToken);
            }
            finally
            {
                _isBusy = false;
                PresentationMainlineHold.Release(holdAcquired, "FieldMotion");
            }
        }

        private async UniTask AnimateCardHopToSlotAsync(
            ManagedCard card,
            int fromSlot,
            int toSlot,
            CancellationToken cancellationToken,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            if (card?.Transform == null
                || !TryGetAnchor(toSlot, out var toAnchor)
                || toAnchor == null)
            {
                return;
            }

            // 收敛前不 RefreshDisplayMode：避免重置 scale/rotation 并抢写 sortingOrder。
            var sourceTime = LayoutSettings != null ? LayoutSettings.moveDuration : 0.35f;

            // 补牌飞行中：只 Redirect L2 目标，禁止与 hop 互抢；由飞牌探针等待同一驱动器完成。
            if (IsDealInFlight(card.Uid))
            {
                if (_deal != null)
                {
                    _deal.TryRedirectFlightToSlot(card.Uid, toSlot, sourceTime);
                }

                if (SlotFrameConvergence.TryGetDriver(card, out var inFlightDriver))
                {
                    await SlotFrameConvergence.AwaitDriverAsync(inFlightDriver, cancellationToken);
                }

                return;
            }

            var useSyncLease = commitment == CommitmentKind.Sync;
            if (useSyncLease)
            {
                IssueSyncL2Lease(card.Uid, sourceTime);
            }

            try
            {
                // L2 收敛只负责位移；跳起/落下质感缩放落在 L4 CardVisual（塔范式分层）。
                var hopScaleTask = UniTask.CompletedTask;
                if (SlotFrameConvergence.TryGetTower(card, out var hopTower)
                    || SlotFrameConvergence.TryEnsureInfrastructure(
                        card,
                        out hopTower,
                        out _,
                        "Ground.HopScale"))
                {
                    if (hopTower?.CardVisual != null)
                    {
                        var visualDriver = card.View != null
                            ? card.View.GetComponent<CardVisualDriver>()
                            : null;
                        visualDriver?.InterruptFeedbackMotion();

                        var peak = LayoutSettings != null
                            ? LayoutSettings.hopPeakScaleIntensity
                            : 0.06f;
                        var land = LayoutSettings != null
                            ? LayoutSettings.hopLandScaleIntensity
                            : 0.04f;
                        hopScaleTask = CardDeckTween.PlayHopScalePulseAsync(
                            hopTower.CardVisual,
                            sourceTime,
                            peak,
                            land,
                            cancellationToken);
                    }
                }

                await UniTask.WhenAll(
                    SlotFrameConvergence.ConvergeVisualToWorldAsync(
                        card,
                        toAnchor.position,
                        sourceTime,
                        cancellationToken,
                        snapHomeOnComplete: true,
                        commitment: commitment),
                    hopScaleTask);
            }
            finally
            {
                if (useSyncLease)
                {
                    ReleaseSyncL2Lease(card.Uid);
                }
            }

            CardEntityLifecycleHook.CardsOrNull().RefreshDisplayMode(card);
        }

        private void SyncPresentationClock()
        {
            _presentationClock.Seek(Time.time);
        }

        private void IssueSyncL2Lease(int cardId, float sourceTime)
        {
            if (_leaseArbiter == null || cardId <= 0)
            {
                return;
            }

            SyncPresentationClock();
            var now = _presentationClock.Now;
            var key = new LeaseKey(cardId, TowerLayer.SlotFrame);
            var request = LeaseRequest.Sync(key, now, now + sourceTime, committed: true);
            var result = _leaseArbiter.TryAcquire(request, () =>
            {
                if (CardEntityLifecycleHook.CardsOrNull() != null
                    && CardEntityLifecycleHook.CardsOrNull().TryGet(cardId, out var card)
                    && SlotFrameConvergence.TryGetDriver(card, out var driver)
                    && SlotFrameConvergence.TryGetTower(card, out var tower)
                    && tower.SlotFrame != null)
                {
                    return new HandoffState(tower.SlotFrame.localPosition, driver.SampleVelocity());
                }

                return HandoffState.AtRest(Vector3.zero);
            });

            ConvergenceDiagProbe.LeaseAcquire(
                cardId,
                TowerLayer.SlotFrame.ToString(),
                result.Verdict.ToString(),
                CommitmentKind.Sync.ToString(),
                result.LeaseId,
                now,
                now + sourceTime,
                disciplineB: result.RaisedDisciplineBAlarm,
                commandeered: result.HasCommandeerHandoff);

            if (result.HasCommandeerHandoff
                && CardEntityLifecycleHook.CardsOrNull() != null
                && CardEntityLifecycleHook.CardsOrNull().TryGet(cardId, out var commandeerCard)
                && SlotFrameConvergence.TryGetDriver(commandeerCard, out var commandeerDriver))
            {
                ConvergenceDiagProbe.Handoff(
                    cardId,
                    TowerLayer.SlotFrame.ToString(),
                    "commandeerAdmit",
                    result.CommandeerHandoff.LocalPosition,
                    result.CommandeerHandoff.LocalVelocity,
                    site: "Lease.Commandeer");
                commandeerDriver.Admit(result.CommandeerHandoff);
            }

            if (!result.IsWriteAllowed)
            {
                Debug.LogWarning(
                    $"[GroundMotionExecutor] L2 sync lease denied uid={cardId} verdict={result.Verdict}");
            }
        }

        private void ReleaseSyncL2Lease(int cardId)
        {
            if (_leaseArbiter == null || cardId <= 0)
            {
                return;
            }

            var key = new LeaseKey(cardId, TowerLayer.SlotFrame);
            if (_leaseArbiter.TryGetActiveLeaseId(key, out var leaseId))
            {
                ConvergenceDiagProbe.CommitmentArrive(
                    cardId,
                    TowerLayer.SlotFrame.ToString(),
                    CommitmentKind.Sync.ToString(),
                    leaseId,
                    site: "Ground.HopComplete");
                _leaseArbiter.MarkFulfilled(key);
                _leaseArbiter.Release(key, leaseId);
                ConvergenceDiagProbe.LeaseRelease(
                    cardId,
                    TowerLayer.SlotFrame.ToString(),
                    leaseId,
                    reason: "fulfilled");
            }
        }

        private static bool TryGetCrossSwapPair(
            IReadOnlyList<PostKillCardMove> moves,
            out PostKillCardMove first,
            out PostKillCardMove second)
        {
            first = default;
            second = default;
            if (moves == null || moves.Count != 2)
            {
                return false;
            }

            var a = moves[0];
            var b = moves[1];
            if (a.Uid <= 0 || b.Uid <= 0 || a.Uid == b.Uid)
            {
                return false;
            }

            if (a.FromSlot == b.ToSlot && a.ToSlot == b.FromSlot)
            {
                first = a;
                second = b;
                return true;
            }

            return false;
        }

        private async UniTask ApplyCrossSwapMovesInternalAsync(
            PostKillCardMove moveA,
            PostKillCardMove moveB,
            CancellationToken cancellationToken,
            bool skipBusyGuard,
            CommitmentKind commitment = CommitmentKind.Sync)
        {
            if (!skipBusyGuard && IsBusy)
            {
                Debug.LogWarning("[GroundMotionExecutor] 当前忙碌，无法应用换位。");
                return;
            }

            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            if (cardManager == null)
            {
                return;
            }

            if (!cardManager.TryGet(moveA.Uid, out var cardA)
                || !cardManager.TryGet(moveB.Uid, out var cardB)
                || cardA?.Transform == null
                || cardB?.Transform == null)
            {
                Debug.LogWarning("[GroundMotionExecutor] 换位缺少视图，回退 Sync。");
                return;
            }

            if (!TryGetAnchor(moveA.ToSlot, out var anchorA)
                || !TryGetAnchor(moveB.ToSlot, out var anchorB))
            {
                return;
            }

            var holdAcquired = false;
            if (!skipBusyGuard)
            {
                if (!PresentationMainlineHold.TryAcquire("FieldMotion", out holdAcquired))
                {
                    Debug.LogWarning("[GroundMotionExecutor] 换位：无法获取主线租约，本地 field busy 仍继续。");
                }

                _isBusy = true;
            }

            try
            {
                var batch = new List<(int uid, int toSlot)>
                {
                    (moveA.Uid, moveA.ToSlot),
                    (moveB.Uid, moveB.ToSlot),
                };

                if (!_index.TryCommitPermutation(batch, "Cross.Swap"))
                {
                    Debug.LogError("[GroundMotionExecutor] 换位占格批量置换失败，跳过动画。");
                    return;
                }

                // ADR-0023：换位飞行中不认领。
                ReleaseGroundCardClaim(cardA);
                ReleaseGroundCardClaim(cardB);

                var duration = LayoutSettings != null ? LayoutSettings.swapMoveDuration : 0.3f;
                CardEntityLifecycleHook.CardsOrNull().RefreshDisplayMode(cardA);
                CardEntityLifecycleHook.CardsOrNull().RefreshDisplayMode(cardB);

                if (IsDealInFlight(moveA.Uid))
                {
                    _deal?.TryRedirectFlightToSlot(moveA.Uid, moveA.ToSlot, duration);
                }

                if (IsDealInFlight(moveB.Uid))
                {
                    _deal?.TryRedirectFlightToSlot(moveB.Uid, moveB.ToSlot, duration);
                }

                var useSyncLease = commitment == CommitmentKind.Sync;
                if (useSyncLease)
                {
                    IssueSyncL2Lease(moveA.Uid, duration);
                    IssueSyncL2Lease(moveB.Uid, duration);
                }

                try
                {
                    await UniTask.WhenAll(
                        SlotFrameConvergence.ConvergeVisualToWorldAsync(
                            cardA,
                            anchorA.position,
                            duration,
                            cancellationToken,
                            snapHomeOnComplete: true,
                            commitment: commitment),
                        SlotFrameConvergence.ConvergeVisualToWorldAsync(
                            cardB,
                            anchorB.position,
                            duration,
                            cancellationToken,
                            snapHomeOnComplete: true,
                            commitment: commitment));
                }
                finally
                {
                    if (useSyncLease)
                    {
                        ReleaseSyncL2Lease(moveA.Uid);
                        ReleaseSyncL2Lease(moveB.Uid);
                    }
                }

                CardEntityLifecycleHook.CardsOrNull().RefreshDisplayMode(cardA);
                CardEntityLifecycleHook.CardsOrNull().RefreshDisplayMode(cardB);
                SyncGroundCardClaim(cardA, moveA.ToSlot);
                SyncGroundCardClaim(cardB, moveB.ToSlot);
                RefreshAllSlotHitColliders();
            }
            finally
            {
                if (!skipBusyGuard)
                {
                    _isBusy = false;
                    PresentationMainlineHold.Release(holdAcquired, "FieldMotion");
                }
            }
        }

        private async UniTask MoveCardAnimatedThenClaimAsync(
            ManagedCard card,
            int fromSlot,
            int toSlot,
            CancellationToken cancellationToken)
        {
            try
            {
                await AnimateCardHopToSlotAsync(card, fromSlot, toSlot, cancellationToken);
            }
            finally
            {
                SyncGroundCardClaim(card, toSlot);
            }
        }

        private async UniTask RemoveCardAnimatedAsync(ManagedCard card, int slot, CancellationToken cancellationToken)
        {
            try
            {
                if (card?.Transform == null)
                {
                    if (card != null)
                    {
                        CardEntityLifecycleHook.CardsOrNull()?.Release(card, "Ground.RemoveAnimatedNoTransform");
                    }

                    return;
                }

                if (card.TryGetEffectManager(out var effectManager))
                {
                    await effectManager.PlayDeathAsync(slot, cancellationToken: cancellationToken);
                }
                else
                {
                    CardLifecycleAudioCues.Pulse(
                        CardLifecycleAudioCues.Exit,
                        "GroundMotionExecutor.RemoveCardAnimatedAsync",
                        card.DefId);
                    var initialScale = card.Transform.localScale;
                    await RunViewTweenAsync(
                        CardViewTween.ScaleDisappear(
                            card.Transform,
                            initialScale,
                            LayoutSettings.removeDisappearDuration),
                        cancellationToken);
                }

                CardEntityLifecycleHook.CardsOrNull()?.Release(card, "Ground.RemoveAnimatedComplete");
            }
            catch (OperationCanceledException)
            {
                // 清场取消：视图由外层 ReleaseAll 处理。
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


        private bool HasLivingNonAvatarCard()
        {
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
            {
                if (slot == GroundSlotTopology.AvatarReservedSlot)
                {
                    continue;
                }

                var uid = _index.GetUidAt(slot);
                if (uid == 0)
                {
                    continue;
                }

                if (cardManager != null &&
                    cardManager.TryGet(uid, out var card) &&
                    card != null &&
                    card.IsFieldDead)
                {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
