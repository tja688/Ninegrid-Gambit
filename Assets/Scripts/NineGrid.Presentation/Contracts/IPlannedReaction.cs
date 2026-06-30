namespace NineGrid.Presentation.Contracts
{
    /// <summary>
    /// Flow Marker 或 Builder 编排的副作用（Planned Reaction）。
    /// </summary>
    public interface IPlannedReaction
    {
        bool IsPlaying { get; }
        void StopAndRestore();
    }
}
