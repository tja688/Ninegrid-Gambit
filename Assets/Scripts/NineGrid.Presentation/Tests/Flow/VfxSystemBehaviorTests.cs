using System.Collections.Generic;
using NineGrid.Content.Vfx;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class VfxSystemBehaviorTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":1,\"ticket\":\"#196\",\"cueBindings\":["
            + "{\"cueId\":\"vfx.test\",\"enabled\":true,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet + "\",\"materialKey\":\"fx/test\","
            + "\"spatialOwnership\":\"independent\"},"
            + "{\"cueId\":\"vfx.attached\",\"enabled\":true,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet + "\",\"materialKey\":\"fx/attached\","
            + "\"spatialOwnership\":\"attached\"},"
            + "{\"cueId\":\"vfx.ambiguous\",\"enabled\":true,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet + "\",\"materialKey\":\"fx/a\","
            + "\"selectorCardDefId\":\"card.a\"},"
            + "{\"cueId\":\"vfx.ambiguous\",\"enabled\":true,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet + "\",\"materialKey\":\"fx/b\","
            + "\"selectorCardDefId\":\"card.a\"}"
            + "],\"stateBindings\":[]}";

        [Test]
        public void RequestCue_ReturnsUnboundWhenBindingMissing()
        {
            var system = CreateSystem(new FakeVfxPlayerFactory());

            var result = system.RequestCue(VfxCueRequest.Simple("missing.cue", "test"));

            Assert.AreEqual(VfxCueOutcome.Unbound, result.Outcome);
            Assert.That(result.FailureReason, Does.Contain("不存在"));
        }

        [Test]
        public void RequestCue_ReturnsInvalidBindingWhenAmbiguous()
        {
            var system = CreateSystem(new FakeVfxPlayerFactory());
            var request = new VfxCueRequest(
                "vfx.ambiguous",
                "test",
                "card.a",
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty);

            var result = system.RequestCue(request);

            Assert.AreEqual(VfxCueOutcome.InvalidBinding, result.Outcome);
            Assert.That(result.FailureReason, Does.Contain("歧义").Or.Contain("多条"));
        }

        [Test]
        public void RequestCue_ReturnsSuppressedWhenBindingDisabled()
        {
            var catalogJson =
                "{\"schemaVersion\":1,\"cueBindings\":["
                + "{\"cueId\":\"vfx.mute\",\"enabled\":false,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet + "\",\"materialKey\":\"fx/mute\"}"
                + "],\"stateBindings\":[]}";
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory, catalogJson);

            var result = system.RequestCue(VfxCueRequest.Simple("vfx.mute", "test"));

            Assert.AreEqual(VfxCueOutcome.Suppressed, result.Outcome);
            Assert.AreEqual(0, factory.CreatedCount);
        }

        [Test]
        public void RequestCue_ReturnsPlayerUnavailableWhenFactoryCannotCreate()
        {
            var catalogJson =
                "{\"schemaVersion\":1,\"cueBindings\":["
                + "{\"cueId\":\"vfx.missing\",\"enabled\":true,\"playerId\":\"sprite-sheet\",\"materialKey\":\"fx/x\"}"
                + "],\"stateBindings\":[]}";
            var system = CreateSystem(new FakeVfxPlayerFactory(createPlayers: false), catalogJson);

            var result = system.RequestCue(VfxCueRequest.Simple("vfx.missing", "test"));

            Assert.AreEqual(VfxCueOutcome.PlayerUnavailable, result.Outcome);
        }

        [Test]
        public void RequestCue_ReturnsDomainUnavailableForAttachedWithoutHost()
        {
            var system = CreateSystem(new FakeVfxPlayerFactory());

            var result = system.RequestCue(VfxCueRequest.Simple("vfx.attached", "test"));

            Assert.AreEqual(VfxCueOutcome.DomainUnavailable, result.Outcome);
            Assert.That(result.FailureReason, Does.Contain("宿主"));
        }

        [Test]
        public void RequestCue_ReturnsBackendFailureWhenPlayerStartFails()
        {
            var factory = new FakeVfxPlayerFactory(failStart: true);
            var system = CreateSystem(factory);

            var result = system.RequestCue(VfxCueRequest.Simple("vfx.test", "test"));

            Assert.AreEqual(VfxCueOutcome.BackendFailure, result.Outcome);
            Assert.AreEqual("backend failure", result.FailureReason);
        }

        [Test]
        public void RequestCue_PlaysThroughFakePlayer()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);

            var result = system.RequestCue(VfxCueRequest.Simple("vfx.test", "test"));

            Assert.AreEqual(VfxCueOutcome.Played, result.Outcome);
            Assert.AreEqual(1, factory.CreatedCount);
            Assert.AreEqual(1, factory.Players[0].Started.Count);
            Assert.AreEqual("fx/test", factory.Players[0].Started[0].MaterialKey);
        }

        [Test]
        public void ScheduleCue_RequiresExplicitCancellation()
        {
            var scheduler = new FakeVfxScheduler();
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory, CatalogJson, scheduler);

            var kept = system.ScheduleCue(VfxCueRequest.Simple("vfx.test", "kept"), 2f);
            var cancelled = system.ScheduleCue(VfxCueRequest.Simple("vfx.test", "cancelled"), 3f);

            Assert.IsTrue(kept.IsValid);
            Assert.IsTrue(system.CancelScheduledCue(cancelled));
            scheduler.Fire(kept);
            Assert.AreEqual(1, factory.CreatedCount);
            Assert.IsFalse(system.CancelScheduledCue(cancelled));
        }

        [Test]
        public void IndependentPulse_ContinuesAfterHostLostAndBindingDisabled()
        {
            var host = new FakeDomainHost { IsAvailable = true };
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var spatial = new VfxSpatialContext("impact", host, new Vector3(1f, 2f, 0f));

            var first = system.RequestCue(VfxCueRequest.Simple("vfx.test", "first"), spatial);
            Assert.AreEqual(VfxCueOutcome.Played, first.Outcome);
            Assert.AreEqual(1, factory.Players[0].Started.Count);

            host.IsAvailable = false;
            system = CreateSystem(factory, DisabledTestCatalogJson);

            var second = system.RequestCue(VfxCueRequest.Simple("vfx.test", "second"), spatial);
            Assert.AreEqual(VfxCueOutcome.Suppressed, second.Outcome);
            Assert.AreEqual(1, factory.Players[0].Started.Count);
            Assert.IsFalse(factory.Players[0].Cancelled);
        }

        [Test]
        public void ClearSceneInstances_RemovesAttachedButKeepsIndependent()
        {
            var attachedHost = new FakeDomainHost { IsAvailable = true };
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);

            system.RequestCue(
                VfxCueRequest.Simple("vfx.attached", "attached"),
                new VfxSpatialContext("slot", attachedHost, Vector3.zero));
            system.RequestCue(VfxCueRequest.Simple("vfx.test", "independent"));

            Assert.AreEqual(2, factory.CreatedCount);
            system.ClearSceneInstances();

            Assert.IsTrue(factory.Players[0].Cancelled);
            Assert.IsFalse(factory.Players[1].Cancelled);
        }

        [Test]
        public void TriggerPulseHub_PulseVfx_EndToEnd()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            TriggerPulseHub.Configure(
                NullTriggerPulseSink.Instance,
                NullTriggerPulseSink.Instance,
                new VfxTriggerPulseSink(system));

            TriggerPulseHub.PulseVfx(VfxCueRequest.Simple("vfx.test", "hub"));

            Assert.AreEqual(1, factory.CreatedCount);
            TriggerPulseHub.ResetToNull();
        }

        [Test]
        public void ApplyWorkbenchCatalog_IncrementsRevisionAndAffectsFutureRequests()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            var applied = system.ApplyWorkbenchCatalog(DisabledTestCatalogJson);
            Assert.IsTrue(applied.Succeeded);
            Assert.AreEqual(1L, system.GetWorkbenchSnapshot().Revision);

            var suppressed = system.RequestCue(VfxCueRequest.Simple("vfx.test", "workbench"));
            Assert.AreEqual(VfxCueOutcome.Suppressed, suppressed.Outcome);
            Assert.AreEqual(0, factory.CreatedCount);
        }

        [Test]
        public void WorkbenchSnapshot_IncludesHistoryAggregatesAndActiveInstances()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory);
            system.RequestCue(VfxCueRequest.Simple("vfx.test", "snap"));
            var snapshot = system.GetWorkbenchSnapshot();
            Assert.GreaterOrEqual(snapshot.History.Count, 1);
            Assert.GreaterOrEqual(snapshot.Aggregates.Count, 1);
            Assert.AreEqual(1, snapshot.ActivePulses.Count);
        }

        [Test]
        public void PreviewWorkbenchBinding_IgnoresEnabledAndDoesNotAdvanceCooldown()
        {
            var factory = new FakeVfxPlayerFactory();
            var system = CreateSystem(factory, DisabledTestCatalogJson);
            var bindingKey = VfxBindingKey.Compose("vfx.test", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            var preview = system.PreviewWorkbenchBinding(bindingKey, includeBindingDelay: false, isStateBinding: false);
            Assert.IsTrue(preview.Succeeded);
            Assert.AreEqual(1, factory.CreatedCount);
        }

        private const string DisabledTestCatalogJson =
            "{\"schemaVersion\":1,\"cueBindings\":["
            + "{\"cueId\":\"vfx.test\",\"enabled\":false,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet + "\",\"materialKey\":\"fx/test\","
            + "\"spatialOwnership\":\"independent\"}"
            + "],\"stateBindings\":[]}";

        private static VfxSystem CreateSystem(
            FakeVfxPlayerFactory factory,
            string catalogJson = CatalogJson,
            FakeVfxScheduler scheduler = null)
        {
            return new VfxSystem(
                VfxBindingCatalog.FromJson(catalogJson),
                factory,
                new FakeClock(),
                scheduler: scheduler,
                randomValue: () => 0d);
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

        private sealed class FakeVfxScheduler : IVfxCueScheduler
        {
            private readonly Dictionary<long, (float delay, System.Action callback)> mPending =
                new Dictionary<long, (float, System.Action)>();
            private long mNextKey = 1;

            public VfxScheduleKey Schedule(float delaySeconds, System.Action callback)
            {
                var key = new VfxScheduleKey(mNextKey++);
                mPending[key.Value] = (delaySeconds, callback);
                return key;
            }

            public bool Cancel(VfxScheduleKey key)
            {
                return mPending.Remove(key.Value);
            }

            public void Fire(VfxScheduleKey key)
            {
                if (mPending.TryGetValue(key.Value, out var entry))
                {
                    mPending.Remove(key.Value);
                    entry.callback?.Invoke();
                }
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

            public readonly List<FakeVfxPulsePlayer> Players = new List<FakeVfxPulsePlayer>();
            public readonly List<FakeVfxStatePlayer> StatePlayers = new List<FakeVfxStatePlayer>();
            public int CreatedCount => Players.Count;

            public bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason)
            {
                if (!mCreatePlayers)
                {
                    player = null;
                    failureReason = "factory disabled";
                    return false;
                }

                var created = new FakeVfxPulsePlayer(mFailStart);
                Players.Add(created);
                player = created;
                failureReason = string.Empty;
                return true;
            }

            public bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason)
            {
                player = null;
                failureReason = "state not used in cue tests";
                return false;
            }
        }

        private sealed class FakeVfxStatePlayer : IVfxStatePlayer
        {
            public VfxPlayerStartResult StartState(VfxStateStartRequest request)
            {
                return VfxPlayerStartResult.Success("unused");
            }

            public void BeginExit(bool immediate, int exitLoopLimit)
            {
            }

            public void Cancel()
            {
            }

            public bool Tick(float deltaTime)
            {
                return false;
            }
        }

        private sealed class FakeVfxPulsePlayer : IVfxPulsePlayer
        {
            private readonly bool mFailStart;

            public FakeVfxPulsePlayer(bool failStart)
            {
                mFailStart = failStart;
            }

            public readonly List<VfxPulseStartRequest> Started = new List<VfxPulseStartRequest>();
            public bool Cancelled { get; private set; }

            public VfxPlayerStartResult StartPulse(VfxPulseStartRequest request)
            {
                Started.Add(request);
                if (mFailStart)
                {
                    return VfxPlayerStartResult.Failure("backend failure");
                }

                return VfxPlayerStartResult.Success("fake-instance");
            }

            public void Cancel()
            {
                Cancelled = true;
            }

            public bool Tick(float deltaTime)
            {
                return false;
            }
        }
    }
}
