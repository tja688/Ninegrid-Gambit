using UnityEngine;

namespace NineGrid.Cards
{
    /// <summary>
    /// 飞牌协调器宿主：不依赖具体 MonoBehaviour 业务状态。
    /// </summary>
    public interface IDealFlightHost
    {
        bool IsPlaceable(int slot);

        bool IsFieldBusy { get; }

        GroundFieldLayoutSettings LayoutSettings { get; }

        bool TryGetExploreAnchorPosition(int slot, out Vector3 position);

        bool PlaceForExplore(int slot, ManagedCard card);

        /// <summary>
        /// 飞牌落地后登记格位认领（ADR-0023：起飞注销、落地才认领）。
        /// 调用方须已从 in-flight 表移除该 uid，否则 <see cref="GroundCardHitProxy"/> 会拒领。
        /// </summary>
        void NotifyDealFlightLanded(ManagedCard card, int slot);
    }
}
