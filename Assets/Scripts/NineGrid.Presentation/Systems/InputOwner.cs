namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 输入所有权轴：某一时刻拥有玩家输入的表面。
    /// 优先级（高→低）与枚举序一致：ChoiceOverlay &gt; Opening &gt; BoardSelect &gt; ProtectedField。
    /// </summary>
    public enum InputOwner
    {
        ProtectedField = 0,
        BoardSelect = 1,
        Opening = 2,
        ChoiceOverlay = 3,
    }
}
