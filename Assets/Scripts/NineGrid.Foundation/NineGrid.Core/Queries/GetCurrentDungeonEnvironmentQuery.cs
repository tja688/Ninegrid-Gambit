using NineGrid.Core.Content;
using QFramework;

namespace NineGrid.Core
{
    /// <summary>
    /// 读当前 Run 进度下的地下城环境（显示名、卡面背景 Resources 路径、是否血色、Floor/Room）。
    /// </summary>
    public sealed class GetCurrentDungeonEnvironmentQuery : AbstractQuery<DungeonEnvironmentInfo>
    {
        protected override DungeonEnvironmentInfo OnDo()
        {
            var run = this.GetModel<RunModel>();
            if (run == null)
            {
                return DungeonEnvironmentCatalog.Resolve(1, 1, false);
            }

            var floor = run.Floor != null ? run.Floor.Value : 1;
            var nodeIndex = run.NodeIndex != null ? run.NodeIndex.Value : 0;
            var difficultyId = run.DifficultyId != null
                ? run.DifficultyId.Value
                : RunDifficultyIds.Normal;
            return DungeonEnvironmentCatalog.ResolveFromRun(floor, nodeIndex, difficultyId);
        }
    }
}
