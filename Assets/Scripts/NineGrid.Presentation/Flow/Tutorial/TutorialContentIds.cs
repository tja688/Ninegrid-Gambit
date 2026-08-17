namespace NineGrid.Flow.Tutorial
{
    /// <summary>教学关卡内容主键与提示卡标签（五阶段剧本）。</summary>
    public static class TutorialContentIds
    {
        public const string TutorialTrapPrefix = "trap.tutorial.";
        public const string HintTag = "tutorial.hint";

        public const string DummyTrapDefId = "trap.tutorial.aim";
        public const string KnifeDefId = "help.throwing_knife";
        public const string PotionDefId = "help.healing_potion";

        public const string ActionDummyDefId = "monster.tutorial.action_dummy";
        public const string MoveDummyDefId = "monster.tutorial.move_dummy";
        public const string SteelSlimeDefId = "monster.tutorial.steel_slime";

        public const string Phase1HintA = "trap.tutorial.advance";
        public const string Phase1HintB = "trap.tutorial.countdown";
        public const string Phase2HintA = "trap.tutorial.attack";
        public const string Phase2HintB = "trap.tutorial.boss_door";
        public const string Phase3HintA = "trap.tutorial.fire";
        public const string Phase3HintB = "trap.tutorial.greed";
        public const string Phase4HintA = "trap.tutorial.early_leave";
        public const string Phase4HintB = "trap.tutorial.filler";
        public const string Phase5HintA = "trap.tutorial.deal";
        public const string Phase5HintB = "trap.tutorial.door";

        public const string NoticeAttackDummy =
            "请攻击教学假人，击败以进入下一教学阶段。";

        public const string NoticeOutOfRange = "目标不在攻击范围内";
    }
}
