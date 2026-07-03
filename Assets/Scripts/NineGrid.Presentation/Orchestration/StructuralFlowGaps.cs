namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 去除批末 Reconcile 后，以下结构性变更仍需补最小 Flow（表演不到位即无视觉）：
    /// <list type="bullet">
    /// <item><description><c>RemoveCard</c>：无 kill flow 时回收棋盘/手牌演员</description></item>
    /// <item><description><c>SwapCards</c>：两卡落位交换或瞬移</description></item>
    /// <item><description><c>FillSlots</c>：payload 不完整时的补 spawn</description></item>
    /// <item><description>道具用后 despawn：<c>ItemUseFlow</c> 淡出后回收手牌演员（当前仅 SetActive false）</description></item>
    /// </list>
    /// </summary>
    internal static class StructuralFlowGaps
    {
    }
}
