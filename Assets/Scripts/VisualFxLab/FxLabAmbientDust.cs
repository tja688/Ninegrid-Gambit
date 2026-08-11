using UnityEngine;

namespace NineGrid.VisualFxLab
{
    /// <summary>
    /// 画面实验室模块：全场氛围粒子（F7）。
    /// 两套 ParticleSystem 挂在主相机视野平面上——
    /// 「浮尘」：半透明暖白微尘，缓慢漂浮，像烛光房间里的空气感；
    /// 「余烬」：加色暖橙火星，稀疏上升，HDR 亮度配合辉光 Look 会自然发光。
    /// 关闭（OnDisable）即整棵销毁，不留任何痕迹。
    /// </summary>
    public sealed class FxLabAmbientDust : MonoBehaviour
    {
        GameObject _root;
        Material _dustMaterial;
        Material _emberMaterial;

        void OnEnable()
        {
            var camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            _root = new GameObject("FxLab_AmbientDust");
            _root.transform.SetParent(camera.transform, false);
            _root.transform.localPosition = new Vector3(0f, 0f, -camera.transform.position.z);

            float height = camera.orthographic ? camera.orthographicSize * 2f : 18f;
            float width = height * Mathf.Max(camera.aspect, 1f) + 4f;

            _dustMaterial = FxLabRuntimeAssets.CreateParticleMaterial(
                FxLabRuntimeAssets.SoftCircle, additive: false, "FxLab_DustMat");
            _emberMaterial = FxLabRuntimeAssets.CreateParticleMaterial(
                FxLabRuntimeAssets.SoftCircle, additive: true, "FxLab_EmberMat");

            CreateDust(width, height + 2f);
            CreateEmbers(width, height + 2f);
        }

        void OnDisable()
        {
            if (_root != null)
            {
                Destroy(_root);
                _root = null;
            }

            if (_dustMaterial != null)
            {
                Destroy(_dustMaterial);
                _dustMaterial = null;
            }

            if (_emberMaterial != null)
            {
                Destroy(_emberMaterial);
                _emberMaterial = null;
            }
        }

        ParticleSystem CreateSystem(string name, float width, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, height, 0.1f);

            return ps;
        }

        void CreateDust(float width, float height)
        {
            var ps = CreateSystem("Dust", width, height);

            var main = ps.main;
            main.maxParticles = 70;
            main.startLifetime = new ParticleSystem.MinMaxCurve(12f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.96f, 0.88f, 1f), new Color(0.92f, 0.88f, 0.78f, 1f));

            var emission = ps.emission;
            emission.rateOverTime = 3.4f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // 速度模块所有曲线须同一模式，orbital/z 也显式给 TwoConstants
            velocity.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.16f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.06f, 0.08f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.07f;
            noise.scrollSpeed = 0.04f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOutGradient(0.42f);

            ConfigureRenderer(ps, _dustMaterial, "UI", 260);
            ps.Play();
        }

        void CreateEmbers(float width, float height)
        {
            var ps = CreateSystem("Embers", width, height);

            var main = ps.main;
            main.maxParticles = 24;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.7f, 0.75f, 0.22f, 1f), new Color(1.9f, 0.4f, 0.12f, 1f));

            var emission = ps.emission;
            emission.rateOverTime = 1.4f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.1f, 0.34f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.12f;
            noise.scrollSpeed = 0.08f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeInOutGradient(1f);

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 0.6f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0.25f)));

            ConfigureRenderer(ps, _emberMaterial, "Main", 140);
            ps.Play();
        }

        static void ConfigureRenderer(
            ParticleSystem ps, Material material, string sortingLayer, int order)
        {
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = material;
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = order;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static ParticleSystem.MinMaxGradient FadeInOutGradient(float peakAlpha)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, 0.18f),
                    new GradientAlphaKey(peakAlpha, 0.6f),
                    new GradientAlphaKey(0f, 1f),
                });
            return new ParticleSystem.MinMaxGradient(gradient);
        }
    }
}
