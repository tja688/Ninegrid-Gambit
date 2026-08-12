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
    /// <summary>projectile 程序化弹道播放器契约：预设表一致性、飞行生命周期、命中计划、绑定参数映射、卫生校验。</summary>
    public sealed class VfxProjectilePlayerContractTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (transform != null
                    && transform.parent == null
                    && transform.gameObject.name.StartsWith("vfx-projectile-"))
                {
                    Object.DestroyImmediate(transform.gameObject);
                }
            }
        }

        [Test]
        public void PresetLibrary_MatchesContentPresetIdTable()
        {
            var libraryIds = new HashSet<string>(VfxProjectilePresetLibrary.AllIds);
            var contentIds = new HashSet<string>(VfxProjectilePresetIds.All);

            Assert.IsTrue(
                libraryIds.SetEquals(contentIds),
                "预设库与 VfxProjectilePresetIds 不一致。仅库有：{0}；仅表有：{1}",
                string.Join(",", libraryIds.Except(contentIds)),
                string.Join(",", contentIds.Except(libraryIds)));

            foreach (var id in contentIds)
            {
                Assert.IsTrue(VfxProjectilePresetLibrary.TryGet(id, out var preset));
                Assert.IsFalse(string.IsNullOrWhiteSpace(preset.Note), "预设 {0} 缺少中文说明。", id);
                Assert.Greater(preset.FlightSpeed, 0f, "预设 {0} FlightSpeed 必须为正。", id);
                Assert.That(preset.Count, Is.InRange(1, 8), "预设 {0} Count 超出 1~8。", id);
                AssertBurstPresetValid(id, "Impact", preset.ImpactPresetId);
                AssertBurstPresetValid(id, "Muzzle", preset.MuzzlePresetId);
            }
        }

        private static void AssertBurstPresetValid(string projectileId, string label, string particlePresetId)
        {
            if (string.IsNullOrEmpty(particlePresetId))
            {
                return;
            }

            Assert.IsTrue(
                VfxParticlePresetLibrary.TryGet(particlePresetId, out var burst),
                "预设 {0} 的 {1} 引用了未知粒子预设：{2}",
                projectileId,
                label,
                particlePresetId);
            Assert.IsFalse(burst.Loop, "预设 {0} 的 {1} 不得引用 loop 粒子预设。", projectileId, label);
        }

        [Test]
        public void Registry_ProjectileCapabilities()
        {
            Assert.IsTrue(VfxPlayerRegistry.IsKnownPlayerId(VfxPlayerRegistry.Projectile));
            Assert.IsTrue(VfxPlayerRegistry.SupportsPulse(VfxPlayerRegistry.Projectile));
            Assert.IsFalse(VfxPlayerRegistry.SupportsState(VfxPlayerRegistry.Projectile), "弹道播放器不提供 State 能力。");
            Assert.IsFalse(VfxPlayerRegistry.IsMaterialPlayer(VfxPlayerRegistry.Projectile));
            Assert.IsFalse(VfxPlayerRegistry.IsParticlePlayer(VfxPlayerRegistry.Projectile));
            Assert.IsTrue(VfxPlayerRegistry.IsProjectilePlayer(VfxPlayerRegistry.Projectile));
            Assert.IsTrue(VfxPlayerRegistry.UsesMaterialVariantSelection(VfxPlayerRegistry.Projectile));
            Assert.IsTrue(VfxPlayerRegistry.AllowsParamOverride(VfxPlayerRegistry.Projectile, "materialKey"));
            Assert.IsTrue(VfxPlayerRegistry.AllowsParamOverride(VfxPlayerRegistry.Projectile, "scale"));
            Assert.IsFalse(VfxPlayerRegistry.AllowsParamOverride(VfxPlayerRegistry.Projectile, "bindingDelaySeconds"));
        }

        [Test]
        public void PulsePlayer_FliesFromSourceToTarget_SpawnsImpact_AndCompletes()
        {
            var factory = new VfxProjectilePlayerFactory();
            Assert.IsTrue(factory.TryCreatePulsePlayer(VfxPlayerRegistry.Projectile, out var player, out _));

            var source = new Vector3(0f, 0f, 0f);
            var target = new Vector3(4f, 0f, 0f);
            var result = player.StartPulse(BuildPulseRequest(
                VfxProjectilePresetIds.ArrowShot,
                fps: 0f,
                scale: 1f,
                tint: Color.white,
                useUnscaledTime: false,
                source: source,
                target: target));
            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.IsTrue(result.InstanceId.StartsWith("vfx-projectile-"));

            // arrow_shot：17 单位/秒，4 单位距离 → 时长 clamp(0.235, 0.14, 0.4)=0.235s。
            Assert.IsTrue(result.PresentationPlan.IsValid, "弹道必须回传命中计划。");
            Assert.AreEqual(result.PresentationPlan.FirstArrivalDelay, result.PresentationPlan.LastArrivalDelay, 0.0001f);
            Assert.AreEqual(4f / 17f, result.PresentationPlan.LastArrivalDelay, 0.01f);

            var root = FindProjectileRoot(VfxProjectilePresetIds.ArrowShot);
            Assert.IsNotNull(root, "未找到弹道根对象。");

            // 推进到飞行中段：弹头应已激活并离开源位置、朝目标推进。
            Assert.IsFalse(player.Tick(0.1f));
            var shot = root.transform.Find("shot-0");
            Assert.IsNotNull(shot);
            Assert.IsTrue(shot.gameObject.activeSelf, "起飞后弹头应激活。");
            Assert.Greater(shot.position.x, 0.5f, "弹头应向目标推进。");
            Assert.Less(shot.position.x, 4f, "飞行中段不应已达目标。");

            // 推进过命中时刻：弹头隐藏、命中爆点粒子在场。
            Assert.IsFalse(player.Tick(0.2f));
            var head = shot.Find("head");
            Assert.IsNotNull(head);
            Assert.IsFalse(head.GetComponent<SpriteRenderer>().enabled, "命中后弹头应隐藏。");
            Assert.IsNotNull(
                FindChildStartingWith(root.transform, "vfx-particle-" + VfxParticlePresetIds.HitSpark),
                "命中后应生成爆点粒子。");

            // 推进过收尾窗口：演出自然结束并拆净。
            var finished = false;
            for (var i = 0; i < 40 && !finished; i++)
            {
                finished = player.Tick(0.1f);
            }

            Assert.IsTrue(finished, "收尾窗口后 Tick 应返回结束。");
            Assert.IsNull(FindProjectileRoot(VfxProjectilePresetIds.ArrowShot), "结束后应拆除弹道对象。");
        }

        [Test]
        public void PulsePlayer_MultiShot_DefaultCount_AndFpsOverride()
        {
            var factory = new VfxProjectilePlayerFactory();
            factory.TryCreatePulsePlayer(VfxPlayerRegistry.Projectile, out var player, out _);

            var result = player.StartPulse(BuildPulseRequest(
                VfxProjectilePresetIds.BoneShard,
                fps: 0f,
                scale: 1f,
                tint: Color.white,
                useUnscaledTime: false,
                source: Vector3.zero,
                target: new Vector3(5f, 0f, 0f)));
            Assert.IsTrue(result.Succeeded, result.FailureReason);

            var root = FindProjectileRoot(VfxProjectilePresetIds.BoneShard);
            Assert.AreEqual(3, CountShots(root.transform), "bone_shard 默认三连发。");
            Assert.Greater(
                result.PresentationPlan.LastArrivalDelay,
                result.PresentationPlan.FirstArrivalDelay,
                "错峰齐射的末达应晚于首达。");
            player.Cancel();

            var overridden = player.StartPulse(BuildPulseRequest(
                VfxProjectilePresetIds.BoneShard,
                fps: 5f,
                scale: 1f,
                tint: Color.white,
                useUnscaledTime: false,
                source: Vector3.zero,
                target: new Vector3(5f, 0f, 0f)));
            Assert.IsTrue(overridden.Succeeded, overridden.FailureReason);
            root = FindProjectileRoot(VfxProjectilePresetIds.BoneShard);
            Assert.AreEqual(5, CountShots(root.transform), "fps=5 应覆盖弹道数量。");
            player.Cancel();
        }

        [Test]
        public void PulsePlayer_MissingTarget_FallsBackToPreviewFlight()
        {
            var factory = new VfxProjectilePlayerFactory();
            factory.TryCreatePulsePlayer(VfxPlayerRegistry.Projectile, out var player, out _);

            var result = player.StartPulse(BuildPulseRequest(
                VfxProjectilePresetIds.MagicBolt,
                fps: 0f,
                scale: 1f,
                tint: Color.white,
                useUnscaledTime: false,
                source: new Vector3(1f, 2f, 0f),
                target: null));
            Assert.IsTrue(result.Succeeded, "缺靶坐标应退化为演示飞行而非失败：" + result.FailureReason);
            Assert.IsTrue(result.PresentationPlan.IsValid);
            player.Cancel();
        }

        [Test]
        public void PulsePlayer_UnknownPreset_AndParticlePresetKey_AreRejected()
        {
            var factory = new VfxProjectilePlayerFactory();
            factory.TryCreatePulsePlayer(VfxPlayerRegistry.Projectile, out var player, out _);

            var unknown = player.StartPulse(BuildPulseRequest(
                "projectile.not_exists", 0f, 1f, Color.white, false, Vector3.zero, Vector3.right));
            Assert.IsFalse(unknown.Succeeded);

            var particleKey = player.StartPulse(BuildPulseRequest(
                VfxParticlePresetIds.HitSpark, 0f, 1f, Color.white, false, Vector3.zero, Vector3.right));
            Assert.IsFalse(particleKey.Succeeded, "粒子预设键不可用于弹道播放器。");
        }

        [Test]
        public void PulsePlayer_Cancel_TearsDownEverything()
        {
            var factory = new VfxProjectilePlayerFactory();
            factory.TryCreatePulsePlayer(VfxPlayerRegistry.Projectile, out var player, out _);
            Assert.IsFalse(factory.TryCreateStatePlayer(VfxPlayerRegistry.Projectile, out _, out _), "弹道播放器无 State 形态。");

            var result = player.StartPulse(BuildPulseRequest(
                VfxProjectilePresetIds.Fireball,
                fps: 0f,
                scale: 1f,
                tint: Color.white,
                useUnscaledTime: false,
                source: Vector3.zero,
                target: new Vector3(5f, 2f, 0f)));
            Assert.IsTrue(result.Succeeded, result.FailureReason);
            Assert.IsNotNull(FindProjectileRoot(VfxProjectilePresetIds.Fireball));

            player.Cancel();
            Assert.IsNull(FindProjectileRoot(VfxProjectilePresetIds.Fireball), "Cancel 后应拆除全部弹道对象。");
            Assert.IsTrue(player.Tick(0.016f));
        }

        [Test]
        public void HygieneValidator_ChecksProjectilePresetKeys()
        {
            var catalog = new VfxBindingCatalogDto
            {
                schemaVersion = 1,
                cueBindings = new[]
                {
                    BuildCueDto("vfx.test.ok", VfxProjectilePresetIds.ArrowShot),
                    BuildCueDto("vfx.test.unknown", "projectile.not_exists"),
                    BuildCueDto("vfx.test.particle_key", VfxParticlePresetIds.HitSpark),
                },
                stateBindings = new[]
                {
                    BuildStateDto("vfx.state.projectile", VfxProjectilePresetIds.ArrowShot),
                },
            };

            var declaredCues = new HashSet<string>(
                new[] { "vfx.test.ok", "vfx.test.unknown", "vfx.test.particle_key" });
            var declaredStates = new HashSet<string>(new[] { "vfx.state.projectile" });
            var materials = new HashSet<string>();

            var cueFindings = VfxBindingCatalogHygieneValidator.ValidateCueBindings(catalog, declaredCues, materials);
            var stateFindings = VfxBindingCatalogHygieneValidator.ValidateStateBindings(catalog, declaredStates, materials);

            Assert.IsFalse(
                cueFindings.Any(f => f.IdentityId == "vfx.test.ok" && f.Category == "missing-material"),
                "合法弹道 Pulse 绑定不应报素材缺失。");
            Assert.IsTrue(
                cueFindings.Any(f => f.IdentityId == "vfx.test.unknown" && f.Category == "missing-material"),
                "未知弹道预设键应报 missing-material。");
            Assert.IsTrue(
                cueFindings.Any(f => f.IdentityId == "vfx.test.particle_key" && f.Category == "missing-material"),
                "粒子预设键挂弹道播放器应报错。");
            Assert.IsTrue(
                stateFindings.Any(f => f.IdentityId == "vfx.state.projectile" && f.Category == "missing-player"),
                "projectile 挂 State 绑定应报 player 不支持 State。");
        }

        private static VfxCueBindingDto BuildCueDto(string cueId, string presetKey)
        {
            return new VfxCueBindingDto
            {
                cueId = cueId,
                note = "测试",
                module = "Test",
                enabled = true,
                playerId = VfxPlayerRegistry.Projectile,
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
                playerId = VfxPlayerRegistry.Projectile,
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
            bool useUnscaledTime,
            Vector3 source,
            Vector3? target)
        {
            var binding = new VfxCueBinding(new VfxCueBindingDto
            {
                cueId = "vfx.test.projectile",
                enabled = true,
                playerId = VfxPlayerRegistry.Projectile,
                spatialOwnership = "independent",
                materialKey = presetKey,
            });
            return new VfxPulseStartRequest(
                VfxCueRequest.Simple("vfx.test.projectile", "test"),
                binding,
                "default",
                presetKey,
                fps,
                1f,
                scale,
                0f,
                tint,
                useUnscaledTime,
                new VfxSpatialContext("test", null, source, 0, 0, target));
        }

        private static GameObject FindProjectileRoot(string presetId)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (transform != null && transform.gameObject.name == "vfx-projectile-" + presetId)
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        private static int CountShots(Transform root)
        {
            var count = 0;
            for (var i = 0; i < root.childCount; i++)
            {
                if (root.GetChild(i).name.StartsWith("shot-"))
                {
                    count++;
                }
            }

            return count;
        }

        private static Transform FindChildStartingWith(Transform root, string prefix)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (child != root && child.name.StartsWith(prefix))
                {
                    return child;
                }
            }

            return null;
        }
    }
}
