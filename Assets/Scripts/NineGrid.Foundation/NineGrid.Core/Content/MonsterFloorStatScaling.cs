using System;

namespace NineGrid.Core
{
    /// <summary>
    /// 怪物成长数值叠加（策划 2026-08 微调）：
    /// ① 节点档位：层内前段（节点 1~3，display 1~3，nodeIndex 0~2）0 档；后段（节点 4~8，display 4~8，nodeIndex 3~7，含层主/Boss 战）1 档：攻击 +1、血量 +2；
    /// ② 层数档位：每打完一层（打完 Boss 进入新层，第 1 层为基准 0）增加 2 档（攻击 +2、血量 +4，即每层包含中段与打完 Boss 各 1 档）；
    /// 总档数 = (floor - 1) * 2 + (nodeIndex >= 3 ? 1 : 0)。
    /// 每档基础攻击 +1、血量 +2。
    /// 困难档（<see cref="RunDifficultyIds.Hard"/>）将上述档位翻倍为 +2/+4；普通/进阶公式不变。
    /// 任意主题卡组均适用。
    /// </summary>
    public static class MonsterFloorStatScaling
    {
        public const int AttackPerTier = 1;
        public const int HpPerTier = 2;
        public const int MidFloorNodeIndexThreshold = 3;
        public const int TiersPerCompletedFloor = 2;

        public const int AttackPerFloorAboveBase = AttackPerTier;
        public const int HpPerFloorAboveBase = HpPerTier;
        public const int AttackPerFourNodes = AttackPerTier;
        public const int HpPerFourNodes = HpPerTier;
        public const int NodesPerTier = 4;

        /// <summary>计算给定层数与层内节点序号（0 起）对应的总档数。</summary>
        public static int TotalTiers(int floor, int nodeIndex)
        {
            var completedFloorTiers = Math.Max(0, floor - 1) * TiersPerCompletedFloor;
            var currentFloorTier = Math.Max(0, nodeIndex) >= MidFloorNodeIndexThreshold ? 1 : 0;
            return completedFloorTiers + currentFloorTier;
        }

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

        public static int AttackBonus(int floor, int nodeIndex, bool isHardDifficulty = false)
        {
            var bonus = TotalTiers(floor, nodeIndex) * AttackPerTier;
            return isHardDifficulty ? bonus * 2 : bonus;
        }

        public static int HpBonus(int floor, int nodeIndex, bool isHardDifficulty = false)
        {
            var bonus = TotalTiers(floor, nodeIndex) * HpPerTier;
            return isHardDifficulty ? bonus * 2 : bonus;
        }

        public static void ApplyToDraft(CardDraft draft, int floor, int nodeIndex, bool isHardDifficulty = false)
        {
            if (draft == null || draft.Kind != CardKind.Monster)
            {
                return;
            }

            var attackBonus = AttackBonus(floor, nodeIndex, isHardDifficulty);
            var hpBonus = HpBonus(floor, nodeIndex, isHardDifficulty);
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
