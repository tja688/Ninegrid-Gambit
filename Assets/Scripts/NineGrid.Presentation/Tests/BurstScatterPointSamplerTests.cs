using NineGrid.Cards;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 炸牌入组（2026-08-14 重构）纯逻辑护栏：
    /// 散点必须落在 GroundPanel 边界内、网格不叠点、尾行居中；场地矩形逐级回退可预期。
    /// </summary>
    public class BurstScatterPointSamplerTests
    {
        private const int Seed = 20260814;

        private static Rect TestRect => new Rect(-6f, -4.5f, 12f, 10f);

        [Test]
        public void SampleInRect_EmptyCount_ReturnsEmpty()
        {
            Random.InitState(Seed);
            var points = BurstScatterPointSampler.SampleInRect(TestRect, 0);
            Assert.That(points, Is.Empty);
        }

        [Test]
        public void SampleInRect_SingleCard_StaysInsideRect()
        {
            Random.InitState(Seed);
            for (var run = 0; run < 20; run++)
            {
                var points = BurstScatterPointSampler.SampleInRect(TestRect, 1);
                Assert.That(points, Has.Length.EqualTo(1));
                Assert.That(TestRect.Contains(points[0]), Is.True, $"run={run} point={points[0]}");
            }
        }

        [Test]
        public void SampleInRect_AllPoints_StayInsideRect()
        {
            Random.InitState(Seed);
            for (var count = 1; count <= 12; count++)
            {
                for (var run = 0; run < 5; run++)
                {
                    var points = BurstScatterPointSampler.SampleInRect(TestRect, count);
                    Assert.That(points, Has.Length.EqualTo(count));
                    for (var i = 0; i < points.Length; i++)
                    {
                        Assert.That(
                            TestRect.Contains(points[i]),
                            Is.True,
                            $"count={count} run={run} i={i} point={points[i]}");
                    }
                }
            }
        }

        [Test]
        public void SampleInRect_GridKeepsMinimumSeparation()
        {
            Random.InitState(Seed);
            var count = 9;
            var points = BurstScatterPointSampler.SampleInRect(TestRect, count);

            // 抖动网格保证相邻格不叠：最小间距 ≥ (1 - 2×0.32) × 格宽。
            var cols = Mathf.CeilToInt(Mathf.Sqrt(count * (TestRect.width / TestRect.height)));
            var cellW = TestRect.width / cols;
            var minExpected = cellW * (1f - 2f * 0.32f) * 0.98f;

            for (var i = 0; i < points.Length; i++)
            {
                for (var j = i + 1; j < points.Length; j++)
                {
                    var dist = Vector3.Distance(points[i], points[j]);
                    Assert.That(
                        dist,
                        Is.GreaterThanOrEqualTo(minExpected),
                        $"i={i} j={j} d={dist} min={minExpected}");
                }
            }
        }

        [Test]
        public void ResolveWorldRect_NoPanelNoField_FallsBackToDefaultInset()
        {
            // EditMode 无场景 GroundPanel、无 GroundFieldGeometryHook 绑定 → 兜底矩形。
            var rect = BurstScatterFieldBounds.ResolveWorldRect(1f);
            Assert.That(rect.x, Is.EqualTo(-5f).Within(0.0001f));
            Assert.That(rect.y, Is.EqualTo(-3.5f).Within(0.0001f));
            Assert.That(rect.width, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(rect.height, Is.EqualTo(8f).Within(0.0001f));
        }

        [Test]
        public void ResolveWorldRect_PaddingLargerThanHalfRect_KeepsOriginalRect()
        {
            var rect = BurstScatterFieldBounds.ResolveWorldRect(999f);
            Assert.That(rect.width, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(rect.height, Is.EqualTo(10f).Within(0.0001f));
        }
    }
}
