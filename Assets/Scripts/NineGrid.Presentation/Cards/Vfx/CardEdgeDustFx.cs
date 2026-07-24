using System.Collections.Generic;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Vfx
{
    /// <summary>
    /// 卡牌边沿尘雾：Place 落地喷发 + Trail 飞牌双尾迹。
    /// 算法移植自 LostForSwords <c>DustManagerNode</c>（小图叠色，非帧动画）。
    /// </summary>
    public static class CardEdgeDustFx
    {
        public static void PlayPlace(ManagedCard card, float intensityOverride = -1f)
        {
            if (card?.Transform == null)
            {
                return;
            }

            CardEdgeDustFxRunner.Ensure().PlayPlace(card, intensityOverride);
        }

        public static void PlayPlaceAt(Vector3 center, Vector2 size, float intensityOverride = -1f)
        {
            CardEdgeDustFxRunner.Ensure().PlayPlaceAt(center, size, intensityOverride);
        }

        /// <summary>飞牌出场开始：跟随卡牌洒双尾迹，粒子先后消散形成连续轨迹。</summary>
        public static void BeginTrail(ManagedCard card)
        {
            if (card?.Transform == null)
            {
                return;
            }

            CardEdgeDustFxRunner.Ensure().BeginTrail(card);
        }

        public static void EndTrail(ManagedCard card)
        {
            if (card == null)
            {
                return;
            }

            CardEdgeDustFxRunner.Ensure().EndTrail(card.Uid);
        }

        public static void SetIntensity(float intensity)
        {
            var settings = CardEdgeDustFxRunner.Ensure().Settings;
            settings?.SetIntensity(intensity);
        }
    }

    [DisallowMultipleComponent]
    public sealed class CardEdgeDustFxRunner : MonoBehaviour
    {
        private enum DustKind
        {
            Place,
            Move,
        }

        private static readonly Vector2[] CardCorners =
        {
            new(0f, 0f),
            new(1f, 0f),
            new(1f, 1f),
            new(0f, 1f),
        };

        private static CardEdgeDustFxRunner sInstance;

        private readonly List<DustParticle> _live = new(256);
        private readonly Stack<SpriteRenderer> _pool = new(256);
        private readonly Dictionary<int, TrailState> _trails = new(16);

        private CardEdgeDustFxSettingsSO _settings;
        private Transform _poolRoot;
        private Sprite _runtimeSprite;
        private bool _loggedMissingSprite;
        private bool _loggedFirstPlay;

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

            EnsureInfrastructure();
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
            UpdateTrails();
            UpdateParticles();
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
            EnsureInfrastructure();
            if (_settings == null || !_settings.Enabled)
            {
                return;
            }

            center.z = 0f;
            var intensity = ResolveIntensity(intensityOverride);
            if (intensity <= 0.001f)
            {
                return;
            }

            var sprite = ResolveSprite();
            if (sprite == null)
            {
                LogMissingSpriteOnce();
                return;
            }

            size.x = Mathf.Max(0.05f, Mathf.Abs(size.x));
            size.y = Mathf.Max(0.05f, Mathf.Abs(size.y));

            var perEdge = Mathf.Max(1, Mathf.RoundToInt(_settings.ParticlesPerEdge * intensity));
            if (!_loggedFirstPlay)
            {
                _loggedFirstPlay = true;
                Debug.Log(
                    $"[CardEdgeDustFx] PlayPlace center={center} size={size} perEdge={perEdge} intensity={intensity:0.##}");
            }

            var jitter = _settings.EdgeJitter;
            var ttlRange = _settings.PlaceTtlSeconds;
            var scaleRange = _settings.PlaceBaseScaleRange;
            var speedRange = _settings.PlaceSpeedRange;

            for (var j = 0; j < 4; j++)
            {
                var from = CardCorners[j];
                var to = CardCorners[(j + 1) % 4];
                var edge = from - to;
                var velFrom = Mathf.Atan2(edge.y, edge.x) + 0.7853982f;
                var velTo = Mathf.Atan2(edge.y, edge.x) + 2.3561945f;
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
                        0f);

                    SpawnParticle(
                        sprite,
                        DustKind.Place,
                        finalPos,
                        vel * Mathf.Lerp(speedRange.x, speedRange.y, Random.value) * intensity,
                        Mathf.Lerp(ttlRange.x, ttlRange.y, Random.value),
                        Mathf.Lerp(scaleRange.x, scaleRange.y, Random.value));
                }
            }
        }

        public void BeginTrail(ManagedCard card)
        {
            EnsureInfrastructure();
            if (_settings == null || !_settings.Enabled || !_settings.TrailEnabled || card?.Transform == null)
            {
                return;
            }

            var pos = SlotFrameConvergence.GetVisualWorldPosition(card);
            pos.z = 0f;
            TryResolveCardRect(card, out _, out var size);
            _trails[card.Uid] = new TrailState
            {
                Card = card,
                LastPos = pos,
                Carry = 0f,
                Size = size,
            };
        }

        public void EndTrail(int uid)
        {
            _trails.Remove(uid);
        }

        private void UpdateTrails()
        {
            if (_trails.Count == 0 || _settings == null || !_settings.Enabled || !_settings.TrailEnabled)
            {
                return;
            }

            var intensity = _settings.Intensity * _settings.TrailIntensity;
            if (intensity <= 0.001f)
            {
                return;
            }

            var sprite = ResolveSprite();
            if (sprite == null)
            {
                LogMissingSpriteOnce();
                return;
            }

            var emitDist = _settings.TrailEmitDistance;
            var dead = (List<int>)null;
            foreach (var kv in _trails)
            {
                var state = kv.Value;
                var card = state.Card;
                if (card?.Transform == null)
                {
                    dead ??= new List<int>();
                    dead.Add(kv.Key);
                    continue;
                }

                var pos = SlotFrameConvergence.GetVisualWorldPosition(card);
                pos.z = 0f;
                var delta = (Vector2)(pos - state.LastPos);
                var dist = delta.magnitude;
                if (dist < 0.0001f)
                {
                    continue;
                }

                state.Carry += dist;
                state.LastPos = pos;
                TryResolveCardRect(card, out _, out state.Size);

                if (state.Carry < emitDist)
                {
                    continue;
                }

                var steps = Mathf.FloorToInt(state.Carry / emitDist);
                state.Carry -= steps * emitDist;
                var velDir = delta / dist;
                for (var s = 0; s < steps; s++)
                {
                    EmitMoveAt(sprite, pos, state.Size, velDir, intensity);
                }
            }

            if (dead == null)
            {
                return;
            }

            for (var i = 0; i < dead.Count; i++)
            {
                _trails.Remove(dead[i]);
            }
        }

        /// <summary>
        /// LFS Move：沿飞行反方向的两个尾角洒粒子，形成双路径；TTL 拉开后先后消散。
        /// </summary>
        private void EmitMoveAt(
            Sprite sprite,
            Vector3 center,
            Vector2 size,
            Vector2 velDir,
            float intensity)
        {
            size.x = Mathf.Max(0.05f, Mathf.Abs(size.x));
            size.y = Mathf.Max(0.05f, Mathf.Abs(size.y));

            var perCorner = Mathf.Max(1, Mathf.RoundToInt(_settings.TrailParticlesPerCorner * intensity));
            var inward = _settings.TrailInwardJitter;
            var ttlRange = _settings.TrailTtlSeconds;
            var scaleRange = _settings.TrailBaseScaleRange;
            var speed = _settings.TrailSpeed * intensity;

            for (var c = 0; c < CardCorners.Length; c++)
            {
                var corner = CardCorners[c];
                var fromCenter = corner - new Vector2(0.5f, 0.5f);
                // 尾部两角：相对卡心在速度反方向一侧
                if (Vector2.Dot(fromCenter, velDir) >= -0.01f)
                {
                    continue;
                }

                for (var m = 0; m < perCorner; m++)
                {
                    if (_live.Count >= _settings.MaxLiveParticles)
                    {
                        return;
                    }

                    var pull = Random.value * inward;
                    var dpos = Vector2.Lerp(corner, new Vector2(0.5f, 0.5f), pull);
                    // 沿轨迹略向后错开，强化「先飞出的先淡」的连续感
                    var along = -velDir * (0.04f * m + Random.value * 0.03f);
                    var r = new Vector2(
                        (Random.value - 0.5f) * 0.08f,
                        (Random.value - 0.5f) * 0.08f);
                    var local = dpos - new Vector2(0.5f, 0.5f) + r + along;
                    var finalPos = new Vector3(
                        center.x + local.x * size.x,
                        center.y + local.y * size.y,
                        0f);

                    // 越靠后的粒子 TTL 略长 → 尾迹从靠近卡牌一侧先淡
                    var ttlT = Mathf.Clamp01((m + Random.value) / Mathf.Max(1, perCorner));
                    var ttl = Mathf.Lerp(ttlRange.x, ttlRange.y, ttlT);

                    SpawnParticle(
                        sprite,
                        DustKind.Move,
                        finalPos,
                        velDir * speed,
                        ttl,
                        Mathf.Lerp(scaleRange.x, scaleRange.y, Random.value));
                }
            }
        }

        private void UpdateParticles()
        {
            if (_live.Count == 0)
            {
                return;
            }

            var dt = Time.deltaTime;
            for (var i = _live.Count - 1; i >= 0; i--)
            {
                var dp = _live[i];
                dp.Ttl -= dt;
                if (dp.Ttl <= 0f)
                {
                    ReleasePair(dp);
                    _live.RemoveAt(i);
                    continue;
                }

                var ttlN = dp.StartTtl > 0f ? dp.Ttl / dp.StartTtl : 0f;
                var speed = EaseInQuint(ttlN);
                dp.Pos += new Vector3(dp.Vel.x * speed * dt, dp.Vel.y * speed * dt, 0f);
                dp.Scale = dp.Kind == DustKind.Move
                    ? EvaluateMoveScale(dp.BaseScale, ttlN)
                    : EvaluatePlaceScale(dp.BaseScale, ttlN);
                ApplyVisuals(dp);
            }
        }

        private void SpawnParticle(
            Sprite sprite,
            DustKind kind,
            Vector3 pos,
            Vector2 vel,
            float ttl,
            float baseScale)
        {
            ttl = Mathf.Max(0.05f, ttl);
            var dp = RentPair(sprite);
            dp.Kind = kind;
            dp.StartTtl = ttl;
            dp.Ttl = ttl * 0.99f;
            dp.BaseScale = baseScale;
            dp.Scale = kind == DustKind.Move
                ? EvaluateMoveScale(baseScale, 0.99f)
                : EvaluatePlaceScale(baseScale, 0.99f);
            dp.Pos = pos;
            dp.Vel = vel;
            ApplyVisuals(dp);
            _live.Add(dp);
        }

        private static float EvaluatePlaceScale(float baseScale, float ttlN)
        {
            if (ttlN > 0.9f)
            {
                return baseScale * Mathf.Lerp(1f, 0f, Mathf.InverseLerp(0.9f, 1f, ttlN));
            }

            var t = Mathf.InverseLerp(0f, 0.9f, ttlN);
            return baseScale * EaseOutQuad(t);
        }

        private static float EvaluateMoveScale(float baseScale, float ttlN)
        {
            if (ttlN > 0.9f)
            {
                return baseScale * Mathf.Lerp(1f, 0f, Mathf.InverseLerp(0.9f, 1f, ttlN));
            }

            var t = Mathf.InverseLerp(0f, 0.9f, ttlN);
            return baseScale * EaseInOutCubic(t);
        }

        private float ResolveIntensity(float intensityOverride) =>
            intensityOverride >= 0f ? intensityOverride : (_settings != null ? _settings.Intensity : 1f);

        private void EnsureInfrastructure()
        {
            if (_poolRoot == null)
            {
                var existing = transform.Find("_Pool");
                _poolRoot = existing != null ? existing : new GameObject("_Pool").transform;
                _poolRoot.SetParent(transform, false);
            }

            LoadSettings();
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

        private void LogMissingSpriteOnce()
        {
            if (_loggedMissingSprite)
            {
                return;
            }

            _loggedMissingSprite = true;
            Debug.LogWarning("[CardEdgeDustFx] Missing dust sprite. Assign on VFX/CardEdgeDustFx settings.");
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
            var order = _settings != null ? _settings.SortingOrder : 120;
            var light = RentRenderer("DustLight", sprite, order + 1);
            var dark = RentRenderer("DustDark", sprite, order);
            return new DustParticle { Dark = dark, Light = light };
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
            var worldSize = (_settings != null ? _settings.ParticleWorldSize : 0.35f) * dp.Scale;
            var darkMul = _settings != null ? _settings.DarkLayerScale : 1.25f;
            var darkColor = _settings != null ? _settings.DarkColor : new Color(0.1f, 0.1f, 0.1f, 1f);
            var lightColor = _settings != null ? _settings.LightColor : new Color(0.96f, 0.96f, 0.96f, 1f);

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
                if (r == null || !r.enabled || r.sprite == null || r.name.StartsWith("Dust"))
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

        private static float EaseInOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        private sealed class TrailState
        {
            public ManagedCard Card;
            public Vector3 LastPos;
            public float Carry;
            public Vector2 Size;
        }

        private sealed class DustParticle
        {
            public DustKind Kind;
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
