using System.Collections.Generic;
using NineGrid.Content.Vfx;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class VfxPersistentStateBehaviorTests
    {
        private const string StateCatalogJson =
            "{\"schemaVersion\":1,\"ticket\":\"#201\",\"cueBindings\":[],\"stateBindings\":["
            + "{\"stateId\":\"vfx.state.a\",\"enabled\":true,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet
            + "\",\"materialKey\":\"fx/a\",\"spatialOwnership\":\"independent\",\"exitMode\":\"immediate\"},"
            + "{\"stateId\":\"vfx.state.b\",\"enabled\":true,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet
            + "\",\"materialKey\":\"fx/b\",\"spatialOwnership\":\"independent\",\"exitMode\":\"segment\",\"exitLoopLimit\":2},"
            + "{\"stateId\":\"vfx.state.attached\",\"enabled\":true,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet
            + "\",\"materialKey\":\"fx/attached\",\"spatialOwnership\":\"attached\",\"exitMode\":\"immediate\"},"
            + "{\"stateId\":\"vfx.state.disabled\",\"enabled\":false,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet
            + "\",\"materialKey\":\"fx/disabled\"}"
            + "]}";

        [Test]
        public void SetSlot_SameState_ReturnsNoOp()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var owner = new FakeSlotOwner();

            var first = system.SetSlot(owner, "aura", "vfx.state.a");
            var second = system.SetSlot(owner, "aura", "vfx.state.a");

            Assert.AreEqual(VfxStateSlotOutcome.Applied, first.Outcome);
            Assert.AreEqual(VfxStateSlotOutcome.NoOp, second.Outcome);
            Assert.AreEqual(1, factory.StatePlayers.Count);
            Assert.AreEqual(1, factory.StatePlayers[0].Started.Count);
        }

        [Test]
        public void SetSlot_NewState_ReplacesOldProjection()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var owner = new FakeSlotOwner();

            system.SetSlot(owner, "aura", "vfx.state.a");
            system.SetSlot(owner, "aura", "vfx.state.b");

            Assert.AreEqual(2, factory.StatePlayers.Count);
            Assert.IsTrue(factory.StatePlayers[0].ExitRequested);
            Assert.IsTrue(factory.StatePlayers[0].ExitImmediate);
            Assert.AreEqual("vfx.state.b", factory.StatePlayers[1].Started[0].StateRequest.StateId);
        }

        [Test]
        public void SetSlot_NewState_RespectsSegmentExitOnReplace()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var owner = new FakeSlotOwner();

            system.SetSlot(owner, "aura", "vfx.state.b");
            system.SetSlot(owner, "aura", "vfx.state.a");

            Assert.IsTrue(factory.StatePlayers[0].ExitRequested);
            Assert.IsFalse(factory.StatePlayers[0].ExitImmediate);
            Assert.AreEqual(2, factory.StatePlayers[0].ExitLoopLimit);
        }

        [Test]
        public void Tick_AttachedHostLost_ReleasesActiveSlot()
        {
            var host = new FakeDomainHost { IsAvailable = true };
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var owner = new FakeSlotOwner();
            var spatial = new VfxSpatialContext("slot", host, Vector3.zero);

            system.SetSlot(owner, "aura", "vfx.state.attached", spatial);
            factory.StatePlayers[0].CompleteOnNextTick = true;
            system.Tick(0.1f);

            var replay = system.SetSlot(owner, "aura", "vfx.state.attached", spatial);

            Assert.AreEqual(VfxStateSlotOutcome.Applied, replay.Outcome);
            Assert.AreEqual(2, factory.StatePlayers.Count);
        }

        [Test]
        public void ClearSlotIf_OnlyClearsMatchingState()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var owner = new FakeSlotOwner();

            system.SetSlot(owner, "aura", "vfx.state.a");
            var mismatch = system.ClearSlotIf(owner, "aura", "vfx.state.b");
            var match = system.ClearSlotIf(owner, "aura", "vfx.state.a");

            Assert.AreEqual(VfxStateSlotOutcome.NoOp, mismatch.Outcome);
            Assert.AreEqual(VfxStateSlotOutcome.Cleared, match.Outcome);
            Assert.IsTrue(factory.StatePlayers[0].ExitRequested);
        }

        [Test]
        public void SetSlot_NullState_ClearsSlot()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var owner = new FakeSlotOwner();

            system.SetSlot(owner, "aura", "vfx.state.a");
            var cleared = system.SetSlot(owner, "aura", null);

            Assert.AreEqual(VfxStateSlotOutcome.Cleared, cleared.Outcome);
            Assert.IsTrue(factory.StatePlayers[0].ExitRequested);
        }

        [Test]
        public void SetSlot_SegmentExit_EnqueuesExitingPlayer()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var owner = new FakeSlotOwner();

            system.SetSlot(owner, "aura", "vfx.state.b");
            system.SetSlot(owner, "aura", null);

            Assert.IsTrue(factory.StatePlayers[0].ExitRequested);
            Assert.IsFalse(factory.StatePlayers[0].ExitImmediate);
            Assert.AreEqual(2, factory.StatePlayers[0].ExitLoopLimit);
        }

        [Test]
        public void ReleaseOwner_ClearsAllOwnerSlots()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var ownerA = new FakeSlotOwner();
            var ownerB = new FakeSlotOwner();

            system.SetSlot(ownerA, "aura", "vfx.state.a");
            system.SetSlot(ownerA, "shield", "vfx.state.b");
            system.SetSlot(ownerB, "aura", "vfx.state.a");

            system.ReleaseOwner(ownerA);

            Assert.IsTrue(factory.StatePlayers[0].ExitRequested);
            Assert.IsTrue(factory.StatePlayers[1].ExitRequested);
            Assert.IsFalse(factory.StatePlayers[2].ExitRequested);
        }

        [Test]
        public void MultipleOwnersAndSlots_CanCoexist()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var ownerA = new FakeSlotOwner();
            var ownerB = new FakeSlotOwner();

            system.SetSlot(ownerA, "aura", "vfx.state.a");
            system.SetSlot(ownerB, "aura", "vfx.state.a");
            system.SetSlot(ownerA, "shield", "vfx.state.b");

            Assert.AreEqual(3, factory.StatePlayers.Count);
            Assert.AreEqual(VfxStateSlotOutcome.Applied, system.SetSlot(ownerB, "shield", "vfx.state.a").Outcome);
        }

        [Test]
        public void SetSlot_ReturnsUnboundWhenBindingMissing()
        {
            var system = CreateSystem(new FakeVfxPlayerFactory());
            var owner = new FakeSlotOwner();

            var result = system.SetSlot(owner, "aura", "missing.state");

            Assert.AreEqual(VfxStateSlotOutcome.Unbound, result.Outcome);
        }

        [Test]
        public void SetSlot_ReturnsSuppressedWhenBindingDisabled()
        {
            var system = CreateSystem(new FakeVfxPlayerFactory());
            var owner = new FakeSlotOwner();

            var result = system.SetSlot(owner, "aura", "vfx.state.disabled");

            Assert.AreEqual(VfxStateSlotOutcome.Suppressed, result.Outcome);
        }

        [Test]
        public void SetSlot_ReturnsDomainUnavailableForAttachedWithoutHost()
        {
            var system = CreateSystem(new FakeVfxPlayerFactory());
            var owner = new FakeSlotOwner();

            var result = system.SetSlot(owner, "aura", "vfx.state.attached");

            Assert.AreEqual(VfxStateSlotOutcome.DomainUnavailable, result.Outcome);
        }

        [Test]
        public void ClearSceneInstances_RemovesAttachedStateSlots()
        {
            var host = new FakeDomainHost { IsAvailable = true };
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var owner = new FakeSlotOwner();

            system.SetSlot(
                owner,
                "aura",
                "vfx.state.attached",
                new VfxSpatialContext("slot", host, Vector3.zero));
            system.SetSlot(owner, "shield", "vfx.state.a");

            system.ClearSceneInstances();

            Assert.IsTrue(factory.StatePlayers[0].ExitRequested);
            Assert.IsFalse(factory.StatePlayers[1].ExitRequested);
        }

        [Test]
        public void StateProjectionFailure_DoesNotThrow()
        {
            var factory = new FakeVfxPlayerFactory(failStart: true);
            var system = CreateSystem(factory);
            var owner = new FakeSlotOwner();

            Assert.DoesNotThrow(() => system.SetSlot(owner, "aura", "vfx.state.a"));
            Assert.AreEqual(VfxStateSlotOutcome.BackendFailure, system.SetSlot(owner, "aura", "vfx.state.a").Outcome);
        }

        private static VfxSystem CreateSystem(FakeVfxPlayerFactory factory)
        {
            return new VfxSystem(
                VfxBindingCatalog.FromJson(StateCatalogJson),
                factory,
                new FakeClock(),
                randomValue: () => 0d);
        }

        private sealed class FakeSlotOwner : IVfxSlotOwner
        {
            public VfxStateRequest BuildStateRequest(string stateId)
            {
                return new VfxStateRequest(stateId, "test", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            }
        }

        private sealed class FakeClock : IAudioClock
        {
            public double UnscaledTime { get; set; } = 10d;
        }

        private sealed class FakeDomainHost : IVfxDomainHost
        {
            private readonly GameObject mRoot = new GameObject("FakeDomainHost");
            private readonly Transform mAttachment = new GameObject("Attachment").transform;

            public FakeDomainHost()
            {
                mAttachment.SetParent(mRoot.transform, false);
            }

            public bool IsAvailable { get; set; }
            public Transform AttachmentParent => mAttachment;
            public SpriteMask Mask => null;

            public bool TryWorldToLocal(Vector3 worldPosition, out Vector3 localPosition)
            {
                localPosition = mAttachment.InverseTransformPoint(worldPosition);
                return true;
            }

            public bool TryGetFollowTarget(out Transform followTarget)
            {
                followTarget = mAttachment;
                return true;
            }

            public bool TryGetSortingBounds(out VfxSortingBounds bounds)
            {
                bounds = new VfxSortingBounds("Main", 100, 200);
                return true;
            }
        }

        private sealed class FakeVfxPlayerFactory : IVfxPlayerFactory
        {
            private readonly bool mCreatePlayers;
            private readonly bool mFailStart;

            public FakeVfxPlayerFactory(bool createPlayers = true, bool failStart = false)
            {
                mCreatePlayers = createPlayers;
                mFailStart = failStart;
            }

            public readonly List<FakeVfxStatePlayer> StatePlayers = new List<FakeVfxStatePlayer>();

            public bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason)
            {
                player = null;
                failureReason = "pulse not used";
                return false;
            }

            public bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason)
            {
                if (!mCreatePlayers)
                {
                    player = null;
                    failureReason = "factory disabled";
                    return false;
                }

                var created = new FakeVfxStatePlayer(mFailStart);
                StatePlayers.Add(created);
                player = created;
                failureReason = string.Empty;
                return true;
            }
        }

        private sealed class FakeVfxStatePlayer : IVfxStatePlayer
        {
            private readonly bool mFailStart;

            public FakeVfxStatePlayer(bool failStart)
            {
                mFailStart = failStart;
            }

            public readonly List<VfxStateStartRequest> Started = new List<VfxStateStartRequest>();
            public bool ExitRequested { get; private set; }
            public bool ExitImmediate { get; private set; }
            public int ExitLoopLimit { get; private set; }
            public bool CompleteOnNextTick { get; set; }

            public VfxPlayerStartResult StartState(VfxStateStartRequest request)
            {
                Started.Add(request);
                if (mFailStart)
                {
                    return VfxPlayerStartResult.Failure("backend failure");
                }

                return VfxPlayerStartResult.Success("fake-state-instance");
            }

            public void BeginExit(bool immediate, int exitLoopLimit)
            {
                ExitRequested = true;
                ExitImmediate = immediate;
                ExitLoopLimit = exitLoopLimit;
            }

            public void Cancel()
            {
            }

            public bool Tick(float deltaTime)
            {
                if (CompleteOnNextTick)
                {
                    CompleteOnNextTick = false;
                    return true;
                }

                return false;
            }
        }
    }
}
