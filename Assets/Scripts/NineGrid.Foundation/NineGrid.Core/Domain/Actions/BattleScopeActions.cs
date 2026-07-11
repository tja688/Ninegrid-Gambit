using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    public sealed class BeginPlayerMonsterEngagementAction : GameAction
    {
        public BeginPlayerMonsterEngagementAction(int monsterUid)
        {
            MonsterUid = monsterUid;
        }

        public int MonsterUid { get; private set; }
        public override string ActionName { get { return "BeginPlayerMonsterEngagement"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetSystem<IBattleScopeSystem>().BeginPlayerMonsterEngagement(MonsterUid);
            return GameActionResult.Empty;
        }
    }

    public sealed class EndBattleScopeCleanupAction : GameAction
    {
        public override string ActionName { get { return "EndBattleScopeCleanup"; } }

        public override GameActionResult Apply(GameActionContext context)
        {
            context.GetSystem<IBattleScopeSystem>().EndCurrentBattle();
            return GameActionResult.Empty;
        }
    }
}
