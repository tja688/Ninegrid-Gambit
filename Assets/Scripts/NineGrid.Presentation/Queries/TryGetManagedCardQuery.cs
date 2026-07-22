using NineGrid.Cards;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 只读：经 <see cref="ICardEntityLifecycleSystem"/> 查找已注册 ManagedCard。
    /// </summary>
    public sealed class TryGetManagedCardQuery : AbstractQuery<ManagedCard>
    {
        private readonly int mUid;

        public TryGetManagedCardQuery(int uid)
        {
            mUid = uid;
        }

        protected override ManagedCard OnDo()
        {
            if (mUid == CardManagerSingleton.InvalidUid)
            {
                return null;
            }

            var lifecycle = this.GetSystem<ICardEntityLifecycleSystem>();
            if (lifecycle == null || !lifecycle.TryGet(mUid, out var card))
            {
                return null;
            }

            return card;
        }
    }
}
