using NineGrid.Presentation.Systems;

namespace NineGrid.Cards
{
    /// <summary>
    /// System Bind 面：允许几何 System 附着 owner / 回调，不依赖具体 MonoBehaviour 类型名。
    /// </summary>
    public interface IGroundFieldViewBinding : IGroundFieldView
    {
        GroundFieldGeometrySystem GeometryOwnerOrNull { get; }

        void AttachGeometryOwner(GroundFieldGeometrySystem owner);

        void NotifyFieldMaybeClear();

        void NotifyEmptySlotClicked(int slot);
    }
}
