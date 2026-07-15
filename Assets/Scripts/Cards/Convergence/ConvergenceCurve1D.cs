using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 单标量维度的五次 Hermite / 最小 jerk 收敛曲线。
    /// 给定 p0、v0、p1、v1=0、a0=0、a1=0、T，解析唯一五次多项式；
    /// t=T 时精确命中 p1，末速度/加速度=0。
    /// 在归一化时间 s=t/T 上用闭式系数求值，避免小 T 时绝对时间系数病态。
    /// </summary>
    public readonly struct ConvergenceCurve1D
    {
        public const float MinDuration = 1e-5f;

        /// <summary>调用方比较位置时可用的容差（求值本身不依赖 epsilon）。</summary>
        public const float PositionEpsilon = 1e-4f;

        /// <summary>调用方比较速度/加速度时可用的容差。</summary>
        public const float VelocityEpsilon = 1e-3f;

        public float P0 { get; }
        public float V0 { get; }
        public float P1 { get; }
        public float Duration { get; }

        /// <summary>归一化多项式 P(s)=P0+V0Norm·s+B3·s³+B4·s⁴+B5·s⁵ 的系数（s∈[0,1]）。</summary>
        public float V0Norm { get; }
        public float B3 { get; }
        public float B4 { get; }
        public float B5 { get; }

        ConvergenceCurve1D(
            float p0,
            float v0,
            float p1,
            float duration,
            float v0Norm,
            float b3,
            float b4,
            float b5)
        {
            P0 = p0;
            V0 = v0;
            P1 = p1;
            Duration = duration;
            V0Norm = v0Norm;
            B3 = b3;
            B4 = b4;
            B5 = b5;
        }

        public static ConvergenceCurve1D Create(
            float p0,
            float v0,
            float p1,
            float sourceTime,
            ICornerReshapePolicy cornerPolicy = null)
        {
            cornerPolicy ??= SprintCornerReshapePolicy.Instance;
            cornerPolicy.Reshape(ref p0, ref v0, ref p1, ref sourceTime);

            var duration = Mathf.Max(sourceTime, MinDuration);
            SolveNormalizedClosedForm(p0, v0, p1, duration, out var v0Norm, out var b3, out var b4, out var b5);
            return new ConvergenceCurve1D(p0, v0, p1, duration, v0Norm, b3, b4, b5);
        }

        public float EvaluatePosition(float t)
        {
            // 端点硬返回边界条件：解析等式不吃 float 累加误差。
            if (t <= 0f)
            {
                return P0;
            }

            if (t >= Duration)
            {
                return P1;
            }

            var s = t / Duration;
            var s2 = s * s;
            var s3 = s2 * s;
            var s4 = s3 * s;
            var s5 = s4 * s;
            return P0 + V0Norm * s + B3 * s3 + B4 * s4 + B5 * s5;
        }

        public float EvaluateVelocity(float t)
        {
            if (t <= 0f)
            {
                return V0;
            }

            if (t >= Duration)
            {
                return 0f;
            }

            var s = t / Duration;
            var s2 = s * s;
            var s3 = s2 * s;
            var s4 = s3 * s;
            var dPds = V0Norm + 3f * B3 * s2 + 4f * B4 * s3 + 5f * B5 * s4;
            return dPds / Duration;
        }

        public float EvaluateAcceleration(float t)
        {
            if (t <= 0f || t >= Duration)
            {
                return 0f;
            }

            var s = t / Duration;
            var s2 = s * s;
            var s3 = s2 * s;
            var d2Pds2 = 6f * B3 * s + 12f * B4 * s2 + 20f * B5 * s3;
            return d2Pds2 / (Duration * Duration);
        }

        /// <summary>完成判定：解析时刻 t≥T，不靠位置 epsilon。</summary>
        public bool IsComplete(float t) => t >= Duration;

        /// <summary>
        /// 以当前时刻的位置与速度为新的 p0、v0，重解到新目标（C1 连续）。
        /// </summary>
        public ConvergenceCurve1D Redirect(
            float elapsedTime,
            float newP1,
            float newSourceTime,
            ICornerReshapePolicy cornerPolicy = null)
        {
            var clampedElapsed = Mathf.Clamp(elapsedTime, 0f, Duration);
            var currentP = EvaluatePosition(clampedElapsed);
            var currentV = EvaluateVelocity(clampedElapsed);
            return Create(currentP, currentV, newP1, newSourceTime, cornerPolicy);
        }

        /// <summary>
        /// 闭式解（a0=a1=0, v1=0）：
        /// vn = T·v0，δ = p1−p0−vn，
        /// B3=10δ+4vn，B4=−15δ−7vn，B5=6δ+3vn。
        /// v0=0 时退化为经典最小 jerk：10s³−15s⁴+6s⁵。
        /// </summary>
        static void SolveNormalizedClosedForm(
            float p0,
            float v0,
            float p1,
            float duration,
            out float v0Norm,
            out float b3,
            out float b4,
            out float b5)
        {
            v0Norm = duration * v0;
            var delta = p1 - p0 - v0Norm;
            b3 = 10f * delta + 4f * v0Norm;
            b4 = -15f * delta - 7f * v0Norm;
            b5 = 6f * delta + 3f * v0Norm;
        }
    }
}
