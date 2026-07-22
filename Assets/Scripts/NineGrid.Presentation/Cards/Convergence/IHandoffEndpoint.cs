namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 全域一致的域边界交接口。复杂域与净土域均实现；内部驱动器可黑盒。
    /// </summary>
    public interface IHandoffEndpoint
    {
        /// <summary>离开本域：交出当前局部位姿与速度快照。</summary>
        HandoffState Evict();

        /// <summary>进入本域：承接对方交出的局部位姿与速度。</summary>
        void Admit(in HandoffState state);
    }
}
