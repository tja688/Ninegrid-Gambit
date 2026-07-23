using NineGrid.Presentation.Systems;

namespace NineGrid.Cards
{
    /// <summary>
    /// System Bind 面：允许交战 System 附着 owner，不依赖具体 MonoBehaviour 类型名。
    /// </summary>
    public interface IFieldBattleViewBinding : IFieldBattleView
    {
        FieldBattlePresentationSystem BattleOwnerOrNull { get; }

        void AttachBattleOwner(FieldBattlePresentationSystem owner);
    }
}
