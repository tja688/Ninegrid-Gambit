namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 编排时间线上的原子 Step。表现侧一律称 Step，不用 Action。
    /// </summary>
    public interface ITimelineStep
    {
        TimelineStepStatus Tick(float deltaTime);
    }
}
