using NineGrid.Core;
using NineGrid.Presentation.Visuals;

namespace NineGrid.Presentation.Orchestration
{
    /// <summary>
    /// 批末仅对齐玩家化身 HP/护甲；金币与互动次数走 HUD Model 直连。
    /// </summary>
    public sealed class StatusPanelReconcilable : IReconcilable
    {
        private readonly TableNineStatusPanelView mStatusPanel;

        public StatusPanelReconcilable(TableNineStatusPanelView statusPanel)
        {
            mStatusPanel = statusPanel;
        }

        public void ApplySnapshot(CoreViewSnapshot snapshot)
        {
            mStatusPanel?.ApplyAvatarCombatStats(snapshot);
        }
    }
}
