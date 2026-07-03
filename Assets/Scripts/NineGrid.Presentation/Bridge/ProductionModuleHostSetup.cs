using DamageNumbersPro;
using NineGrid.Presentation.Feedback;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Flow.Core;
using NineGrid.Presentation.Flow.Deck;
using NineGrid.Presentation.Flow.Hand;
using NineGrid.Presentation.Flow.Item;
using NineGrid.Presentation.Flow.Shell;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Shared;
using NineGrid.Presentation.Shell;
using UnityEngine;

namespace NineGrid.Presentation.Bridge
{
    internal static class ProductionModuleHostSetup
    {
        public static void Install(
            GameObject host,
            SceneStagingRoots roots,
            GameObject cardPrefab,
            DamageNumber damagePrefab)
        {
            Transform actorsRoot = roots.ActorsRoot;
            Transform nineGrid = roots.NineGridAnchors;
            Transform deckAnchors = roots.CardDeckAnchors;
            Transform handAnchors = roots.HandCardAnchors;
            Transform handActors = roots.HandActorsRoot;

            GetOrAdd<CardAttackFlow>(host);
            GetOrAdd<CounterattackFlow>(host);
            GetOrAdd<CardKillFlow>(host);
            GetOrAdd<CounterattackKillFlow>(host);
            GetOrAdd<BoardRotateFlow>(host);
            GetOrAdd<PlayerAppearFlow>(host);
            GetOrAdd<CardDeckEntryFlow>(host);
            GetOrAdd<CardDeckDealFlow>(host);
            GetOrAdd<CardDeckSubstituteFlow>(host);
            GetOrAdd<SelectionPresentation>(host);
            GetOrAdd<ItemUseFlow>(host);
            GetOrAdd<InGameUiFlow>(host);
            GetOrAdd<RoomChoiceFlow>(host);
            GetOrAdd<SnapshotAlignFlow>(host);

            var damageNumbers = GetOrAdd<DamageNumberFeedback>(host);
            GetOrAdd<CardAcquisitionFlow>(host);

            GetOrAdd<HitFlashCue>(host);
            GetOrAdd<CardShakeCue>(host);
            GetOrAdd<AttackTrailCue>(host);

            GetOrAdd<BoardCardHoverPresenter>(host);
            var handLayout = GetOrAdd<HandLayoutPresenter>(host);
            var handDrag = GetOrAdd<HandCardDragPresenter>(host);
            var handReturn = GetOrAdd<HandCardReturnPresenter>(host);

            PresentationSerializationUtil.SetField(damageNumbers, "damagePrefab", damagePrefab);
            PresentationSerializationUtil.SetField(damageNumbers, "healPrefab", damagePrefab);
            PresentationSerializationUtil.SetField(damageNumbers, "goldPrefab", damagePrefab);

            WireDeckEntryFlow(host.GetComponent<CardDeckEntryFlow>(), cardPrefab, actorsRoot, deckAnchors);
            WireDeckDealFlow(host.GetComponent<CardDeckDealFlow>(), cardPrefab, actorsRoot, nineGrid, deckAnchors);
            WireSubstituteFlow(host.GetComponent<CardDeckSubstituteFlow>(), cardPrefab, actorsRoot, nineGrid, deckAnchors);
            WireBoardRotate(host.GetComponent<BoardRotateFlow>(), cardPrefab, actorsRoot, nineGrid);
            WireAcquisition(host.GetComponent<CardAcquisitionFlow>(), cardPrefab, handActors, handAnchors, handLayout, handReturn);
            WireHover(host.GetComponent<BoardCardHoverPresenter>(), cardPrefab, actorsRoot);
            WireHandDrag(handDrag, handLayout, host.GetComponent<CardShakeCue>());
            WireItemUse(host.GetComponent<ItemUseFlow>(), handReturn);
            WireShellFlows(
                host.GetComponent<SelectionPresentation>(),
                host.GetComponent<RoomChoiceFlow>());
        }

        private static void WireItemUse(ItemUseFlow flow, HandCardReturnPresenter returnPresenter)
        {
            if (flow != null)
            {
                flow.Configure(returnPresenter);
            }
        }

        private static void WireShellFlows(SelectionPresentation selection, RoomChoiceFlow roomChoiceFlow)
        {
            if (roomChoiceFlow != null)
            {
                roomChoiceFlow.Configure(selection);
            }
        }

