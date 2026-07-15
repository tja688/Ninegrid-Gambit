using System.Collections.Generic;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 依赖 DAG 升级门：长尾预算在 DAG 上向下游传播。初版仅预留接口，不全建。
    /// </summary>
    public interface IDependencyDag
    {
        void AddEdge(int predecessorBeatId, int successorBeatId);

        IReadOnlyList<int> GetPredecessors(int beatId);

        IReadOnlyList<int> GetSuccessors(int beatId);
    }

    /// <summary>空实现：初版栅栏不走 DAG。</summary>
    public sealed class NullDependencyDag : IDependencyDag
    {
        public static readonly NullDependencyDag Instance = new();

        private NullDependencyDag()
        {
        }

        public void AddEdge(int predecessorBeatId, int successorBeatId)
        {
        }

        public IReadOnlyList<int> GetPredecessors(int beatId) => System.Array.Empty<int>();

        public IReadOnlyList<int> GetSuccessors(int beatId) => System.Array.Empty<int>();
    }
}
