namespace NineGrid.Flow
{
    public enum QuickTestNodeOrderMode
    {
        Shuffled,
        Sequential,
    }

    /// <summary>
    /// DevTest 快速测试开局选项（主菜单 \ 选关）。
    /// </summary>
    public sealed class QuickTestRunOptions
    {
        public QuickTestNodeOrderMode NodeOrder = QuickTestNodeOrderMode.Shuffled;
        public string PinnedFirstBattleDeckId;
    }
}
