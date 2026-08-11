using System.Collections.Generic;
using System.Linq;
using NineGrid.Content.Editor;
using NineGrid.Content.Vfx;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Systems.Vfx;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>particle 程序化粒子播放器契约：预设表一致性、Pulse/State 生命周期、绑定参数映射、卫生校验。</summary>
    public sealed class VfxParticlePlayerContractTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var system in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (system != null && system.gameObject.name.StartsWith("vfx-particle-"))
                {
                    Object.DestroyImmediate(system.gameObject);
                }
            }
        }

        [Test]
        public void PresetLibrary_MatchesContentPresetIdTable()
        {
            var libraryIds = new HashSet<string>(VfxParticlePresetLibrary.AllIds);
            var contentIds = new HashSet<string>(VfxParticlePresetIds.All);

            Assert.IsTrue(
                libraryIds.SetEquals(contentIds),
                "预设库与 VfxParticlePresetIds 不一致。仅库有：{0}；仅表有：{1}",
                string.Join(",", libraryIds.Except(contentIds)),
                string.Join(",", contentIds.Except(libraryIds)));

            foreach (var id in contentIds)
            {
                Assert.IsTrue(VfxParticlePresetLibrary.TryGet(id, out var preset));
                Assert.AreEqual(
                    VfxParticlePresetIds.IsLoopPreset(id),
                    preset.Loop,
                    "预设 {0} 的 Loop 标记与 particle.loop.* 命名约定不符。",
                    id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(preset.Note), "预设 {0} 缺少中文说明。", id);
                Assert.Greater(preset.Count, 0, "预设 {0} Count 必须为正。", id);
            }
        }

        [Test]
        public void Registry_ParticleCapabilities()
        {
            Assert.IsTrue(VfxPlayerRegistry.IsKnownPlayerId(VfxPlayerRegistry.Particle));
            Assert.IsTrue(VfxPlayerRegistry.SupportsPulse(VfxPlayerRegistry.Particle));
            Assert.IsTrue(VfxPlayerRegistry.SupportsState(VfxPlayerRegistry.Particle));
            Assert.IsFalse(VfxPlayerRegistry.IsMaterialPlayer(VfxPlayerRegistry.Particle));
            Assert.IsTrue(VfxPlayerRegistry.IsParticlePlayer(VfxPlayerRegistry.Particle));
            Assert.IsTrue(VfxPlayerRegistry.UsesMaterialVariantSelection(VfxPlayerRegistry.Particle));
            Assert.IsTrue(VfxPlayerRegistry.AllowsParamOverride(VfxPlayerRegistry.Particle, "materialKey"));
            Assert.IsTrue(VfxPlayerRegistry.AllowsParamOverride(VfxPlayerRegistry.Particle, "scale"));
            Assert.IsFalse(VfxPlayerRegistry.AllowsParamOverride(VfxPlayerRegistry.Particle, "bindingDelaySeconds"));
        }

        [Test]
        public void PulsePlayer_Lifecycle_CreatesConfiguredSystem_AndCancelCleansUp()
        {
            var factory = new VfxParticlePlayerFactory();
            Assert.IsTrue(factory.TryCreatePulsePlayer(VfxPlayerRegistry.Particle, out var player, out _));

            var result = player.StartPulse(BuildPulseRequest(
                VfxParticlePresetIds.HitSpark,
                fps: 0f,
                scale: 1f,
                tint: Color.white,
                useUnscaledTime: true));
            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.IsTrue(result.InstanceId.StartsWith("vfx-particle-"));

            var system = FindSpawnedSystem(VfxParticlePresetIds.HitSpark);
            Assert.IsNotNull(system, "未找到装配出的 ParticleSystem。");
            Assert.IsTrue(system.main.useUnscaledTime);
            Assert.IsFalse(system.main.loop);

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            Assert.AreEqual("Main", renderer.sortingLayerName);
            Assert.AreEqual(5002, renderer.sortingOrder, "hit_spark SortingOrderDelta=2 应叠加在基准 5000 上。");
            Assert.IsNotNull(renderer.sharedMaterial);

            Assert.IsFalse(player.Tick(0.016f), "粒子未放完前不应结束。");

            player.Cancel();
            Assert.IsNull(FindSpawnedSystem(VfxParticlePresetIds.HitSpark), "Cancel 后应拆除粒子对象。");
            Assert.IsTrue(player.Tick(0.016f));
        }

        [Test]
        public void PulsePlayer_FpsOverridesBurstCount_AndTintMultipliesColor()
        {
            var factory = new VfxParticlePlayerFactory();
            factory.TryCreatePulsePlayer(VfxPlayerRegistry.Particle, out var player, out _);

            var tint = new Color(1f, 0.5f, 0.25f, 0.8f);
            var result = player.StartPulse(BuildPulseRequest(
                VfxParticlePresetIds.BloodSpray,
                fps: 30f,
                scale: 2f,
                tint: tint,
                useUnscaledTime: false));
            Assert.IsTrue(result.Succeeded, result.FailureReason);

            var system = FindSpawnedSystem(VfxParticlePresetIds.BloodSpray);
            Assert.IsNotNull(system);

            var bursts = new ParticleSystem.Burst[system.emission.burstCount];
            system.emission.GetBursts(bursts);
            Assert.AreEqual(1, bursts.Length);
            Assert.AreEqual(30, (int)bursts[0].count.constant, "fps=30 应覆盖爆发数量。");

            Assert.IsTrue(VfxParticlePresetLibrary.TryGet(VfxParticlePresetIds.BloodSpray, out var preset));
            var startColor = system.main.startColor;
            Assert.AreEqual(ParticleSystemGradientMode.TwoColors, startColor.mode);
            Assert.AreEqual(preset.ColorA * tint, startColor.colorMin);
            Assert.AreEqual(preset.ColorB * tint, startColor.colorMax);

            var expectedMinSize = preset.StartSize.x * 2f;
            Assert.AreEqual(expectedMinSize, system.main.startSize.constantMin, 0.0001f, "scale 应缩放粒子尺寸。");

            player.Cancel();
        }

        [Test]
        public void PulsePlayer_UnknownPreset_AndLoopPreset_AreRejected()
        {
            var factory = new VfxParticlePlayerFactory();
            factory.TryCreatePulsePlayer(VfxPlayerRegistry.Particle, out var player, out _);

            var unknown = player.StartPulse(BuildPulseRequest("particle.not_exists", 0f, 1f, Color.white, false));
            Assert.IsFalse(unknown.Succeeded);

            var loop = player.StartPulse(BuildPulseRequest(VfxParticlePresetIds.LoopEmber, 0f, 1f, Color.white, false));
            Assert.IsFalse(loop.Succeeded, "loop 预设不可用于 Pulse。");
        }

        [Test]
        public void PulsePlayer_CompletesAfterParticlesDie()
        {
            var factory = new VfxParticlePlayerFactory();
            factory.TryCreatePulsePlayer(VfxPlayerRegistry.Particle, out var player, out _);

            var result = player.StartPulse(BuildPulseRequest(
                VfxParticlePresetIds.BlockSpark,
                fps: 0f,
                scale: 1f,
                tint: Color.white,
                useUnscaledTime: false));
            Assert.IsTrue(result.Succeeded, result.FailureReason);

            var system = FindSpawnedSystem(VfxParticlePresetIds.BlockSpark);
            Assert.IsNotNull(system);
            // 编辑器 Simulate 后系统处于暂停态、IsAlive 恒真；完成判定依赖播放器的排空兜底。
            system.Simulate(15f, withChildren: true, restart: false);
            Assert.AreEqual(0, system.particleCount);

            Assert.IsTrue(player.Tick(0.016f), "粒子全部消亡后 Tick 应返回结束。");
            Assert.IsNull(FindSpawnedSystem(VfxParticlePresetIds.BlockSpark));
        }

        [Test]
        public void StatePlayer_LoopLifecycle_ExitSegmentDrains()
        {
            var factory = new VfxParticlePlayerFactory();
            Assert.IsTrue(factory.TryCreateStatePlayer(VfxPlayerRegistry.Particle, out var player, out _));

            var wrongKind = player.StartState(BuildStateRequest(VfxParticlePresetIds.HitSpark));
            Assert.IsFalse(wrongKind.Succeeded, "一次性预设不可用于 State。");

            var result = player.StartState(BuildStateRequest(VfxParticlePresetIds.LoopEmber));
            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.IsTrue(result.InstanceId.StartsWith("vfx-state-particle-"));

            var system = FindSpawnedSystem(VfxParticlePresetIds.LoopEmber);
            Assert.IsNotNull(system);
            Assert.IsTrue(system.main.loop);

            Assert.IsFalse(player.Tick(0.1f), "主段无限循环不应自行结束。");

            player.BeginExit(immediate: false, exitLoopLimit: 1);
            system.Simulate(15f, withChildren: true, restart: false);
            Assert.IsTrue(player.Tick(0.1f), "退出段粒子放完后应结束。");
            Assert.IsNull(FindSpawnedSystem(VfxParticlePresetIds.LoopEmber));
        }

        [Test]
        public void StatePlayer_ImmediateExit_TearsDownAtOnce()
        {
            var factory = new VfxParticlePlayerFactory();
            factory.TryCreateStatePlayer(VfxPlayerRegistry.Particle, out var player, out _);

            var result = player.StartState(BuildStateRequest(VfxParticlePresetIds.LoopSparkle));
            Assert.IsTrue(result.Succeeded, result.FailureReason);

            player.BeginExit(immediate: true, exitLoopLimit: 1);
            Assert.IsNull(FindSpawnedSystem(VfxParticlePresetIds.LoopSparkle));
            Assert.IsTrue(player.Tick(0.1f));
        }

        [Test]
        public void HygieneValidator_ChecksParticlePresetKeys()
        {
            var catalog = new VfxBindingCatalogDto
            {
                schemaVersion = 1,
                cueBindings = new[]
                {
                    BuildCueDto("vfx.test.ok", VfxParticlePresetIds.HitSpark),
                    BuildCueDto("vfx.test.unknown", "particle.not_exists"),
                    BuildCueDto("vfx.test.loop_on_pulse", VfxParticlePresetIds.LoopEmber),
                },
                stateBindings = new[]
                {
                    BuildStateDto("vfx.state.ok", VfxParticlePresetIds.LoopEmber),
                    BuildStateDto("vfx.state.pulse_on_state", VfxParticlePresetIds.HitSpark),
                },
            };

            var declaredCues = new HashSet<string>(
                new[] { "vfx.test.ok", "vfx.test.unknown", "vfx.test.loop_on_pulse" });
            var declaredStates = new HashSet<string>(
                new[] { "vfx.state.ok", "vfx.state.pulse_on_state" });
            var materials = new HashSet<string>();

            var cueFindings = VfxBindingCatalogHygieneValidator.ValidateCueBindings(catalog, declaredCues, materials);
            var stateFindings = VfxBindingCatalogHygieneValidator.ValidateStateBindings(catalog, declaredStates, materials);

            Assert.IsFalse(
                cueFindings.Any(f => f.IdentityId == "vfx.test.ok" && f.Category == "missing-material"),
                "合法粒子 Pulse 绑定不应报素材缺失。");
            Assert.IsTrue(
                cueFindings.Any(f => f.IdentityId == "vfx.test.unknown" && f.Category == "missing-material"),
                "未知预设键应报 missing-material。");
            Assert.IsTrue(
                cueFindings.Any(f => f.IdentityId == "vfx.test.loop_on_pulse" && f.Category == "missing-material"),
                "loop 预设挂 Pulse 应报错。");
            Assert.IsFalse(
                stateFindings.Any(f => f.IdentityId == "vfx.state.ok" && f.Category == "missing-material"),
                "合法粒子 State 绑定不应报素材缺失。");
            Assert.IsTrue(
                stateFindings.Any(f => f.IdentityId == "vfx.state.pulse_on_state" && f.Category == "missing-material"),
                "一次性预设挂 State 应报错。");
        }

        private static VfxCueBindingDto BuildCueDto(string cueId, string presetKey)
        {
            return new VfxCueBindingDto
            {
                cueId = cueId,
                note = "测试",
                module = "Test",
                enabled = true,
                playerId = VfxPlayerRegistry.Particle,
                spatialOwnership = "independent",
                materialKey = presetKey,
            };
        }

        private static VfxStateBindingDto BuildStateDto(string stateId, string presetKey)
        {
            return new VfxStateBindingDto
            {
                stateId = stateId,
                note = "测试",
                module = "Test",
                enabled = true,
                playerId = VfxPlayerRegistry.Particle,
                spatialOwnership = "independent",
                materialKey = presetKey,
                exitMode = "segment",
            };
        }

        private static VfxPulseStartRequest BuildPulseRequest(
            string presetKey,
            float fps,
            float scale,
            Color tint,
            bool useUnscaledTime)
        {
            var binding = new VfxCueBinding(new VfxCueBindingDto
            {
                cueId = "vfx.test.particle",
                enabled = true,
                playerId = VfxPlayerRegistry.Particle,
                spatialOwnership = "independent",
                materialKey = presetKey,
            });
            return new VfxPulseStartRequest(
                VfxCueRequest.Simple("vfx.test.particle", "test"),
                binding,
                "default",
                presetKey,
                fps,
                1f,
                scale,
                0f,
                tint,
                useUnscaledTime,
                new VfxSpatialContext("test", null, new Vector3(1f, 2f, 0f)));
        }

        private static VfxStateStartRequest BuildStateRequest(string presetKey)
        {
            var binding = new VfxStateBinding(new VfxStateBindingDto
            {
                stateId = "vfx.state.test.particle",
                enabled = true,
                playerId = VfxPlayerRegistry.Particle,
                spatialOwnership = "independent",
                materialKey = presetKey,
                exitMode = "segment",
            });
            return new VfxStateStartRequest(
                new VfxStateRequest(
                    "vfx.state.test.particle",
                    "test",
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty),
                binding,
                "default",
                presetKey,
                0f,
                1f,
                1f,
                0f,
                Color.white,
                false,
                new VfxSpatialContext("test", null, new Vector3(0f, 0f, 0f)));
        }

        private static ParticleSystem FindSpawnedSystem(string presetId)
        {
            foreach (var system in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                if (system != null && system.gameObject.name == "vfx-particle-" + presetId)
                {
                    return system;
                }
            }

            return null;
        }
    }
}
