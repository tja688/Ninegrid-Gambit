namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 编排步骤步进结果：未完成则驻留，完成后时间线前进；Aborted 清空剩余剧本。
    /// </summary>
    public enum TimelineStepStatus
    {
        Continue = 0,
        Finished = 1,
        /// <summary>终态失败：时间线清空，主线立即 idle（勿与 Continue 驻留混淆）。</summary>
        Aborted = 2,
    }

    /// <summary>
    /// ResolveBatch 开门结果：仅 WaitHasOpen 可驻留重试；Failed 为终态，须中止剧本。
    /// </summary>
    public enum BatchOpenResult
    {
        Opened = 0,
        WaitHasOpen = 1,
        Failed = 2,
    }
}
