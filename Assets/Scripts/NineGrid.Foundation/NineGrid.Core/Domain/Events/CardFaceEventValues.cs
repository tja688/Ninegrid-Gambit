using NineGrid.Core.Stats;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    /// <summary>
    /// 卡面生成类事件的结算后绝对值写入（攻=ResultValue，血/甲=Remaining*）。
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
    }
}
