using System.Collections;
using UnityEngine;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 精灵受击闪白（Animator 换帧安全版）。
    /// 通过 MaterialPropertyBlock 驱动 HitFlash shader，不改 spriteRenderer.color，
    /// 避免与动画/悬停高亮抢控制权；撞击 hit-stop 期间用 unscaled 时间。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteImpactFlashEffect : MonoBehaviour
    {
        static readonly int HitFlashAmountId = Shader.PropertyToID("_HitFlashAmount");
        static readonly int HitFlashColorId = Shader.PropertyToID("_HitFlashColor");

#if UNITY_EDITOR
        const string DefaultMaterialPath = "Assets/Arts/VisualProfiles/TableNineSpriteHitFlash.mat";
#endif

        [SerializeField] Material hitFlashMaterial;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] [Range(0.5f, 1f)] float flashAmount = 1f;
        [SerializeField] [Min(1)] int flashCount = 3;
        [SerializeField] float flashOnDuration = 0.045f;
        [SerializeField] float flashOffDuration = 0.035f;
        [SerializeField] bool useUnscaledTime = true;

        SpriteRenderer spriteRenderer;
        MaterialPropertyBlock propertyBlock;
        SelectableSceneElement selectableElement;
        Coroutine flashRoutine;
        bool usesShaderFlash;
        Color baseColor = Color.white;

        void Awake()
        {
            CacheComponents();
            EnsureHitFlashMaterial();
        }

        void OnDisable()
        {
            StopFlashImmediate();
        }

        /// <summary>UnityEvent / 撞击回调入口。</summary>
        public void Play()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            CacheComponents();
            EnsureHitFlashMaterial();

            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
            }

            flashRoutine = StartCoroutine(FlashRoutine());
        }

        void CacheComponents()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (selectableElement == null)
            {
                selectableElement = GetComponent<SelectableSceneElement>();
            }

            propertyBlock ??= new MaterialPropertyBlock();
            baseColor = spriteRenderer.color;
            usesShaderFlash = HasHitFlashProperties(spriteRenderer != null ? spriteRenderer.sharedMaterial : null);
        }

        void EnsureHitFlashMaterial()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (HasHitFlashProperties(spriteRenderer.sharedMaterial))
            {
                usesShaderFlash = true;
                return;
            }

            Material material = ResolveHitFlashMaterial();
            if (material != null)
            {
                spriteRenderer.sharedMaterial = material;
                usesShaderFlash = true;
            }
        }

        Material ResolveHitFlashMaterial()
        {
            if (hitFlashMaterial != null)
            {
                return hitFlashMaterial;
            }

#if UNITY_EDITOR
            hitFlashMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(DefaultMaterialPath);
            if (hitFlashMaterial != null)
            {
                return hitFlashMaterial;
            }
#endif

            Shader shader = Shader.Find("TableNine/SpriteHitFlash");
            if (shader == null)
            {
                return null;
            }

            hitFlashMaterial = new Material(shader)
            {
                name = "RuntimeSpriteImpactFlash",
                hideFlags = HideFlags.HideAndDontSave,
            };
            return hitFlashMaterial;
        }

        IEnumerator FlashRoutine()
        {
            for (int i = 0; i < flashCount; i++)
            {
                ApplyFlash(flashAmount);
                yield return WaitFlash(flashOnDuration);

                ApplyFlash(0f);
                if (i < flashCount - 1)
                {
                    yield return WaitFlash(flashOffDuration);
                }
            }

            ApplyFlash(0f);
            RefreshSelectableVisual();
            flashRoutine = null;
        }

        void ApplyFlash(float amount)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (usesShaderFlash)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(HitFlashColorId, flashColor);
                propertyBlock.SetFloat(HitFlashAmountId, amount);
                spriteRenderer.SetPropertyBlock(propertyBlock);
                return;
            }

            // 兜底：无 HitFlash 材质时短暂提亮本体（动画帧仍正常换图）。
            spriteRenderer.color = amount > 0f
                ? Color.Lerp(baseColor, flashColor, amount)
                : baseColor;
        }

        void StopFlashImmediate()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }

            ApplyFlash(0f);
            RefreshSelectableVisual();
        }

        void RefreshSelectableVisual()
        {
            if (selectableElement != null && selectableElement.IsHovered)
            {
                selectableElement.SetHovered(true);
            }
        }

        IEnumerator WaitFlash(float duration)
        {
            if (duration <= 0f)
            {
                yield break;
            }

            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(duration);
            }
            else
            {
                yield return new WaitForSeconds(duration);
            }
        }

        static bool HasHitFlashProperties(Material material)
        {
            return material != null
                   && material.HasProperty(HitFlashAmountId)
                   && material.HasProperty(HitFlashColorId);
        }
    }
}
