namespace NineGrid.Flow
{
    /// <summary>
    /// 开局选项；QuickTest 细节复用 <see cref="QuickTestRunOptions"/>。
    /// </summary>
    public sealed class GameFlowRunOptions
    {
        public bool TestMode = true;

        public bool QuickTestMode;

        public QuickTestRunOptions QuickTest;
    }
}
