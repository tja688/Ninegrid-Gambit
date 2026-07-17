using System;
using UnityEngine;

namespace NineGrid.LivingUI
{
    /// <summary>
    /// 基础缓动类型枚举；Custom 时使用 LivingUiTransitionStyle 中的自定义 AnimationCurve。
    /// </summary>
    public enum LivingUiEasingType
    {
        Linear,
        EaseInSine, EaseOutSine, EaseInOutSine,
        EaseInQuad, EaseOutQuad, EaseInOutQuad,
        EaseInCubic, EaseOutCubic, EaseInOutCubic,
        EaseInQuart, EaseOutQuart, EaseInOutQuart,
        EaseInQuint, EaseOutQuint, EaseInOutQuint,
        EaseInExpo, EaseOutExpo, EaseInOutExpo,
        EaseInCirc, EaseOutCirc, EaseInOutCirc,
        EaseInBack, EaseOutBack, EaseInOutBack,
        EaseInElastic, EaseOutElastic, EaseInOutElastic,
        EaseInBounce, EaseOutBounce, EaseInOutBounce,
        Custom,
    }

    /// <summary>
    /// 缓动函数静态工具：每个枚举值对应的 t→value 映射及其解析导数。
    /// </summary>
    public static class LivingUiEasing
    {
        public static Func<float, float> GetEasing(LivingUiEasingType type, AnimationCurve customCurve = null)
        {
            switch (type)
            {
                case LivingUiEasingType.Linear: return Linear;
                case LivingUiEasingType.EaseInSine: return EaseInSine;
                case LivingUiEasingType.EaseOutSine: return EaseOutSine;
                case LivingUiEasingType.EaseInOutSine: return EaseInOutSine;
                case LivingUiEasingType.EaseInQuad: return EaseInQuad;
                case LivingUiEasingType.EaseOutQuad: return EaseOutQuad;
                case LivingUiEasingType.EaseInOutQuad: return EaseInOutQuad;
                case LivingUiEasingType.EaseInCubic: return EaseInCubic;
                case LivingUiEasingType.EaseOutCubic: return EaseOutCubic;
                case LivingUiEasingType.EaseInOutCubic: return EaseInOutCubic;
                case LivingUiEasingType.EaseInQuart: return EaseInQuart;
                case LivingUiEasingType.EaseOutQuart: return EaseOutQuart;
                case LivingUiEasingType.EaseInOutQuart: return EaseInOutQuart;
                case LivingUiEasingType.EaseInQuint: return EaseInQuint;
                case LivingUiEasingType.EaseOutQuint: return EaseOutQuint;
                case LivingUiEasingType.EaseInOutQuint: return EaseInOutQuint;
                case LivingUiEasingType.EaseInExpo: return EaseInExpo;
                case LivingUiEasingType.EaseOutExpo: return EaseOutExpo;
                case LivingUiEasingType.EaseInOutExpo: return EaseInOutExpo;
                case LivingUiEasingType.EaseInCirc: return EaseInCirc;
                case LivingUiEasingType.EaseOutCirc: return EaseOutCirc;
                case LivingUiEasingType.EaseInOutCirc: return EaseInOutCirc;
                case LivingUiEasingType.EaseInBack: return EaseInBack;
                case LivingUiEasingType.EaseOutBack: return EaseOutBack;
                case LivingUiEasingType.EaseInOutBack: return EaseInOutBack;
                case LivingUiEasingType.EaseInElastic: return EaseInElastic;
                case LivingUiEasingType.EaseOutElastic: return EaseOutElastic;
                case LivingUiEasingType.EaseInOutElastic: return EaseInOutElastic;
                case LivingUiEasingType.EaseInBounce: return EaseInBounce;
                case LivingUiEasingType.EaseOutBounce: return EaseOutBounce;
                case LivingUiEasingType.EaseInOutBounce: return EaseInOutBounce;
                case LivingUiEasingType.Custom:
                    return customCurve != null ? t => customCurve.Evaluate(t) : Linear;
                default:
                    return Linear;
            }
        }

