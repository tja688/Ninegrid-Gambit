using UnityEngine;

namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 域边界交接快照。字段从第一天带速度，C 阶段 Evict 恒填 0，B 阶段再逐边界吐真速度。
    /// </summary>
    public readonly struct HandoffState
    {
        public Vector3 LocalPosition { get; }
        public Vector3 LocalVelocity { get; }

        public HandoffState(Vector3 localPosition, Vector3 localVelocity)
        {
            LocalPosition = localPosition;
            LocalVelocity = localVelocity;
        }

        public static HandoffState AtRest(Vector3 localPosition) =>
            new(localPosition, Vector3.zero);
    }
}