        private static void WireDeckEntryFlow(
            CardDeckEntryFlow flow,
            GameObject cardPrefab,
            Transform actorsRoot,
            Transform deckAnchors)
        {
            PresentationSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PresentationSerializationUtil.SetField(flow, "previewActorsRoot", actorsRoot);
            PresentationSerializationUtil.SetField(flow, "slotRoot", deckAnchors);
            Transform preparationSlot = SceneStagingAnchorUtil.FindDeckChild(
                deckAnchors,
                SceneStagingAnchorUtil.DeckEntryPreparationSlotName);
            PresentationSerializationUtil.SetField(
                flow,
                "previewDeckOrigin",
                preparationSlot != null ? preparationSlot : deckAnchors);
        }

        private static void WireDeckDealFlow(
            CardDeckDealFlow flow,
            GameObject cardPrefab,
            Transform actorsRoot,
            Transform ringSlotRoot,
            Transform deckAnchors)
        {
            PresentationSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PresentationSerializationUtil.SetField(flow, "previewActorsRoot", actorsRoot);
            PresentationSerializationUtil.SetField(flow, "ringSlotRoot", ringSlotRoot);
            PresentationSerializationUtil.SetField(flow, "dealSourceRoot", deckAnchors);

            CardDeckSubstituteFlow substituteFlow = flow.GetComponent<CardDeckSubstituteFlow>();
            if (substituteFlow != null)
            {
                WireSubstituteFlow(substituteFlow, cardPrefab, actorsRoot, ringSlotRoot, deckAnchors);
            }
        }

        private static void WireSubstituteFlow(
            CardDeckSubstituteFlow flow,
            GameObject cardPrefab,
            Transform actorsRoot,
            Transform slotRoot,
            Transform deckAnchors)
        {
            PresentationSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PresentationSerializationUtil.SetField(flow, "previewActorsRoot", actorsRoot);
            PresentationSerializationUtil.SetField(flow, "slotRoot", slotRoot);
            Transform dealOrigin = SceneStagingAnchorUtil.FindDeckChild(
                deckAnchors,
                SceneStagingAnchorUtil.DeckDealOriginSlotName);
            PresentationSerializationUtil.SetField(
                flow,
                "deckOrigin",
                dealOrigin != null ? dealOrigin : deckAnchors);
        }

        private static void WireBoardRotate(BoardRotateFlow flow, GameObject cardPrefab, Transform actorsRoot, Transform slotRoot)
        {
            PresentationSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PresentationSerializationUtil.SetField(flow, "previewActorsRoot", actorsRoot);
            PresentationSerializationUtil.SetField(flow, "slotRoot", slotRoot);
        }

        private static void WireAcquisition(
            CardAcquisitionFlow flow,
            GameObject cardPrefab,
            Transform handActors,
            Transform handAnchors,
            HandLayoutPresenter layout,
            HandCardReturnPresenter returnPresenter)
        {
            PresentationSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PresentationSerializationUtil.SetField(flow, "previewActorsRoot", handActors != null ? handActors : handAnchors);
            PresentationSerializationUtil.SetField(flow, "handRoot", handActors != null ? handActors : handAnchors);
            PresentationSerializationUtil.SetField(flow, "layoutPresenter", layout);
            PresentationSerializationUtil.SetField(flow, "returnPresenter", returnPresenter);

            var solver = new HandCardLayoutSolver();
            Transform[] refs = SceneStagingAnchorUtil.CollectHandCardSlotAnchors(handAnchors);
            if (refs.Length > 0)
            {
                PresentationSerializationUtil.SetField(solver, "referenceAnchors", refs);
            }

            PresentationSerializationUtil.SetField(flow, "layoutSolver", solver);
        }

        private static void WireHover(BoardCardHoverPresenter presenter, GameObject cardPrefab, Transform actorsRoot)
        {
            PresentationSerializationUtil.SetField(presenter, "cardPreviewPrefab", cardPrefab);
            PresentationSerializationUtil.SetField(presenter, "previewActorsRoot", actorsRoot);
        }

        private static void WireHandDrag(HandCardDragPresenter drag, HandLayoutPresenter layout, CardShakeCue rejectCue)
        {
            PresentationSerializationUtil.SetField(drag, "layoutPresenter", layout);
            PresentationSerializationUtil.SetField(drag, "rejectCue", rejectCue);
        }

        private static T GetOrAdd<T>(GameObject host) where T : Component
        {
            T existing = host.GetComponent<T>();
            return existing != null ? existing : host.AddComponent<T>();
        }
    }
}