        public static Func<float, float> GetDerivative(LivingUiEasingType type, AnimationCurve customCurve = null)
        {
            switch (type)
            {
                case LivingUiEasingType.Linear: return LinearDerivative;
                case LivingUiEasingType.EaseInSine: return EaseInSineDerivative;
                case LivingUiEasingType.EaseOutSine: return EaseOutSineDerivative;
                case LivingUiEasingType.EaseInOutSine: return EaseInOutSineDerivative;
                case LivingUiEasingType.EaseInQuad: return EaseInQuadDerivative;
                case LivingUiEasingType.EaseOutQuad: return EaseOutQuadDerivative;
                case LivingUiEasingType.EaseInOutQuad: return EaseInOutQuadDerivative;
                case LivingUiEasingType.EaseInCubic: return EaseInCubicDerivative;
                case LivingUiEasingType.EaseOutCubic: return EaseOutCubicDerivative;
                case LivingUiEasingType.EaseInOutCubic: return EaseInOutCubicDerivative;
                case LivingUiEasingType.EaseInQuart: return EaseInQuartDerivative;
                case LivingUiEasingType.EaseOutQuart: return EaseOutQuartDerivative;
                case LivingUiEasingType.EaseInOutQuart: return EaseInOutQuartDerivative;
                case LivingUiEasingType.EaseInQuint: return EaseInQuintDerivative;
                case LivingUiEasingType.EaseOutQuint: return EaseOutQuintDerivative;
                case LivingUiEasingType.EaseInOutQuint: return EaseInOutQuintDerivative;
                case LivingUiEasingType.EaseInExpo: return EaseInExpoDerivative;
                case LivingUiEasingType.EaseOutExpo: return EaseOutExpoDerivative;
                case LivingUiEasingType.EaseInOutExpo: return EaseInOutExpoDerivative;
                case LivingUiEasingType.EaseInCirc: return EaseInCircDerivative;
                case LivingUiEasingType.EaseOutCirc: return EaseOutCircDerivative;
                case LivingUiEasingType.EaseInOutCirc: return EaseInOutCircDerivative;
                case LivingUiEasingType.EaseInBack: return EaseInBackDerivative;
                case LivingUiEasingType.EaseOutBack: return EaseOutBackDerivative;
                case LivingUiEasingType.EaseInOutBack: return EaseInOutBackDerivative;
                case LivingUiEasingType.EaseInElastic: return EaseInElasticDerivative;
                case LivingUiEasingType.EaseOutElastic: return EaseOutElasticDerivative;
                case LivingUiEasingType.EaseInOutElastic: return EaseInOutElasticDerivative;
                case LivingUiEasingType.EaseInBounce: return EaseInBounceDerivative;
                case LivingUiEasingType.EaseOutBounce: return EaseOutBounceDerivative;
                case LivingUiEasingType.EaseInOutBounce: return EaseInOutBounceDerivative;
                case LivingUiEasingType.Custom:
                    return customCurve != null
                        ? t => NumericalDerivative(customCurve, t)
                        : LinearDerivative;
                default:
                    return LinearDerivative;
            }
        }

        private static float NumericalDerivative(AnimationCurve curve, float t, float h = 0.001f)
        {
            var t0 = Mathf.Max(0f, t - h);
            var t1 = Mathf.Min(1f, t + h);
            return (curve.Evaluate(t1) - curve.Evaluate(t0)) / (t1 - t0);
        }

        // ── 缓动函数 ──────────────────────────────

        public static float Linear(float t) => t;
        public static float LinearDerivative(float t) => 1f;

        public static float EaseInSine(float t) => 1f - Mathf.Cos(t * Mathf.PI * 0.5f);
        public static float EaseInSineDerivative(float t) => Mathf.PI * 0.5f * Mathf.Sin(t * Mathf.PI * 0.5f);

        public static float EaseOutSine(float t) => Mathf.Sin(t * Mathf.PI * 0.5f);
        public static float EaseOutSineDerivative(float t) => Mathf.PI * 0.5f * Mathf.Cos(t * Mathf.PI * 0.5f);

        public static float EaseInOutSine(float t) => -0.5f * (Mathf.Cos(Mathf.PI * t) - 1f);
        public static float EaseInOutSineDerivative(float t) => Mathf.PI * 0.5f * Mathf.Sin(Mathf.PI * t);

        public static float EaseInQuad(float t) => t * t;
        public static float EaseInQuadDerivative(float t) => 2f * t;

        public static float EaseOutQuad(float t) => t * (2f - t);
        public static float EaseOutQuadDerivative(float t) => 2f - 2f * t;

        public static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
        public static float EaseInOutQuadDerivative(float t) => t < 0.5f ? 4f * t : 4f * (1f - t);

        public static float EaseInCubic(float t) => t * t * t;
        public static float EaseInCubicDerivative(float t) => 3f * t * t;

