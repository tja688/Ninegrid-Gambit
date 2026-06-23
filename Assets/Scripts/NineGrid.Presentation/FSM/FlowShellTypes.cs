namespace NineGrid.Presentation.FSM
{
    /// <summary>
    /// App 级流程壳：Boot / 主菜单 / 局内会话。
    /// </summary>
    public enum FlowShellAppState
    {
        Boot,
        MainMenu,
        RunSession,
    }

    /// <summary>
    /// 局内粗粒度屏幕态；由 <see cref="GamePhaseFlowShellProjection"/> 从内核 Phase 弱同步投影。
    /// 表现层其余模块应只读此枚举，不直接引用 <c>GamePhase</c>。
    /// </summary>
    public enum FlowShellScreen
    {
        Idle,
        NodePlaying,
        RewardScreen,
        RoomChoiceScreen,
        RoomEventScreen,
        NodeAdvance,
        RunTerminal,
    }
}
