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
        public void HideDuringTransit_TogglesAtTransitFlag()
        {
            var binding = new LivingUiContentBinding(
                "menu.title",
                4,
                new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                LivingUiContentFollowPolicy.RigidTravel,
                LivingUiContentVisibilityPolicy.HideDuringTransit,
                LivingUiLayoutId.MainMenu,
                new LivingUiContentEnvelope(Vector2.zero));

            Assert.IsTrue(LivingUiContentPolicy.EvaluateVisible(
                binding, LivingUiLayoutId.MainMenu, LivingUiLayoutId.MainMenu, isTransitioning: false));
            Assert.IsFalse(LivingUiContentPolicy.EvaluateVisible(
                binding, LivingUiLayoutId.MainMenu, LivingUiLayoutId.MainMenu, isTransitioning: true));
            Assert.IsFalse(LivingUiContentPolicy.EvaluateVisible(
                binding, LivingUiLayoutId.Battle, LivingUiLayoutId.Battle, isTransitioning: false));
        }

        [Test]
        public void FaceMatch_AllowsCommittedFaceDuringTransit()
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
                binding, LivingUiLayoutId.Battle, LivingUiLayoutId.MainMenu, isTransitioning: true));
            Assert.IsFalse(LivingUiContentPolicy.EvaluateVisible(
                binding, LivingUiLayoutId.Battle, LivingUiLayoutId.MainMenu, isTransitioning: false));
        }

        [Test]
        public void FaceMatch_AllowsTransitionFromFaceDuringExit()
        {
            var binding = new LivingUiContentBinding(
                "menu.icon.pack",
                4,
                new LivingUiContentLocalPose(Vector3.zero, Vector3.one),
                LivingUiContentFollowPolicy.BoundaryReactive,
                LivingUiContentVisibilityPolicy.AlwaysVisible,
                LivingUiLayoutId.MainMenu,
                new LivingUiContentEnvelope(new Vector2(1.2f, 1.5f)));

            // Commit(CharacterChoice) 后：effective/committed 已是目标，Face 靠 transitionFrom 保活以便缩小退场
            Assert.IsTrue(LivingUiContentPolicy.EvaluateVisible(
                binding,
                LivingUiLayoutId.CharacterChoice,
                LivingUiLayoutId.CharacterChoice,
                isTransitioning: true,
                transitionFromLayout: LivingUiLayoutId.MainMenu));

            Assert.IsFalse(LivingUiContentPolicy.EvaluateVisible(
                binding,
                LivingUiLayoutId.CharacterChoice,
                LivingUiLayoutId.CharacterChoice,
                isTransitioning: true,
                transitionFromLayout: LivingUiLayoutId.Battle));

            Assert.IsFalse(LivingUiContentPolicy.EvaluateVisible(
                binding,
                LivingUiLayoutId.CharacterChoice,
                LivingUiLayoutId.CharacterChoice,
                isTransitioning: false,
                transitionFromLayout: LivingUiLayoutId.MainMenu));
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
        public void BoundaryReactive_CollapsesToZeroAndExpandsToOne()
        {
            var baseline = new Vector2(8f, 4f);
            var authored = new LivingUiContentLocalPose(new Vector3(1f, 0.5f, 0f), Vector3.one);

            var atBaseline = LivingUiContentProjector.ProjectBoundaryReactive(
                authored, baseline, baseline, LivingUiContentProjector.DefaultStaggerSpan);
            Assert.AreEqual(1f, atBaseline.LocalScale.x, 0.0001f);
            Assert.AreEqual(authored.LocalPosition.x, atBaseline.LocalPosition.x, 0.0001f);
            Assert.AreEqual(authored.LocalPosition.y, atBaseline.LocalPosition.y, 0.0001f);
            Assert.IsTrue(atBaseline.Visible);

            var collapsed = LivingUiContentProjector.ProjectBoundaryReactive(
                authored, baseline, Vector2.zero, LivingUiContentProjector.DefaultStaggerSpan);
            Assert.AreEqual(0f, collapsed.LocalScale.x, 0.0001f);
            Assert.AreEqual(authored.LocalPosition.x, collapsed.LocalPosition.x, 0.0001f);
            Assert.IsFalse(collapsed.Visible);
        }

        [Test]
        public void BoundaryReactive_KeepsAuthoredAnchorFixedWhileScaling()
        {
            var baseline = new Vector2(8f, 4f);
            var authored = new LivingUiContentLocalPose(new Vector3(2f, -1f, 0.1f), Vector3.one);
            var envelope = new Vector2(0.8f, 0.6f);
            // 高度只刚过锚点：锚点不动，scale 介于 0..1。
            var mid = new Vector2(6f, 2.5f);

            var proj = LivingUiContentProjector.ProjectBoundaryReactive(
                authored,
                baseline,
                mid,
                LivingUiContentProjector.DefaultStaggerSpan,
                envelope);

            Assert.AreEqual(authored.LocalPosition.x, proj.LocalPosition.x, 0.0001f);
            Assert.AreEqual(authored.LocalPosition.y, proj.LocalPosition.y, 0.0001f);
            Assert.AreEqual(authored.LocalPosition.z, proj.LocalPosition.z, 0.0001f);
            Assert.Greater(proj.LocalScale.x, 0f);
            Assert.Less(proj.LocalScale.x, 1f);
        }

        [Test]
        public void BoundaryReactive_VanishesWhenBoundarySweepsAnchor()
        {
            var baseline = new Vector2(8f, 4f);
            var authored = new LivingUiContentLocalPose(new Vector3(2f, -1f, 0f), Vector3.one);
            var envelope = new Vector2(1f, 1f);
            const float padding = LivingUiContentProjector.DefaultContentPadding;

            // insetHalf.y == |ay| → 边界刚好扫过锚点，必须已消失。
            var sweepHeight = 2f * (Mathf.Abs(authored.LocalPosition.y) + padding);
            var atSweep = LivingUiContentProjector.ProjectBoundaryReactive(
                authored,
                baseline,
                new Vector2(baseline.x, sweepHeight),
                LivingUiContentProjector.DefaultStaggerSpan,
                envelope,
                padding);

            Assert.AreEqual(0f, atSweep.LocalScale.x, 0.0001f);
            Assert.AreEqual(authored.LocalPosition, atSweep.LocalPosition);
            Assert.IsFalse(atSweep.Visible);

            // 再放大一点点：种子应出现且仍锚在原点。
            var justPast = LivingUiContentProjector.ProjectBoundaryReactive(
                authored,
                baseline,
                new Vector2(baseline.x, sweepHeight + 0.4f),
                LivingUiContentProjector.DefaultStaggerSpan,
                envelope,
                padding);

            Assert.AreEqual(authored.LocalPosition, justPast.LocalPosition);
            Assert.Greater(justPast.LocalScale.x, 0f);
            Assert.Less(justPast.LocalScale.x, 1f);
            Assert.IsTrue(justPast.Visible);
        }

        [Test]
        public void BoundaryReactive_EdgeAnchorScalesDownBeforeCenter()
        {
            var baseline = new Vector2(8f, 4f);
            var nearCenter = new LivingUiContentLocalPose(new Vector3(0.2f, 0.1f, 0f), Vector3.one);
            var nearEdge = new LivingUiContentLocalPose(new Vector3(3.6f, 1.6f, 0f), Vector3.one);
            var envelope = new Vector2(0.4f, 0.4f);
            var midSize = baseline * 0.7f;

            var centerProj = LivingUiContentProjector.ProjectBoundaryReactive(
                nearCenter, baseline, midSize, 0f, envelope);
            var edgeProj = LivingUiContentProjector.ProjectBoundaryReactive(
                nearEdge, baseline, midSize, 0f, envelope);

            Assert.Greater(centerProj.LocalScale.x, edgeProj.LocalScale.x);
            Assert.AreEqual(nearCenter.LocalPosition, centerProj.LocalPosition);
            Assert.AreEqual(nearEdge.LocalPosition, edgeProj.LocalPosition);
        }

        [Test]
        public void BoundaryReactive_FixedAnchorKeepsAabbInsideInset()
        {
            var baseline = new Vector2(9.91f, 6.98f);
            var authored = new LivingUiContentLocalPose(new Vector3(2.67f, -1.27f, 0f), new Vector3(6f, 6f, 6f));
            var envelope = new Vector2(1.2f, 1.5f);
            const float padding = LivingUiContentProjector.DefaultContentPadding;

            // 覆盖：满尺寸、半高、刚好过锚点、矮到锚点外。
            var sizes = new[]
            {
                baseline,
                new Vector2(9.91f, 4.0f),
                new Vector2(9.91f, 2.8f),
                new Vector2(9.91f, 1.0f),
            };

            foreach (var size in sizes)
            {
                var proj = LivingUiContentProjector.ProjectBoundaryReactive(
                    authored,
                    baseline,
                    size,
                    LivingUiContentProjector.DefaultStaggerSpan,
                    envelope,
                    padding);

                Assert.AreEqual(authored.LocalPosition.x, proj.LocalPosition.x, 0.0001f);
                Assert.AreEqual(authored.LocalPosition.y, proj.LocalPosition.y, 0.0001f);

                // 锚点已被边界扫过时：scale=0 且不可见（Driver 会关掉物体），不再要求点仍在 inset 内。
                if (!proj.Visible)
                {
                    Assert.AreEqual(0f, proj.LocalScale.x, 0.0001f);
                    continue;
                }

                var insetHalf = new Vector2(
                    Mathf.Max(size.x * 0.5f - padding, 0f),
                    Mathf.Max(size.y * 0.5f - padding, 0f));
                var scaleFactor = authored.LocalScale.x > 0.0001f
                    ? proj.LocalScale.x / authored.LocalScale.x
                    : 0f;
                var visualHalf = envelope * 0.5f * scaleFactor;

                Assert.LessOrEqual(Mathf.Abs(proj.LocalPosition.x) + visualHalf.x, insetHalf.x + 0.0001f);
                Assert.LessOrEqual(Mathf.Abs(proj.LocalPosition.y) + visualHalf.y, insetHalf.y + 0.0001f);
            }
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
