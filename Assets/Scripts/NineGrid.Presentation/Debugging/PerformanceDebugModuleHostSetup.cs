using DamageNumbersPro;
using NineGrid.Presentation.Feedback;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Flow.Deck;
using NineGrid.Presentation.Flow.Item;
using NineGrid.Presentation.Flow.Selection;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Reactions;
using NineGrid.Presentation.Shared;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    internal static class PerformanceDebugModuleHostSetup
    {
        public static void Install(GameObject host, PerformanceDebugHarness harness, GameObject cardPrefab, DamageNumber damagePrefab)
        {
            Transform actorsRoot = harness.ActorsRoot;
            Transform nineGrid = harness.NineGridAnchors;
            Transform deckAnchors = harness.CardDeckAnchors;
            Transform handAnchors = harness.HandCardAnchors;
            Transform handActors = harness.HandActorsRoot;

            GetOrAdd<CardAttackFlow>(host);
            GetOrAdd<CounterattackFlow>(host);
            GetOrAdd<CardKillFlow>(host);
            GetOrAdd<CounterattackKillFlow>(host);
            GetOrAdd<BoardRotateFlow>(host);
            GetOrAdd<CardDeckEntryFlow>(host);
            GetOrAdd<CardDeckDealFlow>(host);
            GetOrAdd<CardDeckSubstituteFlow>(host);
            GetOrAdd<SelectionEntranceFlow>(host);
            GetOrAdd<SelectionConfirmFlow>(host);
            GetOrAdd<ItemUseFlow>(host);

            var damageNumbers = GetOrAdd<DamageNumbersReaction>(host);
            GetOrAdd<EffectTriggerReaction>(host);
            GetOrAdd<ModifierApplyReaction>(host);
            GetOrAdd<CardAcquisitionFlow>(host);
            GetOrAdd<SelectionFallOffFlow>(host);
            GetOrAdd<StatusTickReaction>(host);

            GetOrAdd<HitFlashCue>(host);
            GetOrAdd<CardShakeCue>(host);
            GetOrAdd<AttackTrailCue>(host);

            GetOrAdd<BoardCardHoverPresenter>(host);
            var handLayout = GetOrAdd<HandLayoutPresenter>(host);
            var handDrag = GetOrAdd<HandCardDragPresenter>(host);
            var handReturn = GetOrAdd<HandCardReturnPresenter>(host);
            GetOrAdd<SelectionOptionHoverPresenter>(host);

            PerformanceDebugSerializationUtil.SetField(damageNumbers, "damagePrefab", damagePrefab);
            PerformanceDebugSerializationUtil.SetField(damageNumbers, "healPrefab", damagePrefab);
            PerformanceDebugSerializationUtil.SetField(damageNumbers, "goldPrefab", damagePrefab);

            WireDeckEntryFlow(host.GetComponent<CardDeckEntryFlow>(), cardPrefab, actorsRoot, deckAnchors);
            WireDeckDealFlow(host.GetComponent<CardDeckDealFlow>(), cardPrefab, actorsRoot, nineGrid, deckAnchors);
            WireSubstituteFlow(host.GetComponent<CardDeckSubstituteFlow>(), cardPrefab, actorsRoot, nineGrid, deckAnchors);
            WireBoardRotate(host.GetComponent<BoardRotateFlow>(), cardPrefab, actorsRoot, nineGrid);
            WireAcquisition(host.GetComponent<CardAcquisitionFlow>(), cardPrefab, handActors, handAnchors, handLayout, handReturn);
            WireHover(host.GetComponent<BoardCardHoverPresenter>(), cardPrefab, actorsRoot);
            WireHandDrag(handDrag, handLayout, host.GetComponent<CardShakeCue>());
        }

        private static void WireDeckEntryFlow(
            CardDeckEntryFlow flow,
            GameObject cardPrefab,
            Transform actorsRoot,
            Transform deckAnchors)
        {
            PerformanceDebugSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PerformanceDebugSerializationUtil.SetField(flow, "previewActorsRoot", actorsRoot);
            PerformanceDebugSerializationUtil.SetField(flow, "slotRoot", deckAnchors);
            PerformanceDebugSerializationUtil.SetField(flow, "previewDeckOrigin", deckAnchors);
        }

        private static void WireDeckDealFlow(
            CardDeckDealFlow flow,
            GameObject cardPrefab,
            Transform actorsRoot,
            Transform ringSlotRoot,
            Transform deckAnchors)
        {
            PerformanceDebugSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PerformanceDebugSerializationUtil.SetField(flow, "previewActorsRoot", actorsRoot);
            PerformanceDebugSerializationUtil.SetField(flow, "ringSlotRoot", ringSlotRoot);

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
            PerformanceDebugSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PerformanceDebugSerializationUtil.SetField(flow, "previewActorsRoot", actorsRoot);
            PerformanceDebugSerializationUtil.SetField(flow, "slotRoot", slotRoot);
            PerformanceDebugSerializationUtil.SetField(flow, "deckOrigin", deckAnchors);
        }

        private static void WireBoardRotate(BoardRotateFlow flow, GameObject cardPrefab, Transform actorsRoot, Transform slotRoot)
        {
            PerformanceDebugSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PerformanceDebugSerializationUtil.SetField(flow, "previewActorsRoot", actorsRoot);
            PerformanceDebugSerializationUtil.SetField(flow, "slotRoot", slotRoot);
        }

        private static void WireAcquisition(
            CardAcquisitionFlow flow,
            GameObject cardPrefab,
            Transform handActors,
            Transform handAnchors,
            HandLayoutPresenter layout,
            HandCardReturnPresenter returnPresenter)
        {
            PerformanceDebugSerializationUtil.SetField(flow, "cardPreviewPrefab", cardPrefab);
            PerformanceDebugSerializationUtil.SetField(flow, "previewActorsRoot", handActors != null ? handActors : handAnchors);
            PerformanceDebugSerializationUtil.SetField(flow, "handRoot", handActors != null ? handActors : handAnchors);
            PerformanceDebugSerializationUtil.SetField(flow, "layoutPresenter", layout);
            PerformanceDebugSerializationUtil.SetField(flow, "returnPresenter", returnPresenter);

            var solver = new HandCardLayoutSolver();
            Transform[] refs = PerformanceDebugAnchorIndexing.CollectHandCardSlotAnchors(handAnchors);
            if (refs.Length > 0)
            {
                PerformanceDebugSerializationUtil.SetField(solver, "referenceAnchors", refs);
            }

            PerformanceDebugSerializationUtil.SetField(flow, "layoutSolver", solver);
        }

        private static void WireHover(BoardCardHoverPresenter presenter, GameObject cardPrefab, Transform actorsRoot)
        {
            PerformanceDebugSerializationUtil.SetField(presenter, "cardPreviewPrefab", cardPrefab);
            PerformanceDebugSerializationUtil.SetField(presenter, "previewActorsRoot", actorsRoot);
        }

        private static void WireHandDrag(HandCardDragPresenter drag, HandLayoutPresenter layout, CardShakeCue rejectCue)
        {
            PerformanceDebugSerializationUtil.SetField(drag, "layoutPresenter", layout);
            PerformanceDebugSerializationUtil.SetField(drag, "rejectCue", rejectCue);
        }

        private static T GetOrAdd<T>(GameObject host) where T : Component
        {
            T existing = host.GetComponent<T>();
            return existing != null ? existing : host.AddComponent<T>();
        }
    }
}
