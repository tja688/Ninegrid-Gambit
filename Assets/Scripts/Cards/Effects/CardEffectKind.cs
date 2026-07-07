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
    }
}
