namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// IntentIntake 对一次提交的分型处置（#50 / ADR-0004）。
    /// </summary>
    public enum IntentDisposition
    {
        /// <summary>主线 idle：已交给 Director 建脚本，或模式/模态可立即执行。</summary>
        Allow = 0,

        /// <summary>主线 busy：已 latest-wins 缓冲进 Director。</summary>
        BufferToDirector = 1,

        /// <summary>两轴门禁或合法性拒绝；不缓冲。</summary>
        Reject = 2,

        /// <summary>UseItem 命中 MultiBoardSelect：交由棋盘选择模式解释（调用方再提交 BoardSelect begin）。</summary>
        RouteToBoardSelect = 3,
    }
}
