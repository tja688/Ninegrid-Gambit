#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Linq;
using NineGrid.Content.Vfx;
using NineGrid.Flow.Diagnostics;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Systems.Vfx;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    public sealed class VfxDiagnosticsBehaviorTests
    {
        private const string CatalogJson =
            "{\"schemaVersion\":1,\"cueBindings\":["
            + "{\"cueId\":\"vfx.test\",\"enabled\":true,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet
            + "\",\"materialKey\":\"fx/test\",\"spatialOwnership\":\"independent\"},"
            + "{\"cueId\":\"vfx.mute\",\"enabled\":false,\"playerId\":\"" + VfxPlayerRegistry.SpriteSheet
            + "\",\"materialKey\":\"fx/mute\"}"
            + "],\"stateBindings\":[]}";

        [Test]
        public void RequestCue_PlayAndComplete_RecordsLifecycleAndFrameStats()
        {
            ResetPerfTrace();
            var factory = new CompletingFakeVfxPlayerFactory();
            var system = CreateSystem(factory);

            system.RequestCue(VfxCueRequest.Simple("vfx.test", "diag"));
            system.Tick(0.1f);

            var snapshot = system.GetDiagnosticsSnapshot();
            Assert.That(snapshot.Session.TotalRequests, Is.EqualTo(1));
            Assert.That(snapshot.Session.TotalStarted, Is.EqualTo(1));
            Assert.That(snapshot.Session.TotalCompleted, Is.EqualTo(1));
            Assert.That(snapshot.RecentRecords.Any(r => r.Phase == VfxLifecyclePhase.Request), Is.True);
            Assert.That(snapshot.RecentRecords.Any(r => r.Phase == VfxLifecyclePhase.Resolve), Is.True);
            Assert.That(snapshot.RecentRecords.Any(r => r.Phase == VfxLifecyclePhase.Create), Is.True);
            Assert.That(snapshot.RecentRecords.Any(r => r.Phase == VfxLifecyclePhase.Start), Is.True);
            Assert.That(snapshot.RecentRecords.Any(r =>
                r.Phase == VfxLifecyclePhase.Complete
                && r.EndReason == VfxEndReason.NaturalComplete), Is.True);
            Assert.That(snapshot.RecentFrames.Count, Is.GreaterThan(0));
            Assert.That(snapshot.RecentFrames[snapshot.RecentFrames.Count - 1].Active, Is.EqualTo(0));
            Assert.That(CountKind(PerfTraceKinds.VfxLifecycleRequest), Is.EqualTo(1));
            Assert.That(CountKind(PerfTraceKinds.VfxLifecycleComplete), Is.EqualTo(1));
        }

        [Test]
        public void RequestCue_Unbound_IsIssueAndIncrementsReleaseCounters()
        {
            var system = CreateSystem(new CompletingFakeVfxPlayerFactory());

            system.RequestCue(VfxCueRequest.Simple("missing.cue", "diag"));

            var snapshot = system.GetDiagnosticsSnapshot();
            Assert.That(snapshot.Session.TotalIssues, Is.EqualTo(1));
            Assert.That(system.ReleaseCounters.Unbound, Is.EqualTo(1));
            Assert.That(snapshot.RecentRecords.Any(r =>
                r.Phase == VfxLifecyclePhase.Complete
                && r.IsIssue
                && r.EndReason == VfxEndReason.Unbound), Is.True);
        }

        [Test]
        public void RequestCue_Suppressed_IsNotIssue()
        {
            var system = CreateSystem(new CompletingFakeVfxPlayerFactory());

            system.RequestCue(VfxCueRequest.Simple("vfx.mute", "diag"));

            var snapshot = system.GetDiagnosticsSnapshot();
            Assert.That(snapshot.Session.TotalIssues, Is.EqualTo(0));
            Assert.That(snapshot.RecentRecords.Any(r =>
                r.Phase == VfxLifecyclePhase.Complete
                && !r.IsIssue
                && r.EndReason == VfxEndReason.Suppressed), Is.True);
        }

        [Test]
        public void ConcurrentInstances_UpdatePeakWithDrillDownContributors()
        {
            var factory = new CompletingFakeVfxPlayerFactory(completeOnTick: false);
            var system = CreateSystem(factory);

            system.RequestCue(VfxCueRequest.Simple("vfx.test", "a"));
            system.RequestCue(VfxCueRequest.Simple("vfx.test", "b"));
            system.Tick(0.1f);

            var snapshot = system.GetDiagnosticsSnapshot();
            Assert.That(snapshot.Session.PeakActive, Is.GreaterThanOrEqualTo(2));
            Assert.That(snapshot.Peak.Contributors.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(snapshot.Peak.Contributors.All(c => !string.IsNullOrEmpty(c.BindingKey)), Is.True);
            Assert.That(snapshot.Peak.Contributors.All(c => !string.IsNullOrEmpty(c.PlayerId)), Is.True);
            Assert.That(snapshot.Peak.Contributors.All(c => !string.IsNullOrEmpty(c.InstanceId)), Is.True);
            Assert.That(snapshot.BindingAggregates.Any(a => a.Started >= 2), Is.True);
            Assert.That(snapshot.PlayerAggregates.Any(a => a.Started >= 2), Is.True);
        }

        [Test]
        public void BoundedRing_EvictsOldRecordsButKeepsAggregates()
        {
            var factory = new CompletingFakeVfxPlayerFactory();
            var system = new VfxSystem(
                VfxBindingCatalog.FromJson(CatalogJson),
                factory,
                new FakeClock(),
                historyCapacity: 4,
                randomValue: () => 0d);

            for (var i = 0; i < 12; i++)
            {
                system.RequestCue(VfxCueRequest.Simple("vfx.test", "burst-" + i));
                system.Tick(0.1f);
            }

            var snapshot = system.GetDiagnosticsSnapshot();
            Assert.That(snapshot.RecentRecords.Count, Is.LessThanOrEqualTo(4));
            Assert.That(snapshot.Session.TotalRequests, Is.EqualTo(12));
            Assert.That(snapshot.BindingAggregates.Any(a => a.Started >= 12), Is.True);
        }

        [Test]
        public void PerfTracePayload_IncludesBatchAndSessionCorrelation()
        {
            ResetPerfTrace();
            var system = CreateSystem(new CompletingFakeVfxPlayerFactory());
            system.RequestCue(VfxCueRequest.Simple("vfx.test", "trace"));

            var session = PerfTraceRecorder.CurrentSession;
            var ev = session.events.First(e => e.kind == PerfTraceKinds.VfxLifecycleRequest);
            Assert.That(ev.payload.ContainsKey("batchId"), Is.True);
            Assert.That(ev.payload.ContainsKey("sessionId"), Is.True);
            Assert.That(ev.payload.ContainsKey("correlationId"), Is.True);
        }

        private static VfxSystem CreateSystem(CompletingFakeVfxPlayerFactory factory)
        {
            return new VfxSystem(
                VfxBindingCatalog.FromJson(CatalogJson),
                factory,
                new FakeClock(),
                randomValue: () => 0d);
        }

        private static void ResetPerfTrace()
        {
            PerfTraceRecorder.Clear();
            PerfTraceRecorder.Enabled = true;
            PerfTraceRecorder.BeginSessionIfNeeded(1UL);
        }

        private static int CountKind(string kind)
        {
            var session = PerfTraceRecorder.CurrentSession;
            return session.events.Count(e => e.kind == kind);
        }

        private sealed class FakeClock : IAudioClock
        {
            public double UnscaledTime { get; set; } = 10d;
        }

        private sealed class CompletingFakeVfxPlayerFactory : IVfxPlayerFactory
        {
            private readonly bool mCompleteOnTick;
            private int mInstanceCounter;

            public CompletingFakeVfxPlayerFactory(bool completeOnTick = true)
            {
                mCompleteOnTick = completeOnTick;
            }

            public bool TryCreatePulsePlayer(string playerId, out IVfxPulsePlayer player, out string failureReason)
            {
                player = new CompletingFakePulsePlayer(mCompleteOnTick, "inst-" + ++mInstanceCounter);
                failureReason = string.Empty;
                return true;
            }

            public bool TryCreateStatePlayer(string playerId, out IVfxStatePlayer player, out string failureReason)
            {
                player = null;
                failureReason = "state not used";
                return false;
            }
        }

        private sealed class CompletingFakePulsePlayer : IVfxPulsePlayer
        {
            private readonly bool mCompleteOnTick;
            private readonly string mInstanceId;

            public CompletingFakePulsePlayer(bool completeOnTick, string instanceId)
            {
                mCompleteOnTick = completeOnTick;
                mInstanceId = instanceId;
            }

            public VfxPlayerStartResult StartPulse(VfxPulseStartRequest request)
            {
                return VfxPlayerStartResult.Success(mInstanceId);
            }

            public void Cancel()
            {
            }

            public bool Tick(float deltaTime)
            {
                return mCompleteOnTick;
            }
        }
    }
}
#endif
