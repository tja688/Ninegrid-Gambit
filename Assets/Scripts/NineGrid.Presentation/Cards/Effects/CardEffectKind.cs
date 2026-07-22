namespace NineGrid.Cards
{
    /// <summary>
    /// 单卡表现反馈种类。后续可扩展新枚举值并在 CardEffectManager 中装配对应 SO。
    /// </summary>
    public enum CardEffectKind
    {
        Attack = 0,
        Hit = 1,
        Death = 2,
        Use = 3,
        HitFlash = 4,
        /// <summary>基础卡牌效果触发：场地卡自身效果触发时的轻量脉冲（不阻塞）。</summary>
        EffectTrigger = 5,
    }
}
