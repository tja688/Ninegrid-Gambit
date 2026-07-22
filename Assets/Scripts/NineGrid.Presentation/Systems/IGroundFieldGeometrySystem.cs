using NineGrid.Cards;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 场地几何注册与收敛宿主的单一 QF 所有者。
    /// 逻辑占格权威仍在 Core BoardModel；本 System 只持有几何镜像与运动执行入口。
    /// </summary>
    public interface IGroundFieldGeometrySystem : ISystem
    {
        bool IsBound { get; }

        GroundFieldManagerSingleton Field { get; }

        void Bind(GroundFieldManagerSingleton field);

        void Unbind();

        bool IsBusy { get; }

        bool IsFieldBusy { get; }

        int ActiveDealFlightCount { get; }

        bool TryGetCardAt(int slot, out ManagedCard card);

        bool TryGetSlotOf(int uid, out int slot);

        bool IsEmpty(int slot);

        bool IsPlaceable(int slot);

        bool IsDealInFlight(int uid);

        bool RequestPlaceCard(int slot, ManagedCard card, bool skipBusyGuard = false);

        bool ClearSlotOccupancy(int slot, bool skipBusyGuard = false);

        bool TryClearOccupancyForUid(int uid, bool skipBusyGuard = false);
    }
}
