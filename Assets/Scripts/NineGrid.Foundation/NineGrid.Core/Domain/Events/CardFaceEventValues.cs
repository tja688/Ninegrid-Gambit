using System;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 卡面生成类事件的结算后绝对值写入（攻=ResultValue，血/甲=Remaining*）；
    /// 参与敌方行动的怪物另附行动倒计时事件（ADR-0005 / #81）。
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
    }
}
