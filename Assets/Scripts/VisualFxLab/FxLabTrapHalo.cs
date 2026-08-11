using System.Collections.Generic;
using NineGrid.Cards;
using UnityEngine;

namespace NineGrid.VisualFxLab
{
    /// <summary>
    /// 画面实验室模块：机关卡光环环绕（F6）。
    /// 定期扫描场上机关卡（<see cref="CardPresentationKind.Trap"/> 且在场），
    /// 在卡面根下挂一套「呼吸底环（卡后） + 轨道火花（卡前）」的光环 rig，
    /// 颜色按机关 defId 区分（离开=翠绿 / 烈焰=橙红 / 治疗泉=水青 / 复活石=紫 / 其余=暖金）。
    /// 只添加渲染子物体，不触碰卡牌 L0–L3 变换塔与排序黑盒（仅用组内相对 order）。
    /// </summary>
    public sealed class FxLabTrapHalo : MonoBehaviour
    {
        const float ScanInterval = 0.6f;

        sealed class HaloRig
        {
            public GameObject Root;
            public SpriteRenderer Ring;
            public Transform RingTransform;
            public float Phase;
            public Color BaseColor;
        }

        readonly Dictionary<int, HaloRig> _rigs = new();
        readonly List<int> _removeBuffer = new();

        CardManagerSingleton _cardManager;
        Material _ringMaterial;
        Material _sparkMaterial;
        float _nextScanAt;

        void OnEnable()
        {
            _ringMaterial = FxLabRuntimeAssets.CreateParticleMaterial(
                FxLabRuntimeAssets.Ring, additive: true, "FxLab_TrapRingMat");
            _sparkMaterial = FxLabRuntimeAssets.CreateParticleMaterial(
                FxLabRuntimeAssets.Sparkle, additive: true, "FxLab_TrapSparkMat");
            _nextScanAt = 0f;
        }

        void OnDisable()
        {
            foreach (var rig in _rigs.Values)
            {
                if (rig.Root != null)
                {
                    Destroy(rig.Root);
                }
            }

            _rigs.Clear();

            if (_ringMaterial != null)
            {
                Destroy(_ringMaterial);
                _ringMaterial = null;
            }

            if (_sparkMaterial != null)
            {
                Destroy(_sparkMaterial);
                _sparkMaterial = null;
            }
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextScanAt)
            {
                _nextScanAt = Time.unscaledTime + ScanInterval;
                Scan();
            }

            AnimateRigs();
        }

        void Scan()
        {
            if (_cardManager == null)
            {
                _cardManager = FindFirstObjectByType<CardManagerSingleton>(FindObjectsInactive.Include);
                if (_cardManager == null)
                {
                    return;
                }
            }

            foreach (var pair in _cardManager.CardsByUid)
            {
                var card = pair.Value;
                if (card == null || card.View == null)
                {
                    continue;
                }

                bool wantHalo = card.CoreKind == CardPresentationKind.Trap
                    && card.DisplayMode == CardDisplayMode.GroundCardMode
                    && !card.IsFieldDead
                    && card.CommittedPresentation?.FaceUp != false
                    && card.GameObject.activeInHierarchy;

                if (wantHalo)
                {
                    if (!_rigs.ContainsKey(pair.Key))
                    {
                        var rig = CreateRig(card);
                        if (rig != null)
                        {
                            _rigs.Add(pair.Key, rig);
                        }
                    }
                }
                else if (_rigs.TryGetValue(pair.Key, out var existing))
                {
                    if (existing.Root != null)
                    {
                        Destroy(existing.Root);
                    }

                    _rigs.Remove(pair.Key);
                }
            }

            _removeBuffer.Clear();
            foreach (var pair in _rigs)
            {
                if (pair.Value.Root == null
                    || !_cardManager.CardsByUid.ContainsKey(pair.Key))
                {
                    _removeBuffer.Add(pair.Key);
                }
            }

            foreach (int uid in _removeBuffer)
            {
                var rig = _rigs[uid];
                if (rig.Root != null)
                {
                    Destroy(rig.Root);
                }

                _rigs.Remove(uid);
            }
        }

        HaloRig CreateRig(ManagedCard card)
        {
            var parent = card.MountedFaceRoot != null ? card.MountedFaceRoot : card.Transform;
            if (parent == null)
            {
                return null;
            }

            var color = ResolveColor(card.DefId);

            var root = new GameObject("FxLab_TrapHalo");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;

            // 底环：卡身之后（组内 order 压低），呼吸 + 慢转
            var ringGo = new GameObject("Ring");
            ringGo.transform.SetParent(root.transform, false);
            var ring = ringGo.AddComponent<SpriteRenderer>();
            ring.sprite = FxLabRuntimeAssets.RingSprite;
            ring.sharedMaterial = _ringMaterial;
            ring.color = new Color(color.r, color.g, color.b, 0.4f);
            ring.sortingLayerName = "Main";
            ring.sortingOrder = -60;

            // 轨道火花：沿卡缘圆环公转，卡前
            var sparkGo = new GameObject("OrbitSparks");
            sparkGo.transform.SetParent(root.transform, false);
            var ps = sparkGo.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.useUnscaledTime = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startSpeed = 0f;
            main.maxParticles = 42;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                color, Color.Lerp(color, Color.white, 0.45f));

            var emission = ps.emission;
            emission.rateOverTime = 7f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 1.02f;
            shape.radiusThickness = 0.08f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // 速度模块所有曲线须同一模式，统一用 Constant
            velocity.x = new ParticleSystem.MinMaxCurve(0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalX = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalY = new ParticleSystem.MinMaxCurve(0f);
            velocity.orbitalZ = new ParticleSystem.MinMaxCurve(1.25f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
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
                    new GradientAlphaKey(0.85f, 0.2f),
                    new GradientAlphaKey(0f, 1f),
                });
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = _sparkMaterial;
            renderer.sortingLayerName = "Main";
            renderer.sortingOrder = 55;
            ps.Play();

            return new HaloRig
            {
                Root = root,
                Ring = ring,
                RingTransform = ringGo.transform,
                Phase = (card.Uid * 0.618f) % (Mathf.PI * 2f),
                BaseColor = color,
            };
        }

        void AnimateRigs()
        {
            float t = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;
            foreach (var rig in _rigs.Values)
            {
                if (rig.Ring == null)
                {
                    continue;
                }

                float breath = 0.5f + 0.5f * Mathf.Sin(t * 2.3f + rig.Phase);
                var c = rig.BaseColor;
                rig.Ring.color = new Color(c.r, c.g, c.b, 0.26f + 0.22f * breath);
                rig.RingTransform.localScale = Vector3.one * (1f + 0.05f * breath);
                rig.RingTransform.Rotate(0f, 0f, 11f * dt);
            }
        }

        static Color ResolveColor(string defId)
        {
            switch (defId)
            {
                case "trap.leave":
                    return new Color(0.42f, 1f, 0.58f);
                case "trap.flame":
                    return new Color(1f, 0.45f, 0.16f);
                case "trap.healing_spring":
                    return new Color(0.36f, 0.95f, 0.82f);
                case "trap.revive_stone":
                    return new Color(0.74f, 0.5f, 1f);
                default:
                    return new Color(1f, 0.8f, 0.34f);
            }
        }
    }
}
