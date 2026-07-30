namespace NineGrid.Core
{
    public static class CoreCounterKeys
    {
        public const string GoldReward = "goldReward";
        public const string Elite = "elite";
        public const string Boss = "boss";
        public const string Level = "level";
        public const string EffectCounterPrefix = "effect.";
        /// <summary>
        /// 攻击模式行动倒计时保留前缀（ADR-0013）。与 <see cref="EffectCounterPrefix"/> 隔离，
        /// 同怪的模式计数与 DSL 效果计数互不偷值。
        /// </summary>
        public const string AttackPatternPrefix = "attackPattern.";
        /// <summary>攻击模式行动倒计时（进场按模式频率初始化；ADR-0011 / ADR-0013）。</summary>
        public const string AttackPatternCountdown = AttackPatternPrefix + "countdown";
        /// <summary>
        /// 背面专用回合计数前缀（ADR-0016）。与 <see cref="AttackPatternPrefix"/> /
        /// <see cref="EffectCounterPrefix"/> 隔离；仅已 Register 的键在背面向敌方报名时 Tick。
        /// </summary>
        public const string FaceDownTickPrefix = "faceDownTick.";
        /// <summary>实例来自玩家侧 run 卡组（节点末可回库；局内掉落不带此标记）。</summary>
        public const string PlayerSideDeck = "playerSideDeck";
    }
}
