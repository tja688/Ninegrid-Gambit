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
        /// <summary>机关 contentId，经 <c>AddEnemyCard</c> 注入敌侧发牌池；与 <see cref="SkillIds"/> 并存。</summary>
        public IReadOnlyList<string> TrapContentIds = Array.Empty<string>();
        /// <summary>\0 跳格沙盒：拒对战、空盘、只生成玩家可走格。</summary>
        public bool WalkSandbox;
    }
}
