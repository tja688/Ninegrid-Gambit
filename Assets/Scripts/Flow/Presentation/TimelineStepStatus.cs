namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 编排步骤步进结果：未完成则驻留，完成后时间线前进。
    /// </summary>
    public enum TimelineStepStatus
    {
        Continue = 0,
        Finished = 1,
    }
}
