namespace NineGrid.Core.Effects
{
    /// <summary>
    /// 卡店「道具卡数值强化」（RewardSystem.TavernUpgradeDefId）的运行时修正：
    /// PlayerModel.ItemStatBonus 每购一次 +3，只在道具卡自有效果的数值结算时累加，
    /// 不污染 Catalog（#97 约定走运行级修正通道）。
    ///
    /// 规则（对齐策划设计「提升所有道具卡数值3点，无视没有数值的道具卡」）：
    /// 仅对道具卡（HelpCard）自有的固定数值生效；派生自他卡数值的效果
    /// （火球=玩家攻击、撞击教程=玩家血量、绑票=怪物护甲）不算道具卡自身数值，不叠加。
    /// </summary>
    internal static class HelpCardStatBonusUtility
    {
        public static int Resolve(EffectRuntimeContext context)
        {
            if (context == null)
            {
                return 0;
            }

            var owner = context.OwnerCard;
            if (owner == null || owner.Kind != CardKind.HelpCard)
            {
                return 0;
            }

            var player = context.Player;
            return player == null ? 0 : player.ItemStatBonus;
        }
    }
}
