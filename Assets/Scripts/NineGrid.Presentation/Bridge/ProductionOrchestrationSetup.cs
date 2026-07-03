using DamageNumbersPro;
using NineGrid.Core;
using NineGrid.Presentation.Feedback;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Flow.Core;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Flow.Deck;
using NineGrid.Presentation.Flow.Hand;
using NineGrid.Presentation.Flow.Item;
using NineGrid.Presentation.Flow.Shell;
using NineGrid.Presentation.Interaction;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Orchestration.Bindings;
using NineGrid.Presentation.Shared;
using NineGrid.Presentation.Shell;
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
            public HandItemsPresenter HandItemsPresenter { get; set; }
            public InGameUiFlow InGameUiFlow { get; set; }
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

            HandItemsPresenter handItemsPresenter = InGameInteractionSetup.CreateHandItemsPresenter(
                roots,
                viewRegistry,
                actorFactory,
                moduleHost.GetComponent<HandLayoutPresenter>());

            var flowRegistry = new FlowRegistry();
            InGameUiFlow inGameUiFlow = moduleHost.GetComponent<InGameUiFlow>();
            RoomChoiceFlow roomChoiceFlow = moduleHost.GetComponent<RoomChoiceFlow>();
            RoomChoiceScreenPresenter roomPresenter = Object.FindObjectOfType<RoomChoiceScreenPresenter>();
            RegisterFlows(
                moduleHost,
                flowRegistry,
                roots,
                inGameUiFlow,
                roomChoiceFlow,
                roomPresenter);

            TableNineStatusPanelView statusPanel = Object.FindObjectOfType<TableNineStatusPanelView>();
            var statProjection = new StatEventProjection(viewRegistry, statusPanel);
            var inputGate = new CoreSyncInputLockGate(
                NineGridArchitecture.Current.GetSystem<IPresentationSyncSystem>());
            var batchPlayer = new PresentationBatchPlayer(
                viewRegistry,
                flowRegistry,
                inputGate,
                actorFactory,
                statProjection);

            return new OrchestrationServices
            {
                FlowRegistry = flowRegistry,
                BatchPlayer = batchPlayer,
                HandItemsPresenter = handItemsPresenter,
                InGameUiFlow = inGameUiFlow,
            };
        }

        private static void RegisterFlows(
            GameObject moduleHost,
            FlowRegistry registry,
            SceneStagingRoots roots,
            InGameUiFlow inGameUiFlow,
            RoomChoiceFlow roomChoiceFlow,
            RoomChoiceScreenPresenter roomPresenter)
        {
            var damageNumbers = moduleHost.GetComponent<DamageNumberFeedback>();
            registry.Register(new CardAttackFlowBinding(moduleHost.GetComponent<CardAttackFlow>(), damageNumbers));
            registry.Register(new CardKillFlowBinding(moduleHost.GetComponent<CardKillFlow>()));
            registry.Register(new BoardRotateFlowBinding(moduleHost.GetComponent<BoardRotateFlow>()));

            var substituteFlow = moduleHost.GetComponent<CardDeckSubstituteFlow>();
            registry.Register(new MoveCardFlowBinding(substituteFlow));
            registry.Register(new CardDealFlowBinding(moduleHost.GetComponent<CardDeckDealFlow>(), substituteFlow));
            registry.Register(new FillSlotsFlowBinding(substituteFlow));
            registry.Register(new UseItemFlowBinding(moduleHost.GetComponent<ItemUseFlow>()));
            registry.Register(new CardDeckEntryFlowBinding(moduleHost.GetComponent<CardDeckEntryFlow>()));
            registry.Register(new PlayerAppearFlowBinding(moduleHost.GetComponent<PlayerAppearFlow>()));
            registry.Register(new CounterattackFlowBinding(moduleHost.GetComponent<CounterattackFlow>()));
            registry.Register(new CounterattackKillFlowBinding(moduleHost.GetComponent<CounterattackKillFlow>()));
            registry.Register(new SnapshotAlignFlowBinding(moduleHost.GetComponent<SnapshotAlignFlow>()
                ?? moduleHost.AddComponent<SnapshotAlignFlow>()));
            registry.Register(new InGameUiEntranceFlowBinding(inGameUiFlow));
            registry.Register(new InGameUiExitFlowBinding(inGameUiFlow));
            registry.Register(new RoomChoiceInFlowBinding(roomChoiceFlow, roomPresenter));
            registry.Register(new RoomChoiceOutFlowBinding(roomChoiceFlow, roomPresenter));

            var acquisitionFlow = moduleHost.GetComponent<CardAcquisitionFlow>();
            var layoutPresenter = moduleHost.GetComponent<HandLayoutPresenter>();
            var layoutSolver = new HandCardLayoutSolver();
            Transform[] handRefs = SceneStagingAnchorUtil.CollectHandCardSlotAnchors(roots.HandCardAnchors);
            if (handRefs.Length > 0)
            {
                PresentationSerializationUtil.SetField(layoutSolver, "referenceAnchors", handRefs);
            }

            registry.Register(new CardAcquisitionFlowBinding(
                acquisitionFlow,
                layoutPresenter,
                layoutSolver,
                roots.HandActorsRoot));
        }
    }
}
