using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NineGrid.Content.Vfx;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Setup;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// #200 跨系统契约：Hub 三通道、金币迁移收口、正式播放器/素材路径。
    /// Audio transport 回归由 #195 <see cref="EditorWorkbenchTransportTests"/> 承担。
    /// </summary>
    public sealed class VfxCrossSystemContractTests
    {
        [TearDown]
        public void Teardown()
        {
            TriggerPulseHub.ResetToNull();
            GoldHudDomainHost.ClearInstance();
        }

        [Test]
        public void TriggerPulseOutputController_ProductionDefaults_WireThreeChannelsInSource()
        {
            var sourcePath = Path.Combine(
                new DirectoryInfo(Application.dataPath).Parent.FullName,
                "Assets",
                "Scripts",
                "NineGrid.Presentation",
                "Controllers",
                "TriggerPulseOutputController.cs");
            var source = File.ReadAllText(sourcePath, Encoding.UTF8);

            Assert.That(source, Does.Contain("ConfigureProductionDefaults"));
            Assert.That(source, Does.Contain("new CardEffectTriggerPulseSink()"));
            Assert.That(source, Does.Contain("new DebouncingTriggerPulseSink("));
            Assert.That(source, Does.Contain("new AudioTriggerPulseSink(audio)"));
            Assert.That(source, Does.Contain("new VfxTriggerPulseSink(vfx)"));
            Assert.That(source, Does.Contain("TriggerPulseHub.Configure("));
        }

        [Test]
        public void TriggerPulseHub_ThreeChannels_PulseIndependently_AndResetFxKeepsAudioVfx()
        {
            var fx = new RecordingFxSink();
            var audio = new RecordingAudioSink();
            var vfx = new RecordingVfxSink();
            TriggerPulseHub.Configure(fx, audio, vfx);

            TriggerPulseHub.PulseFx("fx.test");
            TriggerPulseHub.PulseAudio("audio.test");
            TriggerPulseHub.PulseVfx(VfxCueRequest.Simple("vfx.test", "cross-system"));

            Assert.AreEqual(1, fx.Count);
            Assert.AreEqual(1, audio.Count);
            Assert.AreEqual(1, vfx.Count);

            TriggerPulseHub.ResetFxToNull();
            Assert.AreSame(NullTriggerPulseSink.Instance, TriggerPulseHub.Fx);
            Assert.AreSame(audio, TriggerPulseHub.Audio);
            Assert.AreSame(vfx, TriggerPulseHub.Vfx);

            TriggerPulseHub.PulseAudio("audio.after-fx-reset");
            TriggerPulseHub.PulseVfx(VfxCueRequest.Simple("vfx.after-fx-reset", "cross-system"));
            Assert.AreEqual(2, audio.Count);
            Assert.AreEqual(2, vfx.Count);
        }

        [Test]
        public void FormalGoldFlightBinding_UsesRegisteredPlayer_AndStableCue()
        {
            var catalog = VfxBindingCatalog.LoadFromResources();
            Assert.IsNotNull(catalog);

            var request = VfxCueRequest.Simple(GoldGainVfxCues.FlyIn, "VfxCrossSystemContractTests");
            Assert.IsTrue(
                catalog.TryResolveCueStrict(request, out var binding, out var error),
                error?.Message);
            Assert.IsNotNull(binding);
            Assert.AreEqual(VfxPlayerRegistry.GoldFlight, binding.PlayerId);
            Assert.IsTrue(VfxPlayerRegistry.IsKnownPlayerId(binding.PlayerId));
            Assert.IsTrue(VfxPlayerRegistry.SupportsPulse(binding.PlayerId));
            Assert.IsFalse(VfxPlayerRegistry.SupportsState(binding.PlayerId));
            Assert.That(binding.BindingKey, Does.Not.Contain("uid"));
            Assert.AreEqual(GoldGainVfxCues.FlyIn, binding.CueId);
        }

        [Test]
        public void GoldFlightCoinSprite_ExistsUnderFormalResourcesPath()
        {
            var projectRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;
            var png = Path.Combine(projectRoot, "Assets", "Resources", "VFX", "GoldFlightCoin.png");
            Assert.IsTrue(File.Exists(png), "缺少正式飞币素材 Assets/Resources/VFX/GoldFlightCoin.png");

            var sprite = Resources.Load<Sprite>("VFX/GoldFlightCoin");
            Assert.IsNotNull(sprite, "Resources.Load<Sprite>(\"VFX/GoldFlightCoin\") 失败");
        }

        [Test]
        public void DefaultPlayerFactory_CanCreateRegisteredPulsePlayers()
        {
            var factory = new VfxSystem.DefaultVfxPlayerFactory();
            Assert.IsTrue(
                factory.TryCreatePulsePlayer(VfxPlayerRegistry.SpriteSheet, out var sheet, out var sheetFail),
                sheetFail);
            Assert.IsNotNull(sheet);

            Assert.IsTrue(
                factory.TryCreatePulsePlayer(VfxPlayerRegistry.GoldFlight, out var gold, out var goldFail),
                goldFail);
            Assert.IsNotNull(gold);

            Assert.IsFalse(factory.TryCreatePulsePlayer("unknown-player", out _, out _));
        }

        [Test]
        public void PresentationBindings_AndTypeSystem_HaveNoGoldGainFxManager()
        {
            Assert.IsNull(
                typeof(PresentationSceneBindings).GetProperty("GoldGainFxManager"),
                "PresentationSceneBindings 不得再暴露 GoldGainFxManager");

            var revived = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t != null && t.Name == "GoldGainFxManagerSingleton");
            Assert.IsNull(revived, "程序集中不得再存在 GoldGainFxManagerSingleton 类型");
        }

        [Test]
        public void GoldGainBinder_EmitsTypedPulseVfx_NotLegacyFxManager()
        {
            var sourcePath = Path.Combine(
                new DirectoryInfo(Application.dataPath).Parent.FullName,
                "Assets",
                "Scripts",
                "NineGrid.Presentation",
                "Flow",
                "GoldGainPresentationBinder.cs");
            var source = File.ReadAllText(sourcePath, Encoding.UTF8);
            Assert.That(source, Does.Contain("TriggerPulseHub.PulseVfx"));
            Assert.That(source, Does.Contain("GoldGainVfxCues.FlyIn").Or.Contain("economy.gold_flight"));
            Assert.That(source, Does.Not.Contain("GoldGainFxManagerSingleton"));
        }

        private static Type[] SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types ?? Array.Empty<Type>();
            }
        }

        private sealed class RecordingFxSink : ITriggerPulseSink
        {
            public int Count { get; private set; }

            public void Pulse(string triggerId)
            {
                Count++;
            }
        }

        private sealed class RecordingAudioSink : ITriggerPulseSink, IAudioCuePulseSink
        {
            public int Count { get; private set; }

            public void Pulse(string triggerId)
            {
                Count++;
            }

            public void Pulse(NineGrid.Content.Audio.AudioCueRequest request)
            {
                Count++;
            }
        }

        private sealed class RecordingVfxSink : IVfxCuePulseSink
        {
            public int Count { get; private set; }

            public VfxCueResult Pulse(VfxCueRequest request, VfxSpatialContext spatialContext = default)
            {
                Count++;
                return null;
            }
        }
    }
}
