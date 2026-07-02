using System.Collections.Generic;
using DamageNumbersPro;
using NineGrid.Core;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Flow.Core;
using NineGrid.Presentation.Flow.Deck;
using NineGrid.Presentation.Flow.Item;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Orchestration.Bindings;
using NineGrid.Presentation.Reactions;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Bridge
{
    internal static class ProductionOrchestrationSetup
    {
        public sealed class OrchestrationServices
        {
            public FlowRegistry FlowRegistry { get; set; }
            public PresentationBatchPlayer BatchPlayer { get; set; }
            public HandItemsReconcilable HandItemsReconcilable { get; set; }
        }

        public static OrchestrationServices Install(
            GameObject moduleHost,
            SceneStagingRoots roots,
            TableNineViewRegistry viewRegistry,
            TableNineActorFactory actorFactory,
            GameObject cardPrefab,
            DamageNumber damagePrefab)
        {
            ProductionModuleHostSetup.Install(moduleHost, roots, cardPrefab, damagePrefab);

            HandItemsReconcilable handItemsReconcilable = InGameInteractionSetup.CreateHandItemsReconcilable(
                roots,
                viewRegistry,
                actorFactory,
                moduleHost.GetComponent<HandLayoutPresenter>());

            var flowRegistry = new FlowRegistry();
            var reconcilables = new List<IReconcilable>();
            RegisterFlows(moduleHost, flowRegistry);
            RegisterReconcilables(reconcilables, actorFactory, viewRegistry, handItemsReconcilable);

            var inputGate = new CoreSyncInputLockGate(
                NineGridArchitecture.Current.GetSystem<IPresentationSyncSystem>());
            var batchPlayer = new PresentationBatchPlayer(
                viewRegistry,
                flowRegistry,
                reconcilables,
                inputGate);

            return new OrchestrationServices
            {
                FlowRegistry = flowRegistry,
                BatchPlayer = batchPlayer,
                HandItemsReconcilable = handItemsReconcilable,
            };
        }

        private static void RegisterFlows(GameObject moduleHost, FlowRegistry registry)
        {
            var damageNumbers = moduleHost.GetComponent<DamageNumbersReaction>();
            registry.Register(new CardAttackFlowBinding(moduleHost.GetComponent<CardAttackFlow>(), damageNumbers));
            registry.Register(new CardKillFlowBinding(moduleHost.GetComponent<CardKillFlow>()));
            registry.Register(new BoardRotateFlowBinding(moduleHost.GetComponent<BoardRotateFlow>()));

            var substituteFlow = moduleHost.GetComponent<CardDeckSubstituteFlow>();
            registry.Register(new MoveCardFlowBinding(substituteFlow));
            registry.Register(new CardDealFlowBinding(moduleHost.GetComponent<CardDeckDealFlow>(), substituteFlow));
            registry.Register(new FillSlotsFlowBinding(substituteFlow));
            registry.Register(new UseItemFlowBinding(moduleHost.GetComponent<ItemUseFlow>()));
            registry.Register(new CardDeckEntryFlowBinding(moduleHost.GetComponent<CardDeckEntryFlow>()));
            registry.Register(new CounterattackFlowBinding(moduleHost.GetComponent<CounterattackFlow>()));
            registry.Register(new CounterattackKillFlowBinding(moduleHost.GetComponent<CounterattackKillFlow>()));
            registry.Register(new SnapshotAlignFlowBinding(moduleHost.GetComponent<SnapshotAlignFlow>()
                ?? moduleHost.AddComponent<SnapshotAlignFlow>()));
        }

        private static void RegisterReconcilables(
            List<IReconcilable> reconcilables,
            TableNineActorFactory actorFactory,
            TableNineViewRegistry viewRegistry,
            HandItemsReconcilable handItemsReconcilable)
        {
            reconcilables.Add(new BoardCardsReconcilable(viewRegistry, actorFactory));
            if (handItemsReconcilable != null)
            {
                reconcilables.Add(handItemsReconcilable);
            }

            TableNineStatusPanelView statusPanel = Object.FindObjectOfType<TableNineStatusPanelView>();
            if (statusPanel != null)
            {
                reconcilables.Add(new StatusPanelReconcilable(statusPanel));
            }
        }
    }
}
