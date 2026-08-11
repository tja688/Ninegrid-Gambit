using NineGrid.Content.Vfx;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Setup;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Systems.Vfx;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>#199 金币调用迁移：Binder 发稳定 Cue，失败时无 PresentationPlan。</summary>
    public sealed class GoldGainPresentationMigrationTests
    {
        private const string CueId = GoldGainVfxCues.FlyIn;

        private const string GoldCatalogJson =
            "{\"schemaVersion\":1,\"cueBindings\":["
            + "{\"cueId\":\"" + CueId + "\",\"note\":\"金币飞入演出\",\"module\":\"Economy\",\"enabled\":true,"
            + "\"playerId\":\"" + VfxPlayerRegistry.GoldFlight + "\",\"spatialOwnership\":\"independent\",\"timeBase\":\"unscaled\"}"
            + "],\"stateBindings\":[]}";

        [TearDown]
        public void Teardown()
        {
            TriggerPulseHub.ResetToNull();
            GoldHudDomainHost.ClearInstance();
        }

        [Test]
        public void PresentationSceneBindings_NoLongerExposesGoldGainFxManager()
        {
            var prop = typeof(PresentationSceneBindings).GetProperty("GoldGainFxManager");
            Assert.IsNull(prop, "GoldGainFxManager 宿主字段应随 #204 删除，避免双轨装配。");
        }

        [Test]
        public void PresentGainVisual_WithoutHost_ReturnsNoPlan_AndDoesNotThrow()
        {
            GoldHudDomainHost.ClearInstance();
            var system = new VfxSystem(
                VfxBindingCatalog.FromJson(GoldCatalogJson),
                new VfxGoldFlightPlayerFactory(),
                new FakeClock(),
                randomValue: () => 0.5d);
            TriggerPulseHub.Configure(null, null, new VfxTriggerPulseSink(system));

            var result = GoldGainPresentationBinder.PresentGainVisual(10, 20, Vector3.zero);
            Assert.IsNotNull(result);
            Assert.IsFalse(result.HasPresentationPlan);
            Assert.AreNotEqual(VfxCueOutcome.Played, result.Outcome);
        }

        [Test]
        public void PresentGainVisual_WithHost_ReturnsPresentationPlan()
        {
            var rootGo = new GameObject("HudRoot");
            var iconGo = new GameObject("GoldIcon");
            iconGo.transform.SetParent(rootGo.transform, false);
            GoldHudDomainHost.Install(rootGo.transform, iconGo.transform);
            try
            {
                var system = new VfxSystem(
                    VfxBindingCatalog.FromJson(GoldCatalogJson),
                    new VfxGoldFlightPlayerFactory(),
                    new FakeClock(),
                    randomValue: () => 0.5d);
                TriggerPulseHub.Configure(null, null, new VfxTriggerPulseSink(system));

                var result = GoldGainPresentationBinder.PresentGainVisual(10, 20, new Vector3(1f, 2f, 0f));
                Assert.AreEqual(VfxCueOutcome.Played, result.Outcome);
                Assert.IsTrue(result.HasPresentationPlan);
                var expected = GoldFlightTiming.Plan(GoldFlightTiming.ResolveVisualCount(10));
                Assert.AreEqual(expected.LastArrivalDelay, result.PresentationPlan.LastArrivalDelay, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        private sealed class FakeClock : IAudioClock
        {
            public double UnscaledTime { get; set; } = 10d;
        }
    }
}
