using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Queries
{
    /// <summary>
    /// 只读：场地自身忙碌（不含交战），经 Geometry System 查询。
    /// </summary>
    public sealed class IsFieldBusyQuery : AbstractQuery<bool>
    {
        protected override bool OnDo()
        {
            var geometry = this.GetSystem<IGroundFieldGeometrySystem>();
            return geometry != null && geometry.IsBound && geometry.IsFieldBusy;
        }
    }
}
