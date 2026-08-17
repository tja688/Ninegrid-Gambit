using NineGrid.Cards;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace NineGrid.Presentation.Tests.Flow
{
    [TestFixture]
    public sealed class SlotClaimRegistryTests
    {
        private SlotClaimRegistry mRegistry;

        [SetUp]
        public void SetUp()
        {
            mRegistry = new SlotClaimRegistry();
        }

        [Test]
        public void TryClaim_Conflict_RejectsSecondOwner_KeepsFirst()
        {
            var ownerA = new object();
            var ownerB = new object();
            var claimantA = new SlotClaimant(ownerA, "A", () => { });
            var claimantB = new SlotClaimant(ownerB, "B", () => { });

            Assert.IsTrue(mRegistry.TryClaim(1, claimantA));
            LogAssert.Expect(UnityEngine.LogType.Error, "[SlotClaimRegistry] 认领冲突：slot=1 已有 owner=Object，拒绝 owner=Object。保留先到者，禁止静默覆盖。");
            LogAssert.Expect(UnityEngine.LogType.Assert, "[SlotClaimRegistry] 一格一认领冲突 slot=1");
            Assert.IsFalse(mRegistry.TryClaim(1, claimantB));

            Assert.IsTrue(mRegistry.TryGet(1, out var current));
            Assert.AreSame(ownerA, current.Owner);
            Assert.AreEqual("A", current.BriefTipText);
        }

        [Test]
        public void TryClaim_FailedClaimThenReleaseFirst_SlotStaysEmptyUntilRetry()
        {
            var ownerA = new object();
            var ownerB = new object();
            var claimantA = new SlotClaimant(ownerA, "A", () => { });
            var claimantB = new SlotClaimant(ownerB, "B", () => { });

            Assert.IsTrue(mRegistry.TryClaim(3, claimantA));
            LogAssert.Expect(UnityEngine.LogType.Error, "[SlotClaimRegistry] 认领冲突：slot=3 已有 owner=Object，拒绝 owner=Object。保留先到者，禁止静默覆盖。");
            LogAssert.Expect(UnityEngine.LogType.Assert, "[SlotClaimRegistry] 一格一认领冲突 slot=3");
            Assert.IsFalse(mRegistry.TryClaim(3, claimantB));

            Assert.IsTrue(mRegistry.Release(3, ownerA));
            Assert.IsFalse(mRegistry.TryGet(3, out _));

            Assert.IsTrue(mRegistry.TryClaim(3, claimantB));
            Assert.IsTrue(mRegistry.TryGet(3, out var current));
            Assert.AreSame(ownerB, current.Owner);
        }

        [Test]
        public void TryClaim_ReleaseFirstThenClaimSecond_Succeeds()
        {
            var ownerA = new object();
            var ownerB = new object();
            var claimantA = new SlotClaimant(ownerA, "A", () => { });
            var claimantB = new SlotClaimant(ownerB, "B", () => { });

            Assert.IsTrue(mRegistry.TryClaim(7, claimantA));
            Assert.IsTrue(mRegistry.Release(7, ownerA));
            Assert.IsTrue(mRegistry.TryClaim(7, claimantB));

            Assert.IsTrue(mRegistry.TryGet(7, out var current));
            Assert.AreSame(ownerB, current.Owner);
        }

        [Test]
        public void TryClaim_SameOwnerMigratesSlot_ReleasesOldSlot()
        {
            var owner = new object();
            var claimant1 = new SlotClaimant(owner, "tip", () => { });
            var claimant2 = new SlotClaimant(owner, "tip", () => { });

            Assert.IsTrue(mRegistry.TryClaim(1, claimant1));
            Assert.IsTrue(mRegistry.TryClaim(9, claimant2));

            Assert.IsFalse(mRegistry.TryGet(1, out _));
            Assert.IsTrue(mRegistry.TryGet(9, out var current));
            Assert.AreSame(owner, current.Owner);
        }

        [Test]
        public void ReleaseAllForOwner_ClearsEverySlotHeldByOwner()
        {
            var owner = new object();
            var other = new object();
            Assert.IsTrue(mRegistry.TryClaim(1, new SlotClaimant(owner, string.Empty, () => { })));
            Assert.IsTrue(mRegistry.TryClaim(2, new SlotClaimant(other, string.Empty, () => { })));
            Assert.IsTrue(mRegistry.TryClaim(3, new SlotClaimant(owner, string.Empty, () => { })));

            mRegistry.ReleaseAllForOwner(owner);

            Assert.IsFalse(mRegistry.TryGet(1, out _));
            Assert.IsTrue(mRegistry.TryGet(2, out _));
            Assert.IsFalse(mRegistry.TryGet(3, out _));
        }
    }
}
