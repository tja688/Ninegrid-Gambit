using System.Collections.Generic;

namespace NineGrid.Flow.AttributeBoard
{
    /// <summary>
    /// 属性房三选二（#137）：把「视觉候选 → 当前 Pending 索引」的映射抽成纯逻辑。
    /// Core 每次接受选择都会从 RewardOptions 移除该实例，剩余候选保持相对顺序；
    /// 因此候选 i 的当前索引 = 其之前仍未选中的候选个数。
    /// </summary>
    public static class AttributePickIndexResolver
    {
        /// <summary>
        /// 计算候选 <paramref name="candidateIndex"/>（视觉/生成序）在 Pending.RewardOptions 中的当前索引。
        /// 越界返回 -1。
        /// </summary>
        public static int ResolveCurrentPendingIndex(
            IReadOnlyList<bool> selectedFlags,
            int candidateIndex)
        {
            if (candidateIndex < 0 || selectedFlags == null || candidateIndex >= selectedFlags.Count)
            {
                return -1;
            }

            var index = 0;
            for (var i = 0; i < candidateIndex; i++)
            {
                if (!selectedFlags[i])
                {
                    index++;
                }
            }

            return index;
        }
    }
}