        public static float EaseOutCubic(float t) { var u = 1f - t; return 1f - u * u * u; }
        public static float EaseOutCubicDerivative(float t) { var u = 1f - t; return 3f * u * u; }

        public static float EaseInOutCubic(float t) => t < 0.5f ? 4f * t * t * t : 1f - 4f * (1f - t) * (1f - t) * (1f - t);
        public static float EaseInOutCubicDerivative(float t) => t < 0.5f ? 12f * t * t : 12f * (1f - t) * (1f - t);

        public static float EaseInQuart(float t) => t * t * t * t;
        public static float EaseInQuartDerivative(float t) => 4f * t * t * t;

        public static float EaseOutQuart(float t) { var u = 1f - t; return 1f - u * u * u * u; }
        public static float EaseOutQuartDerivative(float t) { var u = 1f - t; return 4f * u * u * u; }

        public static float EaseInOutQuart(float t) => t < 0.5f ? 8f * t * t * t * t : 1f - 8f * (1f - t) * (1f - t) * (1f - t) * (1f - t);
        public static float EaseInOutQuartDerivative(float t) => t < 0.5f ? 32f * t * t * t : 32f * (1f - t) * (1f - t) * (1f - t);

        public static float EaseInQuint(float t) => t * t * t * t * t;
        public static float EaseInQuintDerivative(float t) => 5f * t * t * t * t;

        public static float EaseOutQuint(float t) { var u = 1f - t; return 1f - u * u * u * u * u; }
        public static float EaseOutQuintDerivative(float t) { var u = 1f - t; return 5f * u * u * u * u; }

        public static float EaseInOutQuint(float t) => t < 0.5f ? 16f * t * t * t * t * t : 1f - 16f * (1f - t) * (1f - t) * (1f - t) * (1f - t) * (1f - t);
        public static float EaseInOutQuintDerivative(float t) => t < 0.5f ? 80f * t * t * t * t : 80f * (1f - t) * (1f - t) * (1f - t) * (1f - t);

        public static float EaseInExpo(float t) => t <= 0f ? 0f : Mathf.Pow(2f, 10f * (t - 1f));
        public static float EaseInExpoDerivative(float t) => t <= 0f ? 0f : 10f * Mathf.Log(2f) * Mathf.Pow(2f, 10f * (t - 1f));

        public static float EaseOutExpo(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
        public static float EaseOutExpoDerivative(float t) => t >= 1f ? 0f : 10f * Mathf.Log(2f) * Mathf.Pow(2f, -10f * t);

        public static float EaseInOutExpo(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            if (t < 0.5f) return 0.5f * Mathf.Pow(2f, 20f * t - 10f);
            return 1f - 0.5f * Mathf.Pow(2f, -20f * t + 10f);
        }

        public static float EaseInOutExpoDerivative(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            if (t < 0.5f) return 10f * Mathf.Log(2f) * Mathf.Pow(2f, 20f * t - 10f);
            return 10f * Mathf.Log(2f) * Mathf.Pow(2f, -20f * t + 10f);
        }

        public static float EaseInCirc(float t) => 1f - Mathf.Sqrt(1f - t * t);
        public static float EaseInCircDerivative(float t) => t <= 0f || t >= 1f ? 0f : t / Mathf.Sqrt(1f - t * t);

        public static float EaseOutCirc(float t) { var u = 1f - t; return Mathf.Sqrt(1f - u * u); }
        public static float EaseOutCircDerivative(float t) { var u = 1f - t; return u >= 1f ? 0f : u / Mathf.Sqrt(1f - u * u); }

        public static float EaseInOutCirc(float t)
        {
            if (t < 0.5f) return 0.5f * (1f - Mathf.Sqrt(1f - 4f * t * t));
            return 0.5f * (Mathf.Sqrt(1f - 4f * (1f - t) * (1f - t)) + 1f);
        }

        public static float EaseInOutCircDerivative(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            if (t < 0.5f) return 2f * t / Mathf.Sqrt(1f - 4f * t * t);
            var u = 1f - t;
            return 2f * u / Mathf.Sqrt(1f - 4f * u * u);
        }

        public static float EaseInBack(float t)
        {
            const float c = 1.70158f;
            return t * t * ((c + 1f) * t - c);
        }

        public static float EaseInBackDerivative(float t)
        {
            const float c = 1.70158f;
            return t * (3f * (c + 1f) * t - 2f * c);
        }

        public static float EaseOutBack(float t)
        {
            const float c = 1.70158f;
            var u = 1f - t;
            return 1f + u * u * ((c + 1f) * u - c);
        }

        public static float EaseOutBackDerivative(float t)
        {
            const float c = 1.70158f;
            var u = 1f - t;
            return u * (3f * (c + 1f) * u - 2f * c);
        }

        public static float EaseInOutBack(float t)
        {
            const float c = 1.70158f * 1.525f;
            if (t < 0.5f) return 2f * t * t * ((c + 1f) * 2f * t - c);
            var u = 1f - t;
            return 1f + 2f * u * u * ((c + 1f) * 2f * u - c);
        }

        public static float EaseInOutBackDerivative(float t)
        {
            const float c = 1.70158f * 1.525f;
            if (t < 0.5f) return 2f * t * (6f * (c + 1f) * t - 2f * c);
            var u = 1f - t;
            return 2f * u * (6f * (c + 1f) * u - 2f * c);
        }

        public static float EaseInElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return -Mathf.Pow(2f, 10f * (t - 1f)) * Mathf.Sin((t - 1.075f) * (2f * Mathf.PI) / 0.3f);
        }

