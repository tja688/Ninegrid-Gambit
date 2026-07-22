using NineGrid.Core;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 只读：uid 是否在 Core ItemSlots（卡组吸纳门禁等）。
    /// </summary>
    public sealed class IsCardInItemSlotsQuery : AbstractQuery<bool>
    {
        private readonly int mUid;

        public IsCardInItemSlotsQuery(int uid)
        {
            mUid = uid;
        }

        protected override bool OnDo()
        {
            if (mUid <= 0)
            {
                return false;
            }

            return this.GetModel<CardRegistry>().TryGet(mUid, out var card)
                   && card.Zone.Value == ZoneId.ItemSlots;
        }
    }
}
