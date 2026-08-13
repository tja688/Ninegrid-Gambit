namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 攻击/受击表演全局速度（ADR-0050）：交战 rig（起手/冲刺/命中/击退/回位）、效果打击表演
    /// 与触发节拍统一按此倍率提速。调速只改这里，禁止在各调用点散写倍率。
    /// </summary>
    public static class BattlePresentationSpeed
    {
        /// <summary>交战/受击表演统一倍速（2 = 时长减半）。</summary>
        public const float CombatMultiplier = 2f;

        /// <summary>把基准秒数换算成提速后的实际秒数。</summary>
        public static float ScaleSeconds(float baselineSeconds)
        {
            return baselineSeconds / CombatMultiplier;
        }
    }
}
