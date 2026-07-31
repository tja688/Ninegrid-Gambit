using System;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 批次3硬骨头：刺客领袖发牌朝向、天涯若比邻全局怪-怪邻接。
    /// </summary>
    public static class MonsterBoardRules
    {
        public static bool ShouldDealMonsterFaceDown(GameActionContext context)
        {
            return HasActiveBoardRule(context, RuleId.AssassinLeaderDealFaceDown);
        }

        /// <summary>
        /// 打出到格的怪物：若场上有正面刺客领袖，设为背面（不走 Flip，避免误触 OnFlip）。
        /// 机关卡（trap.*）不按怪物卡处理。
        /// </summary>
        public static void ApplyAssassinLeaderFaceDownIfNeeded(GameActionContext context, CardInstance card)
        {
            if (card == null
                || card.Kind != CardKind.Monster
                || IsTrapDefId(card.DefId)
                || !ShouldDealMonsterFaceDown(context))
            {
                return;
            }

            card.FaceUp = false;
        }

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

        private static bool HasActiveBoardRule(GameActionContext context, RuleId rule)
        {
            if (context == null)
            {
                return false;
            }

            var board = context.GetModel<BoardModel>();
            var registry = context.GetModel<CardRegistry>();
            var statSystem = context.GetSystem<IStatSystem>();
            if (board == null || registry == null || statSystem == null)
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

                if (statSystem.EvaluateRule(rule, 0f, statSystem.CreateContext(card)) > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsTrapDefId(string defId)
        {
            return !string.IsNullOrEmpty(defId)
                && defId.StartsWith("trap.", StringComparison.Ordinal);
        }
    }
}
