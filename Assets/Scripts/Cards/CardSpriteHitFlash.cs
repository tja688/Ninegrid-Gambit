using DG.Tweening;
using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 卡牌受击闪白：将目标 SpriteRenderer 切换为 HitFlash 材质实例，通过 _HitFlashAmount 驱动闪白。
    /// 供 Timeline Signal / Animation 轨道或 DOTween 回调直接调用。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CardSpriteHitFlash : MonoBehaviour
    {
        private static readonly int HitFlashAmountId = Shader.PropertyToID("_HitFlashAmount");
        private static readonly int HitFlashColorId = Shader.PropertyToID("_HitFlashColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private const string DefaultHitFlashMaterialPath =
            "Assets/Arts/VisualProfiles/TableNineSpriteHitFlash.mat";

        [Header("Targets")]
        [Tooltip("收集 SpriteRenderer 的根节点；留空时使用父节点（适合挂在 Presentation 子物体上）。")]
        [SerializeField] private Transform rendererSearchRoot;

        [Tooltip("需要闪白的 SpriteRenderer；留空时在 Awake 从 rendererSearchRoot（或父节点）自动收集全部 SpriteRenderer。")]
        [SerializeField] private SpriteRenderer[] targetRenderers;

        [Header("Material")]
        [Tooltip("HitFlash 材质模板（TableNine/SpriteHitFlash）；留空时 Awake 从默认路径加载。")]
        [SerializeField] private Material hitFlashMaterialTemplate;

        [Header("Flash")]
        [Tooltip("闪白叠色。")]
        [SerializeField] private Color flashColor = Color.white;

        [Tooltip("闪白峰值强度（0~1，对应 _HitFlashAmount）。")]
        [SerializeField, Range(0f, 1f)] private float flashPeakAmount = 1f;

        [Tooltip("闪白升起时长（秒）。")]
        [SerializeField] private float flashInDuration = 0.03f;

        [Tooltip("闪白消退时长（秒）。")]
        [SerializeField] private float flashOutDuration = 0.12f;

        [Tooltip("单次受击闪白次数。")]
        [SerializeField, Min(1)] private int flashCount = 3;

        [Tooltip("当前闪白强度；可由 Timeline Animation 轨道关键帧，或代码/回调修改。")]
        [SerializeField, Range(0f, 1f)] private float flashAmount;

        private Material[] _flashMaterials;
        private Tween _activeTween;
        private bool _initialized;

        public float FlashAmount
        {
            get => flashAmount;
            set => SetFlashAmount(value);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            _activeTween?.Kill();
            DestroyFlashMaterials();
        }

        /// <summary>Timeline Signal / 回调：播放默认受击闪白。</summary>
        public void PlayHitFlash()
        {
            PlayHitFlash(flashInDuration, flashOutDuration, flashPeakAmount, flashCount);
        }

        /// <summary>Timeline Signal / 回调：自定义时长、峰值与次数。</summary>
        public void PlayHitFlash(float inDuration, float outDuration, float peakAmount = 1f, int count = -1)
        {
            EnsureInitialized();
            _activeTween?.Kill();

            var peak = Mathf.Clamp01(peakAmount);
            var loops = count > 0 ? count : flashCount;
            loops = Mathf.Max(1, loops);
            SetFlashAmount(0f);

            var sequence = DOTween.Sequence()
                .SetLink(gameObject, LinkBehaviour.KillOnDestroy);

            for (var i = 0; i < loops; i++)
            {
                sequence
                    .Append(DOTween.To(() => flashAmount, SetFlashAmount, peak, Mathf.Max(0f, inDuration)))
                    .Append(DOTween.To(() => flashAmount, SetFlashAmount, 0f, Mathf.Max(0f, outDuration)));
            }

            _activeTween = sequence;
        }

        /// <summary>Timeline Signal / 回调：立即停止并归零。</summary>
        public void StopHitFlash()
        {
            _activeTween?.Kill();
            _activeTween = null;
            SetFlashAmount(0f);
        }

        /// <summary>Timeline Signal / 回调：直接设置闪白强度（0~1）。</summary>
        public void SetFlashAmount(float amount)
        {
            EnsureInitialized();
            flashAmount = Mathf.Clamp01(amount);
            ApplyFlashAmount(flashAmount);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
            {
                SyncMaterialSpriteData();
                return;
            }

#if UNITY_EDITOR
            if (hitFlashMaterialTemplate == null)
            {
                hitFlashMaterialTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                    DefaultHitFlashMaterialPath);
            }
#endif

            if (hitFlashMaterialTemplate == null)
            {
                Debug.LogWarning(
                    $"{nameof(CardSpriteHitFlash)} on {name} 缺少 HitFlash 材质模板，闪白不会生效。",
                    this);
                return;
            }

            if (targetRenderers == null || targetRenderers.Length == 0)
            {
                targetRenderers = ResolveRendererSearchRoot().GetComponentsInChildren<SpriteRenderer>(true);
            }

            DestroyFlashMaterials();
            _flashMaterials = new Material[targetRenderers.Length];

            for (var i = 0; i < targetRenderers.Length; i++)
            {
                var renderer = targetRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                var flashMaterial = new Material(hitFlashMaterialTemplate)
                {
                    name = $"{renderer.name}_HitFlash"
                };

                SyncRendererToFlashMaterial(renderer, flashMaterial);
                renderer.material = flashMaterial;
                _flashMaterials[i] = flashMaterial;
            }

            ApplyFlashAmount(flashAmount);
            _initialized = true;
        }

        private void SyncMaterialSpriteData()
        {
            if (targetRenderers == null || _flashMaterials == null)
            {
                return;
            }

            var count = Mathf.Min(targetRenderers.Length, _flashMaterials.Length);
            for (var i = 0; i < count; i++)
            {
                var renderer = targetRenderers[i];
                var flashMaterial = _flashMaterials[i];
                if (renderer == null || flashMaterial == null)
                {
                    continue;
                }

                SyncRendererToFlashMaterial(renderer, flashMaterial);
            }
        }

        private void SyncRendererToFlashMaterial(SpriteRenderer renderer, Material flashMaterial)
        {
            var sprite = renderer.sprite;
            if (sprite != null && sprite.texture != null)
            {
                flashMaterial.SetTexture(MainTexId, sprite.texture);
            }

            flashMaterial.SetColor(ColorId, renderer.color);
            flashMaterial.SetColor(HitFlashColorId, flashColor);
        }

        private void ApplyFlashAmount(float amount)
        {
            if (_flashMaterials == null)
            {
                return;
            }

            for (var i = 0; i < _flashMaterials.Length; i++)
            {
                var material = _flashMaterials[i];
                if (material != null)
                {
                    material.SetFloat(HitFlashAmountId, amount);
                }
            }
        }

        private Transform ResolveRendererSearchRoot()
        {
            if (rendererSearchRoot != null)
            {
                return rendererSearchRoot;
            }

            return transform.parent != null ? transform.parent : transform;
        }

        private void DestroyFlashMaterials()
        {
            if (_flashMaterials == null)
            {
                return;
            }

            for (var i = 0; i < _flashMaterials.Length; i++)
            {
                if (_flashMaterials[i] != null)
                {
                    Destroy(_flashMaterials[i]);
                }
            }

            _flashMaterials = null;
            _initialized = false;
        }

#if UNITY_EDITOR
        [ContextMenu("Play Hit Flash (Editor)")]
        private void EditorPlayHitFlash()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("请在 Play Mode 下测试闪白。", this);
                return;
            }

            PlayHitFlash();
        }
#endif
    }
}
