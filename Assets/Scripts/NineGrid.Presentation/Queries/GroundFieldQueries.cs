using NineGrid.Cards;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>只读：场地几何快照。</summary>
    public sealed class GroundFieldSnapshotQuery : AbstractQuery<GroundFieldSnapshot>
    {
        protected override GroundFieldSnapshot OnDo()
        {
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            if (geometry == null || !geometry.IsBound)
            {
                return new GroundFieldSnapshot(null, 0);
            }

            return geometry.GetSnapshot();
        }
    }

    /// <summary>只读：uid → slot。</summary>
    public sealed class TryGetSlotOfUidQuery : AbstractQuery<(bool ok, int slot)>
    {
        private readonly int mUid;

        public TryGetSlotOfUidQuery(int uid)
        {
            mUid = uid;
        }

        protected override (bool ok, int slot) OnDo()
        {
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            if (geometry == null || !geometry.IsBound)
            {
                return (false, 0);
            }

            var ok = geometry.TryGetSlotOf(mUid, out var slot);
            return (ok, slot);
        }
    }
}
