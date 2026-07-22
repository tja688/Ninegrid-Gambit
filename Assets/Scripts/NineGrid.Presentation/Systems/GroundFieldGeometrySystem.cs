using NineGrid.Cards;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// V7：持有 GroundField 显式引用，作为几何注册 / 飞牌忙碌查询的 QF 权威入口。
    /// </summary>
    public sealed class GroundFieldGeometrySystem : AbstractSystem, IGroundFieldGeometrySystem
    {
        private GroundFieldManagerSingleton mField;

        public bool IsBound => mField != null;

        public GroundFieldManagerSingleton Field => mField;

        public bool IsBusy => mField != null && mField.IsBusy;

        public bool IsFieldBusy => mField != null && mField.IsFieldBusy;

        public int ActiveDealFlightCount => mField != null ? mField.ActiveDealFlightCount : 0;

        public void Bind(GroundFieldManagerSingleton field)
        {
            mField = field;
        }

        public void Unbind()
        {
            mField = null;
        }

        public bool TryGetCardAt(int slot, out ManagedCard card)
        {
            if (mField != null)
            {
                return mField.TryGetCardAt(slot, out card);
            }

            card = null;
            return false;
        }

        public bool TryGetSlotOf(int uid, out int slot)
        {
            if (mField != null)
            {
                return mField.TryGetSlotOf(uid, out slot);
            }

            slot = 0;
            return false;
        }

        public bool IsEmpty(int slot)
        {
            return mField != null && mField.IsEmpty(slot);
        }

        public bool IsPlaceable(int slot)
        {
            return mField != null && mField.IsPlaceable(slot);
        }

        public bool IsDealInFlight(int uid)
        {
            return mField != null && mField.IsDealInFlight(uid);
        }

        public bool RequestPlaceCard(int slot, ManagedCard card, bool skipBusyGuard = false)
        {
            return mField != null && mField.RequestPlaceCard(slot, card, skipBusyGuard);
        }

        public bool ClearSlotOccupancy(int slot, bool skipBusyGuard = false)
        {
            return mField != null && mField.ClearSlotOccupancy(slot, skipBusyGuard);
        }

        public bool TryClearOccupancyForUid(int uid, bool skipBusyGuard = false)
        {
            return mField != null && mField.TryClearOccupancyForUid(uid, skipBusyGuard);
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            Unbind();
        }
    }
}
