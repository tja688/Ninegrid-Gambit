namespace NineGrid.Presentation.Contracts
{
    /// <summary>
    /// 输入态交互呈现（Enter/Update/Exit）；不发 Command。
    /// </summary>
    public interface IInteractivePresenter
    {
        void Enter();
        void UpdatePresenter();
        void Exit();
        void ForceReset();
    }
}
