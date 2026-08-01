namespace NineGrid.Cards
{
    /// <summary>
    /// 表现层卡牌种类，数值与内核 CardKind 枚举对齐，供 Flow 映射写入。
    /// </summary>
    public enum CardPresentationKind
    {
        Unknown = 0,
        Avatar = 1,
        Monster = 2,
        PlayerCard = 3,
        Relic = 4,
        HelpCard = 5,
        Item = 6,
        Trap = 7,
    }
}
