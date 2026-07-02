using NineGrid.Core;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using NineGrid.Presentation.Shell;
using UnityEngine;

namespace NineGrid.Presentation.Bridge
{
    internal static class InGameInteractionSetup
    {
        public static InGameInteractionCoordinator Install(
            NineGridSceneBootstrap bootstrap,
            GameObject moduleHost,
            SceneStagingRoots roots,
            CommandGateway gateway,
            TableNineViewRegistry viewRegistry,
            TableNineActorFactory actorFactory,
            HandItemsReconcilable handItemsReconcilable)
        {
            if (bootstrap == null || gateway == null || moduleHost == null)
            {
                return null;
            }

            var coordinator = bootstrap.GetComponent<InGameInteractionCoordinator>();
            if (coordinator == null)
            {
                coordinator = bootstrap.gameObject.AddComponent<InGameInteractionCoordinator>();
            }

            Collider2D useZone = EnsureUseZoneCollider(roots?.NineGridAnchors);
            WireEmptySlotRelays(roots?.NineGridAnchors);

            coordinator.Install(
                gateway,
                bootstrap.Architecture,
                moduleHost.GetComponent<BoardCardHoverPresenter>(),
                moduleHost.GetComponent<HandCardDragPresenter>(),
                moduleHost.GetComponent<HandCardReturnPresenter>(),
                moduleHost.GetComponent<HandLayoutPresenter>(),
                viewRegistry,
                useZone);

            if (handItemsReconcilable != null)
            {
                handItemsReconcilable.BindCoordinator(coordinator);
            }

            return coordinator;
        }

        public static void WireItemUseSelection(
            InGameInteractionCoordinator coordinator,
            SceneStagingRoots roots,
            SelectionFsm selectionFsm,
            SelectionPresentation presentation,
            SelectionFsmOwner selectionOwner,
            GameObject optionPrefab)
        {
            if (coordinator == null)
            {
                return;
            }

            Transform optionRoot = EnsureItemOptionRoot(roots);
            coordinator.InstallItemUseSelection(
                selectionFsm,
                presentation,
                selectionOwner,
                optionRoot,
                optionPrefab);
        }

        private static Transform EnsureItemOptionRoot(SceneStagingRoots roots)
        {
            Transform parent = roots?.PanelsAnchor ?? roots?.NineGridAnchors ?? roots?.AnchorsRoot;
            if (parent == null)
            {
                return null;
            }

            const string rootName = "ItemUseOptionRoot";
            Transform existing = parent.Find(rootName);
            if (existing != null)
            {
                return existing;
            }

            var rootObject = new GameObject(rootName);
            rootObject.transform.SetParent(parent, false);
            rootObject.transform.localPosition = Vector3.zero;
            return rootObject.transform;
        }

        public static HandItemsReconcilable CreateHandItemsReconcilable(
            SceneStagingRoots roots,
            TableNineViewRegistry viewRegistry,
            TableNineActorFactory actorFactory,
            HandLayoutPresenter layoutPresenter)
        {
            var solver = new HandCardLayoutSolver();
            Transform[] refs = SceneStagingAnchorUtil.CollectHandCardSlotAnchors(roots?.HandCardAnchors);
            if (refs.Length > 0)
            {
                PresentationSerializationUtil.SetField(solver, "referenceAnchors", refs);
            }

            return new HandItemsReconcilable(
                viewRegistry,
                actorFactory,
                roots?.HandActorsRoot ?? roots?.HandCardAnchors,
                layoutPresenter,
                solver);
        }

        private static Collider2D EnsureUseZoneCollider(Transform nineGridAnchors)
        {
            if (nineGridAnchors == null)
            {
                return null;
            }

            Collider2D existing = nineGridAnchors.GetComponent<Collider2D>();
            if (existing != null)
            {
                return existing;
            }

            var box = nineGridAnchors.gameObject.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(6f, 6f);
            return box;
        }

        private static void WireEmptySlotRelays(Transform nineGridAnchors)
        {
            if (nineGridAnchors == null)
            {
                return;
            }

            for (var i = 1; i <= SlotId.MaxBoardIndex; i++)
            {
                if (i == 5)
                {
                    continue;
                }

                string slotName = $"slot{i}";
                Transform anchor = nineGridAnchors.Find(slotName);
                if (anchor == null)
                {
                    continue;
                }

                InteractionColliderUtility.EnsureCollider2D(anchor.gameObject);
                BoardSlotInputRelay relay = anchor.GetComponent<BoardSlotInputRelay>();
                if (relay == null)
                {
                    relay = anchor.gameObject.AddComponent<BoardSlotInputRelay>();
                }

                relay.BoardSlotIndex = i;
            }
        }
    }
}
