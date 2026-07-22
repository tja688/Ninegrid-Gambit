using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 解析玩家攻击实际目标（嘲讽重定向等）；只读委托 Core Phase。
    /// </summary>
    public sealed class ResolvePlayerAttackTargetQuery : AbstractQuery<int>
    {
        private readonly int mIntendedTargetUid;

        public ResolvePlayerAttackTargetQuery(int intendedTargetUid)
        {
            mIntendedTargetUid = intendedTargetUid;
        }

        protected override int OnDo()
        {
            return this.GetSystem<IPhaseSystem>().ResolvePlayerAttackTargetUid(mIntendedTargetUid);
        }
    }
}
