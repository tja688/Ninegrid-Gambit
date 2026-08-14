using NineGrid.Cards;
using NineGrid.Cards.Vfx;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 手牌 L1 飘动让位纯函数回归：锁住「陈旧 Hover 枚举 + 活 hover 权威」实战死卡路径。
    /// </summary>
    public class HandCardLifeEngagementTests
    {
        private static readonly Vector3 LayoutAnchor = Vector3.zero;

        [Test]
        public void OnAnchor_StaleHoverEnum_Floats()
        {
            var state = NewState(Vector3.zero);
            var outcome = Evaluate(
                CardDisplayMode.HandCardMode,
                hasAnchor: true,
                current: Vector3.zero,
                isLiveHover: false,
                ref state,
                dt: 0.016f);

            Assert.AreEqual(HandCardLifeEngagementOutcome.Float, outcome);
        }

        [Test]
        public void OffAnchor_LiveHover_YieldsAndDoesNotSelfHeal()
        {
            var offAnchor = new Vector3(0f, 0.35f, 0f);
            var state = NewState(offAnchor);
            state.StillSeconds = 1f;

            var outcome = Evaluate(
                CardDisplayMode.HandCardMode,
                hasAnchor: true,
                current: offAnchor,
                isLiveHover: true,
                ref state,
                dt: 0.016f);

            Assert.AreEqual(HandCardLifeEngagementOutcome.Yield, outcome);
            Assert.AreEqual(0f, state.StillSeconds, 0.0001f);
        }

        [Test]
        public void OffAnchor_NotLiveHover_StillBeyondGrace_SnapsAndFloats()
        {
            var offAnchor = new Vector3(0f, 0.35f, 0f);
            var state = NewState(offAnchor);
            state.StillSeconds = HandCardLifeEngagementPolicy.StuckGraceSeconds;

            var outcome = Evaluate(
                CardDisplayMode.HandCardMode,
                hasAnchor: true,
                current: offAnchor,
                isLiveHover: false,
                ref state,
                dt: 0.016f);

            Assert.AreEqual(HandCardLifeEngagementOutcome.SnapAndFloat, outcome);
        }

        [Test]
        public void OffAnchor_FrameMotion_Yields()
        {
            var state = NewState(Vector3.zero);
            var current = new Vector3(0f, 0.06f, 0f);

            var outcome = Evaluate(
                CardDisplayMode.HandCardMode,
                hasAnchor: true,
                current: current,
                isLiveHover: false,
                ref state,
                dt: 0.016f);

            Assert.AreEqual(HandCardLifeEngagementOutcome.Yield, outcome);
        }

        [Test]
        public void OffAnchor_StillWithinGrace_Yields()
        {
            var offAnchor = new Vector3(0f, 0.06f, 0f);
            var state = NewState(offAnchor);
            state.StillSeconds = 0.1f;

            var outcome = Evaluate(
                CardDisplayMode.HandCardMode,
                hasAnchor: true,
                current: offAnchor,
                isLiveHover: false,
                ref state,
                dt: 0.016f);

            Assert.AreEqual(HandCardLifeEngagementOutcome.Yield, outcome);
        }

        [Test]
        public void NoAnchor_Floats()
        {
            var state = NewState(Vector3.one);

            var outcome = Evaluate(
                CardDisplayMode.HandCardMode,
                hasAnchor: false,
                current: Vector3.one,
                isLiveHover: false,
                ref state,
                dt: 0.016f);

            Assert.AreEqual(HandCardLifeEngagementOutcome.Float, outcome);
        }

        [Test]
        public void NonHandMode_Yields()
        {
            var state = NewState(Vector3.zero);

            var outcome = Evaluate(
                CardDisplayMode.DragCardMode,
                hasAnchor: true,
                current: Vector3.zero,
                isLiveHover: false,
                ref state,
                dt: 0.016f);

            Assert.AreEqual(HandCardLifeEngagementOutcome.Yield, outcome);
        }

        private static HandCardLifeEngagementState NewState(Vector3 pos) =>
            new() { LastWorldPos = pos };

        private static HandCardLifeEngagementOutcome Evaluate(
            CardDisplayMode mode,
            bool hasAnchor,
            Vector3 current,
            bool isLiveHover,
            ref HandCardLifeEngagementState state,
            float dt) =>
            HandCardLifeEngagementPolicy.Evaluate(
                mode,
                hasAnchor,
                current,
                LayoutAnchor,
                isLiveHover,
                ref state,
                dt);
    }
}
