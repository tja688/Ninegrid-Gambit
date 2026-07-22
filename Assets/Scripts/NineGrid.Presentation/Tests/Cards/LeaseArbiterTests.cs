using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    public sealed class LeaseArbiterTests
    {
        private LeaseArbiter _arbiter;
        private List<string> _alarms;
        private LeaseKey _key;

        [SetUp]
        public void SetUp()
        {
            _alarms = new List<string>();
            _arbiter = new LeaseArbiter(reason => _alarms.Add(reason));
            _key = new LeaseKey(cardId: 7, layer: TowerLayer.SlotFrame);
        }

        [Test]
        public void AsyncVsAsync_LastWins_NoAlarm()
        {
            var first = _arbiter.TryAcquire(LeaseRequest.Async(_key, 0f, 1f));
            var second = _arbiter.TryAcquire(LeaseRequest.Async(_key, 0.1f, 1.1f));

            Assert.AreEqual(LeaseVerdict.Accepted, first.Verdict);
            Assert.AreEqual(LeaseVerdict.Accepted, second.Verdict);
            Assert.IsTrue(second.IsWriteAllowed);
            Assert.AreEqual(0, _alarms.Count);
        }

        [Test]
        public void SyncHolding_RejectsAsync()
        {
            var sync = _arbiter.TryAcquire(LeaseRequest.Sync(_key, 0f, 1f));
            Assert.AreEqual(LeaseVerdict.Accepted, sync.Verdict);
            Assert.Greater(sync.LeaseId, 0);

            var asyncWrite = _arbiter.TryAcquire(LeaseRequest.Async(_key, 0.2f, 0.8f));

            Assert.AreEqual(LeaseVerdict.Rejected, asyncWrite.Verdict);
            Assert.IsFalse(asyncWrite.IsWriteAllowed);
            Assert.AreEqual(0, _alarms.Count);
        }

        [Test]
        public void Async_CommandeeredBySync_ProducesVelocityHandoff()
        {
            _arbiter.TryAcquire(LeaseRequest.Async(_key, 0f, 2f));

            var velocity = new Vector3(3f, -1.5f, 0.25f);
            var position = new Vector3(1f, 2f, 0f);
            var result = _arbiter.TryAcquire(
                LeaseRequest.Sync(_key, 0.5f, 1.5f),
                sampleHandoff: () => new HandoffState(position, velocity));

            Assert.AreEqual(LeaseVerdict.Commandeered, result.Verdict);
            Assert.IsTrue(result.HasCommandeerHandoff);
            Assert.AreEqual(position, result.CommandeerHandoff.LocalPosition);
            Assert.AreEqual(velocity, result.CommandeerHandoff.LocalVelocity);
            Assert.IsTrue(_arbiter.HasActiveSyncLease(_key));
        }

        [Test]
        public void SyncVsSync_OverlappingWindow_ConflictAlarmAndReject()
        {
            var first = _arbiter.TryAcquire(LeaseRequest.Sync(_key, 0f, 1f));
            var second = _arbiter.TryAcquire(LeaseRequest.Sync(_key, 0.5f, 1.5f));

            Assert.AreEqual(LeaseVerdict.Accepted, first.Verdict);
            Assert.AreEqual(LeaseVerdict.SyncConflict, second.Verdict);
            Assert.IsFalse(second.IsWriteAllowed);
            Assert.IsTrue(second.RaisedDisciplineBAlarm);
            Assert.AreEqual(1, _alarms.Count);
            StringAssert.Contains("Sync vs Sync", _alarms[0]);
            Assert.IsTrue(_arbiter.TryGetActiveLeaseId(_key, out var leaseId));
            Assert.AreEqual(first.LeaseId, leaseId);
        }

        [Test]
        public void AsyncCommitted_PreemptedBeforeFulfillment_DisciplineBAlarm_StillAccepted()
        {
            _arbiter.TryAcquire(LeaseRequest.Async(_key, 0f, 1f, committed: true));

            var second = _arbiter.TryAcquire(LeaseRequest.Async(_key, 0.2f, 1.2f));

            Assert.AreEqual(LeaseVerdict.Accepted, second.Verdict);
            Assert.IsTrue(second.RaisedDisciplineBAlarm);
            Assert.AreEqual(1, _alarms.Count);
            StringAssert.Contains("Preempted committed", _alarms[0]);
        }

        [Test]
        public void AsyncCommitted_FulfilledThenPreempted_NoAlarm()
        {
            _arbiter.TryAcquire(LeaseRequest.Async(_key, 0f, 1f, committed: true));
            _arbiter.MarkFulfilled(_key);

            var second = _arbiter.TryAcquire(LeaseRequest.Async(_key, 0.2f, 1.2f));

            Assert.AreEqual(LeaseVerdict.Accepted, second.Verdict);
            Assert.IsFalse(second.RaisedDisciplineBAlarm);
            Assert.AreEqual(0, _alarms.Count);
        }

        [Test]
        public void Lease_IsPerLayer_NotWholeCard()
        {
            var l2 = new LeaseKey(3, TowerLayer.SlotFrame);
            var l3 = new LeaseKey(3, TowerLayer.EffectFrame);

            var syncL2 = _arbiter.TryAcquire(LeaseRequest.Sync(l2, 0f, 1f));
            var asyncL3 = _arbiter.TryAcquire(LeaseRequest.Async(l3, 0f, 1f));

            Assert.AreEqual(LeaseVerdict.Accepted, syncL2.Verdict);
            Assert.AreEqual(LeaseVerdict.Accepted, asyncL3.Verdict);
            Assert.IsTrue(_arbiter.HasActiveSyncLease(l2));
            Assert.IsFalse(_arbiter.HasActiveSyncLease(l3));
        }

        [Test]
        public void Release_AllowsSubsequentSync()
        {
            var first = _arbiter.TryAcquire(LeaseRequest.Sync(_key, 0f, 1f));
            Assert.IsTrue(_arbiter.Release(_key, first.LeaseId));

            var second = _arbiter.TryAcquire(LeaseRequest.Sync(_key, 1f, 2f));
            Assert.AreEqual(LeaseVerdict.Accepted, second.Verdict);
            Assert.AreEqual(0, _alarms.Count);
        }

        [Test]
        public void Commandeer_ThenAdmit_RoundTrip_PhaseB_PassesVelocity()
        {
            // 模拟异步通道当前态 → 同步征用 → Admit 接手
            var channel = new FakeChannel
            {
                Position = new Vector3(0.5f, -0.25f, 0f),
                Velocity = new Vector3(4f, 0f, 1f),
                EmitRealVelocity = true,
            };

            _arbiter.TryAcquire(LeaseRequest.Async(_key, 0f, 2f));
            var result = _arbiter.TryAcquire(
                LeaseRequest.Sync(_key, 0f, 1f),
                sampleHandoff: channel.SampleForCommandeer);

            Assert.AreEqual(LeaseVerdict.Commandeered, result.Verdict);

            var sink = new FakeChannel();
            sink.Admit(result.CommandeerHandoff);

            Assert.AreEqual(channel.Position, sink.Position);
            Assert.AreEqual(channel.Velocity, sink.Velocity);
        }

        [Test]
        public void Commandeer_SamplePhaseC_VelocityZero()
        {
            var channel = new FakeChannel
            {
                Position = Vector3.one,
                Velocity = new Vector3(9f, 9f, 9f),
                EmitRealVelocity = false,
            };

            _arbiter.TryAcquire(LeaseRequest.Async(_key, 0f, 2f));
            var result = _arbiter.TryAcquire(
                LeaseRequest.Sync(_key, 0f, 1f),
                sampleHandoff: channel.SampleForCommandeer);

            Assert.AreEqual(LeaseVerdict.Commandeered, result.Verdict);
            Assert.AreEqual(Vector3.one, result.CommandeerHandoff.LocalPosition);
            Assert.AreEqual(Vector3.zero, result.CommandeerHandoff.LocalVelocity);
        }

        private sealed class FakeChannel : IHandoffEndpoint
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public bool EmitRealVelocity;

            public HandoffState Evict()
            {
                var velocity = EmitRealVelocity ? Velocity : Vector3.zero;
                var state = new HandoffState(Position, velocity);
                Velocity = Vector3.zero;
                return state;
            }

            public void Admit(in HandoffState state)
            {
                Position = state.LocalPosition;
                Velocity = state.LocalVelocity;
            }

            /// <summary>征用采样：可走 B（吐速度）或 C（填 0），不经 Evict 清零副作用。</summary>
            public HandoffState SampleForCommandeer()
            {
                var velocity = EmitRealVelocity ? Velocity : Vector3.zero;
                return new HandoffState(Position, velocity);
            }
        }
    }
}
