using NineGrid.Cards;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Cards
{
    /// <summary>
    /// #102 / ADR-0023：一格一认领；冲突保留先到；成对登记/注销。
    /// </summary>
    public sealed class SlotClaimRegistryTests
    {
        private SlotClaimRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new SlotClaimRegistry();
        }

        [Test]
        public void TryClaim_EmptySlot_SucceedsAndTryGetReturnsClaimant()
        {
            var owner = new object();
            var claimant = new SlotClaimant(owner, "tip", () => { });

            Assert.IsTrue(_registry.TryClaim(4, claimant));
            Assert.IsTrue(_registry.TryGet(4, out var got));
            Assert.AreSame(claimant, got);
            Assert.AreEqual("tip", got.BriefTipText);
        }

        [Test]
        public void TryClaim_SameOwnerRefresh_Succeeds()
        {
            var owner = new object();
            Assert.IsTrue(_registry.TryClaim(2, new SlotClaimant(owner, "a", null)));
            Assert.IsTrue(_registry.TryClaim(2, new SlotClaimant(owner, "b", null)));
            Assert.IsTrue(_registry.TryGet(2, out var got));
            Assert.AreEqual("b", got.BriefTipText);
        }

        [Test]
        public void TryClaim_Conflict_KeepsFirstAndFails()
        {
            var first = new object();
            var second = new object();
            Assert.IsTrue(_registry.TryClaim(7, new SlotClaimant(first, "first", null)));
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("认领冲突.*slot=7"));
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Assert,
                new System.Text.RegularExpressions.Regex("一格一认领冲突.*slot=7"));
            Assert.IsFalse(_registry.TryClaim(7, new SlotClaimant(second, "second", null)));
            Assert.IsTrue(_registry.TryGet(7, out var got));
            Assert.AreSame(first, got.Owner);
            Assert.AreEqual("first", got.BriefTipText);
        }

        [Test]
        public void TryClaim_SameOwnerMoveSlot_ReleasesOld()
        {
            var owner = new object();
            Assert.IsTrue(_registry.TryClaim(1, new SlotClaimant(owner, "old", null)));
            Assert.IsTrue(_registry.TryClaim(3, new SlotClaimant(owner, "new", null)));
            Assert.IsFalse(_registry.TryGet(1, out _));
            Assert.IsTrue(_registry.TryGet(3, out var got));
            Assert.AreEqual("new", got.BriefTipText);
        }

        [Test]
        public void Release_MatchingOwner_ClearsSlot()
        {
            var owner = new object();
            Assert.IsTrue(_registry.TryClaim(5, new SlotClaimant(owner, "x", null)));
            Assert.IsTrue(_registry.Release(5, owner));
            Assert.IsFalse(_registry.TryGet(5, out _));
        }

        [Test]
        public void Release_WrongOwner_DoesNotClear()
        {
            var owner = new object();
            Assert.IsTrue(_registry.TryClaim(5, new SlotClaimant(owner, "x", null)));
            Assert.IsFalse(_registry.Release(5, new object()));
            Assert.IsTrue(_registry.TryGet(5, out _));
        }

        [Test]
        public void ReleaseAllForOwner_ClearsOnlyThatOwner()
        {
            var a = new object();
            var b = new object();
            Assert.IsTrue(_registry.TryClaim(1, new SlotClaimant(a, "a", null)));
            Assert.IsTrue(_registry.TryClaim(2, new SlotClaimant(b, "b", null)));
            _registry.ReleaseAllForOwner(a);
            Assert.IsFalse(_registry.TryGet(1, out _));
            Assert.IsTrue(_registry.TryGet(2, out var got));
            Assert.AreSame(b, got.Owner);
        }

        [Test]
        public void Clear_RemovesAllClaims()
        {
            Assert.IsTrue(_registry.TryClaim(1, new SlotClaimant(new object(), "a", null)));
            Assert.IsTrue(_registry.TryClaim(9, new SlotClaimant(new object(), "b", null)));
            _registry.Clear();
            Assert.IsFalse(_registry.TryGet(1, out _));
            Assert.IsFalse(_registry.TryGet(9, out _));
        }
    }
}
