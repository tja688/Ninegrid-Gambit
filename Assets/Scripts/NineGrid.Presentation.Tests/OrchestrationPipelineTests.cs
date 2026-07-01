using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Debugging;
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
        public void PlanBuilder_AttackKillRotateFill_BuildsExpectedStepsAndReactions()
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
            Assert.AreEqual(1, plan.Groups.Count);
            Assert.AreEqual(4, plan.Groups[0].Steps.Count);
            AssertContainsFlow(plan, FlowId.CardAttack);
            AssertContainsFlow(plan, FlowId.CardKill);
            AssertContainsFlow(plan, FlowId.BoardRotate);
            AssertContainsFlow(plan, FlowId.MoveCard);
            AssertContainsReaction(plan, ReactionId.ShowDamage, ReactionAnchorKind.StepMarker, FlowMarkers.Impact);
        }

        [Test]
        public void BatchHijack_EditorDirection_OverridesBoardDerivedAttackDirection()
        {
            var events = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.DamageDealt, 1, "attack")
                    .WithActor(PerformanceDebugActorUids.Player)
                    .WithTarget(PerformanceDebugActorUids.Enemy)
                    .WithAmount(3),
                new CoreGameEvent(CoreEventType.CardKilled, 1, "attack")
                    .WithCard(PerformanceDebugActorUids.Enemy),
            };

            var batch = PresentationBatchFixture.Create(11, events, OrchestrationTestSnapshots.Minimal());
            var plan = new PerformancePlanBuilder().Build(batch);

            var payload = new PerformanceDebugPayload();
            payload.Set(PerformanceDebugPayloadKeys.Direction, CardBattleDirection.Up.ToString());
            payload.Set(PerformanceDebugPayloadKeys.ForceDirectionOverride, "true");
            PerformanceDebugBatchHijack.ApplyEditorOverrides(plan, payload);

            Assert.AreEqual(Vector2.up, FindStepPayload(plan, FlowId.CardAttack).Direction);
            Assert.AreEqual(Vector2.up, FindStepPayload(plan, FlowId.CardKill).Direction);
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
            Assert.AreEqual(3, plan.Groups[0].Steps.Count);
            AssertContainsFlow(plan, FlowId.CardDeal);
            AssertContainsFlow(plan, FlowId.FillSlots);
        }

        [Test]
        public void PlanBuilder_HelpCardChain_BuildsUseItemAndReactions()
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
            AssertContainsReaction(plan, ReactionId.TriggerEffect, ReactionAnchorKind.StepEnd, string.Empty);
            AssertContainsReaction(plan, ReactionId.ApplyModifier, ReactionAnchorKind.Immediate, string.Empty);
        }

        [Test]
        public void BatchPlayer_PlaySync_InvokesFlowsReactionsAndReconcile()
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
            var damageReaction = new RecordingReactionBinding(ReactionId.ShowDamage);
            var reconcilable = new RecordingReconcilable();
            var inputLock = new LocalInputLockGate();

            var flowRegistry = new FlowRegistry();
            flowRegistry.Register(attackFlow);
            var reactionRegistry = new ReactionRegistry();
            reactionRegistry.Register(damageReaction);

            var player = new PresentationBatchPlayer(
                new NullViewRegistry(),
                flowRegistry,
                reactionRegistry,
                new IReconcilable[] { reconcilable },
                inputLock);

            Assert.IsFalse(inputLock.IsLocked);
            var result = player.PlaySync(batch);

            Assert.IsFalse(inputLock.IsLocked);
            Assert.AreEqual(1, attackFlow.PlayCount);
            Assert.AreEqual(1, damageReaction.PlayCount);
            Assert.AreEqual(4, damageReaction.LastPayload.Amount);
            Assert.AreEqual(1, reconcilable.ApplyCount);
            Assert.AreSame(snapshot, reconcilable.LastSnapshot);
            Assert.IsTrue(result.Reconciled);
            Assert.Greater(result.PlayedReactionCount, 0);
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
            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                var steps = plan.Groups[groupIndex].Steps;
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    if (steps[stepIndex].FlowId == flowId)
                    {
                        return;
                    }
                }
            }

            Assert.Fail("Plan missing flow: " + flowId);
        }

        private static void AssertContainsReaction(
            PresentationPlan plan,
            ReactionId reactionId,
            ReactionAnchorKind anchorKind,
            string marker)
        {
            for (var i = 0; i < plan.Reactions.Count; i++)
            {
                var reaction = plan.Reactions[i];
                if (reaction.ReactionId != reactionId)
                {
                    continue;
                }

                if (reaction.Anchor.Kind == anchorKind
                    && (string.IsNullOrEmpty(marker) || reaction.Anchor.Marker == marker))
                {
                    return;
                }
            }

            Assert.Fail("Plan missing reaction: " + reactionId + " @" + anchorKind);
        }
    }
}
