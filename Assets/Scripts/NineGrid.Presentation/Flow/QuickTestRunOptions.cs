using System;
using System.Collections.Generic;

namespace NineGrid.Flow
{
    public enum QuickTestNodeOrderMode
    {
        Shuffled,
        Sequential,
    }

    /// <summary>
    /// DevTest 快速测试开局选项（主菜单 \ 选通道）。
    /// </summary>
    public sealed class QuickTestRunOptions
    {
        public QuickTestNodeOrderMode NodeOrder = QuickTestNodeOrderMode.Shuffled;
        public string PinnedFirstBattleDeckId;
        public IReadOnlyList<string> SkillIds = Array.Empty<string>();
    }
}
