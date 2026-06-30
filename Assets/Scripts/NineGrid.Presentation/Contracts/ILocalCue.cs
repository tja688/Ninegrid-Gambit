namespace NineGrid.Presentation.Contracts
{
    /// <summary>
    /// Flow 内部手感反馈（Local Cue）；Builder 不直调。
    /// </summary>
    public interface ILocalCue
    {
        void Play(CueInvocation invocation);
        void StopAndRestore();
    }
}
