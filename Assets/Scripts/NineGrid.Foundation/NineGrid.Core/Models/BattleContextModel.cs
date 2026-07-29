using QFramework;

namespace NineGrid.Core
{
    /// <summary>
    /// 追踪玩家当前交战敌人与交战窗口是否打开。
    /// EngagedEnemyUid 供 UntilEnemyChanges；IsEngagementActive 供 OnBattle 门禁（#78 / ADR-0012）。
    /// </summary>
    public sealed class BattleContextModel : AbstractModel
    {
        public BindableProperty<int> EngagedEnemyUid { get; private set; }

        /// <summary>
        /// BeginPlayerMonsterEngagement 与 EndBattleScopeCleanup 之间为 true。
        /// 与 EngagedEnemyUid 解耦：End 后敌人 uid 可保留，但交战窗口关闭。
        /// </summary>
        public bool IsEngagementActive { get; private set; }

        protected override void OnInit()
        {
            if (EngagedEnemyUid == null)
            {
                EngagedEnemyUid = new BindableProperty<int>(0);
            }
        }

        public void Reset()
        {
            EngagedEnemyUid.Value = 0;
            IsEngagementActive = false;
        }

        public void SetEngagedEnemy(int monsterUid)
        {
            EngagedEnemyUid.Value = monsterUid;
        }

        public void SetEngagementActive(bool active)
        {
            IsEngagementActive = active;
        }
    }
}
