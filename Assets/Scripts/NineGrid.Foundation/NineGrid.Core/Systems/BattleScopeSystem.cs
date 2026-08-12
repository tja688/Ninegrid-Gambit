using System.Collections.Generic;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IBattleScopeSystem : ISystem
    {
        int EngagedEnemyUid { get; }
        bool IsEngagementActive { get; }

        /// <summary>位移锁定窗口（ADR-0044）：交战窗或敌方行动阶段任一打开即为 true。</summary>
        bool IsBoardMotionDeferralActive { get; }

        /// <summary>挂起队列非空（ADR-0044）。</summary>
        bool HasDeferredBoardMotions { get; }

        int BeginPlayerMonsterEngagement(int monsterUid);
        int EndCurrentBattle();
        int ClearScopedModifiers(ModifierScope scope);

        /// <summary>敌方行动阶段窗口开合（报名置 true、收尾置 false；ADR-0044）。</summary>
        void SetEnemyActionPhaseActive(bool active);

        /// <summary>
        /// 窗口打开且动作属盘面位移类型时挂起并返回 true（调用方不再执行该动作）；
        /// 否则返回 false（照常结算）。ADR-0044。
        /// </summary>
        bool TryDeferBoardMotion(GameAction action);

        /// <summary>按挂起顺序倒入 <paramref name="buffer"/> 并清空队列；返回条数。</summary>
        int DrainDeferredBoardMotions(List<DeferredBoardMotion> buffer);

        /// <summary>丢弃全部挂起位移（终局 / 节点重置；ADR-0044）。</summary>
        void ClearDeferredBoardMotions();
    }

    public sealed class BattleScopeSystem : AbstractSystem, IBattleScopeSystem
    {
        public int EngagedEnemyUid
        {
            get { return this.GetModel<BattleContextModel>().EngagedEnemyUid.Value; }
        }

        public bool IsEngagementActive
        {
            get { return this.GetModel<BattleContextModel>().IsEngagementActive; }
        }

        public bool IsBoardMotionDeferralActive
        {
            get
            {
                var context = this.GetModel<BattleContextModel>();
                return context.IsEngagementActive || context.IsEnemyActionPhaseActive;
            }
        }

        public bool HasDeferredBoardMotions
        {
            get { return this.GetModel<BattleContextModel>().HasDeferredBoardMotions; }
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
            // 同敌再进交战窗口也必须打开 IsEngagementActive（End 后 uid 仍保留）。
            context.SetEngagementActive(true);
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
            this.GetModel<BattleContextModel>().SetEngagementActive(false);
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

        public void SetEnemyActionPhaseActive(bool active)
        {
            this.GetModel<BattleContextModel>().SetEnemyActionPhaseActive(active);
        }

        public bool TryDeferBoardMotion(GameAction action)
        {
            if (action == null || !IsBoardMotionDeferralActive)
            {
                return false;
            }

            var motion = DeferredBoardMotion.TryCapture(action, this.GetModel<BoardModel>());
            if (motion == null)
            {
                return false;
            }

            this.GetModel<BattleContextModel>().EnqueueDeferredBoardMotion(motion);
            return true;
        }

        public int DrainDeferredBoardMotions(List<DeferredBoardMotion> buffer)
        {
            return this.GetModel<BattleContextModel>().DrainDeferredBoardMotions(buffer);
        }

        public void ClearDeferredBoardMotions()
        {
            this.GetModel<BattleContextModel>().ClearDeferredBoardMotions();
        }
    }
}
