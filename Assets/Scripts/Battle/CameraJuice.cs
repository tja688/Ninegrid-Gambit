using UnityEngine;
using DG.Tweening;

namespace NineGrid.Battle
{
    /// <summary>
    /// 轻量镜头“汁水”（gamefeel）工具：屏幕抖动 + 可选的对撞前放大。
    /// 全程使用 unscaledTime，因此在时间缓速 / 命中冻结时仍然生效。
    /// 与 PixelPerfectCamera 共存：任一效果激活时临时关闭像素完美（避免正交尺寸/位移被吸附回去），
    /// 效果全部结束后再恢复。挂在 Main Camera 上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraJuice : MonoBehaviour
    {
        public static CameraJuice Instance { get; private set; }

        [Header("引用")]
        [SerializeField] Camera cam;
        [Tooltip("效果期间临时关闭 PixelPerfectCamera，结束后恢复。")]
        [SerializeField] bool suspendPixelPerfectDuringEffects = true;

        [Header("抖动默认值")]
        [SerializeField] float defaultShakeFrequency = 32f;
        [Tooltip("抖动是否也带一点点旋转（roll），更有冲击感。")]
        [SerializeField] float shakeRollDegrees = 2.8f;

        Behaviour _pixelPerfect;
        Vector3 _baseLocalPos;
        Quaternion _baseLocalRot;
        float _baseOrthoSize;

        int _effectCount;

        // 抖动
        float _shakeAmp;
        float _shakeDuration;
        float _shakeTimer;
        float _shakeFreq;
        float _shakeSeed;
        bool _shaking;

        // 缩放
        Tween _zoomTween;
        bool _zoomActive;

        void Awake()
        {
            Instance = this;
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;

            // 通过类型名拿 PixelPerfectCamera，避免对具体程序集产生硬依赖。
            _pixelPerfect = GetComponent("PixelPerfectCamera") as Behaviour;

            _baseLocalPos = transform.localPosition;
            _baseLocalRot = transform.localRotation;
            if (cam != null) _baseOrthoSize = cam.orthographicSize;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            KillZoom();
        }

        void LateUpdate()
        {
            Vector3 offset = Vector3.zero;
            float roll = 0f;

            if (_shaking)
            {
                _shakeTimer -= Time.unscaledDeltaTime;
                float k = _shakeDuration > 0f ? Mathf.Clamp01(_shakeTimer / _shakeDuration) : 0f;
                float amp = _shakeAmp * k * k; // 尾部衰减更自然
                float t = Time.unscaledTime * _shakeFreq;
                offset.x = (Mathf.PerlinNoise(_shakeSeed, t) - 0.5f) * 2f * amp;
                offset.y = (Mathf.PerlinNoise(t, _shakeSeed + 13.37f) - 0.5f) * 2f * amp;
                roll = (Mathf.PerlinNoise(t, _shakeSeed + 71.7f) - 0.5f) * 2f * shakeRollDegrees * k;

                if (_shakeTimer <= 0f)
                {
                    _shaking = false;
                    PopEffect();
                }
            }

            transform.localPosition = _baseLocalPos + offset;
            transform.localRotation = _baseLocalRot * Quaternion.Euler(0f, 0f, roll);
        }

        /// <summary>屏幕抖动。amplitude 为世界单位位移峰值。</summary>
        public void Shake(float amplitude, float duration, float frequency = -1f)
        {
            if (amplitude <= 0f || duration <= 0f) return;

            // 叠加式：取更强的一次，重置计时。
            if (!_shaking) PushEffect();
            _shakeAmp = Mathf.Max(_shaking ? _shakeAmp : 0f, amplitude);
            _shakeDuration = duration;
            _shakeTimer = duration;
            _shakeFreq = frequency > 0f ? frequency : defaultShakeFrequency;
            _shakeSeed = Random.value * 100f;
            _shaking = true;
        }

        /// <summary>放大镜头（正交尺寸变小）。sizeMultiplier &lt; 1 为拉近。</summary>
        public void BeginZoom(float sizeMultiplier, float duration)
        {
            if (cam == null) return;
            KillZoom();
            if (!_zoomActive) PushEffect();
            _zoomActive = true;

            float target = _baseOrthoSize * Mathf.Max(0.05f, sizeMultiplier);
            if (duration <= 0f)
            {
                cam.orthographicSize = target;
                return;
            }

            _zoomTween = DOTween.To(() => cam.orthographicSize, v => cam.orthographicSize = v, target, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true);
        }

        /// <summary>恢复正交尺寸到基础值，结束后放行 PixelPerfect。</summary>
        public void EndZoom(float duration)
        {
            if (cam == null) return;
            KillZoom();

            void Finish()
            {
                cam.orthographicSize = _baseOrthoSize;
                if (_zoomActive)
                {
                    _zoomActive = false;
                    PopEffect();
                }
            }

            if (duration <= 0f)
            {
                Finish();
                return;
            }

            _zoomTween = DOTween.To(() => cam.orthographicSize, v => cam.orthographicSize = v, _baseOrthoSize, duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(Finish);
        }

        /// <summary>强制复位（被打断时）。</summary>
        public void ResetImmediate()
        {
            KillZoom();
            _shaking = false;
            _zoomActive = false;
            _effectCount = 0;
            if (cam != null) cam.orthographicSize = _baseOrthoSize;
            transform.localPosition = _baseLocalPos;
            transform.localRotation = _baseLocalRot;
            SetPixelPerfect(true);
        }

        void KillZoom()
        {
            if (_zoomTween != null && _zoomTween.IsActive()) _zoomTween.Kill();
            _zoomTween = null;
        }

        void PushEffect()
        {
            _effectCount++;
            if (_effectCount == 1) SetPixelPerfect(false);
        }

        void PopEffect()
        {
            _effectCount = Mathf.Max(0, _effectCount - 1);
            if (_effectCount == 0)
            {
                // 恢复到基础位姿，避免像素完美接管时残留亚像素偏移。
                transform.localPosition = _baseLocalPos;
                transform.localRotation = _baseLocalRot;
                SetPixelPerfect(true);
            }
        }

        void SetPixelPerfect(bool enabled)
        {
            if (!suspendPixelPerfectDuringEffects || _pixelPerfect == null) return;
            _pixelPerfect.enabled = enabled;
        }
    }
}
