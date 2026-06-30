namespace NineGrid.Presentation.Contracts
{
    /// <summary>
    /// 批内主步骤（Directed Flow）。
    /// </summary>
    public interface IDirectedFlow
    {
        bool IsPlaying { get; }
        float ExpectedDuration { get; }
        void StopAndRestore();
    }
}
