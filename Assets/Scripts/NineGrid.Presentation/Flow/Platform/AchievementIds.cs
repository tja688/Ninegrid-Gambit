namespace NineGrid.Flow.Platform
{
    /// <summary>
    /// 成就 API Name 目录（占位草案）。
    /// 字符串必须与 Steamworks 后台 App Admin → Stats &amp; Achievements 配置的
    /// API Name 完全一致；注册正式 AppId、定稿成就设计后按最终清单增删。
    /// 当前开发用 AppId 480 (Spacewar) 只有它自带的测试成就，
    /// 本目录里的占位 ID 在 480 上解锁会失败并打 Warning（正常现象）。
    /// </summary>
    public static class AchievementIds
    {
        public const string TutorialComplete = "ACH_TUTORIAL_COMPLETE";
        public const string FirstBattleWon = "ACH_FIRST_BATTLE_WON";
        public const string FirstRunClear = "ACH_FIRST_RUN_CLEAR";

        /// <summary>Spacewar (480) 自带的真实测试成就，开发期可用它验证解锁链路（会真的弹 Steam 通知）。</summary>
        public const string DevSpacewarWinOneGame = "ACH_WIN_ONE_GAME";
    }

    /// <summary>统计 API Name 目录（占位草案），约定同上。</summary>
    public static class StatIds
    {
        public const string BattlesWon = "stat_battles_won";
        public const string RunsCompleted = "stat_runs_completed";
    }
}
