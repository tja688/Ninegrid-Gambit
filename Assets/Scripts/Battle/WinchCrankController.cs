using System;
using NineGrid.GameFlow;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace NineGrid.Battle
{
    /// <summary>
    /// 绞盘“狂点”控制器：检测玩家对绞盘的点击频率，输出一个 0..1 的“绞劲”强度，
    /// 并据此驱动绞盘的放大、晃动与帧动画速度。挂在场景里的 Winch 上（默认失活，
    /// 由 <see cref="AnchorRammingController"/> 在抛锚命中后 <see cref="BeginCrank"/> 唤醒）。
    ///
    /// 频率检测带一点“挑战性”：每次点击给一份增益，同时强度越高衰减越快，
    /// 想把强度顶到最大需要持续高频狂点，松手会迅速回落。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WinchCrankController : MonoBehaviour
    {
        [Header("引用（留空自动获取）")]
        [SerializeField] SpriteRenderer spriteRenderer;
        [SerializeField] Animator animator;
        [SerializeField] Camera inputCamera;

        [Header("点击频率 → 强度")]
        [Tooltip("每次点击增加的强度。偏小 = 起步更艰难，要狂点。")]
        [Range(0.02f, 0.5f)] public float clickGain = 0.12f;
        [Tooltip("基础衰减（每秒）。")]
        [Range(0f, 3f)] public float decayBase = 0.52f;
        [Tooltip("额外衰减：强度越高衰减越猛（每秒，乘以当前强度）。越大越难顶满 = 越有挑战。")]
        [Range(0f, 4f)] public float decayIntensityScale = 1.1f;
        [Tooltip("显示强度向目标强度平滑的速度。")]
        [Range(1f, 30f)] public float intensitySmooth = 14f;
        [Tooltip("点击判定的额外外扩（世界单位），让高速点击更跟手。")]
        public float clickPadding = 0.15f;

        [Header("放大（transform 绝对缩放）")]
        [Tooltip("默认（不点）时的缩放。")]
        public float baseScale = 2f;
        [Tooltip("狂点顶满时的缩放。")]
        public float maxScale = 4f;

        [Header("晃动")]
        [Tooltip("顶满时的位移晃动峰值（世界单位）。")]
        public float maxPositionShake = 0.16f;
        [Tooltip("顶满时的旋转晃动峰值（度）。")]
        public float maxRotationShake = 10f;
        [Tooltip("晃动频率。")]
        public float shakeFrequency = 34f;

        [Header("帧动画速度")]
        [Tooltip("完全不点、自动慢搅时的动画速度倍率。")]
        public float idleAnimSpeed = 0.5f;
        [Tooltip("狂点顶满时的动画速度倍率（绞盘看上去最快）。")]
        public float maxAnimSpeed = 3f;

        [Header("其它")]
        [Tooltip("点击命中在 UI 上时忽略（避免穿透面板）。这里指屏幕射线用途，默认关。")]
        public bool ignoreWhenPointerOverUi = false;

        /// <summary>玩家点击绞盘时触发（用于挂音效 / 特效）。</summary>
        public event Action Clicked;
        public event Action CrankBegun;
        public event Action CrankEnded;

        /// <summary>0..1 的绞劲强度（已平滑）。</summary>
        public float Intensity01 => _intensityVisual;

        /// <summary>正在接受狂点。</summary>
        public bool IsCranking => _active;

        Transform _tf;
        Vector3 _homePosition;
        Quaternion _homeRotation;
        float _intensityRaw;
        float _intensityVisual;
        float _shakeSeed;
        bool _active;
        bool _cached;

        void Awake()
        {
            Cache();
        }

        void Cache()
        {
            if (_cached) return;
            _tf = transform;
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<Animator>();
            _cached = true;
        }

        /// <summary>唤醒绞盘并开始接受狂点。会激活自身 GameObject。</summary>
        public void BeginCrank()
        {
            Cache();
            _homePosition = _tf.localPosition;
            _homeRotation = _tf.localRotation;
            _intensityRaw = 0f;
            _intensityVisual = 0f;
            _shakeSeed = UnityEngine.Random.value * 100f;
            _active = true;

            if (!gameObject.activeSelf) gameObject.SetActive(true);

            ApplyVisual(0f);
            CrankBegun?.Invoke();
        }

        /// <summary>停止狂点并复位绞盘视觉，随后失活自身。</summary>
        public void EndCrank(bool deactivate = true)
        {
            _active = false;
            _intensityRaw = 0f;
            _intensityVisual = 0f;

            if (_cached && _tf != null)
            {
                _tf.localPosition = _homePosition;
                _tf.localRotation = _homeRotation;
                _tf.localScale = Vector3.one * baseScale;
            }

            if (animator != null) animator.speed = idleAnimSpeed;
            if (deactivate && gameObject.activeSelf) gameObject.SetActive(false);
            CrankEnded?.Invoke();
        }

        void Update()
        {
            if (!_active) return;

            float dt = Time.unscaledDeltaTime;

            if (FlowInput.TryGameplayClick() && HitTestPointer())
            {
                _intensityRaw = Mathf.Min(1f, _intensityRaw + clickGain);
                Clicked?.Invoke();
            }

            float decay = decayBase + decayIntensityScale * _intensityRaw;
            _intensityRaw = Mathf.Max(0f, _intensityRaw - decay * dt);

            _intensityVisual = Mathf.Lerp(_intensityVisual, _intensityRaw, 1f - Mathf.Exp(-intensitySmooth * dt));

            ApplyVisual(_intensityVisual);
        }

        void ApplyVisual(float intensity)
        {
            if (_tf == null) return;

            // 缩放：基础 → 最大。
            float scale = Mathf.Lerp(baseScale, maxScale, intensity);
            _tf.localScale = Vector3.one * scale;

            // 晃动：强度越高越夸张。
            float t = Time.unscaledTime * shakeFrequency;
            float posAmp = maxPositionShake * intensity;
            float rotAmp = maxRotationShake * intensity;
            float ox = (Mathf.PerlinNoise(_shakeSeed, t) - 0.5f) * 2f * posAmp;
            float oy = (Mathf.PerlinNoise(t, _shakeSeed + 9.2f) - 0.5f) * 2f * posAmp;
            float rz = (Mathf.PerlinNoise(t, _shakeSeed + 41.3f) - 0.5f) * 2f * rotAmp;

            _tf.localPosition = _homePosition + new Vector3(ox, oy, 0f);
            _tf.localRotation = _homeRotation * Quaternion.Euler(0f, 0f, rz);

            // 帧动画速度：慢搅 → 最快。
            if (animator != null)
            {
                animator.speed = Mathf.Lerp(idleAnimSpeed, maxAnimSpeed, intensity);
            }
        }

        bool HitTestPointer()
        {
            var camera = ResolveCamera();
            if (camera == null || spriteRenderer == null) return false;

            if (ignoreWhenPointerOverUi
                && UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return false;
            }

            if (!TryGetPointerWorld(camera, out Vector2 world)) return false;

            Bounds b = spriteRenderer.bounds;
            b.Expand(new Vector3(clickPadding * 2f, clickPadding * 2f, 0f));
            return world.x >= b.min.x && world.x <= b.max.x
                && world.y >= b.min.y && world.y <= b.max.y;
        }

        Camera ResolveCamera()
        {
            if (inputCamera != null) return inputCamera;
            inputCamera = Camera.main;
            return inputCamera;
        }

        static bool TryGetPointerWorld(Camera camera, out Vector2 world)
        {
            Vector3 screen;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                Vector2 p = Mouse.current.position.ReadValue();
                screen = new Vector3(p.x, p.y, 0f);
            }
            else
            {
                screen = Input.mousePosition;
            }
#else
            screen = Input.mousePosition;
#endif
            if (float.IsNaN(screen.x) || float.IsNaN(screen.y))
            {
                world = default;
                return false;
            }

            float depth = Mathf.Abs(camera.transform.position.z);
            Vector3 w = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            world = w;
            return true;
        }

        static bool WasClickThisFrame()
        {
            if (Input.GetMouseButtonDown(0)) return true;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) return true;
#endif
            return false;
        }
    }
}
