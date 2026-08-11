using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow;
using NineGrid.Flow.Diagnostics;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 场地卡牌命中代理：挂在 Standard Card 根节点，由预制体或运行时装配。
    /// 通过格位认领登记激活与 Hover 视觉（ADR-0023）；命中由 <see cref="GroundFieldHitSurface"/> 统一承接。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CardVisualDriver))]
    public sealed class GroundCardHitProxy : MonoBehaviour
    {
        private const float InputRootAlignEpsilonSqr = 0.01f;

        private CardVisualDriver _driver;

        /// <summary>供场地面右键详述等从认领者取卡（ADR-0023）。</summary>
        public ManagedCard BoundCardOrNull
        {
            get
            {
                _driver ??= GetComponent<CardVisualDriver>();
                return _driver != null ? _driver.BoundCard : null;
            }
        }

        private void Awake()
        {
            _driver = GetComponent<CardVisualDriver>();
            ApplyColliderSize();
        }

        private void OnEnable()
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null)
            {
                return;
            }

            var card = _driver?.BoundCard;
            if (card != null && field.TryGetSlotOf(card.Uid, out var slot))
            {
                SyncClaimForSlot(slot);
            }
        }

        private void OnDisable()
        {
            ReleaseClaim();
        }

        public void ApplyColliderSize()
        {
            var collider = GetComponent<BoxCollider2D>();
            if (collider != null)
            {
                collider.enabled = false;
            }
        }

        public void SyncClaimForSlot(int slot)
        {
            if (!isActiveAndEnabled)
            {
                ReleaseClaim();
                return;
            }

            _driver ??= GetComponent<CardVisualDriver>();
            var card = _driver?.BoundCard;
            if (card == null
                || card.CoreKind == CardPresentationKind.Avatar
                || card.CoreKind == CardPresentationKind.Unknown)
            {
                ReleaseClaim();
                return;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field == null || !GroundSlotTopology.IsValidSlot(slot))
            {
                return;
            }

            if (field.IsDealInFlight(card.Uid))
            {
                ReleaseClaim();
                return;
            }

            var claimant = new SlotClaimant(
                this,
                string.Empty,
                ActivateClaim,
                HoverEnterClaim,
                HoverExitClaim);
            field.TryClaimSlot(slot, claimant);
        }

        public void ReleaseClaim()
        {
            var field = GroundFieldGeometryHook.FieldOrNull();
            if (field != null)
            {
                field.ReleaseAllClaimsForOwner(this);
            }
        }

        private void ActivateClaim()
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

            if (card.CoreKind == CardPresentationKind.Monster
                || card.CoreKind == CardPresentationKind.Trap)
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
            CardEntityLifecycleHook.HandOrNull()?.TryPickupFromGround(card);
        }

        private void HoverEnterClaim()
        {
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
            InteractionAudioCues.PulseCard(
                InteractionAudioCues.GroundCardHover,
                "GroundCardHitProxy.HoverEnterClaim",
                _driver?.BoundCard?.DefId);
        }

        private void HoverExitClaim()
        {
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

                if (card.CoreKind == CardPresentationKind.Monster
                    || card.CoreKind == CardPresentationKind.Trap)
                {
                    BoardIntentGateDiagnostics.LogConsole(
                        "GroundInputGate",
                        "blocked uid=" + card.Uid + " reason=" + blockReason + " slot=" + registeredSlot,
                        NineGridArchitecture.Current,
                        GameCommandKind.Attack);
                }
                else if (!string.IsNullOrEmpty(blockReason))
                {
                    BoardIntentGateDiagnostics.LogConsole(
                        "GroundInputGate",
                        "blocked uid=" + card.Uid + " reason=" + blockReason + " slot=" + registeredSlot,
                        NineGridArchitecture.Current,
                        GameCommandKind.PickupItem);
                }
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

            return _driver.IsGroundHoverEligible
                   && TryPassGroundInputGate(card, out _);
        }

        private bool CanRespondToPickup()
        {
            var hand = CardEntityLifecycleHook.HandOrNull();
            if (hand == null || hand.IsDragging)
            {
                return false;
            }

            if (hand.IsSelfBusy
                || hand.HandCount >= hand.MaxHandSlots
                || PresentationInputGates.ChoiceOverlayActive
                || PresentationInputGates.BattleUiOverlayActive
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

        private bool TryPassGroundInputGate(ManagedCard card, out string blockReason)
        {
            blockReason = null;
            if (card == null)
            {
                blockReason = "nullCard";
                return false;
            }

            var field = GroundFieldGeometryHook.FieldOrNull();

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
