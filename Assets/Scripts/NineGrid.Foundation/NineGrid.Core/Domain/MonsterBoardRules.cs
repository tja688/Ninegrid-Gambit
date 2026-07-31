using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 批次3硬骨头：天涯若比邻全局怪-怪邻接（刺客领袖已改为 OnDeal→Flip 内容效果，不走本类）。
    /// </summary>
    public static class MonsterBoardRules
    {
        public static bool HasGlobalMonsterAdjacency(IStatSystem statSystem, BoardModel board, CardRegistry registry)
        {
            if (statSystem == null || board == null || registry == null)
            {
                return false;
            }

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                CardInstance card;
                if (uid == 0 || !registry.TryGet(uid, out card) || card == null || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                if (statSystem.EvaluateRule(
                        RuleId.GlobalMonsterAdjacency,
                        0f,
                        statSystem.CreateContext(card)) > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasGlobalMonsterAdjacency(StatEvaluationContext context)
        {
            if (context == null || context.Board == null || context.Registry == null || context.RuleModifiers == null)
            {
                return false;
            }

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = context.Board.GetCardUid(SlotId.Board(i));
                CardInstance card;
                if (uid == 0 || !context.Registry.TryGet(uid, out card) || card == null || card.Kind != CardKind.Monster)
                {
                    continue;
                }

                var eval = new StatEvaluationContext(
                    card,
                    context.Registry,
                    context.Board,
                    context.Player,
                    context.RuleModifiers);
                if (context.RuleModifiers.Evaluate(RuleId.GlobalMonsterAdjacency, 0f, eval) > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasVirtualAdjacency(
            StatEvaluationContext context,
            CardInstance owner,
            int targetUid)
        {
            if (context == null
                || context.RuleModifiers == null
                || owner == null
                || owner.Kind != CardKind.Monster
                || targetUid == 0)
            {
                return false;
            }

            CardInstance target;
            if (!context.Registry.TryGet(targetUid, out target) || target == null || target.Kind != CardKind.Monster)
            {
                return false;
            }

            var eval = new StatEvaluationContext(
                    owner,
                    context.Registry,
                    context.Board,
                    context.Player,
                    context.RuleModifiers)
                .WithTarget(targetUid);
            return context.RuleModifiers.Evaluate(RuleId.VirtualAdjacency, 0f, eval) > 0f;
        }
    }
}
