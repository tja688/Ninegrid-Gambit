using System.Collections;
using UnityEngine;

namespace NineGrid.Presentation.AtomicRepresentationTools
{
    [DisallowMultipleComponent]
    public sealed class TableNineSpriteHitFlashPlayer : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private bool includeChildren;

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
                return;
            }

            if (includeChildren)
            {
                cachedTargets = GetComponentsInChildren<SpriteRenderer>(true);
                return;
            }

            SpriteRenderer selfRenderer = GetComponent<SpriteRenderer>();
            cachedTargets = selfRenderer != null
                ? new[] { selfRenderer }
                : System.Array.Empty<SpriteRenderer>();
        }

        private void CaptureBaseColors()
        {
            if (cachedBaseColors.Length != cachedTargets.Length)
            {
                cachedBaseColors = new Color[cachedTargets.Length];
            }

            for (int i = 0; i < cachedTargets.Length; i++)
            {
                SpriteRenderer spriteRenderer = cachedTargets[i];
                cachedBaseColors[i] = spriteRenderer != null ? spriteRenderer.color : Color.white;
            }
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
            }
        }

        private void ApplyFlash(float intensity)
        {
            for (int i = 0; i < cachedTargets.Length; i++)
            {
                SpriteRenderer spriteRenderer = cachedTargets[i];
                if (spriteRenderer == null)
                {
                    continue;
                }

                Color baseColor = cachedBaseColors[i];
                Color blendedColor = Color.LerpUnclamped(baseColor, flashColor, intensity);
                blendedColor.a = baseColor.a;
                spriteRenderer.color = blendedColor;
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
