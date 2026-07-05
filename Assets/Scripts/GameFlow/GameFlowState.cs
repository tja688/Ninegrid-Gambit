namespace NineGrid.GameFlow
{
    /// <summary>
    /// 全局主流程状态。线性战役节点按枚举声明顺序推进（MainMenu 除外）。
    /// </summary>
    public enum GameFlowState
    {
        MainMenu = 0,

        /// <summary>序章教学（含对话的完整入场）。</summary>
        Prologue,

        /// <summary>无教学对话的战斗0（调试 / 特殊入口）。</summary>
        Battle0,

        Island1,
        Route1,
        Event1,
        Battle1,

        Island2,
        Route2,
        Event2,
        Battle2,

        Island3,
        Route3,
        Event3,
        Battle3Elite,

        /// <summary>精英奖励岛屿。</summary>
        IslandEliteReward,

        Battle4,
        Island4,
        Route4,
        Event4,
        Battle5,

        Island5,
        Route5,
        Event5,
        Battle6,

        Island6,
        Route6,
        Event6,
        BossBattle,

        VictorySettlement,
    }
}
