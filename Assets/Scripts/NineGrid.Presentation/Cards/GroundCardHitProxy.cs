using NineGrid.Cards.Convergence;
using UnityEngine;
using NineGrid.Presentation;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地卡牌 hover 命中代理：挂在 Standard Card 根节点，由预制体或运行时装配。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(CardVisualDriver))]
    public sealed class GroundCardHitProxy : MonoBehaviour
    {
        private const float InputRootAlignEpsilonSqr = 0.01f;

        private BoxCollider2D _collider;
        private CardVisualDriver _driver;

        private void Awake()
        {
            _collider = GetComponent<BoxCollider2D>();
            _driver = GetComponent<CardVisualDriver>();
            ApplyColliderSize();
        }

        public void ApplyColliderSize()
        {
            _collider ??= GetComponent<BoxCollider2D>();
            if (_collider == null)
            {
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            var size = field != null
                ? field.LayoutSettings.slotHitBoxSize
                : new Vector2(1.6f, 2.2f);

            _collider.size = size;
            _collider.isTrigger = false;
        }

        private void OnMouseEnter()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (NineGrid.Presentation.Diagnostics.PerfHoverKillSwitch.SuppressGroundOnMouseHover)
            {
                return;
            }
#endif
            if (!CanRespondToHover())
            {
                return;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            if (_driver != null && _driver.IsSelectedVisual)
            {
                return;
            }

            _driver?.SetTarget(CardVisualTarget.Hover);
        }

        private void OnMouseExit()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (NineGrid.Presentation.Diagnostics.PerfHoverKillSwitch.SuppressGroundOnMouseHover)
            {
                return;
            }
#endif
            if (!CanRespondToHover())
            {
                return;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            if (_driver != null && _driver.IsSelectedVisual)
            {
                return;
            }

            _driver?.SetTarget(CardVisualTarget.Base);
        }

        private void OnMouseDown()
        {
            _driver ??= GetComponent<CardVisualDriver>();
            var card = _driver?.BoundCard;
            if (card == null)
            {
                return;
            }

            if (PresentationInputGates.BoardSelectModeActive)
            {
                BoardCardSelectModeController.TryToggleSelection(card);
                return;
            }

            switch (card.CoreKind)
            {
                case CardPresentationKind.Avatar:
                case CardPresentationKind.Unknown:
                    return;
            }

            if (!TryPassGroundInputGate(card, out var blockReason))
            {
                RecordPickupEligibility(card, canRespond: false, blockReason);
                return;
            }

            if (card.CoreKind == CardPresentationKind.Monster)
            {
                FieldBattlePresentationHook.BattleOrNull()?.TryHandleBattleClick(card);
                return;
            }

            if (!CanRespondToPickup())
            {
                RecordPickupEligibility(card, canRespond: false, blockReason: "pickupGate");
                return;
            }

            RecordPickupEligibility(card, canRespond: true);
            // 道具卡 / 帮助卡等：场地仅允许点击入手，禁止拖拽
            CardEntityLifecycleHook.HandOrNull()?.TryPickupFromGround(card);
        }

        private void RecordPickupEligibility(ManagedCard card, bool canRespond, string blockReason = null)
        {
            if (card == null)
            {
                return;
            }

            var hand = CardEntityLifecycleHook.HandOrNull();
            var field = GroundFieldGeometryHook.FieldOrNull();
            var pos = card.Transform != null ? card.Transform.position : Vector3.zero;
            var registeredSlot = -1;
            var hasRegistered = field != null && field.TryGetSlotOf(card.Uid, out registeredSlot);
            var nearestSlot = -1;
            var slotDist = float.MaxValue;
            if (field != null)
            {
                for (var slot = GroundSlotTopology.MinSlot; slot <= GroundSlotTopology.MaxSlot; slot++)
                {
                    var anchor = field.GetGroundAnchor(slot);
                    if (anchor == null)
                    {
                        continue;
                    }

                    var dist = Vector2.Distance(pos, anchor.position);
                    if (dist < slotDist)
                    {
                        slotDist = dist;
                        nearestSlot = slot;
                    }
                }
            }

            var isOrtho = hasRegistered && field != null
                && field.IsAvatarOrthogonalBattleSlot(registeredSlot);
            var isOrphan = card.DisplayMode == CardDisplayMode.GroundCardMode && !hasRegistered;
            var cardManager = CardEntityLifecycleHook.CardsOrNull();
            var isGhost = hasRegistered
                && (cardManager == null || !cardManager.TryGet(card.Uid, out _));

            if (!canRespond && !string.IsNullOrEmpty(blockReason))
            {
                CardPresentationProbe.Anomaly(
                    card.Uid,
                    "InputGateBlocked",
                    "reason=" + blockReason + ";slot=" + registeredSlot,
                    "GroundCardHitProxy",
                    layer: "L0",
                    verdict: "blocked");
            }

            RegistryTraceSink.RecordPickupEligibility?.Invoke(
                card.Uid,
                card.DefId,
                card.CoreKind.ToString(),
                card.DisplayMode.ToString(),
                pos.x,
                pos.y,
                registeredSlot,
                nearestSlot,
                slotDist,
                canRespond,
                field != null && field.IsBusy,
                hand != null && hand.CanAcceptCard,
                isOrtho,
                isOrphan,
                isGhost);
        }

        private bool CanRespondToHover()
        {
            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand != null && hand.IsDragging)
            {
                return false;
            }

            if (hand != null && hand.IsBusy && !PresentationInputGates.BoardSelectModeActive)
            {
                return false;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            var card = _driver?.BoundCard;
            if (card == null)
            {
                return false;
            }

            return TryPassGroundInputGate(card, out _)
                   && _driver.IsGroundHoverEligible;
        }

        private bool CanRespondToPickup()
        {
            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand == null || hand.IsDragging)
            {
                return false;
            }

            // 主线忙仍可点：交 IntentIntake 缓冲；勿用含 MainlineBusy 的 CanAcceptCard 自拒。
            if (hand.IsSelfBusy
                || hand.HandCount >= hand.MaxHandSlots
                || PresentationInputGates.ChoiceOverlayActive
                || PresentationInputGates.BoardSelectModeActive)
            {
                return false;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            var card = _driver?.BoundCard;
            if (card == null)
            {
                return false;
            }

            return TryPassGroundInputGate(card, out _)
                   && _driver.IsGroundHoverEligible;
        }

        /// <summary>
        /// 场地输入资格：占格登记、无 Opening、无 Deal/Slot 收敛、L0 根与注册格锚对齐。
        /// 时序互斥不在此轮询 FieldBusy（#51 → IntentIntake / MainlineBusy）。
        /// </summary>
        private bool TryPassGroundInputGate(ManagedCard card, out string blockReason)
        {
            blockReason = null;
            if (card == null)
            {
                blockReason = "nullCard";
                return false;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            // #51：FieldBusy 不再作独立输入互斥；须阻塞时由主线持有，经 IntentIntake 裁决。

            if (PresentationInputGates.OpeningPresentationActive)
            {
                blockReason = "openingDeal";
                return false;
            }

            if (field != null && field.IsDealInFlight(card.Uid))
            {
                blockReason = "dealInFlight";
                return false;
            }

            if (SlotFrameConvergence.IsSlotConvergenceActive(card))
            {
                blockReason = "slotConverging";
                return false;
            }

            if (field == null || !field.TryGetSlotOf(card.Uid, out var registeredSlot))
            {
                blockReason = "noOccupancy";
                return false;
            }

            if (card.Transform == null)
            {
                blockReason = "noTransform";
                return false;
            }

            var anchor = field.GetGroundAnchor(registeredSlot);
            if (anchor == null)
            {
                blockReason = "noAnchor";
                return false;
            }

            var rootDelta = card.Transform.position - anchor.position;
            if (rootDelta.sqrMagnitude > InputRootAlignEpsilonSqr)
            {
                var visual = SlotFrameConvergence.GetVisualWorldPosition(card);
                var visualDelta = visual - anchor.position;
                CardPresentationProbe.Anomaly(
                    card.Uid,
                    "InputRootMisaligned",
                    "slot=" + registeredSlot
                    + ";rootDx=" + rootDelta.x.ToString("F3")
                    + ";rootDy=" + rootDelta.y.ToString("F3")
                    + ";visualDx=" + visualDelta.x.ToString("F3")
                    + ";visualDy=" + visualDelta.y.ToString("F3"),
                    "GroundCardHitProxy",
                    layer: "L0",
                    verdict: "blocked");
                blockReason = "rootMisaligned";
                return false;
            }

            return true;
        }
    }
}
