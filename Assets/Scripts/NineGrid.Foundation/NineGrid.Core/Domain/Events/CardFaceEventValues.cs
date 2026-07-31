using System;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 卡面生成类事件的结算后绝对值写入（攻=ResultValue，血/甲=Remaining*）；
    /// 参与敌方行动的怪物另附行动倒计时事件（ADR-0005 / #81）。
    /// <para>
    /// Permanent 有效攻旁路：Conditional/常驻光环会改有效攻但不经血甲事件；
    /// Core 主动发 <see cref="CoreEventType.BaseStatModified"/>（仍走 Settled 指令，表现层不对账）。
    /// Temporary 交战加成不上卡面。
    /// </para>
    /// </summary>
    public static class CardFaceEventValues
    {
        public static CoreGameEvent WithFaceAbsolutes(
            this CoreGameEvent gameEvent,
            GameActionContext context,
            CardInstance card)
        {
            if (gameEvent == null || card == null)
            {
                return gameEvent;
            }

            var stats = context != null ? context.GetSystem<IStatSystem>() : null;
            var hp = stats != null
                ? stats.GetEffectiveInt(card, StatId.Hp)
                : (int)card.Stats.GetBase(StatId.Hp);
            var attack = stats != null
                ? stats.GetEffectiveInt(card, StatId.Attack)
                : (int)card.Stats.GetBase(StatId.Attack);
            var armor = StatArmorUtility.GetCurrentArmor(card);
            return gameEvent.WithRemaining(hp, armor).WithResultValue(attack);
        }

        /// <summary>
        /// 写入卡面绝对值事件，并在怪物参与敌方行动时追加 <see cref="CoreEventType.ActionCountdownChanged"/>。
        /// </summary>
        public static GameActionResult AddWithFaceAbsolutes(
            this GameActionResult result,
            GameActionContext context,
            CardInstance card,
            CoreGameEvent faceEvent)
        {
            if (result == null)
            {
                return result;
            }

            if (faceEvent != null)
            {
                result.AddEvent(faceEvent.WithFaceAbsolutes(context, card));
            }

            TryAppendActionCountdownChanged(result, context, card, faceEvent != null ? faceEvent.ActionName : null);
            return result;
        }

        public static void TryAppendActionCountdownChanged(
            GameActionResult result,
            GameActionContext context,
            CardInstance card,
            string actionName)
        {
            if (result == null
                || card == null
                || !AttackPatternRules.ParticipatesInEnemyAction(card.AttackPattern))
            {
                return;
            }

            var actionId = context != null ? context.ActionId : 0;
            var remaining = Math.Max(0, card.Counters.Get(CoreCounterKeys.AttackPatternCountdown));
            result.AddEvent(new CoreGameEvent(
                    CoreEventType.ActionCountdownChanged,
                    actionId,
                    actionName ?? "ActionCountdown")
                .WithCard(card.Uid)
                .WithResultValue(remaining));
        }

        /// <summary>
        /// Permanent Attack 修饰器 Apply/条件翻转后：提交当前有效攻到卡面（ADR-0005 旁路，非直读对账）。
        /// </summary>
        public static void AppendPermanentAttackFaceCommit(
            GameActionResult result,
            GameActionContext context,
            CardInstance card,
            string actionName,
            string source = null,
            string sourceDefId = null,
            int delta = 0)
        {
            if (result == null || card == null || context == null)
            {
                return;
            }

            var effectiveAttack = Math.Max(0, context.GetSystem<IStatSystem>().GetEffectiveInt(card, StatId.Attack));
            result.AddEvent(new CoreGameEvent(CoreEventType.BaseStatModified, context.ActionId, actionName ?? "PermanentAttackFace")
                .WithCard(card.Uid)
                .WithTarget(card.Uid)
                .WithAmount((int)StatId.Attack)
                .WithDelta(delta)
                .WithResultValue(effectiveAttack)
                .WithMessage(source ?? string.Empty)
                .WithSource(sourceDefId ?? string.Empty, source ?? string.Empty));
        }

        /// <summary>
        /// 盘面拓扑/宿主移除后：对仍挂着「带条件的 Permanent Attack」修饰器的场上卡提交有效攻。
        /// </summary>
        public static void AppendConditionalPermanentAttackFaceCommitsForBoard(
            GameActionResult result,
            GameActionContext context,
            string actionName)
        {
            if (result == null || context == null)
            {
                return;
            }

            var board = context.GetModel<BoardModel>();
            var registry = context.GetModel<CardRegistry>();
            if (board == null || registry == null)
            {
                return;
            }

            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var uid = board.GetCardUid(SlotId.Board(i));
                CardInstance card;
                if (uid <= 0 || !registry.TryGet(uid, out card) || card == null)
                {
                    continue;
                }

                if (!HasConditionalPermanentAttackModifier(card))
                {
                    continue;
                }

                AppendPermanentAttackFaceCommit(result, context, card, actionName);
            }
        }

        public static bool HasConditionalPermanentAttackModifier(CardInstance card)
        {
            if (card == null || card.Stats == null)
            {
                return false;
            }

            var modifiers = card.Stats.Modifiers;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier.Stat == StatId.Attack
                    && modifier.Scope == ModifierScope.Permanent
                    && modifier.Condition != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
