using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Orchestration;
using NineGrid.Presentation.Shared;
using NineGrid.Presentation.Tests.Support;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class OrchestrationPipelineTests
    {
        [Test]
        public void PlanBuilder_AttackKillRotateFill_BuildsExpectedFlowSteps()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 1, "attack")
                    .WithActor(1)
                    .WithTarget(2)
                    .WithAmount(3),
                new CoreGameEvent(CoreEventType.CardKilled, 1, "attack")
                    .WithCard(2),
                new CoreGameEvent(CoreEventType.BoardRotated, 1, "attack")
                    .WithAmount(1),
                new CoreGameEvent(CoreEventType.CardMoved, 1, "attack")
                    .WithCard(3)
                    .WithSlots(SlotId.Board(1), SlotId.Board(2)),
            };

            var batch = PresentationBatchFixture.Create(7, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            Assert.AreEqual(7, plan.BatchId);
            Assert.AreEqual(2, plan.Groups.Count);
            Assert.AreEqual(0, CountFlow(plan, FlowId.CardAttack));
            AssertContainsFlow(plan, FlowId.CardKill);
            AssertContainsFlow(plan, FlowId.BoardRotate);
            Assert.AreEqual(0, CountFlow(plan, FlowId.MoveCard));
        }

        [Test]
        public void PlanBuilder_AttackCounterNoKill_BuildsSerialAttackThenCounter()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 1, "DealDamage")
                    .WithActor(1)
                    .WithTarget(2)
                    .WithAmount(1),
                new CoreGameEvent(CoreEventType.DamageDealt, 2, "DealDamage")
                    .WithActor(2)
                    .WithTarget(1)
                    .WithAmount(1),
            };

            var snapshot = OrchestrationTestSnapshots.WithBoardCombatants();
            var batch = PresentationBatchFixture.Create(15, events, snapshot);
            var plan = new PerformancePlanBuilder().Build(batch);

            Assert.AreEqual(2, plan.Groups.Count);
            Assert.AreEqual(FlowId.CardAttack, plan.Groups[0].Steps[0].FlowId);
            Assert.AreEqual(FlowId.Counterattack, plan.Groups[1].Steps[0].FlowId);
            Assert.AreEqual(1, CountFlow(plan, FlowId.CardAttack));
            Assert.AreEqual(1, CountFlow(plan, FlowId.Counterattack));
        }

        [Test]
        public void PlanBuilder_CounterKillPlayer_BuildsCounterThenDeathOnlyKill()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 1, "DealDamage")
                    .WithActor(1)
                    .WithTarget(2)
                    .WithAmount(5),
                new CoreGameEvent(CoreEventType.DamageDealt, 2, "DealDamage")
                    .WithActor(2)
                    .WithTarget(1)
                    .WithAmount(99),
                new CoreGameEvent(CoreEventType.CardKilled, 3, "Kill")
                    .WithActor(2)
                    .WithTarget(1)
                    .WithCard(1),
            };

            var snapshot = OrchestrationTestSnapshots.WithBoardCombatants();
            var batch = PresentationBatchFixture.Create(16, events, snapshot);
            var plan = new PerformancePlanBuilder().Build(batch);

            Assert.AreEqual(3, plan.Groups.Count);
            Assert.AreEqual(FlowId.CardAttack, plan.Groups[0].Steps[0].FlowId);
            Assert.AreEqual(FlowId.Counterattack, plan.Groups[1].Steps[0].FlowId);
            Assert.AreEqual(FlowId.CounterattackKill, plan.Groups[2].Steps[0].FlowId);
            Assert.IsFalse(plan.Groups[2].Steps[0].Payload.IncludeStrike);
            Assert.AreEqual(1, CountFlow(plan, FlowId.CardAttack));
        }

        [Test]
        public void AttackDirectionResolver_PlayerToOrthogonalEnemy_UsesRight()
        {
            var payload = new FlowPayload
            {
                ActorUid = 1,
                TargetUid = 2,
            };

            AttackDirectionResolver.ApplyBoardDirection(payload, null);

            Assert.AreEqual(Vector2.right, payload.Direction);
        }

        [Test]
        public void CardBattleDirectionUtil_DiagonalSlots_ReturnFalse()
        {
            Assert.IsFalse(CardBattleDirectionUtil.TryFromBoardSlots(
                SlotId.Board(5),
                SlotId.Board(3),
                out _));
        }

        [Test]
        public void CardBattleDirectionUtil_OrthogonalSlots_ReturnExpectedDirection()
        {
            Assert.IsTrue(CardBattleDirectionUtil.TryFromBoardSlots(
                SlotId.Board(5),
                SlotId.Board(2),
                out Vector2 up));
            Assert.AreEqual(Vector2.up, up);
        }

        [Test]
        public void PlanBuilder_OpeningDeal_BuildsParallelDealSteps()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardDealt, 10, "deal")
                    .WithCard(101)
                    .WithSlots(SlotId.None, SlotId.Board(1)),
                new CoreGameEvent(CoreEventType.CardDealt, 10, "deal")
                    .WithCard(102)
                    .WithSlots(SlotId.None, SlotId.Board(2)),
                new CoreGameEvent(CoreEventType.SlotsFilled, 10, "deal")
                    .WithAmount(2),
            };

            var batch = PresentationBatchFixture.Create(2, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            Assert.AreEqual(1, plan.Groups.Count);
            Assert.AreEqual(2, plan.Groups[0].Steps.Count);
            AssertContainsFlow(plan, FlowId.CardDeal);
            AssertContainsFlow(plan, FlowId.FillSlots);

            FlowPayload batchedPayload = FindStepPayload(plan, FlowId.CardDeal);
            Assert.IsNotNull(batchedPayload.BatchedDeals);
            Assert.AreEqual(2, batchedPayload.BatchedDeals.Count);
            Assert.AreEqual(101, batchedPayload.BatchedDeals[0].CardUid);
            Assert.AreEqual(102, batchedPayload.BatchedDeals[1].CardUid);
        }

        [Test]
        public void PlanBuilder_FillEmptySlots_CoalescesEightCardDeals()
        {
            var events = BuildFillEmptySlotsEvents(10, 8);
            var batch = PresentationBatchFixture.Create(13, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            Assert.AreEqual(1, plan.Groups.Count);
            Assert.AreEqual(2, plan.Groups[0].Steps.Count);
            Assert.AreEqual(1, CountFlow(plan, FlowId.CardDeal));

            FlowPayload batchedPayload = FindStepPayload(plan, FlowId.CardDeal);
            Assert.IsNotNull(batchedPayload.BatchedDeals);
            Assert.AreEqual(8, batchedPayload.BatchedDeals.Count);
            Assert.AreEqual(SlotId.Board(1), batchedPayload.BatchedDeals[0].ToSlot);
            Assert.AreEqual(SlotId.Board(4), batchedPayload.BatchedDeals[7].ToSlot);
        }

        [Test]
        public void PlanBuilder_SingleCardDeal_HasNoBatchedDeals()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardDealt, 11, "FillEmptySlots")
                    .WithCard(201)
                    .WithSlots(SlotId.None, SlotId.Board(2))
                    .WithAmount(1),
            };

            var batch = PresentationBatchFixture.Create(14, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            Assert.AreEqual(1, plan.Groups.Count);
            Assert.AreEqual(1, plan.Groups[0].Steps.Count);
            FlowPayload payload = plan.Groups[0].Steps[0].Payload;
            Assert.IsNull(payload.BatchedDeals);
            Assert.AreEqual(201, payload.CardUid);
        }

        [Test]
        public void PlanBuilder_HelpCardChain_BuildsUseItemAndSnapshotAlignSteps()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.ItemUsed, 5, "help")
                    .WithCard(1),
                new CoreGameEvent(CoreEventType.EffectTriggered, 5, "help")
                    .WithSource("relic.chain", "help"),
                new CoreGameEvent(CoreEventType.EffectModifierApplied, 5, "help")
                    .WithSource("relic.chain", "modifier"),
            };

            var batch = PresentationBatchFixture.Create(3, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            AssertContainsFlow(plan, FlowId.UseItem);
            Assert.AreEqual(2, CountFlow(plan, FlowId.SnapshotAlign));
        }

        [Test]
        public void PlanBuilder_RequiresPlaybackEvents_AllHaveFlowRoutes()
        {
            var routedKinds = new HashSet<PresentationInstructionKind>();
            foreach (var entry in PresentationEventMap.Entries)
            {
                if (!entry.RequiresPlayback)
                {
                    continue;
                }

                var instruction = new PresentationInstruction(
                    new CoreGameEvent(entry.EventType, 1, "test"),
                    entry);
                Assert.IsTrue(
                    InstructionKindFlowRouter.TryRoute(instruction, out var route),
                    "Missing route for " + entry.InstructionKind);
                Assert.AreEqual(InstructionRouteKind.Flow, route.Kind);
                routedKinds.Add(entry.InstructionKind);
            }

            Assert.Greater(routedKinds.Count, 20);
        }

        [Test]
        public void PlanBuilder_OpeningDeal_RoutesDeckEntry()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardDealt, 10, "deal")
                    .WithAmount(5)
                    .WithMessage("opening"),
            };

            var batch = PresentationBatchFixture.Create(4, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            AssertContainsFlow(plan, FlowId.CardDeckEntry);
        }

        [Test]
        public void PlanBuilder_PhaseEnterGameplay_RoutesInGameUiEntrance()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.PhaseChanged, 1, "ChangePhase")
                    .WithAmount((int)GamePhase.InteractionLoop)
                    .WithDelta((int)GamePhase.RewardItemChoice),
            };

            var batch = PresentationBatchFixture.Create(5, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            AssertContainsFlow(plan, FlowId.InGameUiEntrance);
        }

        [Test]
        public void PlanBuilder_PhaseLeaveGameplay_RoutesInGameUiExit()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.PhaseChanged, 1, "ChangePhase")
                    .WithAmount((int)GamePhase.RewardItemChoice)
                    .WithDelta((int)GamePhase.InteractionLoop),
            };

            var batch = PresentationBatchFixture.Create(6, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            AssertContainsFlow(plan, FlowId.InGameUiExit);
        }

        [Test]
        public void PlanBuilder_PickItem_RoutesCardAcquisition()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.ItemPicked, 2, "pick")
                    .WithCard(42),
            };

            var batch = PresentationBatchFixture.Create(8, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            AssertContainsFlow(plan, FlowId.CardAcquisition);
        }

        [Test]
        public void BatchPlayer_Attack_ReleasesInputLock()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 1, "attack")
                    .WithActor(1)
                    .WithTarget(2)
                    .WithAmount(4),
            };
            var snapshot = OrchestrationTestSnapshots.Minimal();
            var batch = PresentationBatchFixture.Create(9, events, snapshot);

            var attackFlow = new RecordingFlowBinding(FlowId.CardAttack, invokeImpactMarker: true);
            var inputLock = new LocalInputLockGate();

            var flowRegistry = new FlowRegistry();
            flowRegistry.Register(attackFlow);

            var player = new PresentationBatchPlayer(
                new NullViewRegistry(),
                flowRegistry,
                inputLock);

            Assert.IsFalse(inputLock.IsLocked);
            var result = player.PlaySync(batch);

            Assert.IsFalse(inputLock.IsLocked);
            Assert.AreEqual(1, attackFlow.PlayCount);
            Assert.AreEqual(4, attackFlow.LastPayload.Amount);
            Assert.AreEqual(1, result.PlayedFlowCount);
        }

        [Test]
        public void StatEventProjection_SkipsHpChangedCollateralToCardAttack()
        {
            var attackTargets = new HashSet<(int, int)> { (1, 2) };
            var hpChanged = new CoreGameEvent(CoreEventType.HpChanged, 1, "attack")
                .WithCard(2)
                .WithDelta(-3);

            Assert.IsTrue(StatEventProjection.ShouldSkipForAttackImpact(hpChanged, attackTargets));
        }

        [Test]
        public void StatEventProjection_AppliesNonDamageStatEvents()
        {
            var healed = new CoreGameEvent(CoreEventType.Healed, 2, "buff")
                .WithCard(3)
                .WithRemaining(5, 0);

            Assert.IsTrue(StatEventProjection.IsStatEvent(healed.Type));
            Assert.IsFalse(StatEventProjection.ShouldSkipForAttackImpact(healed, new HashSet<(int, int)>()));
        }

        [Test]
        public void PlanBuilder_RotateGroup_SuppressesMoveCardWhenBoardRotatePresent()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardMoved, 3, "RotateBoardClockwise")
                    .WithCard(101)
                    .WithSlots(SlotId.Board(1), SlotId.Board(2)),
                new CoreGameEvent(CoreEventType.CardMoved, 3, "RotateBoardClockwise")
                    .WithCard(102)
                    .WithSlots(SlotId.Board(2), SlotId.Board(3)),
                new CoreGameEvent(CoreEventType.BoardRotated, 3, "RotateBoardClockwise")
                    .WithAmount(1),
            };

            var batch = PresentationBatchFixture.Create(10, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            Assert.AreEqual(1, plan.Groups.Count);
            Assert.AreEqual(1, plan.Groups[0].Steps.Count);
            Assert.AreEqual(FlowId.BoardRotate, plan.Groups[0].Steps[0].FlowId);
        }

        [Test]
        public void PlanBuilder_MonsterCounterDamage_RoutesCounterattackFlow()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 2, "DealDamage")
                    .WithActor(2)
                    .WithTarget(1)
                    .WithAmount(2),
            };

            var snapshot = OrchestrationTestSnapshots.WithBoardCombatants();
            var batch = PresentationBatchFixture.Create(11, events, snapshot);
            var plan = new PerformancePlanBuilder().Build(batch);

            AssertContainsFlow(plan, FlowId.Counterattack);
            Assert.AreEqual(0, CountFlow(plan, FlowId.CardAttack));
        }

        [Test]
        public void PlanBuilder_AttackAndCounterInSameAction_SplitIntoSerialGroups()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 4, "DealDamage")
                    .WithActor(1)
                    .WithTarget(2)
                    .WithAmount(1),
                new CoreGameEvent(CoreEventType.DamageDealt, 4, "DealDamage")
                    .WithActor(2)
                    .WithTarget(1)
                    .WithAmount(1),
            };

            var snapshot = OrchestrationTestSnapshots.WithBoardCombatants();
            var batch = PresentationBatchFixture.Create(12, events, snapshot);
            var plan = new PerformancePlanBuilder().Build(batch);

            Assert.AreEqual(2, plan.Groups.Count);
            Assert.AreEqual(FlowId.CardAttack, plan.Groups[0].Steps[0].FlowId);
            Assert.AreEqual(FlowId.Counterattack, plan.Groups[1].Steps[0].FlowId);
        }

        private static FlowPayload FindStepPayload(PresentationPlan plan, FlowId flowId)
        {
            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                var steps = plan.Groups[groupIndex].Steps;
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    PlanStep step = steps[stepIndex];
                    if (step.FlowId == flowId)
                    {
                        return step.Payload;
                    }
                }
            }

            Assert.Fail("Plan missing flow: " + flowId);
            return null;
        }

        private static void AssertContainsFlow(PresentationPlan plan, FlowId flowId)
        {
            Assert.Greater(CountFlow(plan, flowId), 0, "Plan missing flow: " + flowId);
        }

        private static int CountFlow(PresentationPlan plan, FlowId flowId)
        {
            var count = 0;
            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                var steps = plan.Groups[groupIndex].Steps;
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    if (steps[stepIndex].FlowId == flowId)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static List<CoreGameEvent> BuildFillEmptySlotsEvents(int actionId, int dealCount)
        {
            IReadOnlyList<SlotId> fillOrder = RotateBoardClockwiseAction.ClockwisePath;
            var events = new List<CoreGameEvent>(dealCount + 1);
            for (var i = 0; i < dealCount; i++)
            {
                events.Add(new CoreGameEvent(CoreEventType.CardDealt, actionId, "FillEmptySlots")
                    .WithCard(100 + i)
                    .WithSlots(SlotId.None, fillOrder[i])
                    .WithAmount(i + 1));
            }

            events.Add(new CoreGameEvent(CoreEventType.SlotsFilled, actionId, "FillEmptySlots")
                .WithAmount(dealCount));
            return events;
        }
    }
}
