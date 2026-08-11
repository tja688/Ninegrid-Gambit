using NineGrid.Content.Vfx;
using NineGrid.Flow;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Systems.Vfx;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#203 gold-flight 程序化播放器契约：时间窗、宿主缺失/死亡、图标吞噬复位与生命周期。</summary>
    public sealed class VfxGoldFlightPlayerContractTests
    {
        private const string CueId = "economy.gold_flight";

        private const string GoldCatalogJson =
            "{\"schemaVersion\":1,\"cueBindings\":["
            + "{\"cueId\":\"" + CueId + "\",\"note\":\"金币飞入演出\",\"module\":\"Economy\",\"enabled\":true,"
            + "\"playerId\":\"" + VfxPlayerRegistry.GoldFlight + "\",\"spatialOwnership\":\"independent\",\"timeBase\":\"scaled\"}"
            + "],\"stateBindings\":[]}";

        [TearDown]
        public void Teardown()
        {
            GoldHudDomainHost.ClearInstance();
        }

        [Test]
        public void Timing_PlanFirstAndLastArrival_AreFiniteAndMatchOldAlgorithm()
        {
            for (var count = 1; count <= GoldFlightTiming.MaxVisualCoins * 3; count++)
            {
                var visualCount = GoldFlightTiming.ResolveVisualCount(count);
                var plan = GoldFlightTiming.Plan(visualCount);
                Assert.IsTrue(plan.IsValid, "plan invalid for count " + count);
                Assert.GreaterOrEqual(plan.FirstArrivalDelay, GoldFlightTiming.MinFlyDuration);
                Assert.GreaterOrEqual(plan.LastArrivalDelay, plan.FirstArrivalDelay);
                Assert.LessOrEqual(plan.LastArrivalDelay, GoldFlightTiming.MaxPresentationDuration);
            }
        }

        [Test]
        public void Timing_DistributeValues_AllocatesTotalAcrossCoins()
        {
            var values = GoldFlightTiming.DistributeValues(37, 24);
            var sum = 0;
            for (var i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }

            Assert.AreEqual(37, sum);
        }

        [Test]
        public void StartPulse_WithoutHost_Fails()
        {
            var player = new VfxGoldFlightPlayer(
                hostResolver: () => null,
                randomUnit: () => 0.5f);
            var result = player.StartPulse(BuildRequest(amount: 10));
            Assert.IsFalse(result.Succeeded);
            Assert.IsFalse(string.IsNullOrEmpty(result.FailureReason));
            Assert.IsFalse(result.HasPresentationPlan);
        }

        [Test]
        public void PresentationPlan_NoneAndDefaultAndFailure_HaveNoPlan()
        {
            Assert.IsFalse(VfxPresentationPlan.None.IsValid);
            Assert.IsFalse(default(VfxPresentationPlan).IsValid);
            Assert.IsFalse(VfxPlayerStartResult.Failure("no host").HasPresentationPlan);
            Assert.IsFalse(VfxPlayerStartResult.Success("id").HasPresentationPlan);
        }

        [Test]
        public void StartPulse_ReturnsPresentationPlan_AndCompletesNaturally()
        {
            var host = new FakeGoldHudDomainHost();
            var player = new VfxGoldFlightPlayer(
                hostResolver: () => host,
                randomUnit: () => 0.5f);

            var result = player.StartPulse(BuildRequest(amount: 10));
            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.HasPresentationPlan);

            var plan = GoldFlightTiming.Plan(GoldFlightTiming.ResolveVisualCount(10));
            Assert.AreEqual(plan.FirstArrivalDelay, result.PresentationPlan.FirstArrivalDelay, 1e-4f);
            Assert.AreEqual(plan.LastArrivalDelay, result.PresentationPlan.LastArrivalDelay, 1e-4f);

            var guard = 0;
            while (!player.Tick(0.05f) && guard < 400)
            {
                guard++;
            }

            Assert.Less(guard, 400, "player did not complete in time");
            Assert.AreEqual(GoldFlightTiming.ResolveVisualCount(10), host.PunchCount);
            Assert.AreEqual(1, host.SettleCount);
        }

        [Test]
        public void HostDeath_DoesNotCancelStartedCoins()
        {
            var host = new FakeGoldHudDomainHost();
            var player = new VfxGoldFlightPlayer(
                hostResolver: () => host,
                randomUnit: () => 0.5f);

            Assert.IsTrue(player.StartPulse(BuildRequest(amount: 5)).Succeeded);
            host.IsAvailable = false;

            var guard = 0;
            while (!player.Tick(0.05f) && guard < 400)
            {
                guard++;
            }

            Assert.Less(guard, 400, "player did not complete after host death");
        }

        [Test]
        public void VfxSystem_RequestCue_ReturnsGoldFlightPlan_AndCompletes()
        {
            var rootGo = new GameObject("HudRoot");
            var iconGo = new GameObject("GoldIcon");
            iconGo.transform.SetParent(rootGo.transform, false);
            GoldHudDomainHost.Install(rootGo.transform, iconGo.transform);
            try
            {
                var system = new VfxSystem(
                    VfxBindingCatalog.FromJson(GoldCatalogJson),
                    new VfxSystem.DefaultVfxPlayerFactory(),
                    new FakeClock(),
                    randomValue: () => 0.5d);
                var spatial = new VfxSpatialContext(
                    "gold-flight",
                    null,
                    new Vector3(-3f, 2f, 0f),
                    diagnosticOwnerUid: 0,
                    amount: 10);

                var result = system.RequestCue(VfxCueRequest.Simple(CueId, "test"), spatial);
                Assert.AreEqual(VfxCueOutcome.Played, result.Outcome);
                Assert.IsTrue(result.HasPresentationPlan);
                var expected = GoldFlightTiming.Plan(GoldFlightTiming.ResolveVisualCount(10));
                Assert.AreEqual(expected.LastArrivalDelay, result.PresentationPlan.LastArrivalDelay, 1e-4f);

                var guard = 0;
                while (guard < 400)
                {
                    system.Tick(0.05f);
                    guard++;
                    if (system.GetDiagnosticsSnapshot().ActiveInstances.Count == 0)
                    {
                        break;
                    }
                }

                Assert.Less(guard, 400, "gold-flight instance did not complete through the system");
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
                GoldHudDomainHost.ClearInstance();
            }
        }

        [Test]
        public void GoldHudDomainHost_PunchAccumulates_AndSnapRestoresBaseExactly()
        {
            var rootGo = new GameObject("HudRoot");
            var iconGo = new GameObject("GoldIcon");
            iconGo.transform.SetParent(rootGo.transform, false);
            iconGo.transform.localScale = new Vector3(0.75f, 0.75f, 0.75f);
            try
            {
                Assert.IsTrue(GoldHudDomainHost.Install(rootGo.transform, iconGo.transform));
                var host = GoldHudDomainHost.Instance;
                Assert.IsNotNull(host);
                Assert.IsTrue(host.IsAvailable);
                Assert.IsTrue(host.TryGetSortingBounds(out var bounds));
                Assert.AreEqual("Main", bounds.SortingLayerName);

                host.PunchIcon();
                Assert.AreEqual(0.75f * 1.12f, iconGo.transform.localScale.x, 1e-4f);

                host.PunchIcon();
                Assert.AreEqual(0.75f * 1.24f, iconGo.transform.localScale.x, 1e-4f);

                host.SnapIconToBase();
                Assert.AreEqual(0.75f, iconGo.transform.localScale.x, 1e-4f);
                Assert.AreEqual(0.75f, iconGo.transform.localScale.y, 1e-4f);
                Assert.AreEqual(0.75f, iconGo.transform.localScale.z, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
                GoldHudDomainHost.ClearInstance();
            }
        }

        [Test]
        public void GoldHudDomainHost_SettleIconToBase_RestoresBaseExactly()
        {
            var rootGo = new GameObject("HudRoot");
            var iconGo = new GameObject("GoldIcon");
            iconGo.transform.SetParent(rootGo.transform, false);
            iconGo.transform.localScale = Vector3.one;
            try
            {
                Assert.IsTrue(GoldHudDomainHost.Install(rootGo.transform, iconGo.transform));
                var host = GoldHudDomainHost.Instance;
                host.PunchIcon();
                host.PunchIcon();
                Assert.AreEqual(1.24f, iconGo.transform.localScale.x, 1e-4f);

                host.SettleIconToBase();
                Assert.AreEqual(1f, iconGo.transform.localScale.x, 1e-4f);
                Assert.AreEqual(1f, iconGo.transform.localScale.y, 1e-4f);
                Assert.AreEqual(1f, iconGo.transform.localScale.z, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
                GoldHudDomainHost.ClearInstance();
            }
        }

        private sealed class FakeClock : NineGrid.Presentation.Systems.IAudioClock
        {
            public double UnscaledTime { get; set; } = 10d;
        }

        private static VfxPulseStartRequest BuildRequest(int amount)
        {
            var dto = new VfxCueBindingDto
            {
                cueId = CueId,
                enabled = true,
                playerId = VfxPlayerRegistry.GoldFlight,
                spatialOwnership = "independent",
                timeBase = "scaled",
            };
            var binding = new VfxCueBinding(dto);
            var spatial = new VfxSpatialContext(
                "gold-flight",
                null,
                new Vector3(-3f, 2f, 0f),
                diagnosticOwnerUid: 0,
                amount: amount);
            return new VfxPulseStartRequest(
                VfxCueRequest.Simple(CueId, "test"),
                binding,
                string.Empty,
                string.Empty,
                0f,
                1f,
                1f,
                0f,
                Color.white,
                useUnscaledTime: false,
                spatial);
        }

        private sealed class FakeGoldHudDomainHost : IGoldHudDomainHost
        {
            private readonly Vector3 mSink = new Vector3(0f, 0f, 0f);

            public FakeGoldHudDomainHost(Vector3? sink = null)
            {
                if (sink.HasValue)
                {
                    mSink = sink.Value;
                }
            }

            public int PunchCount { get; private set; }
            public int SettleCount { get; private set; }

            public bool IsAvailable { get; set; } = true;
            public Transform AttachmentParent => null;
            public SpriteMask Mask => null;

            public Vector3 ResolveSinkWorldPosition()
            {
                return mSink;
            }

            public Vector3 ResolveDefaultOriginWorld()
            {
                return new Vector3(0f, 1f, 0f);
            }

            public bool TryGetCoinSprite(out Sprite sprite)
            {
                sprite = null;
                return false;
            }

            public void PunchIcon()
            {
                PunchCount++;
            }

            public void SnapIconToBase()
            {
            }

            public void SettleIconToBase()
            {
                SettleCount++;
            }

            public bool TryWorldToLocal(Vector3 worldPosition, out Vector3 localPosition)
            {
                localPosition = worldPosition;
                return true;
            }

            public bool TryGetFollowTarget(out Transform followTarget)
            {
                followTarget = null;
                return false;
            }

            public bool TryGetSortingBounds(out VfxSortingBounds bounds)
            {
                bounds = new VfxSortingBounds("Main", 1, 500);
                return true;
            }
        }
    }
}
