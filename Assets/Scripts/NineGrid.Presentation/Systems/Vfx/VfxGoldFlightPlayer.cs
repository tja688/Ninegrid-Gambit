using System;
using System.Collections.Generic;
using NineGrid.Content.Vfx;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>
    /// gold-flight 程序化独立 Pulse 播放器（#203）：自治飞币生成、数量分配、散开、错峰、
    /// 惯性滑散 → 加速吸入、缩放、排序与软/硬时间窗压缩；终点与图标吞噬经金币 HUD 域宿主取得。
    /// 批次创建成功时一次性返回首达/末达表现计划，不建立逐金币回调。
    /// </summary>
    public sealed class VfxGoldFlightPlayer : IVfxPulsePlayer
    {
        private const string CoinSheetResourcesKey = "ContentArt/Multiple/icons_full_32";
        private const string CoinSpriteName = "icons_full_32_9";
        private const string LegacyCoinSpriteResourcesKey = "VFX/GoldFlightCoin";
        private const string FallbackSortingLayer = "Main";
        private const int FallbackSortingOrder = 20;
        private const float CoinBaseScale = 0.55f;
        private const float CoinBurstPeakScale = 0.72f;
        private const float CoinSettleScale = 0.58f;

        private static long sNextInstanceId = 1;
        private static Sprite sCachedCoinSprite;
        private static Material sFlightMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sNextInstanceId = 1;
            sCachedCoinSprite = null;
            sFlightMaterial = null;
        }

        private readonly List<FlyingCoin> mCoins = new List<FlyingCoin>();
        private readonly List<GameObject> mVisuals = new List<GameObject>();
        private readonly Func<IGoldHudDomainHost> mHostResolver;
        private readonly Func<float> mRandomUnit;

        private Sprite mCoinSprite;
        private IGoldHudDomainHost mHost;
        private string mInstanceId;
        private float mElapsed;
        private int mRemaining;
        private bool mActive;
        private bool mUseUnscaledTime;
        private float mSoftCap;
        private float mHardCap;

        public VfxGoldFlightPlayer(
            Func<IGoldHudDomainHost> hostResolver = null,
            Func<float> randomUnit = null)
        {
            mHostResolver = hostResolver ?? (() => GoldHudDomainHost.Instance);
            mRandomUnit = randomUnit ?? (() => UnityEngine.Random.value);
        }

        public VfxPlayerStartResult StartPulse(VfxPulseStartRequest request)
        {
            CancelInternal();
            var host = mHostResolver();
            if (host == null || !host.IsAvailable)
            {
                return VfxPlayerStartResult.Failure("金币 HUD 视觉域宿主不可用。");
            }

            mHost = host;
            mUseUnscaledTime = request.UseUnscaledTime;
            mInstanceId = "vfx-gold-flight-" + sNextInstanceId++;
            mElapsed = 0f;

            var origin = ResolveOrigin(request.AcceptedSpatial);
            var sink = mHost.ResolveSinkWorldPosition();
            sink.z = origin.z;

            var amount = Mathf.Max(0, request.AcceptedSpatial.Amount);
            var visualCount = GoldFlightTiming.ResolveVisualCount(amount);
            var timing = GoldFlightTiming.Compute(visualCount);
            var plan = GoldFlightTiming.Plan(visualCount);
            mSoftCap = GoldFlightTiming.PresentationWindow;
            mHardCap = GoldFlightTiming.MaxPresentationDuration;

            mCoinSprite = ResolveCoinSprite();
            var sorting = ResolveSorting();

            mCoins.Clear();
            mVisuals.Clear();
            for (var i = 0; i < visualCount; i++)
            {
                var scatter = RandomScatter(timing.ScatterRadius);
                var peak = origin + scatter;
                peak.z = origin.z;
                // 轻微切向偏置，让滑散更像带着惯性扫过场地，而不是径向炸开。
                var tangent = new Vector3(-scatter.y, scatter.x, 0f);
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    tangent = tangent.normalized * (timing.ScatterRadius * (0.12f + 0.28f * mRandomUnit()));
                    peak += tangent * (mRandomUnit() < 0.5f ? 1f : -1f);
                    peak.z = origin.z;
                }

                var visual = SpawnVisual(origin, sorting);
                if (visual != null)
                {
                    mVisuals.Add(visual);
                }

                var spinSign = mRandomUnit() < 0.5f ? -1f : 1f;
                mCoins.Add(new FlyingCoin
                {
                    Visual = visual,
                    StartDelay = timing.Stagger * i,
                    Burst = timing.Burst,
                    Coast = timing.Coast,
                    Fly = timing.Fly,
                    From = origin,
                    Peak = peak,
                    To = sink,
                    SpinDegrees = spinSign * (55f + 90f * mRandomUnit()),
                });
            }

            mRemaining = mCoins.Count;
            mActive = true;
            return VfxPlayerStartResult.Success(mInstanceId, plan);
        }

        public void Cancel()
        {
            CancelInternal();
        }

        public bool Tick(float deltaTime)
        {
            if (!mActive)
            {
                return true;
            }

            var rawDt = mUseUnscaledTime ? Time.unscaledDeltaTime : Mathf.Max(0f, deltaTime);
            mElapsed += rawDt * ResolveCatchUpWarp();

            for (var i = mCoins.Count - 1; i >= 0; i--)
            {
                var coin = mCoins[i];
                if (coin.Done)
                {
                    continue;
                }

                var local = mElapsed - coin.StartDelay;
                if (local < 0f)
                {
                    continue;
                }

                var total = coin.Burst + coin.Coast + coin.Fly;
                if (total <= 0f || local >= total)
                {
                    AbsorbCoin(coin);
                    continue;
                }

                ApplyCoinPose(coin, local);
            }

            if (mRemaining <= 0)
            {
                FinishNaturally();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 预感本批会拖长时动态加速：越过软窗口后按剩余时间相对硬上限的压力抬升时间倍率。
        /// </summary>
        private float ResolveCatchUpWarp()
        {
            if (mCoins.Count <= 1 || mHardCap <= mSoftCap)
            {
                return 1f;
            }

            if (mElapsed <= mSoftCap)
            {
                return 1f;
            }

            var pressure = Mathf.InverseLerp(mSoftCap, mHardCap, mElapsed);
            return Mathf.Lerp(1f, GoldFlightTiming.MaxCatchUpWarp, pressure * pressure);
        }

        private Vector3 ResolveOrigin(VfxSpatialContext spatial)
        {
            if (spatial.PositionSnapshot.HasValue)
            {
                return spatial.PositionSnapshot.Value;
            }

            return mHost.ResolveDefaultOriginWorld();
        }

        private Sprite ResolveCoinSprite()
        {
            if (mCoinSprite != null)
            {
                return mCoinSprite;
            }

            if (sCachedCoinSprite == null)
            {
                sCachedCoinSprite = LoadPreferredCoinSprite();
            }

            var sprite = sCachedCoinSprite;
            if (sprite == null && mHost != null && mHost.TryGetCoinSprite(out var hostSprite))
            {
                sprite = hostSprite;
            }

            return sprite;
        }

        private static Sprite LoadPreferredCoinSprite()
        {
            var sheet = Resources.LoadAll<Sprite>(CoinSheetResourcesKey);
            if (sheet != null)
            {
                for (var i = 0; i < sheet.Length; i++)
                {
                    var candidate = sheet[i];
                    if (candidate != null
                        && string.Equals(candidate.name, CoinSpriteName, StringComparison.Ordinal))
                    {
                        return candidate;
                    }
                }
            }

            return Resources.Load<Sprite>(LegacyCoinSpriteResourcesKey);
        }

        private (string layer, int order) ResolveSorting()
        {
            if (mHost != null && mHost.TryGetSortingBounds(out var bounds) && bounds.HasLayer)
            {
                return (bounds.SortingLayerName, bounds.ClampOrder(FallbackSortingOrder));
            }

            return (FallbackSortingLayer, FallbackSortingOrder);
        }

        private GameObject SpawnVisual(Vector3 worldPosition, (string layer, int order) sorting)
        {
            if (mCoinSprite == null)
            {
                return null;
            }

            var go = new GameObject("gold-coin");
            go.transform.SetParent(VfxIndependentSpatialRoot.Root, false);
            go.transform.position = worldPosition;
            go.transform.localScale = new Vector3(CoinBaseScale, CoinBaseScale, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = mCoinSprite;
            renderer.sharedMaterial = ResolveFlightMaterial();
            renderer.sortingLayerName = sorting.layer;
            // 飞币略高于金币图标层，避免被 HUD 图标盖住。
            renderer.sortingOrder = sorting.order + 1;
            return go;
        }

        /// <summary>
        /// 飞币挂 DontDestroyOnLoad，不依赖场景 Light2D；用 Unlit 自发光，避免编辑器里 Lit 默认材质全黑。
        /// </summary>
        private static Material ResolveFlightMaterial()
        {
            if (sFlightMaterial != null)
            {
                return sFlightMaterial;
            }

            var shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                return null;
            }

            sFlightMaterial = new Material(shader)
            {
                name = "GoldFlightCoin-Unlit",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return sFlightMaterial;
        }

        private Vector3 RandomScatter(float radius)
        {
            var angle = mRandomUnit() * Mathf.PI * 2f;
            // 偏向外环，减少挤在圆心的僵硬感。
            var distance = radius * Mathf.Lerp(0.45f, 1f, Mathf.Sqrt(mRandomUnit()));
            return new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
        }

        private void ApplyCoinPose(FlyingCoin coin, float local)
        {
            if (coin.Visual == null)
            {
                return;
            }

            var tr = coin.Visual.transform;
            Vector3 position;
            float scale;
            float spinT;

            if (local < coin.Burst)
            {
                var t = coin.Burst > 0f ? local / coin.Burst : 1f;
                var eased = EaseOutCubic(t);
                position = Vector3.LerpUnclamped(coin.From, coin.Peak, eased);
                scale = Mathf.Lerp(CoinBaseScale, CoinBurstPeakScale, EaseOutQuad(t));
                spinT = eased * 0.35f;
            }
            else if (local < coin.Burst + coin.Coast)
            {
                var coastLocal = local - coin.Burst;
                var t = coin.Coast > 0f ? coastLocal / coin.Coast : 1f;
                // 惯性滑散：峰值附近继续沿爆发方向微移，再缓住。
                var drift = (coin.Peak - coin.From) * (0.08f * EaseOutQuad(t));
                position = coin.Peak + drift;
                scale = Mathf.Lerp(CoinBurstPeakScale, CoinBurstPeakScale * 0.96f, t);
                spinT = 0.35f + 0.15f * t;
            }
            else
            {
                var absorbLocal = local - coin.Burst - coin.Coast;
                var t = coin.Fly > 0f ? absorbLocal / coin.Fly : 1f;
                var eased = EaseInQuint(t);
                var coastPeak = coin.Peak + (coin.Peak - coin.From) * 0.08f;
                position = Vector3.LerpUnclamped(coastPeak, coin.To, eased);
                scale = Mathf.Lerp(CoinBurstPeakScale * 0.96f, CoinSettleScale, EaseInQuad(t));
                spinT = 0.5f + 0.5f * eased;
            }

            tr.position = position;
            tr.localScale = new Vector3(scale, scale, 1f);
            tr.localRotation = Quaternion.Euler(0f, 0f, coin.SpinDegrees * spinT);
        }

        private void AbsorbCoin(FlyingCoin coin)
        {
            coin.Done = true;
            mRemaining--;
            if (coin.Visual != null)
            {
                mVisuals.Remove(coin.Visual);
                DestroyVisual(coin.Visual);
                coin.Visual = null;
            }

            mHost?.PunchIcon();
        }

        private void FinishNaturally()
        {
            mHost?.SettleIconToBase();
            CancelInternal();
        }

        private void CancelInternal()
        {
            mActive = false;
            mHost = null;
            for (var i = mVisuals.Count - 1; i >= 0; i--)
            {
                var visual = mVisuals[i];
                if (visual != null)
                {
                    DestroyVisual(visual);
                }
            }

            mVisuals.Clear();
            mCoins.Clear();
            mRemaining = 0;
        }

        private static void DestroyVisual(GameObject visual)
        {
            if (visual == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(visual);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(visual);
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            var inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private static float EaseOutQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t);
        }

        private static float EaseInQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        private static float EaseInQuint(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * t * t;
        }

        private sealed class FlyingCoin
        {
            public GameObject Visual;
            public float StartDelay;
            public float Burst;
            public float Coast;
            public float Fly;
            public Vector3 From;
            public Vector3 Peak;
            public Vector3 To;
            public float SpinDegrees;
            public bool Done;
        }
    }

    /// <summary>gold-flight 飞币调度纯数学（#203）；时间窗/数量分配可独立验证。</summary>
    public static class GoldFlightTiming
    {
        public const float DefaultBurstDuration = 0.22f;
        public const float DefaultCoastDuration = 0.05f;
        public const float DefaultFlyDuration = 0.36f;
        public const float DefaultSpawnStagger = 0.05f;
        public const float DefaultSpawnScatterRadius = 0.9f;
        public const int MaxVisualCoins = 24;
        public const float PresentationWindow = 1.0f;
        public const float MaxPresentationDuration = 1.65f;
        public const float MinBurstDuration = 0.06f;
        public const float MinCoastDuration = 0.01f;
        public const float MinFlyDuration = 0.08f;
        public const float MinStagger = 0.008f;
        public const float MaxCatchUpWarp = 2.15f;

        public struct Result
        {
            public float Burst;
            public float Coast;
            public float Fly;
            public float Stagger;
            public float ScatterRadius;

            public float FirstArrival => Burst + Coast + Fly;
        }

        public static int ResolveVisualCount(int amount)
        {
            return Mathf.Clamp(Mathf.Max(1, amount), 1, MaxVisualCoins);
        }

        /// <summary>
        /// 软窗口内不压缩；超出后先压错峰，逼近硬上限时同步缩短爆发/滑散/吸入，
        /// 末达不超过硬上限。运行时另有 catch-up warp 兜底。
        /// </summary>
        public static Result Compute(int visualCount)
        {
            var burst = Mathf.Max(MinBurstDuration, DefaultBurstDuration);
            var coast = Mathf.Max(MinCoastDuration, DefaultCoastDuration);
            var fly = Mathf.Max(MinFlyDuration, DefaultFlyDuration);
            var stagger = Mathf.Max(0f, DefaultSpawnStagger);
            if (visualCount > 1)
            {
                var first = burst + coast + fly;
                var softCap = Mathf.Max(first + 0.05f, PresentationWindow);
                var hardCap = Mathf.Max(softCap, MaxPresentationDuration);
                var natural = first + stagger * (visualCount - 1);
                if (natural > softCap)
                {
                    stagger = Mathf.Max(MinStagger, (hardCap - first) / (visualCount - 1));
                    var compressed = first + stagger * (visualCount - 1);
                    if (compressed > hardCap)
                    {
                        var scale = hardCap / compressed;
                        burst = Mathf.Max(MinBurstDuration, burst * scale);
                        coast = Mathf.Max(MinCoastDuration, coast * scale);
                        fly = Mathf.Max(MinFlyDuration, fly * scale);
                        stagger = Mathf.Max(MinStagger, stagger * scale);
                    }
                }
            }

            return new Result
            {
                Burst = burst,
                Coast = coast,
                Fly = fly,
                Stagger = stagger,
                ScatterRadius = DefaultSpawnScatterRadius,
            };
        }

        public static VfxPresentationPlan Plan(int visualCount)
        {
            var timing = Compute(visualCount);
            return new VfxPresentationPlan(
                timing.FirstArrival,
                timing.FirstArrival + timing.Stagger * Mathf.Max(0, visualCount - 1));
        }

        /// <summary>把 total 摊到 parts 枚可视币上，余数给前几枚 +1（旧 DistributeValues 等价迁移）。</summary>
        public static int[] DistributeValues(int total, int parts)
        {
            var values = new int[parts];
            if (parts <= 0 || total <= 0)
            {
                return values;
            }

            var coinCount = Mathf.Min(total, parts);
            var baseValue = total / coinCount;
            var remainder = total % coinCount;
            for (var i = 0; i < coinCount; i++)
            {
                values[i] = baseValue + (i < remainder ? 1 : 0);
            }

            return values;
        }
    }
}
