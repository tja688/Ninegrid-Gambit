using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NineGrid.Flow;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// BounceFan 固定 AABB 命中：不跟悬停位移；不依赖卡面 Collider2D。
    /// </summary>
    public sealed class BounceFanChoiceHitTests
    {
        private static readonly Vector2 DefaultHitBox = new(1.9f, 2.5f);

        [Test]
        public void ContainsHitBox_InsideCenter_IsTrue()
        {
            Assert.IsTrue(BounceFanChoicePresenter.ContainsHitBox(
                Vector2.zero,
                Vector2.zero,
                DefaultHitBox));
        }

        [Test]
        public void ContainsHitBox_OutsideEdge_IsFalse()
        {
            Assert.IsFalse(BounceFanChoicePresenter.ContainsHitBox(
                new Vector2(0.96f, 0f),
                Vector2.zero,
                DefaultHitBox));
            Assert.IsFalse(BounceFanChoicePresenter.ContainsHitBox(
                new Vector2(0f, 1.26f),
                Vector2.zero,
                DefaultHitBox));
        }

        [Test]
        public void ContainsHitBox_OnEdge_IsTrue()
        {
            Assert.IsTrue(BounceFanChoicePresenter.ContainsHitBox(
                new Vector2(0.95f, 0f),
                Vector2.zero,
                DefaultHitBox));
            Assert.IsTrue(BounceFanChoicePresenter.ContainsHitBox(
                new Vector2(0f, 1.25f),
                Vector2.zero,
                DefaultHitBox));
        }

        [Test]
        public void ResolveHoveredIndex_UsesBaseCenters_NotVisualDisplacement()
        {
            // 三选项静止中心；指针落在右卡静止框内 → 命中右卡。
            var centers = new List<Vector2>
            {
                new(-1.1f, 0f),
                Vector2.zero,
                new(1.1f, 0f),
            };
            var pointerAtRestRight = new Vector2(1.1f, 0f);
            Assert.AreEqual(
                2,
                BounceFanChoicePresenter.ResolveHoveredIndex(pointerAtRestRight, centers, DefaultHitBox));

            // 悬停推挤把右卡视觉推到 x≈2.7；固定框仍钉在 1.1。
            // 指针跟到推开后的视觉位置 → 不应再命中右卡静止框。
            var pointerFollowingHoverPush = new Vector2(1.1f + 1.6f, 0f);
            Assert.AreEqual(
                -1,
                BounceFanChoicePresenter.ResolveHoveredIndex(pointerFollowingHoverPush, centers, DefaultHitBox),
                "固定判定框不跟随悬停推挤，指针只在推开后的视觉位置时不应命中");
        }

        [Test]
        public void ResolveHoveredIndex_OverlappingPrefersLaterIndex()
        {
            var centers = new List<Vector2>
            {
                Vector2.zero,
                new(0.2f, 0f),
            };
            Assert.AreEqual(
                1,
                BounceFanChoicePresenter.ResolveHoveredIndex(new Vector2(0.1f, 0f), centers, DefaultHitBox));
        }

        [Test]
        public void BounceFanSource_DoesNotUseColliderOverlapPoint()
        {
            var path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "BounceFanChoicePresenter.cs"));
            var text = File.ReadAllText(path);
            Assert.IsFalse(
                Regex.IsMatch(text, @"OverlapPoint"),
                "BounceFan 不得再靠 Collider2D.OverlapPoint（ADR-0023 已关卡面 collider）");
            Assert.IsFalse(
                Regex.IsMatch(text, @"Collider\s*="),
                "BounceEntry 不应再缓存 Collider 做选择");
        }
    }
}
