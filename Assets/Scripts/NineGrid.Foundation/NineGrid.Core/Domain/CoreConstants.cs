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
        /// <summary>实例来自本关玩家侧装填（开局生成 / 注入）；清关时与其它帮助卡一并结算移除。</summary>
        public const string PlayerSideDeck = "playerSideDeck";
    }

    /// <summary>邻接图腾借甲光环：在目标卡上按来源 uid 记录借出前的 CurrentArmor 基线。</summary>
    public static class BorrowedArmorAuraKeys
    {
        /// <summary>
        /// 借甲自然流失（离开邻接）事件 cause：图腾不主动索取，
        /// 表现层不得把该负甲变化编排成「图腾攻击目标」的打击表演。
        /// </summary>
        public const string DecayCause = "borrowedArmorDecay";

        public static string BaselineKey(int sourceUid)
        {
            return CoreCounterKeys.EffectCounterPrefix + "borrowedArmor." + sourceUid;
        }

        public static bool IsTracking(CardInstance card, int sourceUid)
        {
            return card != null
                && card.Counters.Values.ContainsKey(BaselineKey(sourceUid));
        }
    }
}
