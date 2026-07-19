using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using UnityEngine;

namespace NineGrid.Flow
{
    /// <summary>
    /// DevTest 快速测试：整局战斗内容节点乱序规划（每配置出现 floor 数次，全序洗牌）。
    /// 节点索引本身不含怪物牌组；牌组由 RewardSystem 按节点 DeckKind 随机（含骷髅军团）。
    /// </summary>
    internal static class QuickTestRunPlanner
    {
        public const int AvatarAttack = 5;

        public static List<int> BuildShuffledContentNodeQueue(IReadOnlyList<int> ruleNodeIndices)
        {
            var queue = BuildFullContentNodeQueue(ruleNodeIndices);
            ShuffleInPlace(queue);
            return queue;
        }

        public static List<int> BuildSequentialContentNodeQueue(IReadOnlyList<int> ruleNodeIndices)
        {
            return BuildFullContentNodeQueue(ruleNodeIndices);
        }

        private static List<int> BuildFullContentNodeQueue(IReadOnlyList<int> ruleNodeIndices)
        {
            var perFloor = ruleNodeIndices == null || ruleNodeIndices.Count == 0
                ? BuildDefaultRuleNodeIndices()
                : ruleNodeIndices;
            var total = RunModel.FinalFloor * RunModel.NodesPerFloor;
            var queue = new List<int>(total);
            var copies = Mathf.Max(1, RunModel.FinalFloor);
            for (var copy = 0; copy < copies; copy++)
            {
                for (var i = 0; i < perFloor.Count; i++)
                {
                    queue.Add(perFloor[i]);
                }
            }

            return queue;
        }

        public static List<int> CollectRuleNodeIndices(GameContentCatalog catalog)
        {
            var indices = new List<int>();
            if (catalog?.Rewards?.NodeDeckRules == null)
            {
                return indices;
            }

            var rules = catalog.Rewards.NodeDeckRules;
            for (var i = 0; i < rules.Count; i++)
            {
                var nodeIndex = rules[i].NodeIndex;
                if (nodeIndex <= 0 || indices.Contains(nodeIndex))
                {
                    continue;
                }

                indices.Add(nodeIndex);
            }

            indices.Sort();
            return indices;
        }

        public static string FormatNodeOrder(IReadOnlyList<int> queue)
        {
            if (queue == null || queue.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(",", queue);
        }

        private static List<int> BuildDefaultRuleNodeIndices()
        {
            var indices = new List<int>(RunModel.NodesPerFloor);
            for (var i = 1; i <= RunModel.NodesPerFloor; i++)
            {
                indices.Add(i);
            }

            return indices;
        }

        private static void ShuffleInPlace(List<int> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
