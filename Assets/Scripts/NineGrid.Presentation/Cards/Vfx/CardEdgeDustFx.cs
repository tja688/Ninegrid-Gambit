using System.Collections.Generic;
using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 卡牌边沿尘雾播放入口。算法移植自 LostForSwords <c>DustManagerNode</c>
    ///（小图 + 四边喷射 + 暗/亮双层叠画，非帧动画）。
    /// </summary>
    public static class CardEdgeDustFx
    {
        /// <summary>卡牌落地到牌位时的 Place 喷发。</summary>
        /// <param name="intensityOverride">≥0 时覆盖 Settings 总强度；&lt;0 用 Settings。</param>
        public static void PlayPlace(ManagedCard card, float intensityOverride = -1f)
        {
            if (card?.Transform == null)
            {
                return;
            }

            CardEdgeDustFxRunner.Ensure().PlayPlace(card, intensityOverride);
        }

        /// <summary>任意世界矩形上的 Place 喷发（调试 / 后续花样）。</summary>
        public static void PlayPlaceAt(Vector3 center, Vector2 size, float intensityOverride = -1f)
        {
            CardEdgeDustFxRunner.Ensure().PlayPlaceAt(center, size, intensityOverride);
        }

        public static void SetIntensity(float intensity)
        {
            var settings = CardEdgeDustFxRunner.Ensure().Settings;
            if (settings != null)
            {
                settings.SetIntensity(intensity);
            }
        }
    }

    /// <summary>场景运行时尘雾池：CPU 粒子 + SpriteRenderer 双层绘制。</summary>
    [DisallowMultipleComponent]
    public sealed class CardEdgeDustFxRunner : MonoBehaviour
    {
        private static CardEdgeDustFxRunner sInstance;

        private readonly List<DustParticle> _live = new(128);
        private readonly Stack<SpriteRenderer> _pool = new(128);

        private CardEdgeDustFxSettingsSO _settings;
        private Transform _poolRoot;
        private Sprite _runtimeSprite;
        private bool _loggedMissingSprite;

        public CardEdgeDustFxSettingsSO Settings => _settings;

        public static CardEdgeDustFxRunner Ensure()
        {
            if (sInstance != null)
            {
                return sInstance;
            }

            var existing = FindFirstObjectByType<CardEdgeDustFxRunner>();
            if (existing != null)
            {
                sInstance = existing;
                return sInstance;
            }

            var go = new GameObject("CardEdgeDustFx");
            DontDestroyOnLoad(go);
            sInstance = go.AddComponent<CardEdgeDustFxRunner>();
            return sInstance;
        }

        private void Awake()
        {
            if (sInstance != null && sInstance != this)
            {
                Destroy(gameObject);
                return;
            }

            sInstance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            LoadSettings();
            _poolRoot = new GameObject("_Pool").transform;
            _poolRoot.SetParent(transform, false);
            Prewarm();
        }

        private void OnDestroy()
        {
            if (sInstance == this)
            {
                sInstance = null;
            }

            if (_runtimeSprite != null)
            {
                Destroy(_runtimeSprite);
                _runtimeSprite = null;
            }
        }

        private void Update()
        {
            if (_live.Count == 0)
            {
                return;
            }

            var dt = Time.deltaTime;
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var dp = _live[i];
                var ttlN = dp.StartTtl > 0f ? dp.Ttl / dp.StartTtl : 0f;
                var speed = EaseInQuint(ttlN);
                dp.Pos += new Vector3(dp.Vel.x * speed * dt, dp.Vel.y * speed * dt, 0f);

                if (ttlN > 0.9f)
                {
                    dp.Scale = dp.BaseScale * Mathf.Lerp(1f, 0f, Mathf.InverseLerp(0.9f, 1f, ttlN));
                }
                else
                {
                    // Place：前 90% 寿命用 EaseOutQuad 从满缩到 0（与 LFS 一致：ttlN 高→大，低→小）
                    var t = Mathf.InverseLerp(0f, 0.9f, ttlN);
                    dp.Scale = dp.BaseScale * EaseOutQuad(t);
                }

                dp.Ttl -= dt;
                if (dp.Ttl <= 0f || dp.Scale <= 0.001f)
                {
                    ReleasePair(dp);
                    _live.RemoveAt(i);
                    continue;
                }

                ApplyVisuals(dp);
            }
        }

        public void PlayPlace(ManagedCard card, float intensityOverride)
        {
            if (!TryResolveCardRect(card, out var center, out var size))
            {
                return;
            }

            PlayPlaceAt(center, size, intensityOverride);
        }

        public void PlayPlaceAt(Vector3 center, Vector2 size, float intensityOverride)
        {
            LoadSettings();
            if (_settings == null || !_settings.Enabled)
            {
                return;
            }

            var intensity = intensityOverride >= 0f ? intensityOverride : _settings.Intensity;
            if (intensity <= 0.001f)
            {
                return;
            }

            var sprite = ResolveSprite();
            if (sprite == null)
            {
                if (!_loggedMissingSprite)
                {
                    _loggedMissingSprite = true;
                    Debug.LogWarning("[CardEdgeDustFx] Missing dust sprite. Assign on VFX/CardEdgeDustFx settings.");
                }

                return;
            }

            size.x = Mathf.Max(0.05f, Mathf.Abs(size.x));
            size.y = Mathf.Max(0.05f, Mathf.Abs(size.y));

            var perEdge = Mathf.Max(1, Mathf.RoundToInt(_settings.ParticlesPerEdge * intensity));
            var jitter = _settings.EdgeJitter;
            var ttlRange = _settings.TtlSeconds;
            var scaleRange = _settings.BaseScaleRange;
            var speedRange = _settings.SpeedRange;
            var speedMul = intensity;

            // 单位方：BL → BR → TR → TL（与 LFS cardPath 一致；Unity Y-up 外法线自然正确）
            var cardPath = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            };

            for (var j = 0; j < 4; j++)
            {
                var from = cardPath[j];
                var to = cardPath[(j + 1) % 4];
                var edge = from - to;
                var velFrom = Mathf.Atan2(edge.y, edge.x) + 0.7853982f;   // +45°
                var velTo = Mathf.Atan2(edge.y, edge.x) + 2.3561945f;     // +135°
                var denom = Mathf.Max(1, perEdge - 1);

                for (var k = 0; k < perEdge; k++)
                {
                    if (_live.Count >= _settings.MaxLiveParticles)
                    {
                        return;
                    }

                    var r = new Vector2(
                        (Random.value * 2f - 1f) * jitter,
                        (Random.value * 2f - 1f) * jitter);
                    var dpos = Vector2.Lerp(from, to, k / (float)denom);
                    var velD = LerpAngleRad(velFrom, velTo, k / (float)denom);
                    var vel = new Vector2(Mathf.Cos(velD), Mathf.Sin(velD));
                    var local = dpos - new Vector2(0.5f, 0.5f) + r;
                    var finalPos = new Vector3(
                        center.x + local.x * size.x,
                        center.y + local.y * size.y,
                        center.z);

                    var ttl = Mathf.Lerp(ttlRange.x, ttlRange.y, Random.value);
                    var baseScale = Mathf.Lerp(scaleRange.x, scaleRange.y, Random.value);
                    var speed = Mathf.Lerp(speedRange.x, speedRange.y, Random.value) * speedMul;

                    var dp = RentPair(sprite);
                    dp.StartTtl = ttl;
                    dp.Ttl = ttl;
                    dp.BaseScale = baseScale;
                    dp.Scale = 0f;
                    dp.Pos = finalPos;
                    dp.Vel = vel * speed;
                    ApplyVisuals(dp);
                    _live.Add(dp);
                }
            }
        }

        private void LoadSettings()
        {
            if (_settings != null)
            {
                return;
            }

            _settings = Resources.Load<CardEdgeDustFxSettingsSO>(CardEdgeDustFxSettingsSO.ResourcePath);
            if (_settings == null)
            {
                _settings = ScriptableObject.CreateInstance<CardEdgeDustFxSettingsSO>();
                _settings.name = "CardEdgeDustFxSettings_Runtime";
            }
        }

        private Sprite ResolveSprite()
        {
            if (_settings != null && _settings.DustSprite != null)
            {
                return _settings.DustSprite;
            }

            if (_runtimeSprite != null)
            {
                return _runtimeSprite;
            }

            var tex = Resources.Load<Texture2D>("VFX/particleDust");
            if (tex == null)
            {
                return null;
            }

            _runtimeSprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                32f);
            _runtimeSprite.name = "particleDust_runtime";
            return _runtimeSprite;
        }

        private void Prewarm()
        {
            LoadSettings();
            var sprite = ResolveSprite();
            if (sprite == null || _settings == null)
            {
                return;
            }

            var n = _settings.PrewarmPairs;
            for (var i = 0; i < n; i++)
            {
                var dark = CreateRenderer("DustDark", sprite, _settings.SortingOrder);
                var light = CreateRenderer("DustLight", sprite, _settings.SortingOrder + 1);
                dark.gameObject.SetActive(false);
                light.gameObject.SetActive(false);
                _pool.Push(dark);
                _pool.Push(light);
            }
        }

        private DustParticle RentPair(Sprite sprite)
        {
            var light = RentRenderer("DustLight", sprite, (_settings?.SortingOrder ?? 40) + 1);
            var dark = RentRenderer("DustDark", sprite, _settings?.SortingOrder ?? 40);
            return new DustParticle
            {
                Dark = dark,
                Light = light,
            };
        }

        private SpriteRenderer RentRenderer(string name, Sprite sprite, int order)
        {
            SpriteRenderer sr = null;
            while (_pool.Count > 0 && sr == null)
            {
                sr = _pool.Pop();
            }

            if (sr == null)
            {
                sr = CreateRenderer(name, sprite, order);
            }
            else
            {
                sr.name = name;
                sr.sprite = sprite;
                sr.sortingOrder = order;
                if (_settings != null && !string.IsNullOrEmpty(_settings.SortingLayerName))
                {
                    sr.sortingLayerName = _settings.SortingLayerName;
                }
            }

            sr.gameObject.SetActive(true);
            return sr;
        }

        private void ReleasePair(DustParticle dp)
        {
            if (dp.Dark != null)
            {
                dp.Dark.gameObject.SetActive(false);
                _pool.Push(dp.Dark);
            }

            if (dp.Light != null)
            {
                dp.Light.gameObject.SetActive(false);
                _pool.Push(dp.Light);
            }
        }

        private SpriteRenderer CreateRenderer(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_poolRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            if (_settings != null && !string.IsNullOrEmpty(_settings.SortingLayerName))
            {
                sr.sortingLayerName = _settings.SortingLayerName;
            }

            return sr;
        }

        private void ApplyVisuals(DustParticle dp)
        {
            var worldSize = (_settings != null ? _settings.ParticleWorldSize : 0.22f) * dp.Scale;
            var darkMul = _settings != null ? _settings.DarkLayerScale : 1.25f;
            var darkColor = _settings != null ? _settings.DarkColor : new Color(0.1f, 0.1f, 0.1f, 1f);
            var lightColor = _settings != null ? _settings.LightColor : new Color(0.96f, 0.96f, 0.96f, 1f);

            // Sprite 默认本地尺寸 = sprite.bounds；用 localScale 拉到目标世界尺寸
            var spriteSize = dp.Light != null && dp.Light.sprite != null
                ? dp.Light.sprite.bounds.size
                : Vector3.one;
            var sx = spriteSize.x > 0.0001f ? worldSize / spriteSize.x : worldSize;
            var sy = spriteSize.y > 0.0001f ? worldSize / spriteSize.y : worldSize;

            if (dp.Dark != null)
            {
                dp.Dark.transform.position = dp.Pos;
                dp.Dark.transform.localScale = new Vector3(sx * darkMul, sy * darkMul, 1f);
                dp.Dark.color = darkColor;
            }

            if (dp.Light != null)
            {
                dp.Light.transform.position = dp.Pos;
                dp.Light.transform.localScale = new Vector3(sx, sy, 1f);
                dp.Light.color = lightColor;
            }
        }

        private static bool TryResolveCardRect(ManagedCard card, out Vector3 center, out Vector2 size)
        {
            center = card.Transform.position;
            size = new Vector2(1.2f, 1.6f);

            var renderers = card.Transform.GetComponentsInChildren<SpriteRenderer>(includeInactive: false);
            if (renderers == null || renderers.Length == 0)
            {
                return true;
            }

            var has = false;
            var bounds = new Bounds();
            for (var i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || !r.enabled || r.sprite == null)
                {
                    continue;
                }

                // 跳过我们自己的尘雾（若误挂在卡下）
                if (r.name.StartsWith("Dust"))
                {
                    continue;
                }

                if (!has)
                {
                    bounds = r.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }

            if (!has)
            {
                return true;
            }

            center = bounds.center;
            size = new Vector2(bounds.size.x, bounds.size.y);
            return true;
        }

        private static float LerpAngleRad(float from, float to, float t)
        {
            var delta = Mathf.DeltaAngle(from * Mathf.Rad2Deg, to * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return from + delta * Mathf.Clamp01(t);
        }

        private static float EaseInQuint(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * t * t;
        }

        private static float EaseOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private sealed class DustParticle
        {
            public float StartTtl;
            public float Ttl;
            public float BaseScale;
            public float Scale;
            public Vector3 Pos;
            public Vector2 Vel;
            public SpriteRenderer Dark;
            public SpriteRenderer Light;
        }
    }
}
