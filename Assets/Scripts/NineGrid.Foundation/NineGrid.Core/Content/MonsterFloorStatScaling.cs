using System;

namespace NineGrid.Core
{
    /// <summary>
    /// 怪物成长数值叠加（策划 2026-08：去除纯层数提升，改为节点+层双轨叠加）：
    /// ① 每满 4 个全局节点（跨层累计，1 起）一档：攻击 +1、血量 +2；
    /// ② 每进入一个新层一档（第 1 层为基准 0）：攻击 +1、血量 +2。
    /// 两轨相加；任意主题卡组均适用。
    /// </summary>
    public static class MonsterFloorStatScaling
    {
        public const int AttackPerFloorAboveBase = 1;
        public const int HpPerFloorAboveBase = 2;
        public const int AttackPerFourNodes = 1;
        public const int HpPerFourNodes = 2;
        public const int NodesPerTier = 4;

        /// <summary>全局节点序号（1 起，跨层累计）对应的满 4 节点档数。</summary>
        public static int NodeTiers(int globalNodeIndex)
        {
            return Math.Max(0, globalNodeIndex) / NodesPerTier;
        }

        public static int FloorTiers(int floor)
        {
            return Math.Max(0, floor - 1);
        }

        /// <summary>全局节点序号（1 起）：(floor-1)*NodesPerFloor + nodeIndex + 1（nodeIndex 为 0 起层内节点）。</summary>
        public static int GlobalNodeIndex(int floor, int nodeIndex)
        {
            return (Math.Max(1, floor) - 1) * RunModel.NodesPerFloor + Math.Max(0, nodeIndex) + 1;
        }

        public static int AttackBonus(int floor, int nodeIndex)
        {
            return FloorTiers(floor) * AttackPerFloorAboveBase
                + NodeTiers(GlobalNodeIndex(floor, nodeIndex)) * AttackPerFourNodes;
        }

        public static int HpBonus(int floor, int nodeIndex)
        {
            return FloorTiers(floor) * HpPerFloorAboveBase
                + NodeTiers(GlobalNodeIndex(floor, nodeIndex)) * HpPerFourNodes;
        }

        public static void ApplyToDraft(CardDraft draft, int floor, int nodeIndex)
        {
            if (draft == null || draft.Kind != CardKind.Monster)
            {
                return;
            }

            var attackBonus = AttackBonus(floor, nodeIndex);
            var hpBonus = HpBonus(floor, nodeIndex);
            if (attackBonus == 0 && hpBonus == 0)
            {
                return;
            }

            draft.Attack += attackBonus;
            draft.MaxHp += hpBonus;
            if (draft.Hp > 0)
            {
                draft.Hp += hpBonus;
            }
            else
            {
                draft.Hp = draft.MaxHp;
            }
        }
    }
}
