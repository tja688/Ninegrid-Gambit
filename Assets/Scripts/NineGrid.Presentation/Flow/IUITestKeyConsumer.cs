namespace NineGrid.Flow
{
    /// <summary>
    /// UITestBootstrap 可选按键消费者；实现在 Temporary Test 程序集，避免 Flow 反向引用。
    /// </summary>
    public interface IUITestKeyConsumer
    {
        void HandleKeypad1();
    }
}
