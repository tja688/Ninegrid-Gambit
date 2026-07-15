using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    public sealed class ConvergenceCurveTests
    {
        private const float Eps = ConvergenceCurve1D.PositionEpsilon;
        private const float VelEps = ConvergenceCurve1D.VelocityEpsilon;

        [Test]
        public void Create_AtDuration_ExactHitTarget()
        {
            var curve = ConvergenceCurve1D.Create(0f, 2f, 10f, 0.5f);

            Assert.AreEqual(0f, curve.EvaluatePosition(0f), Eps);
            Assert.AreEqual(2f, curve.EvaluateVelocity(0f), VelEps);
            Assert.AreEqual(10f, curve.EvaluatePosition(0.5f), Eps);
            Assert.AreEqual(0f, curve.EvaluateVelocity(0.5f), VelEps);
            Assert.AreEqual(0f, curve.EvaluateAcceleration(0.5f), VelEps);
        }

        [Test]
        public void Create_ZeroInitialVelocity_MatchesClassicMinimumJerk()
        {
            const float p0 = 1f;
            const float p1 = 5f;
            const float t = 1f;
            var curve = ConvergenceCurve1D.Create(p0, 0f, p1, t);
            var delta = p1 - p0;

            for (var i = 0; i <= 20; i++)
            {
                var s = i / 20f;
                var expected = p0 + delta * (10f * s * s * s - 15f * s * s * s * s + 6f * s * s * s * s * s);
                Assert.AreEqual(expected, curve.EvaluatePosition(s * t), Eps);
            }

            Assert.AreEqual(0f, curve.EvaluateVelocity(t), VelEps);
            Assert.AreEqual(0f, curve.EvaluateAcceleration(t), VelEps);
        }

        [Test]
        public void Redirect_MaintainsC1Continuity()
        {
            var curve = ConvergenceCurve1D.Create(0f, 0f, 10f, 1f);
            const float redirectAt = 0.4f;

            var positionAtRedirect = curve.EvaluatePosition(redirectAt);
            var velocityAtRedirect = curve.EvaluateVelocity(redirectAt);
            var redirected = curve.Redirect(redirectAt, 20f, 0.6f);

            Assert.AreEqual(positionAtRedirect, redirected.EvaluatePosition(0f), Eps);
            Assert.AreEqual(velocityAtRedirect, redirected.EvaluateVelocity(0f), VelEps);
            Assert.AreEqual(20f, redirected.EvaluatePosition(0.6f), Eps);
            Assert.AreEqual(0f, redirected.EvaluateVelocity(0.6f), VelEps);
            Assert.AreEqual(0f, redirected.EvaluateAcceleration(0.6f), VelEps);
        }

        [Test]
        public void CornerSprint_TinyDurationLargeDistance_NoNaNAndExactHit()
        {
            var curve = ConvergenceCurve1D.Create(0f, 0f, 100f, 0.02f);

            for (var i = 0; i <= 20; i++)
            {
                var t = 0.02f * i / 20f;
                var p = curve.EvaluatePosition(t);
                var v = curve.EvaluateVelocity(t);
                var a = curve.EvaluateAcceleration(t);

                Assert.IsFalse(float.IsNaN(p), $"position NaN at t={t}");
                Assert.IsFalse(float.IsNaN(v), $"velocity NaN at t={t}");
                Assert.IsFalse(float.IsNaN(a), $"acceleration NaN at t={t}");
                Assert.IsFalse(float.IsInfinity(p), $"position Inf at t={t}");
                Assert.IsFalse(float.IsInfinity(v), $"velocity Inf at t={t}");
                Assert.IsFalse(float.IsInfinity(a), $"acceleration Inf at t={t}");
            }

            Assert.AreEqual(100f, curve.EvaluatePosition(0.02f), Eps);
            Assert.AreEqual(0f, curve.EvaluateVelocity(0.02f), VelEps);
            Assert.AreEqual(0f, curve.EvaluateAcceleration(0.02f), VelEps);
        }

        [Test]
        public void CornerSprint_OpposingVelocity_StillExactHit()
        {
            // 初速背离目标：冲刺策略允许中段尖峰，但终点仍解析到位。
            var curve = ConvergenceCurve1D.Create(0f, -30f, 8f, 0.12f);

            Assert.AreEqual(0f, curve.EvaluatePosition(0f), Eps);
            Assert.AreEqual(-30f, curve.EvaluateVelocity(0f), VelEps);
            Assert.AreEqual(8f, curve.EvaluatePosition(0.12f), Eps);
            Assert.AreEqual(0f, curve.EvaluateVelocity(0.12f), VelEps);
            Assert.AreEqual(0f, curve.EvaluateAcceleration(0.12f), VelEps);
        }

        [Test]
        public void Vector3_Create_AllDimensionsBehaveLikeScalar()
        {
            const float sourceTime = 0.8f;
            var p0 = new Vector3(1f, -2f, 3f);
            var v0 = new Vector3(0.5f, -1f, 0.25f);
            var p1 = new Vector3(4f, 5f, -1f);

            var vectorCurve = ConvergenceCurve.Create(p0, v0, p1, sourceTime);
            var scalarX = ConvergenceCurve1D.Create(p0.x, v0.x, p1.x, sourceTime);
            var scalarY = ConvergenceCurve1D.Create(p0.y, v0.y, p1.y, sourceTime);
            var scalarZ = ConvergenceCurve1D.Create(p0.z, v0.z, p1.z, sourceTime);

            for (var i = 0; i <= 10; i++)
            {
                var t = sourceTime * i / 10f;
                var pos = vectorCurve.EvaluatePosition(t);
                var vel = vectorCurve.EvaluateVelocity(t);

                Assert.AreEqual(scalarX.EvaluatePosition(t), pos.x, Eps);
                Assert.AreEqual(scalarY.EvaluatePosition(t), pos.y, Eps);
                Assert.AreEqual(scalarZ.EvaluatePosition(t), pos.z, Eps);
                Assert.AreEqual(scalarX.EvaluateVelocity(t), vel.x, VelEps);
                Assert.AreEqual(scalarY.EvaluateVelocity(t), vel.y, VelEps);
                Assert.AreEqual(scalarZ.EvaluateVelocity(t), vel.z, VelEps);
            }

            AssertVector3Equal(p1, vectorCurve.EvaluatePosition(sourceTime), Eps);
            AssertVector3Equal(Vector3.zero, vectorCurve.EvaluateVelocity(sourceTime), VelEps);
            AssertVector3Equal(Vector3.zero, vectorCurve.EvaluateAcceleration(sourceTime), VelEps);
        }

        [Test]
        public void CreateScalar_MatchesSingleDimensionCurve()
        {
            var scalar = ConvergenceCurve.CreateScalar(2f, 1f, 8f, 0.5f);
            var expected = ConvergenceCurve1D.Create(2f, 1f, 8f, 0.5f);

            Assert.AreEqual(expected.EvaluatePosition(0.25f), scalar.EvaluatePosition(0.25f).x, Eps);
            Assert.AreEqual(expected.EvaluatePosition(0.5f), scalar.EvaluatePosition(0.5f).x, Eps);
            Assert.AreEqual(0f, scalar.EvaluatePosition(0.5f).y, Eps);
            Assert.AreEqual(0f, scalar.EvaluatePosition(0.5f).z, Eps);
        }

        [Test]
        public void Vector3_Redirect_MaintainsC1Continuity()
        {
            var curve = ConvergenceCurve.Create(Vector3.zero, Vector3.zero, new Vector3(10f, 0f, 0f), 1f);
            const float redirectAt = 0.35f;

            var positionAtRedirect = curve.EvaluatePosition(redirectAt);
            var velocityAtRedirect = curve.EvaluateVelocity(redirectAt);
            var redirected = curve.Redirect(redirectAt, new Vector3(5f, 8f, 2f), 0.7f);

            AssertVector3Equal(positionAtRedirect, redirected.EvaluatePosition(0f), Eps);
            AssertVector3Equal(velocityAtRedirect, redirected.EvaluateVelocity(0f), VelEps);
            AssertVector3Equal(new Vector3(5f, 8f, 2f), redirected.EvaluatePosition(0.7f), Eps);
            AssertVector3Equal(Vector3.zero, redirected.EvaluateVelocity(0.7f), VelEps);
        }

        [Test]
        public void SprintCornerPolicy_DoesNotAlterParameters()
        {
            const float p0 = 1f;
            const float v0 = 2f;
            const float p1 = 9f;
            const float duration = 0.6f;

            var defaultCurve = ConvergenceCurve1D.Create(p0, v0, p1, duration);
            var sprintCurve = ConvergenceCurve1D.Create(p0, v0, p1, duration, SprintCornerReshapePolicy.Instance);

            for (var i = 0; i <= 10; i++)
            {
                var t = duration * i / 10f;
                Assert.AreEqual(defaultCurve.EvaluatePosition(t), sprintCurve.EvaluatePosition(t), Eps);
                Assert.AreEqual(defaultCurve.EvaluateVelocity(t), sprintCurve.EvaluateVelocity(t), VelEps);
            }
        }

        [Test]
        public void IsComplete_UsesExactDurationNotPositionEpsilon()
        {
            var curve = ConvergenceCurve1D.Create(0f, 0f, 1f, 0.5f);

            Assert.IsFalse(curve.IsComplete(0.5f - 1e-6f));
            Assert.IsTrue(curve.IsComplete(0.5f));
            Assert.IsTrue(curve.IsComplete(0.6f));
        }

        static void AssertVector3Equal(Vector3 expected, Vector3 actual, float epsilon)
        {
            Assert.AreEqual(expected.x, actual.x, epsilon);
            Assert.AreEqual(expected.y, actual.y, epsilon);
            Assert.AreEqual(expected.z, actual.z, epsilon);
        }
    }
}
