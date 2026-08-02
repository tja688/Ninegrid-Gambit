namespace NineGrid.Cards
{
    /// <summary>
    /// 表现层卡牌种类。0–7 与内核 CardKind 数值对齐（供 Flow 映射写入）；
    /// 8+ 为编辑器/流程表现扩展，不进入 CardManagerSingleton 真卡 Spawn。
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
        /// <summary>房间场地图标（编辑器预览 / 后续场地投放）。</summary>
        Room = 8,
        /// <summary>房间特殊选项卡（编辑器预览挂房间选项标准模板）。</summary>
        ChoiceOption = 9,
    }
}
