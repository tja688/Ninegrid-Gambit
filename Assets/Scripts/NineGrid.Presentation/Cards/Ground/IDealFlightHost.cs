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
    }
}
