using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IBattleScopeSystem : ISystem
    {
        int EngagedEnemyUid { get; }
        int BeginPlayerMonsterEngagement(int monsterUid);
        int EndCurrentBattle();
        int ClearScopedModifiers(ModifierScope scope);
    }

    public sealed class BattleScopeSystem : AbstractSystem, IBattleScopeSystem
    {
        public int EngagedEnemyUid
        {
            get { return this.GetModel<BattleContextModel>().EngagedEnemyUid.Value; }
        }

        protected override void OnInit()
        {
        }

        public int BeginPlayerMonsterEngagement(int monsterUid)
        {
            if (monsterUid <= 0)
            {
                return 0;
            }

            var context = this.GetModel<BattleContextModel>();
            var previous = context.EngagedEnemyUid.Value;
            if (previous == monsterUid)
            {
                return 0;
            }

            var cleared = previous > 0 ? ClearScopedModifiers(ModifierScope.UntilEnemyChanges) : 0;
            context.SetEngagedEnemy(monsterUid);
            return cleared;
        }

        public int EndCurrentBattle()
        {
            return ClearScopedModifiers(ModifierScope.UntilBattleEnds);
        }

        public int ClearScopedModifiers(ModifierScope scope)
        {
            var statSystem = this.GetSystem<IStatSystem>();
            var cleared = statSystem.RuleModifiers.ClearByScope(scope);

            var registry = this.GetModel<CardRegistry>();
            foreach (var pair in registry.Cards)
            {
                cleared += statSystem.ClearModifiersByScope(pair.Value, scope);
            }

            return cleared;
        }
    }
}
