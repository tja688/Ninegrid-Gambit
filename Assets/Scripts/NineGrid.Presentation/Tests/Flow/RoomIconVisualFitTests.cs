using NineGrid.Flow.RoomIcons;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    public sealed class RoomIconVisualFitTests
    {
        [Test]
        public void ComputeUniformScale_FitsLargerAxis()
        {
            var scale = RoomIconVisualFit.ComputeUniformScale(
                new Vector2(10f, 5f),
                new Vector2(1.6f, 2.2f));
            Assert.AreEqual(0.16f, scale, 0.0001f);
        }

        [Test]
        public void ComputeUniformScale_ZeroSprite_ReturnsOne()
        {
            Assert.AreEqual(1f, RoomIconVisualFit.ComputeUniformScale(Vector2.zero, new Vector2(1f, 1f)));
        }

        [Test]
        public void ResolveTargetSize_AppliesFill()
        {
            var target = RoomIconVisualFit.ResolveTargetSize(new Vector2(1.6f, 2.2f));
            Assert.AreEqual(1.6f * RoomIconVisualFit.TargetFill, target.x, 0.0001f);
            Assert.AreEqual(2.2f * RoomIconVisualFit.TargetFill, target.y, 0.0001f);
        }
    }
}
