using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 多维收敛曲线：各标量维度独立求解同一套五次 Hermite 基元，行为一致。
    /// 初版用于 Vector2/Vector3 位移；将来缩放/透明度可复用同一结构。
    /// </summary>
    public readonly struct ConvergenceCurve
    {
        public ConvergenceCurve1D X { get; }
        public ConvergenceCurve1D Y { get; }
        public ConvergenceCurve1D Z { get; }

        ConvergenceCurve(ConvergenceCurve1D x, ConvergenceCurve1D y, ConvergenceCurve1D z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float Duration => X.Duration;

        public static ConvergenceCurve Create(
            Vector3 p0,
            Vector3 v0,
            Vector3 p1,
            float sourceTime,
            ICornerReshapePolicy cornerPolicy = null)
        {
            return new ConvergenceCurve(
                ConvergenceCurve1D.Create(p0.x, v0.x, p1.x, sourceTime, cornerPolicy),
                ConvergenceCurve1D.Create(p0.y, v0.y, p1.y, sourceTime, cornerPolicy),
                ConvergenceCurve1D.Create(p0.z, v0.z, p1.z, sourceTime, cornerPolicy));
        }

        public static ConvergenceCurve Create(
            Vector2 p0,
            Vector2 v0,
            Vector2 p1,
            float sourceTime,
            ICornerReshapePolicy cornerPolicy = null)
        {
            return Create(new Vector3(p0.x, p0.y, 0f), new Vector3(v0.x, v0.y, 0f), new Vector3(p1.x, p1.y, 0f), sourceTime, cornerPolicy);
        }

        public static ConvergenceCurve CreateScalar(
            float p0,
            float v0,
            float p1,
            float sourceTime,
            ICornerReshapePolicy cornerPolicy = null)
        {
            return Create(new Vector3(p0, 0f, 0f), new Vector3(v0, 0f, 0f), new Vector3(p1, 0f, 0f), sourceTime, cornerPolicy);
        }

        public Vector3 EvaluatePosition(float t)
        {
            return new Vector3(
                X.EvaluatePosition(t),
                Y.EvaluatePosition(t),
                Z.EvaluatePosition(t));
        }

        public Vector3 EvaluateVelocity(float t)
        {
            return new Vector3(
                X.EvaluateVelocity(t),
                Y.EvaluateVelocity(t),
                Z.EvaluateVelocity(t));
        }

        public Vector3 EvaluateAcceleration(float t)
        {
            return new Vector3(
                X.EvaluateAcceleration(t),
                Y.EvaluateAcceleration(t),
                Z.EvaluateAcceleration(t));
        }

        public bool IsComplete(float t) => X.IsComplete(t);

        /// <summary>
        /// 中途换目标：以当前位置+速度重解（各维度 C1 连续）。
        /// </summary>
        public ConvergenceCurve Redirect(
            float elapsedTime,
            Vector3 newP1,
            float newSourceTime,
            ICornerReshapePolicy cornerPolicy = null)
        {
            return new ConvergenceCurve(
                X.Redirect(elapsedTime, newP1.x, newSourceTime, cornerPolicy),
                Y.Redirect(elapsedTime, newP1.y, newSourceTime, cornerPolicy),
                Z.Redirect(elapsedTime, newP1.z, newSourceTime, cornerPolicy));
        }
    }
}
