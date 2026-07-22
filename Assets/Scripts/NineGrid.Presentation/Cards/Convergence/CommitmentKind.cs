namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 表演请求的承诺标签。贴在每次请求上，不绑死表演器类型。
    /// Sync = 必达就位 + 租约独占；Async = 输入锁自由 + 后发先至。
    /// </summary>
    public enum CommitmentKind
    {
        Async = 0,
        Sync = 1,
    }
}
