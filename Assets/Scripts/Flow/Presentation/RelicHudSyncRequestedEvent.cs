namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 请求遗物栏从 Core PlayerModel 同步（由 RelicHudController 消费）。
    /// </summary>
    public struct RelicHudSyncRequestedEvent
    {
        public bool Clear;
    }
}
