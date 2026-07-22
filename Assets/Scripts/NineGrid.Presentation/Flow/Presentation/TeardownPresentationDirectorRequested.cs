namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 流程壳离开战斗生命周期时请求宿主 HardClear / Teardown 导演。
    /// </summary>
    public struct TeardownPresentationDirectorRequested
    {
        public IntentClearReason Reason;
    }
}
