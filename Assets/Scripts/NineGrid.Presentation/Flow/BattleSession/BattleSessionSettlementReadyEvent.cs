namespace NineGrid.Flow
{
    /// <summary>
    /// 节点结算就绪：内核已进入奖励相位时由 BattleSession 发出。
    /// 主流程可订阅此事件，或继续订阅 View 上的兼容回调。
    /// </summary>
    public struct BattleSessionSettlementReadyEvent
    {
    }
}
