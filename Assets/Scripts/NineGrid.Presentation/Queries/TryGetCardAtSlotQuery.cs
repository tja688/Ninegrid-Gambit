using NineGrid.Cards;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 只读：经 <see cref="IGroundFieldGeometrySystem"/> 查询场地几何注册上的卡。
    /// </summary>
    public sealed class TryGetCardAtSlotQuery : AbstractQuery<ManagedCard>
    {
        private readonly int mSlot;

        public TryGetCardAtSlotQuery(int slot)
        {
            mSlot = slot;
        }

        protected override ManagedCard OnDo()
        {
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            if (geometry == null || !geometry.IsBound)
            {
                return null;
            }

            return geometry.TryGetCardAt(mSlot, out var card) ? card : null;
        }
    }
}
