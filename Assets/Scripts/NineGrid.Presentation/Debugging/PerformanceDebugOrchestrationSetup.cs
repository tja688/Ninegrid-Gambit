using System.Collections.Generic;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Flow.Core;
using NineGrid.Presentation.Flow.Deck;
using NineGrid.Presentation.Flow.Item;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Orchestration.Bindings;
using NineGrid.Presentation.Feedback;
using NineGrid.Presentation.Flow.Hand;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    internal static class PerformanceDebugOrchestrationSetup
    {
        public sealed class OrchestrationServices
        {
            public FlowRegistry FlowRegistry { get; set; }
            public PresentationBatchPlayer BatchPlayer { get; set; }
        }

        public static OrchestrationServices Install(GameObject moduleHost, PerformanceDebugViewRegistry viewRegistry)
        {
            var flowRegistry = new FlowRegistry();
            var reconcilables = new List<IReconcilable>();

            RegisterFlows(moduleHost, flowRegistry);
            RegisterReconcilables(reconcilables);

            var batchPlayer = new PresentationBatchPlayer(
                viewRegistry,
                flowRegistry,
                reconcilables,
                new LocalInputLockGate());

            return new OrchestrationServices
            {
                FlowRegistry = flowRegistry,
                BatchPlayer = batchPlayer,
            };
        }

        private static void RegisterFlows(GameObject moduleHost, FlowRegistry registry)
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
            registry.Register(new CounterattackFlowBinding(moduleHost.GetComponent<CounterattackFlow>()));
            registry.Register(new CounterattackKillFlowBinding(moduleHost.GetComponent<CounterattackKillFlow>()));
            registry.Register(new SnapshotAlignFlowBinding(moduleHost.GetComponent<SnapshotAlignFlow>()
                ?? moduleHost.AddComponent<SnapshotAlignFlow>()));
        }

        private static void RegisterReconcilables(List<IReconcilable> reconcilables)
        {
            TableNineStatusPanelView statusPanel = Object.FindObjectOfType<TableNineStatusPanelView>();
            if (statusPanel != null)
            {
                reconcilables.Add(new StatusPanelReconcilable(statusPanel));
            }
        }
    }
}
