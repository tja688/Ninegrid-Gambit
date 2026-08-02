namespace NineGrid.Core
{
    /// <summary>
    /// 地图节点房间来源（ADR-0021）。
    /// </summary>
    public enum NodeRoomSource
    {
        RandomBattle = 1,
        PreviousChoice = 2,
        Boss = 3
    }

    /// <summary>
    /// 清关 / 非战斗节点结束后放出的选项族（ADR-0021）。
    /// </summary>
    public enum NodeOfferFamily
    {
        BattleRooms = 1,
        ConsumerRooms = 2,
        SpecialRooms = 3,
        Leave = 4,
        GoDown = 5
    }

    /// <summary>
    /// 导航图标（不进玩法 Catalog / 不是 <see cref="RoomKind"/>）。
    /// </summary>
    public enum NavigationKind
    {
        None = 0,
        Leave = 1,
        GoDown = 2
    }

    /// <summary>
    /// 单节点编排只读表项。
    /// </summary>
    public readonly struct MapNodeSchedule
    {
        public MapNodeSchedule(
            int displayNode,
            NodeRoomSource roomSource,
            bool entersInteractionLoop,
            NodeOfferFamily postClearOfferFamily,
            bool allowsEliteInBattleOffers)
        {
            DisplayNode = displayNode;
            RoomSource = roomSource;
            EntersInteractionLoop = entersInteractionLoop;
            PostClearOfferFamily = postClearOfferFamily;
            AllowsEliteInBattleOffers = allowsEliteInBattleOffers;
        }

        public int DisplayNode { get; }
        public NodeRoomSource RoomSource { get; }
        public bool EntersInteractionLoop { get; }
        public NodeOfferFamily PostClearOfferFamily { get; }
        public bool AllowsEliteInBattleOffers { get; }

        public NavigationKind NavigationOffer
        {
            get
            {
                switch (PostClearOfferFamily)
                {
                    case NodeOfferFamily.Leave:
                        return NavigationKind.Leave;
                    case NodeOfferFamily.GoDown:
                        return NavigationKind.GoDown;
                    default:
                        return NavigationKind.None;
                }
            }
        }
    }

    /// <summary>
    /// 跑图节点编排（ADR-0021）。入参为 0-based <see cref="RunModel.NodeIndex"/>。
    /// </summary>
    public static class MapNodeProgression
    {
        private static readonly MapNodeSchedule[] sByDisplayNode =
        {
            default,
            new MapNodeSchedule(1, NodeRoomSource.RandomBattle, true, NodeOfferFamily.BattleRooms, false),
            new MapNodeSchedule(2, NodeRoomSource.PreviousChoice, true, NodeOfferFamily.BattleRooms, false),
            new MapNodeSchedule(3, NodeRoomSource.PreviousChoice, true, NodeOfferFamily.ConsumerRooms, false),
            new MapNodeSchedule(4, NodeRoomSource.PreviousChoice, false, NodeOfferFamily.Leave, false),
            new MapNodeSchedule(5, NodeRoomSource.RandomBattle, true, NodeOfferFamily.BattleRooms, true),
            new MapNodeSchedule(6, NodeRoomSource.PreviousChoice, true, NodeOfferFamily.SpecialRooms, true),
            new MapNodeSchedule(7, NodeRoomSource.PreviousChoice, false, NodeOfferFamily.Leave, false),
            new MapNodeSchedule(8, NodeRoomSource.Boss, true, NodeOfferFamily.GoDown, false)
        };

        public static int ToDisplayNode(int nodeIndex)
        {
            return nodeIndex + 1;
        }

        public static bool TryGetSchedule(int nodeIndex, out MapNodeSchedule schedule)
        {
            var display = ToDisplayNode(nodeIndex);
            if (display < 1 || display > RunModel.NodesPerFloor)
            {
                schedule = default;
                return false;
            }

            schedule = sByDisplayNode[display];
            return true;
        }

        public static MapNodeSchedule GetScheduleOrDefault(int nodeIndex)
        {
            MapNodeSchedule schedule;
            if (TryGetSchedule(nodeIndex, out schedule))
            {
                return schedule;
            }

            return new MapNodeSchedule(
                ToDisplayNode(nodeIndex),
                NodeRoomSource.RandomBattle,
                true,
                NodeOfferFamily.BattleRooms,
                false);
        }

        public static bool EntersInteractionLoop(int nodeIndex)
        {
            return GetScheduleOrDefault(nodeIndex).EntersInteractionLoop;
        }

        public static NodeOfferFamily GetPostClearOfferFamily(int nodeIndex)
        {
            return GetScheduleOrDefault(nodeIndex).PostClearOfferFamily;
        }

        public static bool AllowsEliteInBattleOffers(int nodeIndex)
        {
            return GetScheduleOrDefault(nodeIndex).AllowsEliteInBattleOffers;
        }
    }
}
