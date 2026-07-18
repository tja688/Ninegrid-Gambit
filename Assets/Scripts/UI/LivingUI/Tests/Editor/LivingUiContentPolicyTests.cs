using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.LivingUI.Tests
{
    public sealed class LivingUiContentPolicyTests
    {
        [Test]
        public void RigidTravel_KeepsLocalOffsetUnchanged()
        {
            var local = new Vector3(-1.4f, 1.34f, 0.1f);
            var carrierA = new Vector2(-1.55f, 0.08f);
            var carrierB = new Vector2(4f, -2f);

            var worldA = LivingUiContentPolicy.RigidTravelWorldPosition(carrierA, local);
            var worldB = LivingUiContentPolicy.RigidTravelWorldPosition(carrierB, local);

            Assert.AreEqual(local.x, worldA.x - carrierA.x, 0.0001f);
            Assert.AreEqual(local.y, worldA.y - carrierA.y, 0.0001f);
            Assert.AreEqual(local.x, worldB.x - carrierB.x, 0.0001f);
            Assert.AreEqual(local.y, worldB.y - carrierB.y, 0.0001f);
            Assert.AreEqual(local.z, worldA.z);
            Assert.AreEqual(local.z, worldB.z);
        }

        [Test]
        public void ContentPhase_DistinguishesEnteringAndExiting()
        {
            var binding = new LivingUiContentBinding(
                "menu.title",
                4,
                new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                LivingUiContentFollowPolicy.BoundaryReactive,
                LivingUiContentVisibilityPolicy.AlwaysVisible,
                LivingUiLayoutId.MainMenu,
                new LivingUiContentEnvelope(Vector2.zero));

            Assert.AreEqual(
                LivingUiContentPhase.Stable,
                LivingUiContentPolicy.EvaluatePhase(
                    binding, LivingUiLayoutId.MainMenu, LivingUiLayoutId.MainMenu,
                    LivingUiLayoutId.MainMenu, isTransitioning: false));
            Assert.AreEqual(
                LivingUiContentPhase.Exiting,
                LivingUiContentPolicy.EvaluatePhase(
                    binding, LivingUiLayoutId.MainMenu, LivingUiLayoutId.Battle,
                    LivingUiLayoutId.Battle, isTransitioning: true));
            Assert.AreEqual(
                LivingUiContentPhase.Entering,
                LivingUiContentPolicy.EvaluatePhase(
                    binding, LivingUiLayoutId.Battle, LivingUiLayoutId.MainMenu,
                    LivingUiLayoutId.MainMenu, isTransitioning: true));
            Assert.AreEqual(
                LivingUiContentPhase.Hidden,
                LivingUiContentPolicy.EvaluatePhase(
                    binding, LivingUiLayoutId.MainMenu, LivingUiLayoutId.Battle,
                    LivingUiLayoutId.Battle, isTransitioning: false));
        }

        [Test]
        public void ExitScaleFactor_ReachesZeroWithinDuration()
        {
            Assert.AreEqual(1f, LivingUiContentProjector.ComputeExitScaleFactor(0f), 0.0001f);
            Assert.AreEqual(0.5f, LivingUiContentProjector.ComputeExitScaleFactor(0.05f), 0.0001f);
            Assert.AreEqual(0f, LivingUiContentProjector.ComputeExitScaleFactor(0.1f), 0.0001f);
            Assert.AreEqual(0f, LivingUiContentProjector.ComputeExitScaleFactor(0.5f), 0.0001f);
        }

        [Test]
        public void HideDuringTransit_NoLongerHidesDuringTransit()
        {
            var binding = new LivingUiContentBinding(
                "menu.start",
                12,
                new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                LivingUiContentFollowPolicy.BoundaryReactive,
                LivingUiContentVisibilityPolicy.HideDuringTransit,
                LivingUiLayoutId.MainMenu,
                new LivingUiContentEnvelope(Vector2.zero));

            Assert.IsTrue(LivingUiContentPolicy.EvaluateVisible(
                binding, LivingUiLayoutId.MainMenu, LivingUiLayoutId.Battle,
                LivingUiLayoutId.Battle, isTransitioning: true));
        }

        [Test]
        public void FaceMatch_AllowsExitingFaceDuringTransit()
        {
            var binding = new LivingUiContentBinding(
                "menu.title",
                4,
                new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                LivingUiContentFollowPolicy.BoundaryReactive,
                LivingUiContentVisibilityPolicy.AlwaysVisible,
                LivingUiLayoutId.MainMenu,
                new LivingUiContentEnvelope(Vector2.zero));

            Assert.IsTrue(LivingUiContentPolicy.EvaluateVisible(
                binding, LivingUiLayoutId.MainMenu, LivingUiLayoutId.Battle,
                LivingUiLayoutId.Battle, isTransitioning: true));
            Assert.IsFalse(LivingUiContentPolicy.EvaluateVisible(
                binding, LivingUiLayoutId.MainMenu, LivingUiLayoutId.Battle,
                LivingUiLayoutId.Battle, isTransitioning: false));
        }

        [Test]
        public void AlwaysVisible_RespectsFaceOnly()
        {
            var binding = new LivingUiContentBinding(
                "menu.start",
                12,
                new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                LivingUiContentFollowPolicy.RigidTravel,
                LivingUiContentVisibilityPolicy.AlwaysVisible,
                LivingUiLayoutId.MainMenu,
                new LivingUiContentEnvelope(Vector2.zero));

            Assert.IsTrue(LivingUiContentPolicy.EvaluateVisible(binding, LivingUiLayoutId.MainMenu, true));
            Assert.IsFalse(LivingUiContentPolicy.EvaluateVisible(binding, LivingUiLayoutId.Room, false));
        }

        [Test]
        public void EnterProgress_StartsAtZeroWhenSourceLargerThanTarget()
        {
            var source = new Vector2(7.72f, 2.06f);
            var target = new Vector2(3.32f, 1.63f);

            Assert.AreEqual(0f, LivingUiContentProjector.ComputeEnterProgress(source, target, source), 0.0001f);
            Assert.AreEqual(1f, LivingUiContentProjector.ComputeEnterProgress(source, target, target), 0.0001f);
        }

        [Test]
        public void EnterProjection_DoesNotStartAtFullScaleWhenSourceExceedsBaseline()
        {
            var authored = new LivingUiContentLocalPose(Vector3.zero, Vector3.one);
            var source = new Vector2(7.72f, 2.06f);
            var target = new Vector2(3.32f, 1.63f);

            var atStart = LivingUiContentProjector.ProjectBoundaryReactiveEnter(
                authored, source, target, source, LivingUiContentProjector.DefaultStaggerSpan);
            var oldWay = LivingUiContentProjector.ProjectBoundaryReactive(
                authored, target, source, LivingUiContentProjector.DefaultStaggerSpan);

            Assert.AreEqual(0f, atStart.LocalScale.x, 0.0001f);
            Assert.IsFalse(atStart.Visible);
            Assert.AreEqual(1f, oldWay.LocalScale.x, 0.0001f);
        }

        [Test]
        public void BoundaryReactive_CollapsesToZeroAndExpandsToOne()
        {
            var baseline = new Vector2(8f, 4f);
            var authored = new LivingUiContentLocalPose(new Vector3(1f, 0.5f, 0f), Vector3.one);

            var atBaseline = LivingUiContentProjector.ProjectBoundaryReactive(
                authored, baseline, baseline, LivingUiContentProjector.DefaultStaggerSpan);
            Assert.AreEqual(1f, atBaseline.LocalScale.x, 0.0001f);
            Assert.IsTrue(atBaseline.Visible);

            var collapsed = LivingUiContentProjector.ProjectBoundaryReactive(
                authored, baseline, Vector2.zero, LivingUiContentProjector.DefaultStaggerSpan);
            Assert.AreEqual(0f, collapsed.LocalScale.x, 0.0001f);
            Assert.IsFalse(collapsed.Visible);
        }

        [Test]
        public void BoundaryReactive_StaggerOrderFollowsNormalizedPosition()
        {
            var baseline = new Vector2(8f, 4f);
            var nearCenter = new LivingUiContentLocalPose(new Vector3(0.2f, 0.1f, 0f), Vector3.one);
            var nearEdge = new LivingUiContentLocalPose(new Vector3(3.6f, 1.6f, 0f), Vector3.one);
            var midSize = baseline * 0.55f;
            const float span = 0.35f;

            var centerProj = LivingUiContentProjector.ProjectBoundaryReactive(nearCenter, baseline, midSize, span);
            var edgeProj = LivingUiContentProjector.ProjectBoundaryReactive(nearEdge, baseline, midSize, span);

            Assert.Greater(centerProj.LocalScale.x, edgeProj.LocalScale.x);
            Assert.Greater(
                LivingUiContentProjector.Stagger01FromLocal(nearEdge.LocalPosition, baseline),
                LivingUiContentProjector.Stagger01FromLocal(nearCenter.LocalPosition, baseline));
        }

        [Test]
        public void PartialFollow_DisplacesAlongEdgeWithoutScaling()
        {
            var baseline = new Vector2(4f, 2f);
            var authored = new LivingUiContentLocalPose(new Vector3(-1.5f, 0.25f, 0.1f), new Vector3(1f, 1f, 1f));
            var wider = new Vector2(6f, 2f);

            var left = LivingUiContentProjector.ProjectPartialFollow(
                authored, baseline, wider, LivingUiPartialFollowEdge.Left);
            Assert.AreEqual(authored.LocalPosition.x - 1f, left.LocalPosition.x, 0.0001f);
            Assert.AreEqual(authored.LocalPosition.y, left.LocalPosition.y, 0.0001f);
            Assert.AreEqual(authored.LocalScale, left.LocalScale);
            Assert.IsTrue(left.Visible);

            var right = LivingUiContentProjector.ProjectPartialFollow(
                authored, baseline, wider, LivingUiPartialFollowEdge.Right);
            Assert.AreEqual(authored.LocalPosition.x + 1f, right.LocalPosition.x, 0.0001f);

            var taller = new Vector2(4f, 3f);
            var top = LivingUiContentProjector.ProjectPartialFollow(
                authored, baseline, taller, LivingUiPartialFollowEdge.Top);
            Assert.AreEqual(authored.LocalPosition.y + 0.5f, top.LocalPosition.y, 0.0001f);
            Assert.AreEqual(authored.LocalScale, top.LocalScale);
        }

        [Test]
        public void Envelope_RaisesCarrierFlowFloor()
        {
            var styleFloor = new Vector2(0.18f, 0.18f);
            var bindings = new List<LivingUiContentBinding>
            {
                new(
                    "menu.title",
                    4,
                    new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                    LivingUiContentFollowPolicy.BoundaryReactive,
                    LivingUiContentVisibilityPolicy.AlwaysVisible,
                    LivingUiLayoutId.MainMenu,
                    new LivingUiContentEnvelope(new Vector2(2.5f, 0.8f))),
                new(
                    "menu.icon.pack",
                    4,
                    new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                    LivingUiContentFollowPolicy.BoundaryReactive,
                    LivingUiContentVisibilityPolicy.AlwaysVisible,
                    LivingUiLayoutId.MainMenu,
                    new LivingUiContentEnvelope(new Vector2(1.2f, 1.5f))),
                new(
                    "menu.start",
                    12,
                    new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                    LivingUiContentFollowPolicy.RigidTravel,
                    LivingUiContentVisibilityPolicy.HideDuringTransit,
                    LivingUiLayoutId.MainMenu,
                    new LivingUiContentEnvelope(Vector2.zero)),
            };

            var aggregated = LivingUiContentProjector.AggregateEnvelopeFloors(bindings);
            Assert.AreEqual(new Vector2(2.5f, 1.5f), aggregated[4]);
            Assert.IsFalse(aggregated.ContainsKey(12));

            var floor4 = LivingUiContentProjector.ResolveCarrierFlowFloor(styleFloor, aggregated, 4);
            Assert.AreEqual(new Vector2(2.5f, 1.5f), floor4);

            var floor12 = LivingUiContentProjector.ResolveCarrierFlowFloor(styleFloor, aggregated, 12);
            Assert.AreEqual(styleFloor, floor12);
        }

        [Test]
        public void Planner_RespectsEnvelopeRaisedFlowFloor()
        {
            var planner = new TransitionPlanner();
            var style = new LivingUiTransitionStyle
            {
                BaseDuration = 0.5f,
                DistanceSecondsPerUnit = 0.01f,
                MaximumDistanceAddition = 0.1f,
                CanonSpan = 0f,
                ExpelledLead = 0f,
                EnteringDelay = 0f,
                FlowWidthFloor = 0.2f,
                FlowHeightFloor = 0.2f,
                SizeCeilingMultiplier = 4f,
            };
            var overrides = new Dictionary<int, Vector2> { [1] = new Vector2(0.9f, 0.7f) };
            var source = new LivingUiCarrierState(1, Vector2.zero, Vector2.zero, new Vector2(2f, 1.5f), new Vector2(-8f, -5f));
            var terminals = new Dictionary<int, LivingUiTerminal>
            {
                [1] = new LivingUiTerminal(1, Vector2.one, new Vector2(1f, 0.8f), 0, 0),
            };
            var plan = planner.Plan(
                new[] { source },
                new LivingUiLayout(LivingUiLayoutId.MainMenu, terminals),
                Rect.MinMaxRect(-8f, -5f, 8f, 5f),
                style,
                overrides);
            var program = plan.GetProgram(1);

            for (var index = 0; index <= 100; index++)
            {
                var sample = program.Sample(program.EndTime * index / 100f);
                Assert.GreaterOrEqual(sample.Size.x, 0.9f - 0.0001f);
                Assert.GreaterOrEqual(sample.Size.y, 0.7f - 0.0001f);
            }
        }
    }
}
