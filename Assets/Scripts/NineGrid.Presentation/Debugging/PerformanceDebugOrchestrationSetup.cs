using System.Collections.Generic;
using NineGrid.Presentation.Flow.Battle;
using NineGrid.Presentation.Flow.Board;
using NineGrid.Presentation.Flow.Deck;
using NineGrid.Presentation.Flow.Item;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Orchestration.Bindings;
using NineGrid.Presentation.Reactions;
using NineGrid.Presentation.Visuals;
using UnityEngine;

namespace NineGrid.Presentation.Debugging
{
    internal static class PerformanceDebugOrchestrationSetup
    {
        public sealed class OrchestrationServices
        {
            public FlowRegistry FlowRegistry { get; set; }
            public ReactionRegistry ReactionRegistry { get; set; }
            public PresentationBatchPlayer BatchPlayer { get; set; }
        }

        public static OrchestrationServices Install(GameObject moduleHost, PerformanceDebugViewRegistry viewRegistry)
        {
            var flowRegistry = new FlowRegistry();
            var reactionRegistry = new ReactionRegistry();
            var reconcilables = new List<IReconcilable>();

            RegisterFlows(moduleHost, flowRegistry);
            RegisterReactions(moduleHost, reactionRegistry, reconcilables);

            var batchPlayer = new PresentationBatchPlayer(
                viewRegistry,
                flowRegistry,
                reactionRegistry,
                reconcilables,
                new LocalInputLockGate());

            return new OrchestrationServices
            {
                FlowRegistry = flowRegistry,
                ReactionRegistry = reactionRegistry,
                BatchPlayer = batchPlayer,
            };
        }

        private static void RegisterFlows(GameObject moduleHost, FlowRegistry registry)
        {
            registry.Register(new CardAttackFlowBinding(moduleHost.GetComponent<CardAttackFlow>()));
            registry.Register(new CardKillFlowBinding(moduleHost.GetComponent<CardKillFlow>()));
            registry.Register(new BoardRotateFlowBinding(moduleHost.GetComponent<BoardRotateFlow>()));

            var substituteFlow = moduleHost.GetComponent<CardDeckSubstituteFlow>();
            registry.Register(new MoveCardFlowBinding(substituteFlow));
            registry.Register(new CardDealFlowBinding(moduleHost.GetComponent<CardDeckDealFlow>(), substituteFlow));
            registry.Register(new FillSlotsFlowBinding(substituteFlow));
            registry.Register(new UseItemFlowBinding(moduleHost.GetComponent<ItemUseFlow>()));
            registry.Register(new CardDeckEntryFlowBinding(moduleHost.GetComponent<CardDeckEntryFlow>()));
        }

        private static void RegisterReactions(
            GameObject moduleHost,
            ReactionRegistry registry,
            List<IReconcilable> reconcilables)
        {
            registry.Register(new ShowDamageReactionBinding(moduleHost.GetComponent<DamageNumbersReaction>()));
            registry.Register(new TriggerEffectReactionBinding(moduleHost.GetComponent<EffectTriggerReaction>()));
            registry.Register(new ApplyModifierReactionBinding(moduleHost.GetComponent<ModifierApplyReaction>()));
            registry.Register(new StatusTickReactionBinding(moduleHost.GetComponent<StatusTickReaction>()));

            TableNineStatusPanelView statusPanel = Object.FindObjectOfType<TableNineStatusPanelView>();
            if (statusPanel != null)
            {
                registry.Register(new UpdateHpReactionBinding(statusPanel));
                registry.Register(new UpdateArmorReactionBinding(statusPanel));
                reconcilables.Add(new StatusPanelReconcilable(statusPanel));
            }
        }
    }
}
