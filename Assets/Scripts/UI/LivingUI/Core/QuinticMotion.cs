using System;
using UnityEngine;

namespace NineGrid.LivingUI
{
    public readonly struct QuinticMotion
    {
        public const float MinimumDuration = 0.0001f;

        private readonly Func<float, float> _easing;
        private readonly Func<float, float> _easingDerivative;

        private QuinticMotion(float p0, float v0, float p1, float duration, float v0Normalized,
            float b3, float b4, float b5,
            Func<float, float> easing, Func<float, float> easingDerivative)
        {
            P0 = p0;
            V0 = v0;
            P1 = p1;
            Duration = duration;
            V0Normalized = v0Normalized;
            B3 = b3;
            B4 = b4;
            B5 = b5;
            _easing = easing;
            _easingDerivative = easingDerivative;
        }

        public float P0 { get; }
        public float V0 { get; }
        public float P1 { get; }
        public float Duration { get; }
        public float V0Normalized { get; }
        public float B3 { get; }
        public float B4 { get; }
        public float B5 { get; }

        public static QuinticMotion Create(float p0, float v0, float p1, float duration,
            Func<float, float> easing = null, Func<float, float> easingDerivative = null)
        {
            duration = Mathf.Max(duration, MinimumDuration);

            if (easing != null && Mathf.Abs(v0) < 0.0001f)
            {
                return CreateEased(p0, p1, duration, easing, easingDerivative);
            }

            var v0Normalized = duration * v0;
            var delta = p1 - p0 - v0Normalized;
            return new QuinticMotion(
                p0,
                v0,
                p1,
                duration,
                v0Normalized,
                10f * delta + 4f * v0Normalized,
                -15f * delta - 7f * v0Normalized,
                6f * delta + 3f * v0Normalized,
                null,
                null);
        }

        private static QuinticMotion CreateEased(float p0, float p1, float duration,
            Func<float, float> easing, Func<float, float> easingDerivative)
        {
            return new QuinticMotion(
                p0,
                0f,
                p1,
                duration,
                0f,
                0f, 0f, 0f,
                easing,
                easingDerivative ?? (t => NumericalDerivative(easing, t)));
        }

        private static float NumericalDerivative(Func<float, float> f, float t, float h = 0.001f)
        {
            var t0 = Mathf.Max(0f, t - h);
            var t1 = Mathf.Min(1f, t + h);
            return (f(t1) - f(t0)) / (t1 - t0);
        }

        public float EvaluatePosition(float time)
        {
            if (time <= 0f) return P0;
            if (time >= Duration) return P1;

            var s = time / Duration;

            if (_easing != null)
            {
                return P0 + (P1 - P0) * _easing(s);
            }

            var s2 = s * s;
            var s3 = s2 * s;
            var s4 = s3 * s;
            var s5 = s4 * s;
            return P0 + V0Normalized * s + B3 * s3 + B4 * s4 + B5 * s5;
        }

        public float EvaluateVelocity(float time)
        {
            if (time <= 0f) return V0;
            if (time >= Duration) return 0f;

            var s = time / Duration;

            if (_easingDerivative != null)
            {
                return (P1 - P0) * _easingDerivative(s) / Duration;
            }

            var s2 = s * s;
            var s3 = s2 * s;
            var s4 = s3 * s;
            return (V0Normalized + 3f * B3 * s2 + 4f * B4 * s3 + 5f * B5 * s4) / Duration;
        }

        public bool StaysWithin(float minimum, float maximum, int sampleCount = 48)
        {
            for (var index = 0; index <= sampleCount; index++)
            {
                var value = EvaluatePosition(Duration * index / sampleCount);
                if (value < minimum - 0.0001f || value > maximum + 0.0001f)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public readonly struct BoundedScalarMotion
    {
        private BoundedScalarMotion(QuinticMotion first, QuinticMotion second, bool hasSecond)
        {
            First = first;
            Second = second;
            HasSecond = hasSecond;
        }

        public QuinticMotion First { get; }
        public QuinticMotion Second { get; }
        public bool HasSecond { get; }
        public float Duration => First.Duration + (HasSecond ? Second.Duration : 0f);

        public static BoundedScalarMotion Create(
            float source,
            float sourceVelocity,
            float target,
            float duration,
            float minimum,
            float maximum,
            Func<float, float> easing = null,
            Func<float, float> easingDerivative = null)
        {
            if (minimum > maximum) throw new ArgumentException("标量运动边界无效。");
            if (source < minimum || source > maximum || target < minimum || target > maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(target), "源值与目标值必须处于运动边界内。");
            }

            var direct = QuinticMotion.Create(source, sourceVelocity, target, duration, easing, easingDerivative);
            if (direct.StaysWithin(minimum, maximum))
            {
                return new BoundedScalarMotion(direct, default, false);
            }

            var brakeDuration = Mathf.Min(duration * 0.24f, 0.08f);
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var brakeTarget = source + sourceVelocity * brakeDuration * 0.18f;
                if (brakeTarget >= minimum && brakeTarget <= maximum)
                {
                    var brake = QuinticMotion.Create(source, sourceVelocity, brakeTarget, brakeDuration, easing, easingDerivative);
                    var arrival = QuinticMotion.Create(brakeTarget, 0f, target, Mathf.Max(duration - brakeDuration, 0.02f), easing, easingDerivative);
                    if (brake.StaysWithin(minimum, maximum) && arrival.StaysWithin(minimum, maximum))
                    {
                        return new BoundedScalarMotion(brake, arrival, true);
                    }
                }

                brakeDuration *= 0.5f;
            }

            throw new InvalidOperationException("无法在给定边界内连续制动尺寸通道。");
        }

        public float EvaluatePosition(float time)
        {
            if (!HasSecond || time <= First.Duration) return First.EvaluatePosition(time);
            return Second.EvaluatePosition(time - First.Duration);
        }

        public float EvaluateVelocity(float time)
        {
            if (!HasSecond || time <= First.Duration) return First.EvaluateVelocity(time);
            return Second.EvaluateVelocity(time - First.Duration);
        }
    }
}
