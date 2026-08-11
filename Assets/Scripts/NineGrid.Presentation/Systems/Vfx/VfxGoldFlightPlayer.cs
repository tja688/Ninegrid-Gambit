using System;
using System.Collections.Generic;
using NineGrid.Content.Vfx;
using NineGrid.Flow;
using UnityEngine;

namespace NineGrid.Presentation.Systems.Vfx
{
    /// <summary>
    /// gold-flight 程序化独立 Pulse 播放器（#203）：自治飞币生成、数量分配、散布、错峰、飞行、
    /// 缩放、排序与软/硬时间窗压缩；终点、排序边界与图标吞噬反馈经金币 HUD 域宿主取得，
    /// 不搜索或任意修改场景。批次创建成功时一次性返回首达/末达表现计划，不建立逐金币回调。
    /// </summary>
    public sealed class VfxGoldFlightPlayer : IVfxPulsePlayer
    {
        private const string CoinSpriteResourcesKey = "VFX/GoldFlightCoin";
        private const string FallbackSortingLayer = "Main";
        private const int FallbackSortingOrder = 20;
        private const float CoinBaseScale = 0.5f;
        private const float CoinSettleScale = 0.65f * CoinBaseScale;

        private static long sNextInstanceId = 1;

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

            mCoinSprite = ResolveCoinSprite();
            var sorting = ResolveSorting();

            mCoins.Clear();
            mVisuals.Clear();
            for (var i = 0; i < visualCount; i++)
            {
                var from = origin + RandomScatter(timing.ScatterRadius);
                from.z = origin.z;
                var visual = SpawnVisual(from, sorting);
                if (visual != null)
                {
                    mVisuals.Add(visual);
                }

                mCoins.Add(new FlyingCoin
                {
                    Visual = visual,
                    StartDelay = timing.Stagger * i,
                    Duration = timing.Fly,
                    From = from,
                    To = sink,
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

            mElapsed += mUseUnscaledTime ? Time.unscaledDeltaTime : Mathf.Max(0f, deltaTime);

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

                var t = coin.Duration > 0f ? local / coin.Duration : 1f;
                if (t >= 1f)
                {
                    AbsorbCoin(coin);
                    continue;
                }

                ApplyCoinPose(coin, t);
            }

            if (mRemaining <= 0)
            {
                FinishNaturally();
                return true;
            }

            return false;
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

            var sprite = Resources.Load<Sprite>(CoinSpriteResourcesKey);
            if (sprite == null && mHost != null && mHost.TryGetCoinSprite(out var hostSprite))
            {
                sprite = hostSprite;
            }

            return sprite;
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
            renderer.sortingLayerName = sorting.layer;
            // 飞币略高于金币图标层，避免被 HUD 图标盖住。
            renderer.sortingOrder = sorting.order + 1;
            return go;
        }

        private Vector3 RandomScatter(float radius)
        {
            var angle = mRandomUnit() * Mathf.PI * 2f;
            var distance = radius * Mathf.Sqrt(mRandomUnit());
            return new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
        }

        private void ApplyCoinPose(FlyingCoin coin, float t)
        {
            if (coin.Visual == null)
            {
                return;
            }

            var tr = coin.Visual.transform;
            tr.position = Vector3.LerpUnclamped(coin.From, coin.To, EaseInCubic(t));
            var scale = Mathf.Lerp(CoinBaseScale, CoinSettleScale, EaseInQuad(t));
            tr.localScale = new Vector3(scale, scale, 1f);
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

        private static float EaseInCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        private static float EaseInQuad(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        private sealed class FlyingCoin
        {
            public GameObject Visual;
            public float StartDelay;
            public float Duration;
            public Vector3 From;
            public Vector3 To;
            public bool Done;
        }
    }

    /// <summary>gold-flight 飞币调度纯数学（#203）；时间窗/数量分配可独立验证。</summary>
    public static class GoldFlightTiming
    {
        public const float DefaultFlyDuration = 0.45f;
        public const float DefaultSpawnStagger = 0.07f;
        public const float DefaultSpawnScatterRadius = 0.55f;
        public const int MaxVisualCoins = 24;
        public const float PresentationWindow = 1.35f;
        public const float MaxPresentationDuration = 2.2f;
        public const float MinFlyDuration = 0.08f;
        public const float MinStagger = 0.01f;

        public struct Result
        {
            public float Fly;
            public float Stagger;
            public float ScatterRadius;
        }

        public static int ResolveVisualCount(int amount)
        {
            return Mathf.Clamp(Mathf.Max(1, amount), 1, MaxVisualCoins);
        }

        /// <summary>
        /// 旧 GoldGainFxManagerSingleton.ComputeTiming 的等价迁移：软窗口内不压缩，
        /// 超出软窗口压缩错峰，逼近硬上限时同步加速飞入，末达不超过硬上限。
        /// </summary>
        public static Result Compute(int visualCount)
        {
            var fly = Mathf.Max(MinFlyDuration, DefaultFlyDuration);
            var stagger = Mathf.Max(0f, DefaultSpawnStagger);
            if (visualCount > 1)
            {
                var softCap = Mathf.Max(fly + 0.05f, PresentationWindow);
                var hardCap = Mathf.Max(softCap, MaxPresentationDuration);
                var natural = fly + stagger * (visualCount - 1);
                if (natural > softCap)
                {
                    stagger = Mathf.Max(MinStagger, (hardCap - fly) / (visualCount - 1));
                    var compressed = fly + stagger * (visualCount - 1);
                    if (compressed > hardCap)
                    {
                        var scale = hardCap / compressed;
                        fly *= scale;
                        stagger *= scale;
                    }
                }
            }

            return new Result
            {
                Fly = fly,
                Stagger = stagger,
                ScatterRadius = DefaultSpawnScatterRadius,
            };
        }

        public static VfxPresentationPlan Plan(int visualCount)
        {
            var timing = Compute(visualCount);
            return new VfxPresentationPlan(
                timing.Fly,
                timing.Fly + timing.Stagger * Mathf.Max(0, visualCount - 1));
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
