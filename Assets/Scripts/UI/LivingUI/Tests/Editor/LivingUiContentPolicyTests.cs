using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.LivingUI.Tests
{
    public sealed class LivingUiContentPolicyTests
    {
        [Test]
        public void Anchor_TopLeft_PinsWhenSizeGrows()
        {
            var offset = new Vector3(0.5f, -0.25f, 0f);
            var small = new Vector2(2f, 2f);
            var large = new Vector2(4f, 4f);

            var atSmall = LivingUiContentProjector.ResolveAnchoredLocal(
                LivingUiContentAnchor.TopLeft, offset, small);
            var atLarge = LivingUiContentProjector.ResolveAnchoredLocal(
                LivingUiContentAnchor.TopLeft, offset, large);

            Assert.AreEqual(-1f + 0.5f, atSmall.x, 0.0001f);
            Assert.AreEqual(1f - 0.25f, atSmall.y, 0.0001f);
            Assert.AreEqual(-2f + 0.5f, atLarge.x, 0.0001f);
            Assert.AreEqual(2f - 0.25f, atLarge.y, 0.0001f);
        }

        [Test]
        public void Anchor_Center_IgnoresSize()
        {
            var offset = new Vector3(-1.4f, 1.34f, 0.1f);
            var a = LivingUiContentProjector.ResolveAnchoredLocal(
                LivingUiContentAnchor.Center, offset, new Vector2(2f, 2f));
            var b = LivingUiContentProjector.ResolveAnchoredLocal(
                LivingUiContentAnchor.Center, offset, new Vector2(9f, 6f));
            Assert.AreEqual(offset, a);
            Assert.AreEqual(offset, b);
        }

        [Test]
        public void CenterLocalToAnchorOffset_RoundTripsAtAuthoringSize()
        {
            var centerLocal = new Vector3(-1.4f, 1.34f, 0f);
            var size = new Vector2(9.91f, 6.98f);
            var offset = LivingUiContentProjector.CenterLocalToAnchorOffset(
                LivingUiContentAnchor.TopLeft, centerLocal, size);
            var back = LivingUiContentProjector.ResolveAnchoredLocal(
                LivingUiContentAnchor.TopLeft, offset, size);
            Assert.AreEqual(centerLocal.x, back.x, 0.0001f);
            Assert.AreEqual(centerLocal.y, back.y, 0.0001f);
        }

        [Test]
        public void CoreRule_CrossLayout_IsInvariant()
        {
            Assert.AreEqual(
                LivingUiContentMotionMode.Invariant,
                LivingUiContentPolicy.ResolveMotionMode(true, true, isTransitioning: true));
            Assert.AreEqual(
                LivingUiContentMotionMode.Scale,
                LivingUiContentPolicy.ResolveMotionMode(false, true, isTransitioning: true));
            Assert.AreEqual(
                LivingUiContentMotionMode.Scale,
                LivingUiContentPolicy.ResolveMotionMode(true, false, isTransitioning: true));
            Assert.AreEqual(
                LivingUiContentMotionMode.Invariant,
                LivingUiContentPolicy.ResolveMotionMode(true, false, isTransitioning: false));
        }

        [Test]
        public void ContentPhase_DistinguishesEnteringAndExiting()
        {
            Assert.AreEqual(
                LivingUiContentPhase.Stable,
                LivingUiContentPolicy.EvaluatePhase(true, true, true, isTransitioning: false));
            Assert.AreEqual(
                LivingUiContentPhase.Exiting,
                LivingUiContentPolicy.EvaluatePhase(true, false, false, isTransitioning: true));
            Assert.AreEqual(
                LivingUiContentPhase.Entering,
                LivingUiContentPolicy.EvaluatePhase(false, true, true, isTransitioning: true));
            Assert.AreEqual(
                LivingUiContentPhase.Hidden,
                LivingUiContentPolicy.EvaluatePhase(false, false, false, isTransitioning: false));
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
        public void EnterProgress_StartsAtZeroWhenSourceLargerThanTarget()
        {
            var source = new Vector2(7.72f, 2.06f);
            var target = new Vector2(3.32f, 1.63f);

            Assert.AreEqual(0f, LivingUiContentProjector.ComputeEnterProgress(source, target, source), 0.0001f);
            Assert.AreEqual(1f, LivingUiContentProjector.ComputeEnterProgress(source, target, target), 0.0001f);
        }

        [Test]
        public void EnterProgress_SizeStable_UsesPositionTravel()
        {
            var size = new Vector2(3f, 1f);
            var sourcePos = new Vector2(1.41f, -6.59f);
            var targetPos = new Vector2(-5.5f, 1.53f);

            Assert.AreEqual(
                0f,
                LivingUiContentProjector.ComputeEnterProgress(
                    size, size, size, sourcePos, targetPos, sourcePos, timeProgress01: 1f),
                0.0001f);
            Assert.AreEqual(
                1f,
                LivingUiContentProjector.ComputeEnterProgress(
                    size, size, size, sourcePos, targetPos, targetPos, timeProgress01: 0f),
                0.0001f);

            var mid = Vector2.Lerp(sourcePos, targetPos, 0.4f);
            Assert.AreEqual(
                0.4f,
                LivingUiContentProjector.ComputeEnterProgress(
                    size, size, size, sourcePos, targetPos, mid, timeProgress01: 0f),
                0.02f);
        }

        [Test]
        public void EnterProgress_SizeAndPositionStable_UsesTimeProgress()
        {
            var size = new Vector2(3f, 3f);
            var pos = new Vector2(5.47f, 0.03f);

            Assert.AreEqual(
                0f,
                LivingUiContentProjector.ComputeEnterProgress(
                    size, size, size, pos, pos, pos, timeProgress01: 0f),
                0.0001f);
            Assert.AreEqual(
                0.35f,
                LivingUiContentProjector.ComputeEnterProgress(
                    size, size, size, pos, pos, pos, timeProgress01: 0.35f),
                0.0001f);
        }

        [Test]
        public void ScaleEnter_DoesNotPopWhenSizeStable()
        {
            var authored = new LivingUiContentLocalPose(Vector3.zero, Vector3.one);
            var size = new Vector2(3f, 1f);
            var sourcePos = new Vector2(0f, 0f);
            var targetPos = new Vector2(10f, 0f);

            var enter0 = LivingUiContentProjector.ComputeEnterProgress(
                size, size, size, sourcePos, targetPos, sourcePos, 1f);
            var proj = LivingUiContentProjector.ProjectScale(
                LivingUiContentAnchor.Center, authored, size, enter0);
            Assert.AreEqual(0f, proj.LocalScale.x, 0.0001f);
            Assert.IsFalse(proj.Visible);
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
                    LivingUiContentAnchor.Center,
                    LivingUiLayoutId.MainMenu,
                    new LivingUiContentEnvelope(new Vector2(2.5f, 0.8f))),
                new(
                    "menu.icon.pack",
                    4,
                    new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                    LivingUiContentAnchor.Center,
                    LivingUiLayoutId.MainMenu,
                    new LivingUiContentEnvelope(new Vector2(1.2f, 1.5f))),
                new(
                    "menu.start",
                    12,
                    new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                    LivingUiContentAnchor.Center,
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
