using NineGrid.Core;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Flow.RoomIcons
{
    /// <summary>
    /// 软占登记变更后刷新空槽 Hit，使 BoardWalkSlotHitPolicy 与 RoomIconOccupancy 对齐。
    /// </summary>
    public static class RoomIconOccupancySlotHits
    {
        public static void Refresh(IArchitecture arch = null)
        {
            var geometry = arch?.GetSystem<IGroundFieldGeometrySystem>()
                           ?? NineGridArchitecture.Interface?.GetSystem<IGroundFieldGeometrySystem>()
                           ?? NineGridArchitecture.Current?.GetSystem<IGroundFieldGeometrySystem>();
            geometry?.RefreshSlotHitColliders();
        }
    }
}
