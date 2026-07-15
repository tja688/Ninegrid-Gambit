using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    public sealed class HandoffProtocolTests
    {
        private sealed class FakeEndpoint : IHandoffEndpoint
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public bool EmitRealVelocity;

            public HandoffState Evict()
            {
                // C：恒 0；B：吐真速度。接口签名不变。
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
        }

        [Test]
        public void EvictAdmit_RoundTrip_PreservesPosition()
        {
            var source = new FakeEndpoint
            {
                Position = new Vector3(1.5f, -2f, 0.25f),
                Velocity = new Vector3(3f, 4f, 0f),
                EmitRealVelocity = false,
            };
            var sink = new FakeEndpoint();

            sink.Admit(source.Evict());

            Assert.AreEqual(new Vector3(1.5f, -2f, 0.25f), sink.Position);
            Assert.AreEqual(Vector3.zero, sink.Velocity);
        }

        [Test]
        public void Evict_PhaseC_VelocityAlwaysZero()
        {
            var source = new FakeEndpoint
            {
                Position = Vector3.one,
                Velocity = new Vector3(9f, -7f, 2f),
                EmitRealVelocity = false,
            };

            var handoff = source.Evict();

            Assert.AreEqual(Vector3.one, handoff.LocalPosition);
            Assert.AreEqual(Vector3.zero, handoff.LocalVelocity);
        }

        [Test]
        public void Evict_PhaseB_PassesThroughVelocity()
        {
            var velocity = new Vector3(2f, -1f, 0.5f);
            var source = new FakeEndpoint
            {
                Position = new Vector3(0.1f, 0.2f, 0.3f),
                Velocity = velocity,
                EmitRealVelocity = true,
            };
            var sink = new FakeEndpoint();

            sink.Admit(source.Evict());

            Assert.AreEqual(new Vector3(0.1f, 0.2f, 0.3f), sink.Position);
            Assert.AreEqual(velocity, sink.Velocity);
        }

        [Test]
        public void AtRest_Factory_ZeroVelocity()
        {
            var state = HandoffState.AtRest(new Vector3(4f, 5f, 6f));
            Assert.AreEqual(new Vector3(4f, 5f, 6f), state.LocalPosition);
            Assert.AreEqual(Vector3.zero, state.LocalVelocity);
        }
    }
}
