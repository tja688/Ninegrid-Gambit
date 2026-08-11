using NineGrid.Content.Vfx;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>粒子发射装配的运行时参数（绑定字段 → 播放语义映射后的结果）。</summary>
    internal readonly struct VfxParticleRigParams
    {
        public VfxParticleRigParams(
            int countOverride,
            float simulationSpeed,
            float scale,
            Color tint,
            bool useUnscaledTime,
            float startDelaySeconds,
            string sortingLayerName,
            int sortingOrder)
        {
            CountOverride = countOverride;
            SimulationSpeed = simulationSpeed;
            Scale = scale;
            Tint = tint;
            UseUnscaledTime = useUnscaledTime;
            StartDelaySeconds = startDelaySeconds;
            SortingLayerName = sortingLayerName;
            SortingOrder = sortingOrder;
        }

        /// <summary>绑定 fps 字段 &gt; 0 时的数量覆盖（Pulse 爆发数 / Loop 每秒发射数）。</summary>
        public int CountOverride { get; }

        public float SimulationSpeed { get; }
        public float Scale { get; }
        public Color Tint { get; }
        public bool UseUnscaledTime { get; }
        public float StartDelaySeconds { get; }
        public string SortingLayerName { get; }
        public int SortingOrder { get; }
    }

    /// <summary>
    /// 程序化 ParticleSystem 装配：把 VfxParticlePreset + 绑定参数拼成一台可播放的发射器。
    /// 不触碰共享相机 / Canvas / 卡级排序塔；空间挂接由播放器经域宿主协商后传入。
    /// </summary>
    internal sealed class VfxParticleEmitterRig
    {
        private const int IndependentBaseOrder = 5000;

        private GameObject mRoot;
        private ParticleSystem mSystem;

        public GameObject Root => mRoot;

        public bool IsFinished
        {
            get
            {
                if (mRoot == null || mSystem == null)
                {
                    return true;
                }

                if (!mSystem.IsAlive(withChildren: true))
                {
                    return true;
                }

                // 暂停态（编辑器 Simulate / 外部暂停）IsAlive 恒真；用粒子数、发射态与时间轴兜底。
                if (mSystem.particleCount > 0 || mSystem.isEmitting)
                {
                    return false;
                }

                var main = mSystem.main;
                return !main.loop && mSystem.time >= main.duration - 0.0001f;
            }
        }

        /// <summary>发射已停且场上无存活粒子（State 退出段排空判定）。</summary>
        public bool IsDrained
        {
            get
            {
                if (mRoot == null || mSystem == null)
                {
                    return true;
                }

                return !mSystem.isEmitting && mSystem.particleCount == 0;
            }
        }

        public static VfxParticleEmitterRig Create(VfxParticlePreset preset, in VfxParticleRigParams rigParams)
        {
            var rig = new VfxParticleEmitterRig();
            rig.Build(preset, rigParams);
            return rig;
        }

        public void Play()
        {
            if (mSystem != null)
            {
                mSystem.Play(withChildren: true);
            }
        }

        /// <summary>停止发射但让已出生粒子自然走完（State 退出段）。</summary>
        public void StopEmitting()
        {
            if (mSystem != null)
            {
                mSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public void Destroy()
        {
            if (mRoot == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(mRoot);
            }
            else
            {
                Object.DestroyImmediate(mRoot);
            }

            mRoot = null;
            mSystem = null;
        }

        /// <summary>把锚点世界坐标挂到父级下（含发射器竖直偏移）；parent 为空时直接用世界位。</summary>
        public void Mount(Transform parent, Vector3 worldPosition, float emitOffsetY)
        {
            if (mRoot == null)
            {
                return;
            }

            var anchored = worldPosition + new Vector3(0f, emitOffsetY, 0f);
            if (parent != null)
            {
                mRoot.transform.SetParent(parent, worldPositionStays: false);
            }

            mRoot.transform.position = anchored;
        }

        private void Build(VfxParticlePreset preset, in VfxParticleRigParams rigParams)
        {
            var scale = rigParams.Scale <= 0f ? 1f : rigParams.Scale;
            mRoot = new GameObject("vfx-particle-" + preset.Id);
            mSystem = mRoot.AddComponent<ParticleSystem>();
            mSystem.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var count = rigParams.CountOverride > 0 ? rigParams.CountOverride : preset.Count;
            count = Mathf.Max(1, count);
            var maxLifetime = Mathf.Max(preset.Lifetime.x, preset.Lifetime.y);

            var main = mSystem.main;
            main.playOnAwake = false;
            main.loop = preset.Loop;
            main.duration = preset.Loop
                ? 2f
                : Mathf.Max(0.05f, preset.EmitWindow) + 0.05f;
            main.startDelay = Mathf.Max(0f, rigParams.StartDelaySeconds);
            main.startLifetime = new ParticleSystem.MinMaxCurve(preset.Lifetime.x, maxLifetime);
            main.startSpeed = BuildStartSpeed(preset, scale);
            main.startSize = new ParticleSystem.MinMaxCurve(
                preset.StartSize.x * scale,
                Mathf.Max(preset.StartSize.x, preset.StartSize.y) * scale);
            main.startColor = BuildStartColor(preset, rigParams.Tint);
            main.gravityModifier = preset.Gravity * scale;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = rigParams.SimulationSpeed <= 0f ? 1f : rigParams.SimulationSpeed;
            main.useUnscaledTime = rigParams.UseUnscaledTime;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = Mathf.Clamp(count * 6, 16, 512);
            if (preset.RandomRotation)
            {
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            }

            ConfigureEmission(preset, count);
            ConfigureShape(preset, scale);
            ConfigureColorOverLifetime(preset);
            ConfigureSizeOverLifetime(preset);
            ConfigureVelocity(preset, scale);
            ConfigureRotationOverLifetime(preset);
            ConfigureNoise(preset, scale);
            ConfigureRenderer(preset, rigParams);
        }

        private void ConfigureEmission(VfxParticlePreset preset, int count)
        {
            var emission = mSystem.emission;
            emission.enabled = true;
            if (preset.Loop)
            {
                emission.rateOverTime = count;
                return;
            }

            emission.rateOverTime = 0f;
            if (preset.EmitWindow > 0f)
            {
                // 错峰：拆 4 波铺满发射窗口，用于彩纸 / 余烬这类持续洒落的一次性演出。
                var perCycle = (short)Mathf.Max(1, Mathf.RoundToInt(count / 4f));
                var burst = new ParticleSystem.Burst(0f, perCycle, perCycle, 4, preset.EmitWindow / 3f);
                emission.SetBursts(new[] { burst });
            }
            else
            {
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)count),
                });
            }
        }

        private void ConfigureShape(VfxParticlePreset preset, float scale)
        {
            var shape = mSystem.shape;
            shape.enabled = true;
            shape.alignToDirection = false;
            switch (preset.Shape)
            {
                case VfxParticleShape.CircleFill:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = Mathf.Max(0.01f, preset.ShapeRadius * scale);
                    shape.radiusThickness = 1f;
                    break;
                case VfxParticleShape.CircleEdge:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = Mathf.Max(0.01f, preset.ShapeRadius * scale);
                    shape.radiusThickness = 0f;
                    break;
                case VfxParticleShape.UpCone:
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = preset.ConeAngle;
                    shape.radius = Mathf.Max(0.01f, preset.ShapeRadius * scale);
                    // 默认锥轴朝 +Z；旋到 +Y 才是 2D 的「向上喷」。
                    shape.rotation = new Vector3(-90f, 0f, 0f);
                    break;
                case VfxParticleShape.Box:
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(preset.BoxSize.x * scale, preset.BoxSize.y * scale, 0.01f);
                    break;
                default:
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.01f;
                    shape.radiusThickness = 1f;
                    break;
            }
        }

        private static ParticleSystem.MinMaxCurve BuildStartSpeed(VfxParticlePreset preset, float scale)
        {
            // Box 形状默认发射方向为 +Z（垂直屏幕），出生速度无意义；运动交给漂移/重力/噪声。
            if (preset.Shape == VfxParticleShape.Box)
            {
                return new ParticleSystem.MinMaxCurve(0f);
            }

            return new ParticleSystem.MinMaxCurve(preset.Speed.x * scale, preset.Speed.y * scale);
        }

        private static ParticleSystem.MinMaxGradient BuildStartColor(VfxParticlePreset preset, Color tint)
        {
            if (preset.Palette != null && preset.Palette.Length > 0)
            {
                var gradient = new Gradient();
                var keyCount = Mathf.Min(preset.Palette.Length, 8);
                var colorKeys = new GradientColorKey[keyCount];
                for (var i = 0; i < keyCount; i++)
                {
                    var t = keyCount == 1 ? 0f : i / (float)(keyCount - 1);
                    colorKeys[i] = new GradientColorKey(preset.Palette[i] * tint, t);
                }

                gradient.SetKeys(colorKeys, new[]
                {
                    new GradientAlphaKey(tint.a, 0f),
                    new GradientAlphaKey(tint.a, 1f),
                });
                return new ParticleSystem.MinMaxGradient(gradient)
                {
                    mode = ParticleSystemGradientMode.RandomColor,
                };
            }

            return new ParticleSystem.MinMaxGradient(preset.ColorA * tint, preset.ColorB * tint);
        }

        private void ConfigureColorOverLifetime(VfxParticlePreset preset)
        {
            var colorModule = mSystem.colorOverLifetime;
            colorModule.enabled = true;
            var fadeIn = Mathf.Clamp01(preset.FadeInFraction);
            var gradient = new Gradient();
            var alphaKeys = fadeIn > 0.001f
                ? new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, fadeIn),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                }
                : new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                };
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                alphaKeys);
            colorModule.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        private void ConfigureSizeOverLifetime(VfxParticlePreset preset)
        {
            if (preset.SizeCurve == VfxParticleSizeCurve.Constant)
            {
                return;
            }

            var sizeModule = mSystem.sizeOverLifetime;
            sizeModule.enabled = true;
            AnimationCurve curve;
            switch (preset.SizeCurve)
            {
                case VfxParticleSizeCurve.Grow:
                    curve = new AnimationCurve(
                        new Keyframe(0f, 0.35f),
                        new Keyframe(0.4f, 0.9f),
                        new Keyframe(1f, 1f));
                    break;
                case VfxParticleSizeCurve.Pulse:
                    curve = new AnimationCurve(
                        new Keyframe(0f, 0f),
                        new Keyframe(0.4f, 1f),
                        new Keyframe(1f, 0f));
                    break;
                default:
                    curve = new AnimationCurve(
                        new Keyframe(0f, 1f),
                        new Keyframe(0.7f, 0.55f),
                        new Keyframe(1f, 0f));
                    break;
            }

            sizeModule.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private void ConfigureVelocity(VfxParticlePreset preset, float scale)
        {
            if (preset.RiseVelocity != Vector2.zero)
            {
                var velocity = mSystem.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0f);
                velocity.y = new ParticleSystem.MinMaxCurve(
                    preset.RiseVelocity.x * scale,
                    preset.RiseVelocity.y * scale);
                velocity.z = new ParticleSystem.MinMaxCurve(0f);
            }

            if (preset.Dampen > 0f)
            {
                var limit = mSystem.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.separateAxes = false;
                limit.limit = new ParticleSystem.MinMaxCurve(0f);
                limit.dampen = Mathf.Clamp01(preset.Dampen);
            }
        }

        private void ConfigureRotationOverLifetime(VfxParticlePreset preset)
        {
            if (preset.SpinSpeed == Vector2.zero)
            {
                return;
            }

            var rotation = mSystem.rotationOverLifetime;
            rotation.enabled = true;
            var maxRadians = Mathf.Max(preset.SpinSpeed.x, preset.SpinSpeed.y) * Mathf.Deg2Rad;
            rotation.z = new ParticleSystem.MinMaxCurve(-maxRadians, maxRadians);
        }

        private void ConfigureNoise(VfxParticlePreset preset, float scale)
        {
            if (preset.NoiseStrength <= 0f)
            {
                return;
            }

            var noise = mSystem.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(preset.NoiseStrength * scale);
            noise.frequency = Mathf.Max(0.01f, preset.NoiseFrequency);
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }

        private void ConfigureRenderer(VfxParticlePreset preset, in VfxParticleRigParams rigParams)
        {
            var renderer = mRoot.GetComponent<ParticleSystemRenderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sharedMaterial = VfxParticleTextureBank.GetMaterial(preset.Texture, preset.Blend);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingLayerName = string.IsNullOrEmpty(rigParams.SortingLayerName)
                ? "Main"
                : rigParams.SortingLayerName;
            renderer.sortingOrder = rigParams.SortingOrder + preset.SortingOrderDelta;
        }

        /// <summary>
        /// 按空间所有权解析挂接目标：附着型经域宿主取父级与排序边界；独立型固定世界位。
        /// 返回 false 时给出结构化失败原因（域宿主缺父级等）。
        /// </summary>
        public static bool TryResolveMount(
            VfxSpatialOwnership ownership,
            VfxSpatialContext spatial,
            out Transform parent,
            out Vector3 worldPosition,
            out string sortingLayer,
            out int sortingOrder,
            out string failureReason)
        {
            parent = null;
            worldPosition = spatial.PositionSnapshot ?? Vector3.zero;
            sortingLayer = "Main";
            sortingOrder = IndependentBaseOrder;
            failureReason = string.Empty;

            if (ownership == VfxSpatialOwnership.Attached && spatial.DomainHost != null)
            {
                var host = spatial.DomainHost;
                if (host.TryGetFollowTarget(out var follow) && follow != null)
                {
                    parent = follow;
                }
                else if (host.AttachmentParent != null)
                {
                    parent = host.AttachmentParent;
                }

                if (parent == null)
                {
                    failureReason = "视觉域宿主缺少附着父级。";
                    return false;
                }

                if (host.TryGetSortingBounds(out var bounds) && bounds.HasLayer)
                {
                    sortingLayer = bounds.SortingLayerName;
                    sortingOrder = bounds.ClampOrder(IndependentBaseOrder);
                }

                return true;
            }

            parent = VfxIndependentSpatialRoot.Root;
            return true;
        }
    }
}
