using System.Collections;
using UnityEngine;

namespace NineGrid.Presentation.Cards
{
    internal static class CardViewTween
    {
        public static IEnumerator PunchScale(Transform target, float intensity = 0.18f, float duration = 0.22f)
        {
            if (target == null)
            {
                yield break;
            }

            var baseScale = target.localScale;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var eased = EaseOutBack(t);
                var scale = 1f + intensity * (1f - eased);
                target.localScale = baseScale * scale;
                yield return null;
            }

            target.localScale = baseScale;
        }

        public static IEnumerator ScaleAppear(Transform target, Vector3 finalScale, float duration = 0.2f)
        {
            if (target == null)
            {
                yield break;
            }

            target.localScale = Vector3.zero;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                target.localScale = finalScale * EaseOutBack(t);
                yield return null;
            }

            target.localScale = finalScale;
        }

        public static IEnumerator ScaleDisappear(Transform target, Vector3 initialScale, float duration = 0.16f)
        {
            if (target == null)
            {
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                target.localScale = initialScale * (1f - EaseInBack(t));
                yield return null;
            }

            target.localScale = Vector3.zero;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }

        private static float EaseInBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return c3 * t * t * t - c1 * t * t;
        }
    }
}
