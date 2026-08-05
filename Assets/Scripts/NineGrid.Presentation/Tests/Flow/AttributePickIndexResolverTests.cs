using System.Collections.Generic;
using NineGrid.Flow.AttributeBoard;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Flow
{
    /// <summary>
    /// #137：视觉候选 → 当前 Pending 索引映射。Core 每次接受选择都移除该实例，
    /// 剩余候选保持相对顺序；索引 = 该候选之前仍未选中的候选个数。
    /// </summary>
    public sealed class AttributePickIndexResolverTests
    {
        [Test]
        public void NoSelection_PendingIndexEqualsSpawnIndex()
        {
            var flags = new List<bool> { false, false, false };
            Assert.AreEqual(0, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 0));
            Assert.AreEqual(1, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 1));
            Assert.AreEqual(2, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 2));
        }

        [Test]
        public void FirstCandidateSelected_RemainingShiftDown()
        {
            // Core 移除候选 0 后，候选 1/2 在 RewardOptions 中变为索引 0/1。
            var flags = new List<bool> { true, false, false };
            Assert.AreEqual(0, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 1));
            Assert.AreEqual(1, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 2));
        }

        [Test]
        public void MiddleCandidateSelected_OnlyFollowingShiftDown()
        {
            var flags = new List<bool> { false, true, false };
            Assert.AreEqual(0, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 0));
            Assert.AreEqual(1, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 2));
        }

        [Test]
        public void DuplicateDefIdCandidates_IndexFollowsInstanceOrder()
        {
            // 两张同种候选（如两个血量卡）：先选候选 0（Pending 0），候选 1 移到 Pending 0。
            var flags = new List<bool> { true, false, false };
            Assert.AreEqual(0, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 1));
        }

        [Test]
        public void TwoSelected_RemainingCandidateIndexZero()
        {
            var flags = new List<bool> { true, true, false };
            Assert.AreEqual(0, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 2));
        }

        [Test]
        public void OutOfRange_ReturnsMinusOne()
        {
            var flags = new List<bool> { false, false, false };
            Assert.AreEqual(-1, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, -1));
            Assert.AreEqual(-1, AttributePickIndexResolver.ResolveCurrentPendingIndex(flags, 3));
            Assert.AreEqual(-1, AttributePickIndexResolver.ResolveCurrentPendingIndex(null, 0));
        }
    }
}
