namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学关卡开局选项（主菜单教学入口 / 首次开局自动进入）。
    /// </summary>
    public sealed class TutorialRunOptions
    {
        /// <summary>教学通关后是否直接转入正式开局（首次「开始游戏」路径为 true；主菜单教学入口为 false）。</summary>
        public bool ContinueToFormalRun;
    }
}
