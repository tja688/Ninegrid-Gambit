using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 屏幕冲击（抖屏）静态门面：纯装饰层，不参与 Batch-ack，也不改任何规则状态。
    /// 由 <see cref="ScreenImpactRunner"/> 独占主相机的 localPosition 偏移；
    /// 位移按参考分辨率整像素量化，避免像素画在 PixelPerfectCamera 下抖出亚像素糊边。
    /// </summary>
    public static class ScreenImpact
    {
        /// <summary>全局开关。关闭后既不再累积 trauma，也会把相机复位。</summary>
        public static bool Enabled
        {
            get => sEnabled;
            set
            {
                sEnabled = value;
                if (!value)
                {
                    Reset();
                }
            }
        }

        /// <summary>全局强度倍率（0 = 静音，1 = 默认）。</summary>
        public static float IntensityScale { get; set; } = 1f;

        private static bool sEnabled = true;
        private static ScreenImpactRunner sRunner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sRunner = null;
            sEnabled = true;
            IntensityScale = 1f;
        }

        /// <summary>直接注入 trauma（0..1）。同帧多次调用取叠加后钳制。</summary>
        public static void Kick(float trauma)
        {
            if (!sEnabled)
            {
                return;
            }

            var scaled = trauma * Mathf.Max(0f, IntensityScale);
            if (scaled <= 0.0005f)
            {
                return;
            }

            EnsureRunner()?.AddTrauma(scaled);
        }

        /// <summary>命中抖屏：按扣血/破甲量映射到克制的 trauma 区间。</summary>
        public static void Hit(int magnitude)
        {
            if (magnitude <= 0)
            {
                return;
            }

            var t = Mathf.InverseLerp(1f, 16f, magnitude);
            Kick(Mathf.Lerp(HitTraumaMin, HitTraumaMax, t));
        }

        /// <summary>轻点抖屏：治疗 / 护甲 / 拾取一类的弱反馈。</summary>
        public static void Tap(float strength = 1f)
        {
            Kick(TapTrauma * Mathf.Clamp01(strength));
        }

        /// <summary>爆裂抖屏：爆弹、滚石等签名冲击。strength 0..1。</summary>
        public static void Burst(float strength)
        {
            Kick(Mathf.Lerp(BurstTraumaMin, BurstTraumaMax, Mathf.Clamp01(strength)));
        }

        /// <summary>立即清零 trauma 并把相机放回基准位（切场景 / 关表现时用）。</summary>
        public static void Reset()
        {
            sRunner?.ResetImmediate();
        }

        private const float HitTraumaMin = 0.26f;
        private const float HitTraumaMax = 0.82f;
        private const float TapTrauma = 0.16f;
        private const float BurstTraumaMin = 0.62f;
        private const float BurstTraumaMax = 1f;

        private static ScreenImpactRunner EnsureRunner()
        {
            if (sRunner != null)
            {
                return sRunner;
            }

            if (!Application.isPlaying)
            {
                return null;
            }

            var host = new GameObject("~ScreenImpactRunner");
            host.hideFlags = HideFlags.DontSave;
            Object.DontDestroyOnLoad(host);
            sRunner = host.AddComponent<ScreenImpactRunner>();
            return sRunner;
        }
    }

    /// <summary>
    /// 抖屏执行体：trauma 衰减 + Perlin 噪声偏移，LateUpdate 末段写相机 localPosition。
    /// ExecutionOrder 靠后，保证在任何镜头跟随/构图逻辑之后叠加偏移。
    /// </summary>
    [DefaultExecutionOrder(20000)]
    public sealed class ScreenImpactRunner : MonoBehaviour
    {
        /// <summary>trauma 满值时的最大偏移（参考分辨率像素）。</summary>
        private const float MaxOffsetPixels = 4.5f;

        /// <summary>trauma 从 1 线性衰减到 0 的秒数。</summary>
        private const float DecaySeconds = 0.42f;

        /// <summary>噪声频率（Hz 级别），偏高才有"撞击"而非"漂移"感。</summary>
        private const float NoiseFrequency = 26f;

        /// <summary>trauma → 幅度的指数。>1 让小伤害更克制。</summary>
        private const float TraumaExponent = 1.45f;

        /// <summary>参考分辨率纵向像素数（与 PixelPerfectCamera 的 refResolutionY 一致）。</summary>
        private const float ReferencePixelHeight = 540f;

        private const float FallbackPixelsPerUnit = 32f;

        private float _trauma;
        private Camera _camera;
        private Transform _cameraTransform;
        private Vector3 _basePosition;
        private bool _hasBase;
        private bool _offsetApplied;
        private float _noiseSeedX;
        private float _noiseSeedY;

        private void Awake()
        {
            _noiseSeedX = Random.Range(0f, 128f);
            _noiseSeedY = Random.Range(128f, 256f);
        }

        internal void AddTrauma(float amount)
        {
            if (!TryAcquireCamera())
            {
                return;
            }

            if (_trauma <= 0.0001f)
            {
                // 起震瞬间重新采样基准位：期间若有别的系统搬过相机，也以它为家。
                CaptureBase();
                // 换一组噪声偏移，避免连续冲击的抖动轨迹雷同。
                _noiseSeedX += 7.31f;
                _noiseSeedY += 5.17f;
            }

            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        internal void ResetImmediate()
        {
            _trauma = 0f;
            RestoreBase();
        }

        private void LateUpdate()
        {
            if (_trauma <= 0.0001f)
            {
                if (_offsetApplied)
                {
                    RestoreBase();
                }

                return;
            }

            if (!TryAcquireCamera())
            {
                _trauma = 0f;
                _offsetApplied = false;
                _hasBase = false;
                return;
            }

            var dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _trauma = Mathf.Max(0f, _trauma - dt / DecaySeconds);

            var amplitude = Mathf.Pow(_trauma, TraumaExponent);
            if (amplitude <= 0.0001f)
            {
                _trauma = 0f;
                RestoreBase();
                return;
            }

            var t = Time.unscaledTime * NoiseFrequency;
            var nx = Mathf.PerlinNoise(_noiseSeedX, t) * 2f - 1f;
            var ny = Mathf.PerlinNoise(_noiseSeedY, t) * 2f - 1f;

            // 整像素量化：抖动只走参考栅格的整格。相机不旋转，像素栅格始终轴对齐，画面不糊不抖边。
            var pixelSize = ResolvePixelSize();
            var pixelsX = Mathf.Round(nx * amplitude * MaxOffsetPixels);
            var pixelsY = Mathf.Round(ny * amplitude * MaxOffsetPixels);

            _cameraTransform.localPosition = _basePosition + new Vector3(pixelsX * pixelSize, pixelsY * pixelSize, 0f);
            _offsetApplied = true;
        }

        private float ResolvePixelSize()
        {
            if (_camera != null && _camera.orthographic && _camera.orthographicSize > 0.0001f)
            {
                var ppu = ReferencePixelHeight / (2f * _camera.orthographicSize);
                if (ppu > 0.0001f)
                {
                    return 1f / ppu;
                }
            }

            return 1f / FallbackPixelsPerUnit;
        }

        private bool TryAcquireCamera()
        {
            if (_cameraTransform != null)
            {
                return true;
            }

            _camera = Camera.main;
            if (_camera == null)
            {
                return false;
            }

            _cameraTransform = _camera.transform;
            CaptureBase();
            return true;
        }

        private void CaptureBase()
        {
            if (_cameraTransform == null)
            {
                return;
            }

            // 已经在抖的状态下不要把带偏移的位姿当基准。
            if (_offsetApplied)
            {
                return;
            }

            _basePosition = _cameraTransform.localPosition;
            _hasBase = true;
        }

        private void RestoreBase()
        {
            if (_cameraTransform != null && _hasBase)
            {
                _cameraTransform.localPosition = _basePosition;
            }

            _offsetApplied = false;
        }

        private void OnDisable()
        {
            ResetImmediate();
        }
    }
}
