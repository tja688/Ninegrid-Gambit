using NineGrid.Flow.Presentation;
using NUnit.Framework;
using NineGrid.Flow;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #10 占格退场门：强制对账降级为永不触发的断言；触发即失败并留诊断。
    /// </summary>
    public sealed class OccupancyForceSyncGuardTests
    {
        [SetUp]
        public void SetUp()
        {
            OccupancyForceSyncGuard.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            OccupancyForceSyncGuard.ResetForTests();
        }

        [Test]
        public void Reset_LeavesInvocationCountAtZero()
        {
            Assert.AreEqual(0, OccupancyForceSyncGuard.InvocationCount);
        }

        [Test]
        public void RecordForbiddenSync_AlwaysReturnsFalse_AndRecordsDiagnosis()
        {
            Assert.IsFalse(OccupancyForceSyncGuard.RecordForbiddenSync("drainOccupancyConflict", "force"));
            Assert.AreEqual(1, OccupancyForceSyncGuard.InvocationCount);
            Assert.AreEqual("drainOccupancyConflict", OccupancyForceSyncGuard.LastReason);
            Assert.AreEqual("force", OccupancyForceSyncGuard.LastDetail);
            Assert.AreEqual("OccupancyForceSyncAssert", OccupancyForceSyncGuard.AnomalyCode);
        }

        [Test]
        public void RecordForbiddenSync_IncrementsAcrossCalls_NeverAuthorizesHeal()
        {
            Assert.IsFalse(OccupancyForceSyncGuard.RecordForbiddenSync("softSync"));
            Assert.IsFalse(OccupancyForceSyncGuard.RecordForbiddenSync("forceSync", "flushDeferred"));
            Assert.AreEqual(2, OccupancyForceSyncGuard.InvocationCount);
            Assert.AreEqual("forceSync", OccupancyForceSyncGuard.LastReason);
        }
    }
}
