using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>弹道装配的运行时参数（绑定字段 → 播放语义映射后的结果）。</summary>
    internal readonly struct VfxProjectileRigParams
    {
        public VfxProjectileRigParams(
            int countOverride,
            float speedMultiplier,
            float scale,
            Color tint,
            bool useUnscaledTime,
            float startDelaySeconds,
            string sortingLayerName,
            int sortingOrder)
        {
            CountOverride = countOverride;
            SpeedMultiplier = speedMultiplier;
            Scale = scale;
            Tint = tint;
            UseUnscaledTime = useUnscaledTime;
            StartDelaySeconds = startDelaySeconds;
            SortingLayerName = sortingLayerName;
            SortingOrder = sortingOrder;
        }

        /// <summary>绑定 fps 字段 &gt; 0 时的弹道数量覆盖（夹紧 1~8）。</summary>
        public int CountOverride { get; }

        /// <summary>绑定 speed 字段：飞行与拖尾/爆点模拟的整体时间倍率。</summary>
        public float SpeedMultiplier { get; }

        public float Scale { get; }
        public Color Tint { get; }
        public bool UseUnscaledTime { get; }
        public float StartDelaySeconds { get; }
        public string SortingLayerName { get; }
        public int SortingOrder { get; }
    }

    /// <summary>程序化弹头 Sprite 库：贴图复用 VfxParticleTextureBank，按形状缓存 1 世界单位 Sprite。</summary>
    internal static class VfxProjectileSpriteBank
    {
        private static readonly Dictionary<VfxParticleTexture, Sprite> sSprites =
            new Dictionary<VfxParticleTexture, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sSprites.Clear();
        }

        public static Sprite GetSprite(VfxParticleTexture kind)
        {
            if (sSprites.TryGetValue(kind, out var cached) && cached != null)
            {
                return cached;
            }

            var texture = VfxParticleTextureBank.GetTexture(kind);
            // PPU = 贴图边长 → Sprite 恒为 1 世界单位见方，弹头尺寸交给 localScale。
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                texture.width);
            sprite.name = "VfxProjectileSprite-" + kind;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            sSprites[kind] = sprite;
            return sprite;
        }
    }

    /// <summary>
    /// 程序化弹道装配：把 VfxProjectilePreset + 绑定参数 + 源/靶世界坐标拼成一次「谁打了谁」演出。
    /// 每发 = 弹头 Sprite（可发光/拉伸/自旋）+ 世界空间 rateOverDistance 拖尾粒子；
    /// 起手 / 命中爆点复用 VfxParticleEmitterRig（粒子预设库）。
    /// 不触碰共享相机 / Canvas / 卡级排序塔；空间挂接由播放器经域宿主协商后传入。
    /// </summary>
    internal sealed class VfxProjectileFlightRig
    {
        private const float LaunchGrowSeconds = 0.12f;
        private const float LingerBufferSeconds = 0.1f;

        private VfxProjectilePreset mPreset;
        private VfxProjectileRigParams mParams;
        private GameObject mRoot;
        private readonly List<ShotState> mShots = new List<ShotState>();
        private readonly List<VfxParticleEmitterRig> mBurstRigs = new List<VfxParticleEmitterRig>();
        private float mElapsed;
        private float mFinishAtElapsed;
        private float mScale;
        private float mSpeedMultiplier;

        public GameObject Root => mRoot;

        /// <summary>首发命中延迟（秒，含起手延迟）；供 PresentationPlan。</summary>
        public float FirstArrivalDelay { get; private set; }

        /// <summary>末发命中延迟（秒，含起手延迟与错峰）；供 PresentationPlan。</summary>
        public float LastArrivalDelay { get; private set; }

        public bool IsFinished
        {
            get
            {
                if (mRoot == null)
                {
                    return true;
                }

                for (var i = 0; i < mShots.Count; i++)
                {
                    if (!mShots[i].Arrived)
                    {
                        return false;
                    }
                }

                return mElapsed >= mFinishAtElapsed;
            }
        }

        public static VfxProjectileFlightRig Create(
            VfxProjectilePreset preset,
            in VfxProjectileRigParams rigParams,
            Transform parent,
            Vector3 sourceWorld,
            Vector3 targetWorld)
        {
            var rig = new VfxProjectileFlightRig();
            rig.Build(preset, rigParams, parent, sourceWorld, targetWorld);
            return rig;
        }

        /// <summary>推进弹道；dt 由播放器按 timeBase 换算好后传入。返回 true 表示演出全部收尾。</summary>
        public bool Advance(float deltaTime)
        {
            if (mRoot == null)
            {
                return true;
            }

            mElapsed += Mathf.Max(0f, deltaTime);
            for (var i = 0; i < mShots.Count; i++)
            {
                AdvanceShot(mShots[i]);
            }

            return IsFinished;
        }

        public void Destroy()
        {
            for (var i = 0; i < mBurstRigs.Count; i++)
            {
                mBurstRigs[i]?.Destroy();
            }

            mBurstRigs.Clear();
            mShots.Clear();
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
        }

        private void Build(
            VfxProjectilePreset preset,
            in VfxProjectileRigParams rigParams,
            Transform parent,
            Vector3 sourceWorld,
            Vector3 targetWorld)
        {
            mPreset = preset;
            mParams = rigParams;
            mScale = rigParams.Scale <= 0f ? 1f : rigParams.Scale;
            mSpeedMultiplier = rigParams.SpeedMultiplier <= 0f ? 1f : rigParams.SpeedMultiplier;
            mElapsed = 0f;

            mRoot = new GameObject("vfx-projectile-" + preset.Id);
            if (parent != null)
            {
                mRoot.transform.SetParent(parent, worldPositionStays: false);
            }

            mRoot.transform.position = sourceWorld;

            var count = rigParams.CountOverride > 0 ? rigParams.CountOverride : preset.Count;
            count = Mathf.Clamp(count, 1, 8);

            var startDelay = Mathf.Max(0f, rigParams.StartDelaySeconds);
            FirstArrivalDelay = float.MaxValue;
            LastArrivalDelay = 0f;
            var maxTrailLifetime = preset.TrailPerUnit > 0f
                ? Mathf.Max(preset.TrailLifetime.x, preset.TrailLifetime.y)
                : 0f;

            for (var i = 0; i < count; i++)
            {
                var shot = BuildShot(i, sourceWorld, targetWorld, startDelay);
                mShots.Add(shot);
                var arrival = shot.LaunchDelay + shot.Duration;
                FirstArrivalDelay = Mathf.Min(FirstArrivalDelay, arrival);
                LastArrivalDelay = Mathf.Max(LastArrivalDelay, arrival);
            }

            var lingerSeconds = (maxTrailLifetime + LingerBufferSeconds) / mSpeedMultiplier;
            lingerSeconds = Mathf.Max(lingerSeconds, ResolveImpactLingerSeconds());
            mFinishAtElapsed = LastArrivalDelay + lingerSeconds;

            SpawnMuzzle(sourceWorld, startDelay);
        }

        private ShotState BuildShot(int index, Vector3 sourceWorld, Vector3 targetWorld, float startDelay)
        {
            var shot = new ShotState
            {
                Source = sourceWorld,
                LaunchDelay = startDelay + index * Mathf.Max(0f, mPreset.StaggerSeconds),
                SpinSign = Random.value < 0.5f ? -1f : 1f,
            };

            // 首发精确命中靶心保证可读性，多发时后续弹道在靶位周围散布。
            var target = targetWorld;
            if (index > 0 && mPreset.TargetJitterRadius > 0f)
            {
                var jitter = Random.insideUnitCircle * mPreset.TargetJitterRadius * mScale;
                target += new Vector3(jitter.x, jitter.y, 0f);
            }

            shot.Target = target;

            var flightVector = shot.Target - shot.Source;
            var distance = flightVector.magnitude;
            var speed = Mathf.Max(0.01f, mPreset.FlightSpeed);
            shot.Duration = Mathf.Clamp(distance / speed, mPreset.MinFlightSeconds, mPreset.MaxFlightSeconds)
                / mSpeedMultiplier;
            shot.Duration = Mathf.Max(0.02f, shot.Duration);

            var direction = distance > 0.0001f ? flightVector / distance : Vector3.right;
            var perp = new Vector3(-direction.y, direction.x, 0f);
            if (Mathf.Abs(perp.y) < 0.01f)
            {
                perp *= shot.SpinSign;
            }
            else if (perp.y < 0f)
            {
                perp = -perp;
            }

            shot.Perp = perp;

            var arcHeight = 0f;
            if (mPreset.PathStyle == VfxProjectilePathStyle.Arc)
            {
                arcHeight = mPreset.ArcHeight
                    + Random.Range(-mPreset.ArcHeightJitter, mPreset.ArcHeightJitter);
                arcHeight *= mScale;
            }

            // 二次贝塞尔中点偏移是控制点偏移的一半，故控制点抬 2 倍弧高。
            var midpoint = (shot.Source + shot.Target) * 0.5f;
            shot.Control = midpoint + perp * (arcHeight * 2f);

            BuildShotVisual(shot, index);
            return shot;
        }

        private void BuildShotVisual(ShotState shot, int index)
        {
            var shotRoot = new GameObject("shot-" + index);
            shotRoot.transform.SetParent(mRoot.transform, worldPositionStays: false);
            shotRoot.transform.position = shot.Source;
            shot.ShotRoot = shotRoot;
            shot.LastPosition = shot.Source;

            var baseOrder = mParams.SortingOrder + mPreset.SortingOrderDelta;

            if (mPreset.GlowSize > 0f)
            {
                var glowGo = new GameObject("glow");
                glowGo.transform.SetParent(shotRoot.transform, worldPositionStays: false);
                var glow = glowGo.AddComponent<SpriteRenderer>();
                glow.sprite = VfxProjectileSpriteBank.GetSprite(VfxParticleTexture.SoftDot);
                glow.sharedMaterial = VfxParticleTextureBank.GetMaterial(
                    VfxParticleTexture.SoftDot,
                    VfxParticleBlend.Additive);
                glow.color = mPreset.GlowColor * mParams.Tint;
                glow.sortingLayerName = mParams.SortingLayerName;
                glow.sortingOrder = baseOrder - 1;
                shot.Glow = glow;
            }

            var headGo = new GameObject("head");
            headGo.transform.SetParent(shotRoot.transform, worldPositionStays: false);
            var head = headGo.AddComponent<SpriteRenderer>();
            head.sprite = VfxProjectileSpriteBank.GetSprite(mPreset.HeadTexture);
            head.sharedMaterial = VfxParticleTextureBank.GetMaterial(mPreset.HeadTexture, mPreset.HeadBlend);
            head.color = mPreset.HeadColorA * mParams.Tint;
            head.sortingLayerName = mParams.SortingLayerName;
            head.sortingOrder = baseOrder;
            shot.Head = head;

            if (mPreset.TrailPerUnit > 0f)
            {
                shot.Trail = BuildTrailSystem(shotRoot.transform, shot.Duration, baseOrder - 2);
            }

            ApplyShotTransform(shot, growFactor: 0f, direction: shot.Target - shot.Source, easedT: 0f);
            shotRoot.SetActive(false);
        }

        private ParticleSystem BuildTrailSystem(Transform parent, float flightDuration, int sortingOrder)
        {
            var trailGo = new GameObject("trail");
            trailGo.transform.SetParent(parent, worldPositionStays: false);
            var system = trailGo.AddComponent<ParticleSystem>();
            system.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var preset = mPreset;
            var maxLifetime = Mathf.Max(preset.TrailLifetime.x, preset.TrailLifetime.y);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = Mathf.Max(0.5f, flightDuration + maxLifetime + 0.5f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(preset.TrailLifetime.x, maxLifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                preset.TrailSize.x * mScale,
                Mathf.Max(preset.TrailSize.x, preset.TrailSize.y) * mScale);
            main.startColor = new ParticleSystem.MinMaxGradient(
                preset.TrailColorA * mParams.Tint,
                preset.TrailColorB * mParams.Tint);
            main.gravityModifier = preset.TrailGravity * mScale;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.simulationSpeed = mSpeedMultiplier;
            main.useUnscaledTime = mParams.UseUnscaledTime;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.maxParticles = 256;
            if (preset.TrailRandomRotation)
            {
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            }

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = preset.TrailPerUnit;

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.02f;
            shape.radiusThickness = 1f;

            var colorModule = system.colorOverLifetime;
            colorModule.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorModule.color = new ParticleSystem.MinMaxGradient(gradient);

            if (preset.TrailSizeCurve != VfxParticleSizeCurve.Constant)
            {
                var sizeModule = system.sizeOverLifetime;
                sizeModule.enabled = true;
                AnimationCurve curve;
                switch (preset.TrailSizeCurve)
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

            if (preset.TrailDrift != Vector2.zero)
            {
                var velocity = system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = new ParticleSystem.MinMaxCurve(0f);
                velocity.y = new ParticleSystem.MinMaxCurve(
                    preset.TrailDrift.x * mScale,
                    preset.TrailDrift.y * mScale);
                velocity.z = new ParticleSystem.MinMaxCurve(0f);
            }

            if (preset.TrailNoiseStrength > 0f)
            {
                var noise = system.noise;
                noise.enabled = true;
                noise.strength = new ParticleSystem.MinMaxCurve(preset.TrailNoiseStrength * mScale);
                noise.frequency = Mathf.Max(0.01f, preset.TrailNoiseFrequency);
                noise.damping = true;
                noise.quality = ParticleSystemNoiseQuality.Medium;
            }

            var renderer = trailGo.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.sharedMaterial = VfxParticleTextureBank.GetMaterial(preset.TrailTexture, preset.TrailBlend);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingLayerName = mParams.SortingLayerName;
                renderer.sortingOrder = sortingOrder;
            }

            return system;
        }

        private void SpawnMuzzle(Vector3 sourceWorld, float startDelay)
        {
            if (string.IsNullOrEmpty(mPreset.MuzzlePresetId)
                || !VfxParticlePresetLibrary.TryGet(mPreset.MuzzlePresetId, out var muzzlePreset)
                || muzzlePreset.Loop)
            {
                return;
            }

            var rigParams = new VfxParticleRigParams(
                countOverride: 0,
                simulationSpeed: mSpeedMultiplier,
                scale: mPreset.MuzzleScale * mScale,
                tint: mPreset.MuzzleTint * mParams.Tint,
                useUnscaledTime: mParams.UseUnscaledTime,
                startDelaySeconds: startDelay,
                sortingLayerName: mParams.SortingLayerName,
                sortingOrder: mParams.SortingOrder);
            var rig = VfxParticleEmitterRig.Create(muzzlePreset, rigParams);
            rig.Mount(mRoot.transform, sourceWorld, muzzlePreset.EmitOffsetY * mPreset.MuzzleScale * mScale);
            rig.Play();
            mBurstRigs.Add(rig);
        }

        private void SpawnImpact(Vector3 targetWorld)
        {
            if (string.IsNullOrEmpty(mPreset.ImpactPresetId)
                || !VfxParticlePresetLibrary.TryGet(mPreset.ImpactPresetId, out var impactPreset)
                || impactPreset.Loop)
            {
                return;
            }

            var rigParams = new VfxParticleRigParams(
                countOverride: mPreset.ImpactCountOverride,
                simulationSpeed: mSpeedMultiplier,
                scale: mPreset.ImpactScale * mScale,
                tint: mPreset.ImpactTint * mParams.Tint,
                useUnscaledTime: mParams.UseUnscaledTime,
                startDelaySeconds: 0f,
                sortingLayerName: mParams.SortingLayerName,
                sortingOrder: mParams.SortingOrder);
            var rig = VfxParticleEmitterRig.Create(impactPreset, rigParams);
            rig.Mount(mRoot.transform, targetWorld, impactPreset.EmitOffsetY * mPreset.ImpactScale * mScale);
            rig.Play();
            mBurstRigs.Add(rig);
        }

        private float ResolveImpactLingerSeconds()
        {
            if (string.IsNullOrEmpty(mPreset.ImpactPresetId)
                || !VfxParticlePresetLibrary.TryGet(mPreset.ImpactPresetId, out var impactPreset))
            {
                return 0f;
            }

            var lifetime = Mathf.Max(impactPreset.Lifetime.x, impactPreset.Lifetime.y);
            return (lifetime + impactPreset.EmitWindow + LingerBufferSeconds) / mSpeedMultiplier;
        }

        private void AdvanceShot(ShotState shot)
        {
            if (shot.Arrived || shot.ShotRoot == null)
            {
                return;
            }

            if (!shot.Launched)
            {
                if (mElapsed < shot.LaunchDelay)
                {
                    return;
                }

                shot.Launched = true;
                shot.ShotRoot.SetActive(true);
                if (shot.Trail != null)
                {
                    shot.Trail.Play(withChildren: true);
                }
            }

            var rawT = (mElapsed - shot.LaunchDelay) / shot.Duration;
            if (rawT >= 1f)
            {
                ArriveShot(shot);
                return;
            }

            var easedT = Ease(Mathf.Clamp01(rawT), mPreset.AccelBias);
            var position = EvaluatePosition(shot, easedT);
            shot.ShotRoot.transform.position = position;

            var direction = position - shot.LastPosition;
            if (direction.sqrMagnitude < 0.0000001f)
            {
                direction = shot.Target - shot.Source;
            }

            var growFactor = Mathf.Clamp01((mElapsed - shot.LaunchDelay) / LaunchGrowSeconds);
            ApplyShotTransform(shot, growFactor, direction, easedT);
            shot.LastPosition = position;
        }

        private void ApplyShotTransform(ShotState shot, float growFactor, Vector3 direction, float easedT)
        {
            var grow = Mathf.Lerp(0.45f, 1f, growFactor);
            var headSize = mPreset.HeadSize * mScale * grow;
            if (shot.Head != null)
            {
                if (mPreset.AlignToVelocity)
                {
                    var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                    shot.Head.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                    shot.Head.transform.localScale = new Vector3(
                        headSize * mPreset.HeadStretch.x,
                        headSize * mPreset.HeadStretch.y,
                        1f);
                }
                else
                {
                    if (mPreset.HeadSpinSpeed > 0f)
                    {
                        var spin = mPreset.HeadSpinSpeed * shot.SpinSign * mElapsed * mSpeedMultiplier;
                        shot.Head.transform.localRotation = Quaternion.Euler(0f, 0f, spin);
                    }

                    shot.Head.transform.localScale = new Vector3(headSize, headSize, 1f);
                }

                shot.Head.color = Color.Lerp(mPreset.HeadColorA, mPreset.HeadColorB, easedT) * mParams.Tint;
            }

            if (shot.Glow != null)
            {
                var glowSize = mPreset.GlowSize * mScale * grow;
                shot.Glow.transform.localScale = new Vector3(glowSize, glowSize, 1f);
            }
        }

        private void ArriveShot(ShotState shot)
        {
            shot.Arrived = true;
            shot.ShotRoot.transform.position = shot.Target;
            if (shot.Head != null)
            {
                shot.Head.enabled = false;
            }

            if (shot.Glow != null)
            {
                shot.Glow.enabled = false;
            }

            if (shot.Trail != null)
            {
                shot.Trail.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
            }

            SpawnImpact(shot.Target);
        }

        private Vector3 EvaluatePosition(ShotState shot, float easedT)
        {
            var a = Vector3.Lerp(shot.Source, shot.Control, easedT);
            var b = Vector3.Lerp(shot.Control, shot.Target, easedT);
            var position = Vector3.Lerp(a, b, easedT);
            if (mPreset.PathStyle == VfxProjectilePathStyle.Wave && mPreset.WaveAmplitude > 0f)
            {
                // sin(πt) 包络保证首尾精确贴合源/靶。
                var wave = Mathf.Sin(easedT * Mathf.PI * 2f * mPreset.WaveCycles)
                    * mPreset.WaveAmplitude * mScale
                    * Mathf.Sin(easedT * Mathf.PI);
                position += shot.Perp * wave;
            }

            return position;
        }

        private static float Ease(float t, float bias)
        {
            if (bias > 0.001f)
            {
                return Mathf.Pow(t, 1f + bias * 2f);
            }

            if (bias < -0.001f)
            {
                return 1f - Mathf.Pow(1f - t, 1f - bias * 2f);
            }

            return t;
        }

        private sealed class ShotState
        {
            public Vector3 Source;
            public Vector3 Target;
            public Vector3 Control;
            public Vector3 Perp;
            public Vector3 LastPosition;
            public float LaunchDelay;
            public float Duration;
            public float SpinSign;
            public bool Launched;
            public bool Arrived;
            public GameObject ShotRoot;
            public SpriteRenderer Head;
            public SpriteRenderer Glow;
            public ParticleSystem Trail;
        }
    }
}
