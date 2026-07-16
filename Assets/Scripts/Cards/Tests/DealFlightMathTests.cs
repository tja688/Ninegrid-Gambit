using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    public sealed class DealFlightMathTests
    {
        private static readonly DealFlightLayoutSettings DefaultSettings = new();

        [Test]
        public void EvaluateQuadraticBezier_EndpointsMatchControlPoints()
        {
            var p0 = new Vector3(0f, 0f, 0f);
            var p1 = new Vector3(1f, 2f, 0f);
            var p2 = new Vector3(2f, 0f, 0f);

            Assert.AreEqual(p0, DealFlightMath.EvaluateQuadraticBezier(p0, p1, p2, 0f));
            Assert.AreEqual(p2, DealFlightMath.EvaluateQuadraticBezier(p0, p1, p2, 1f));
        }

        [Test]
        public void EvaluateQuadraticBezier_MidpointUsesControlPoint()
        {
            var p0 = Vector3.zero;
            var p1 = new Vector3(1f, 2f, 0f);
            var p2 = new Vector3(2f, 0f, 0f);
            var mid = DealFlightMath.EvaluateQuadraticBezier(p0, p1, p2, 0.5f);

            Assert.AreEqual(1f, mid.x, 0.0001f);
            Assert.AreEqual(1f, mid.y, 0.0001f);
        }

        [Test]
        public void ComputeSpeedFactor_RemapsFarToNear()
        {
            var far = DealFlightMath.ComputeSpeedFactor(
                3f,
                DefaultSettings.speedNearDistance,
                DefaultSettings.speedFarDistance,
                DefaultSettings.easeNear,
                DefaultSettings.easeFar);
            var near = DealFlightMath.ComputeSpeedFactor(
                0.05f,
                DefaultSettings.speedNearDistance,
                DefaultSettings.speedFarDistance,
                DefaultSettings.easeNear,
                DefaultSettings.easeFar);

            Assert.Greater(far, near);
            Assert.AreEqual(DefaultSettings.easeFar, far, 0.0001f);
            Assert.AreEqual(DefaultSettings.easeNear, near, 0.0001f);
        }

        [Test]
        public void ComputeBudget_IncreasesWithDistanceAndPendingRotate()
        {
            var start = Vector3.zero;
            var near = new Vector3(1f, 0f, 0f);
            var far = new Vector3(6f, 0f, 0f);

            var nearBudget = DealSettleBudget.Compute(
                start,
                near,
                new DealFlightContext(false, 1, 0),
                DefaultSettings);
            var farBudget = DealSettleBudget.Compute(
                start,
                far,
                new DealFlightContext(false, 1, 0),
                DefaultSettings);
            var rotateBudget = DealSettleBudget.Compute(
                start,
                near,
                new DealFlightContext(false, 1, 2),
                DefaultSettings);

            Assert.Greater(farBudget.Total, nearBudget.Total);
            Assert.Greater(rotateBudget.Total, nearBudget.Total);
        }

        [Test]
        public void ComputeHopMidpoint_AddsArcHeight()
        {
            var start = Vector3.zero;
            var end = new Vector3(2f, 0f, 0f);
            var mid = DealFlightMath.ComputeHopMidpoint(start, end, 0.5f);

            Assert.AreEqual(1f, mid.x, 0.0001f);
            Assert.AreEqual(0.5f, mid.y, 0.0001f);
        }

        [Test]
        public void ComputeBudget_RespectsClamp()
        {
            var budget = DealSettleBudget.Compute(
                Vector3.zero,
                new Vector3(100f, 0f, 0f),
                new DealFlightContext(true, 5, 10),
                DefaultSettings);

            Assert.LessOrEqual(budget.Total, DefaultSettings.maxDuration);
            Assert.GreaterOrEqual(budget.Total, DefaultSettings.minDuration);
        }

        [Test]
        public void BudgetConsume_DecreasesRemaining()
        {
            var budget = new DealSettleBudget(0.5f);
            budget.Consume(0.2f);
            Assert.AreEqual(0.3f, budget.Remaining, 0.0001f);
            Assert.IsFalse(budget.IsExhausted);

            budget.Consume(0.5f);
            Assert.IsTrue(budget.IsExhausted);
        }

        [Test]
        public void ResolveVisualDealSlot_NoRotate_KeepsBirth()
        {
            var steps = new[]
            {
                new BoardPresentationStep { Kind = BoardPresentationStepKind.Deal },
                new BoardPresentationStep { Kind = BoardPresentationStepKind.Remove },
            };

            var visual = DealVisualTargetResolver.ResolveVisualDealSlot(
                birthSlot: 1,
                steps,
                rotateScanStart: 1,
                out var pending);

            Assert.AreEqual(1, visual);
            Assert.AreEqual(0, pending);
        }

        [Test]
        public void ResolveVisualDealSlot_OneClockwise_AdvancesOneRingStep()
        {
            var steps = new[]
            {
                new BoardPresentationStep { Kind = BoardPresentationStepKind.Deal },
                new BoardPresentationStep
                {
                    Kind = BoardPresentationStepKind.Rotate,
                    Clockwise = true,
                },
            };

            var visual = DealVisualTargetResolver.ResolveVisualDealSlot(
                birthSlot: 1,
                steps,
                rotateScanStart: 1,
                out var pending);

            Assert.AreEqual(2, visual);
            Assert.AreEqual(1, pending);
        }

        [Test]
        public void ResolveVisualDealSlot_ClockwiseThenCounter_CancelsToBirth()
        {
            var steps = new[]
            {
                new BoardPresentationStep { Kind = BoardPresentationStepKind.Deal },
                new BoardPresentationStep
                {
                    Kind = BoardPresentationStepKind.Rotate,
                    Clockwise = true,
                },
                new BoardPresentationStep
                {
                    Kind = BoardPresentationStepKind.Rotate,
                    Clockwise = false,
                },
            };

            var visual = DealVisualTargetResolver.ResolveVisualDealSlot(
                birthSlot: 6,
                steps,
                rotateScanStart: 1,
                out var pending);

            Assert.AreEqual(6, visual);
            Assert.AreEqual(2, pending);
        }

        [Test]
        public void CollectPendingRotateDirections_StopsAtNonRotate()
        {
            var steps = new[]
            {
                new BoardPresentationStep
                {
                    Kind = BoardPresentationStepKind.Rotate,
                    Clockwise = true,
                },
                new BoardPresentationStep { Kind = BoardPresentationStepKind.Remove },
                new BoardPresentationStep
                {
                    Kind = BoardPresentationStepKind.Rotate,
                    Clockwise = false,
                },
            };
            var dirs = new System.Collections.Generic.List<bool>();
            DealVisualTargetResolver.CollectPendingRotateDirections(steps, 0, dirs);

            Assert.AreEqual(1, dirs.Count);
            Assert.IsTrue(dirs[0]);
        }

        [Test]
        public void ApplyRingSteps_CounterClockwiseFrom1_GoesTo4()
        {
            var visual = DealVisualTargetResolver.ApplyRingSteps(
                1,
                new[] { false });

            Assert.AreEqual(4, visual);
        }
    }
}
