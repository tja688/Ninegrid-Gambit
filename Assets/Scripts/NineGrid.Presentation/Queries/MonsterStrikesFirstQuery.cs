using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 只读：该交战是否应由怪物先出手（委托 Core Phase 先攻裁决）。
    /// </summary>
    public sealed class MonsterStrikesFirstQuery : AbstractQuery<bool>
    {
        private readonly int mAvatarUid;
        private readonly int mMonsterUid;

        public MonsterStrikesFirstQuery(int avatarUid, int monsterUid)
        {
            mAvatarUid = avatarUid;
            mMonsterUid = monsterUid;
        }

        protected override bool OnDo()
        {
            return this.GetSystem<IPhaseSystem>().MonsterStrikesFirst(mAvatarUid, mMonsterUid);
        }
    }
}
