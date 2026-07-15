namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 墙角策略注入点（唯一）。初版 X 冲刺 = 纯五次基线、不做 reshape；
    /// 未来 Y 过冲回勾可实现本接口，在 Solve 前调整边界或时间映射。
    /// </summary>
    public interface ICornerReshapePolicy
    {
        /// <summary>
        /// 在五次 Hermite 求解前可选地调整边界条件或 sourceTime。
        /// 默认冲刺策略应原样返回（零额外代码）。
        /// </summary>
        void Reshape(ref float p0, ref float v0, ref float p1, ref float sourceTime);
    }

    /// <summary>
    /// X 冲刺：纯五次基线，不修改任何参数。
    /// </summary>
    public sealed class SprintCornerReshapePolicy : ICornerReshapePolicy
    {
        public static readonly SprintCornerReshapePolicy Instance = new();

        public void Reshape(ref float p0, ref float v0, ref float p1, ref float sourceTime)
        {
        }
    }
}
