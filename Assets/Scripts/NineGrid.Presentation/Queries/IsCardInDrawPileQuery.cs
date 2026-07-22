using NineGrid.Core;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 只读：uid 是否在 Core DrawPile。
    /// </summary>
    public sealed class IsCardInDrawPileQuery : AbstractQuery<bool>
    {
        private readonly int mUid;

        public IsCardInDrawPileQuery(int uid)
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
                   && card.Zone.Value == ZoneId.DrawPile;
        }
    }
}
