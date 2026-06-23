using System.Collections;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace NineGrid.Presentation.Performance
{
    [MovedFrom("NineGrid.Presentation.AtomicRepresentationTools")]
    [DisallowMultipleComponent]
    public sealed class CardHitFlashPerformance : MonoBehaviour
    {
        private static readonly int HitFlashAmountId = Shader.PropertyToID("_HitFlashAmount");
        private static readonly int HitFlashColorId = Shader.PropertyToID("_HitFlashColor");

        [Header("Targets")]
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private bool includeChildren;

        [Header("Material")]
        [SerializeField] private Material flashMaterialTemplate;

        [Header("Timing")]
        [SerializeField, Min(1)] private int flashCount = 2;
        [SerializeField, Min(0.01f)] private float flashFrequency = 12f;
        [SerializeField] private bool ignoreTimeScale = true;

        [Header("Look")]
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField, Range(0f, 1f)] private float flashStrength = 1f;
        [SerializeField] private AnimationCurve flashCurve = new(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f));

        private Coroutine playRoutine;
        private SpriteRenderer[] cachedTargets = System.Array.Empty<SpriteRenderer>();
        private Color[] cachedBaseColors = System.Array.Empty<Color>();
        private Material[] cachedOriginalMaterials = System.Array.Empty<Material>();
        private Material[] cachedFlashMaterials = System.Array.Empty<Material>();
        private Material resolvedFlashMaterialTemplate;
        private bool materialsInstalled;

        private void Awake()
        {
            CacheTargets();
            CaptureBaseColors();
        }

        private void OnEnable()
        {
            CacheTargets();
            CaptureBaseColors();
            RestoreBaseColors();
        }

        private void OnDisable()
        {
            StopAndRestore();
            RestoreOriginalMaterials();
        }

        private void OnDestroy()
        {
            DestroyInstalledMaterials();

            if (resolvedFlashMaterialTemplate != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(resolvedFlashMaterialTemplate);
                }
                else
                {
                    DestroyImmediate(resolvedFlashMaterialTemplate);
                }

                resolvedFlashMaterialTemplate = null;
            }
        }

        private void OnValidate()
        {
            flashCount = Mathf.Max(1, flashCount);
            flashFrequency = Mathf.Max(0.01f, flashFrequency);

            CacheTargets();
            CaptureBaseColors();

            if (!Application.isPlaying)
            {
                RestoreBaseColors();
            }
        }

        public void SetFlashMaterialTemplate(Material template)
        {
            flashMaterialTemplate = template;
        }

        [ContextMenu("Play Hit Flash")]
        public void PlayHitFlash()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            StopCurrentRoutine();
            CacheTargets();
            CaptureBaseColors();
            EnsureFlashMaterialsInstalled();
            playRoutine = StartCoroutine(PlayHitFlashRoutine());
        }

        [ContextMenu("Stop And Restore")]
        public void StopAndRestore()
        {
            StopCurrentRoutine();
            RestoreBaseColors();
        }

        public void RebindTargets()
        {
            RestoreOriginalMaterials();
            CacheTargets();
            CaptureBaseColors();
            RestoreBaseColors();
        }

        private IEnumerator PlayHitFlashRoutine()
        {
            float cycleDuration = 1f / flashFrequency;

            for (int i = 0; i < flashCount; i++)
            {
                float elapsed = 0f;
                while (elapsed < cycleDuration)
                {
                    float progress = cycleDuration <= Mathf.Epsilon ? 1f : elapsed / cycleDuration;
                    float intensity = Mathf.Clamp01(flashCurve.Evaluate(progress) * flashStrength);
                    ApplyFlash(intensity);

                    elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }

                ApplyFlash(0f);
            }

            playRoutine = null;
            RestoreBaseColors();
        }

        private void CacheTargets()
        {
            if (target != null)
            {
                cachedTargets = includeChildren
                    ? target.GetComponentsInChildren<SpriteRenderer>(true)
                    : new[] { target };
                EnsureTargetCapacity();
                return;
            }

            if (includeChildren)
            {
                cachedTargets = GetComponentsInChildren<SpriteRenderer>(true);
                EnsureTargetCapacity();
                return;
            }

            SpriteRenderer selfRenderer = GetComponent<SpriteRenderer>();
            cachedTargets = selfRenderer != null
                ? new[] { selfRenderer }
                : System.Array.Empty<SpriteRenderer>();
            EnsureTargetCapacity();
        }

        private void EnsureTargetCapacity()
        {
            int count = cachedTargets.Length;
            if (cachedBaseColors.Length != count)
            {
                cachedBaseColors = new Color[count];
            }

            if (cachedOriginalMaterials.Length != count)
            {
                cachedOriginalMaterials = new Material[count];
            }

            if (cachedFlashMaterials.Length != count)
            {
                cachedFlashMaterials = new Material[count];
            }
        }

        private void CaptureBaseColors()
        {
            EnsureTargetCapacity();

            for (int i = 0; i < cachedTargets.Length; i++)
            {
                SpriteRenderer spriteRenderer = cachedTargets[i];
                cachedBaseColors[i] = spriteRenderer != null ? spriteRenderer.color : Color.white;
            }
        }

        private void EnsureFlashMaterialsInstalled()
        {
            Material template = ResolveFlashMaterialTemplate();
            if (template == null)
            {
                Debug.LogWarning(
                    $"{nameof(CardHitFlashPerformance)} on '{name}' could not resolve flash material.",
                    this);
                return;
            }

            for (int i = 0; i < cachedTargets.Length; i++)
            {
                SpriteRenderer spriteRenderer = cachedTargets[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                if (cachedOriginalMaterials[i] == null)
                {
                    cachedOriginalMaterials[i] = spriteRenderer.sharedMaterial;
                }

                if (cachedFlashMaterials[i] == null)
                {
                    cachedFlashMaterials[i] = new Material(template);
                }

                spriteRenderer.sharedMaterial = cachedFlashMaterials[i];
            }

            materialsInstalled = true;
        }

        private Material ResolveFlashMaterialTemplate()
        {
            if (flashMaterialTemplate != null)
            {
                return flashMaterialTemplate;
            }

            if (resolvedFlashMaterialTemplate != null)
            {
                return resolvedFlashMaterialTemplate;
            }

            Shader shader = Shader.Find("TableNine/SpriteHitFlash");
            if (shader == null)
            {
                return null;
            }

            resolvedFlashMaterialTemplate = new Material(shader);
            return resolvedFlashMaterialTemplate;
        }

        private void RestoreBaseColors()
        {
            for (int i = 0; i < cachedTargets.Length; i++)
            {
                SpriteRenderer spriteRenderer = cachedTargets[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                spriteRenderer.color = cachedBaseColors[i];
                ApplyFlash(0f);
            }
        }

        private void ApplyFlash(float intensity)
        {
            EnsureFlashMaterialsInstalled();

            float amount = Mathf.Clamp01(intensity);

            for (int i = 0; i < cachedTargets.Length; i++)
            {
                SpriteRenderer spriteRenderer = cachedTargets[i];
                Material flashMaterial = cachedFlashMaterials[i];
                if (spriteRenderer == null || flashMaterial == null)
                {
                    continue;
                }

                flashMaterial.SetColor(HitFlashColorId, flashColor);
                flashMaterial.SetFloat(HitFlashAmountId, amount);
            }
        }

        private void RestoreOriginalMaterials()
        {
            if (!materialsInstalled)
            {
                return;
            }

            for (int i = 0; i < cachedTargets.Length; i++)
            {
                SpriteRenderer spriteRenderer = cachedTargets[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                Material originalMaterial = cachedOriginalMaterials[i];
                if (originalMaterial != null)
                {
                    spriteRenderer.sharedMaterial = originalMaterial;
                }
            }

            materialsInstalled = false;
        }

        private void DestroyInstalledMaterials()
        {
            for (int i = 0; i < cachedFlashMaterials.Length; i++)
            {
                Material material = cachedFlashMaterials[i];
                if (material == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }

                cachedFlashMaterials[i] = null;
            }
        }

        private void StopCurrentRoutine()
        {
            if (playRoutine == null)
            {
                return;
            }

            StopCoroutine(playRoutine);
            playRoutine = null;
        }
    }
}
