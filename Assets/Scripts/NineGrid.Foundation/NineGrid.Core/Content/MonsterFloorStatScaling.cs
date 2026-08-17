using System;
using NineGrid.Core.Content;

namespace NineGrid.Core
{
    /// <summary>
    /// 怪物成长数值叠加：
    /// ① 简单难度（<see cref="RunDifficultyIds.Normal"/>）：
    ///    频率减半，过层不加档，仅在每层中间节点（节点 4~8，display 4~8，nodeIndex 3~7，含层主/Boss 战）加 1 档（攻击 +1、血量 +2）。
    ///    总档数 = (floor - 1) * 1 + (nodeIndex >= 3 ? 1 : 0)。
    /// ② 中等难度（<see cref="RunDifficultyIds.Advanced"/>）：
    ///    双轨累加：层内中段（节点 4~8）+1 档；每打完一层进入新层 +2 档。
    ///    总档数 = (floor - 1) * 2 + (nodeIndex >= 3 ? 1 : 0)。
    ///    每档基础攻击 +1、血量 +2。
    /// ③ 困难难度（<see cref="RunDifficultyIds.Hard"/>）：
    ///    总档数计算同中等难度，但每档攻血加成翻倍为攻击 +2、血量 +4。
    /// 任意主题卡组均适用。
    /// </summary>
    public static class MonsterFloorStatScaling
    {
        public const int AttackPerTier = 1;
        public const int HpPerTier = 2;
        public const int MidFloorNodeIndexThreshold = 3;
        public const int TiersPerCompletedFloor = 2;
        public const int TiersPerCompletedFloorEasy = 1;

        public const int AttackPerFloorAboveBase = AttackPerTier;
        public const int HpPerFloorAboveBase = HpPerTier;
        public const int AttackPerFourNodes = AttackPerTier;
        public const int HpPerFourNodes = HpPerTier;
        public const int NodesPerTier = 4;

        public static bool IsHardDifficulty(string difficultyId)
        {
            return string.Equals(difficultyId, RunDifficultyIds.Hard, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEasyDifficulty(string difficultyId)
        {
            return string.IsNullOrEmpty(difficultyId)
                || string.Equals(difficultyId, RunDifficultyIds.Normal, StringComparison.OrdinalIgnoreCase)
                || string.Equals(difficultyId, "easy", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>计算给定难度、层数与层内节点序号（0 起）对应的总档数。</summary>
        public static int TotalTiers(int floor, int nodeIndex, string difficultyId)
        {
            var tiersPerFloor = IsEasyDifficulty(difficultyId) ? TiersPerCompletedFloorEasy : TiersPerCompletedFloor;
            var completedFloorTiers = Math.Max(0, floor - 1) * tiersPerFloor;
            var currentFloorTier = Math.Max(0, nodeIndex) >= MidFloorNodeIndexThreshold ? 1 : 0;
            return completedFloorTiers + currentFloorTier;
        }

        public static int TotalTiers(int floor, int nodeIndex)
        {
            return TotalTiers(floor, nodeIndex, RunDifficultyIds.Advanced);
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

        public static int AttackBonus(int floor, int nodeIndex, string difficultyId)
        {
            var isHard = IsHardDifficulty(difficultyId);
            var bonus = TotalTiers(floor, nodeIndex, difficultyId) * AttackPerTier;
            return isHard ? bonus * 2 : bonus;
        }

        public static int AttackBonus(int floor, int nodeIndex, bool isHardDifficulty = false)
        {
            return AttackBonus(floor, nodeIndex, isHardDifficulty ? RunDifficultyIds.Hard : RunDifficultyIds.Advanced);
        }

        public static int HpBonus(int floor, int nodeIndex, string difficultyId)
        {
            var isHard = IsHardDifficulty(difficultyId);
            var bonus = TotalTiers(floor, nodeIndex, difficultyId) * HpPerTier;
            return isHard ? bonus * 2 : bonus;
        }

        public static int HpBonus(int floor, int nodeIndex, bool isHardDifficulty = false)
        {
            return HpBonus(floor, nodeIndex, isHardDifficulty ? RunDifficultyIds.Hard : RunDifficultyIds.Advanced);
        }

        public static void ApplyToDraft(CardDraft draft, int floor, int nodeIndex, string difficultyId)
        {
            if (draft == null || draft.Kind != CardKind.Monster)
            {
                return;
            }

            var attackBonus = AttackBonus(floor, nodeIndex, difficultyId);
            var hpBonus = HpBonus(floor, nodeIndex, difficultyId);
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

        public static void ApplyToDraft(CardDraft draft, int floor, int nodeIndex, bool isHardDifficulty = false)
        {
            ApplyToDraft(draft, floor, nodeIndex, isHardDifficulty ? RunDifficultyIds.Hard : RunDifficultyIds.Advanced);
        }
    }
}
