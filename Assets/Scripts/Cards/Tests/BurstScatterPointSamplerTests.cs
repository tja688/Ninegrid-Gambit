using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    public sealed class BurstScatterPointSamplerTests
    {
        private const float Epsilon = 0.0001f;

        [Test]
        public void SampleOnCircle_CountZero_ReturnsEmpty()
        {
            var points = BurstScatterPointSampler.SampleOnCircle(Vector3.zero, 1f, 0, 0f);
            Assert.AreEqual(0, points.Length);
        }

        [Test]
        public void SampleOnCircle_TwoPoints_LieOnRadiusAndOpposite()
        {
            var center = new Vector3(1f, 2f, 0f);
            const float radius = 1.5f;
            var points = BurstScatterPointSampler.SampleOnCircle(center, radius, 2, 0f);

            Assert.AreEqual(2, points.Length);
            Assert.AreEqual(radius, Vector3.Distance(center, points[0]), Epsilon);
            Assert.AreEqual(radius, Vector3.Distance(center, points[1]), Epsilon);
            Assert.AreEqual(radius * 2f, Vector3.Distance(points[0], points[1]), Epsilon);
        }

        [Test]
        public void SampleOnCircle_ThreeAndFour_EqualAngularSpacing()
        {
            AssertEqualSpacing(3);
            AssertEqualSpacing(4);
        }

        [Test]
        public void SampleOnCircle_DifferentPhase_YieldsDifferentPoints()
        {
            var center = Vector3.zero;
            var a = BurstScatterPointSampler.SampleOnCircle(center, 1f, 2, 0f);
            var b = BurstScatterPointSampler.SampleOnCircle(center, 1f, 2, Mathf.PI * 0.5f);

            Assert.AreEqual(2, a.Length);
            Assert.AreEqual(2, b.Length);
            Assert.Greater(Vector3.Distance(a[0], b[0]), 0.1f);
        }

        private static void AssertEqualSpacing(int count)
        {
            var center = new Vector3(0.5f, -0.25f, 0f);
            const float radius = 2f;
            var points = BurstScatterPointSampler.SampleOnCircle(center, radius, count, 0.3f);
            Assert.AreEqual(count, points.Length);

            var expectedChord = 2f * radius * Mathf.Sin(Mathf.PI / count);
            for (var i = 0; i < count; i++)
            {
                Assert.AreEqual(radius, Vector3.Distance(center, points[i]), Epsilon);
                var next = points[(i + 1) % count];
                Assert.AreEqual(expectedChord, Vector3.Distance(points[i], next), 0.001f);
            }
        }
    }
}