        public static float EaseInElasticDerivative(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            var a = Mathf.Pow(2f, 10f * (t - 1f));
            var b = Mathf.Sin((t - 1.075f) * (2f * Mathf.PI) / 0.3f);
            var c = Mathf.Cos((t - 1.075f) * (2f * Mathf.PI) / 0.3f);
            var d = 10f * Mathf.Log(2f);
            var e = (2f * Mathf.PI) / 0.3f;
            return -a * (d * b + e * c);
        }

        public static float EaseOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t - 0.075f) * (2f * Mathf.PI) / 0.3f) + 1f;
        }

        public static float EaseOutElasticDerivative(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            var a = Mathf.Pow(2f, -10f * t);
            var b = Mathf.Sin((t - 0.075f) * (2f * Mathf.PI) / 0.3f);
            var c = Mathf.Cos((t - 0.075f) * (2f * Mathf.PI) / 0.3f);
            var d = -10f * Mathf.Log(2f);
            var e = (2f * Mathf.PI) / 0.3f;
            return a * (d * b + e * c);
        }

        public static float EaseInOutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            if (t < 0.5f)
                return -0.5f * Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI) / 4.5f);
            return 0.5f * Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * (2f * Mathf.PI) / 4.5f) + 1f;
        }

        public static float EaseInOutElasticDerivative(float t)
        {
            if (t <= 0f || t >= 1f) return 0f;
            const float k = 2f * Mathf.PI / 4.5f;
            if (t < 0.5f)
            {
                var a = Mathf.Pow(2f, 20f * t - 10f);
                var arg = (20f * t - 11.125f) * k;
                var d = 20f * Mathf.Log(2f);
                return -0.5f * a * (d * Mathf.Sin(arg) + 20f * k * Mathf.Cos(arg));
            }

            var a2 = Mathf.Pow(2f, -20f * t + 10f);
            var arg2 = (20f * t - 11.125f) * k;
            var d2 = -20f * Mathf.Log(2f);
            return 0.5f * a2 * (d2 * Mathf.Sin(arg2) + 20f * k * Mathf.Cos(arg2));
        }

        public static float EaseInBounce(float t) => 1f - EaseOutBounce(1f - t);

        public static float EaseInBounceDerivative(float t) => EaseOutBounceDerivative(1f - t);

        public static float EaseOutBounce(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { var u = t - 1.5f / d1; return n1 * u * u + 0.75f; }
            if (t < 2.5f / d1) { var u = t - 2.25f / d1; return n1 * u * u + 0.9375f; }
            var u2 = t - 2.625f / d1;
            return n1 * u2 * u2 + 0.984375f;
        }

        public static float EaseOutBounceDerivative(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;
            if (t < 1f / d1) return 2f * n1 * t;
            if (t < 2f / d1) return 2f * n1 * (t - 1.5f / d1);
            if (t < 2.5f / d1) return 2f * n1 * (t - 2.25f / d1);
            return 2f * n1 * (t - 2.625f / d1);
        }

        public static float EaseInOutBounce(float t)
            => t < 0.5f ? 0.5f * EaseInBounce(2f * t) : 0.5f * EaseOutBounce(2f * t - 1f) + 0.5f;

        public static float EaseInOutBounceDerivative(float t)
            => t < 0.5f ? EaseInBounceDerivative(2f * t) : EaseOutBounceDerivative(2f * t - 1f);
    }
}
