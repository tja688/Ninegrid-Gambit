using NineGrid.Cards;
using NineGrid.Core;
using QFramework;

namespace NineGrid.Flow.BoardBriefTip
{
    /// <summary>
    /// 跑图导航图标（消费离开 / Boss 战后下楼）虚构地点预制体解析器。
    /// 依据当前楼层（Floor）与节点将抽象的离开/下楼转换为对应的地形图标资产。
    /// </summary>
    public static class BoardNavigationIconResolver
    {
        /// <summary>
        /// 解析选房离开图标预制体路径（前往下半区节点 5）。
        /// 商店/牌店/奖励房内离开不走本方法，固定 <see cref="CardChassisPaths.RoomIconLeave"/>。
        /// 1 层（密林）→ 通往失落遗迹图标；
        /// 2 层（岩层）→ 通往熔岩之地图标；
        /// 3 层（溶洞）→ 通往黄昏礼堂图标。
        /// </summary>
        public static string ResolveLeaveIconPrefab(int floor)
        {
            switch (floor)
            {
                case 1:
                    return CardChassisPaths.RoomIconLostRuins;
                case 2:
                    return CardChassisPaths.RoomIconMagma;
                case 3:
                    return CardChassisPaths.RoomIconTwilightHall;
                default:
                    return CardChassisPaths.RoomIconLeave;
            }
        }

        public static string ResolveLeaveIconPrefab(IArchitecture arch)
        {
            var floor = arch?.GetModel<RunModel>()?.Floor?.Value ?? 1;
            return ResolveLeaveIconPrefab(floor);
        }

        /// <summary>
        /// 解析 Boss 战胜后前往下一层的下楼图标预制体路径。
        /// 1 层 Boss 战胜（前往 2 层岩层）→ 通往岩层图标；
        /// 2 层 Boss 战胜（前往 3 层溶洞）→ 通往苍白之路图标；
        /// 3 层或未命中 → 下楼图标。
        /// </summary>
        public static string ResolveGoDownIconPrefab(int floor)
        {
            switch (floor)
            {
                case 1:
                    return CardChassisPaths.RoomIconRockLayer;
                case 2:
                    return CardChassisPaths.RoomIconPaleRoad;
                default:
                    return CardChassisPaths.RoomIconGoDown;
            }
        }

        public static string ResolveGoDownIconPrefab(IArchitecture arch)
        {
            var floor = arch?.GetModel<RunModel>()?.Floor?.Value ?? 1;
            return ResolveGoDownIconPrefab(floor);
        }

        public static string ResolveNavigationIconPrefab(NavigationKind kind, int floor)
        {
            switch (kind)
            {
                case NavigationKind.Leave:
                    return ResolveLeaveIconPrefab(floor);
                case NavigationKind.GoDown:
                    return ResolveGoDownIconPrefab(floor);
                default:
                    return CardChassisPaths.RoomIconBattle;
            }
        }

        public static string ResolveNavigationIconPrefab(NavigationKind kind, IArchitecture arch)
        {
            var floor = arch?.GetModel<RunModel>()?.Floor?.Value ?? 1;
            return ResolveNavigationIconPrefab(kind, floor);
        }
    }
}
