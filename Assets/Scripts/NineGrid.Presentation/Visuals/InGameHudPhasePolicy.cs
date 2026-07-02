using NineGrid.Core;

namespace NineGrid.Presentation.Visuals
{
    /// <summary>
    /// 局内 HUD 根节点显隐：仅在跑图交互阶段展示数值条与遗物/技能槽。
    /// </summary>
    public static class InGameHudPhasePolicy
    {
        public static bool ShouldShowGameplayHud(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.BuildEnemyPool:
                case GamePhase.ResetNode:
                case GamePhase.DealOpeningCards:
                case GamePhase.InteractionLoop:
                case GamePhase.ClearCheck:
                    return true;
                default:
                    return false;
            }
        }
    }
}
