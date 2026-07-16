namespace NineGrid.Core.Content
{
    /// <summary>
    /// 关卡怪物牌组路由策略（首发版临时）。
    /// <para>
    /// 骷髅军团（<see cref="SkeletonLegionDeckId"/>）因 bug 较多，已从<strong>随机路由</strong>中暂时排除：
    /// 正常开局、DevTest 快速测试「0 正式顺序」、以及 \ 选关 1/2/3/5/6 的首关之后，均不会再随机抽到骷髅牌组。
    /// </para>
    /// <para>
    /// 唯一仍可进入骷髅牌组的路径：主菜单长按 \ 打开选关，<strong>直接点选 4 骷髅军团</strong>（首关固定该牌组）。
    /// 见 <c>NineGrid.Flow.QuickTestDeckCatalog</c> 与 <c>NineGrid.Flow.MainGameLoopManagerSingleton</c>。
    /// </para>
    /// </summary>
    public static class LevelRouteDeckPolicy
    {
        public const string SkeletonLegionDeckId = "deck.skeleton_legion";

        /// <summary>
        /// 按 <see cref="NodeDeckRule.DeckKind"/> 随机抽怪物牌组时是否排除。
        /// 调用方已明确传入 <paramref name="deckId"/> 时不应使用本过滤。
        /// </summary>
        public static bool IsExcludedFromRandomMonsterDeckRoute(string deckId)
        {
            return deckId == SkeletonLegionDeckId;
        }
    }
}
