using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 输入所有权轴只读提供者：投影当前所有者；MainlineBusy 只读投影 Runtime。
    /// Opening / Overlay / BoardSelect 的 Set* 仅声明「该表面在索取」；门禁读 <see cref="CurrentOwner"/>。
    /// BoardSelect 另镜像 <see cref="NineGrid.Cards.BoardCardSelectModeController"/>。
    /// 不自持第二份互斥/缓冲可写真相。
    /// </summary>
    public interface IPresentationInputStateSystem : ISystem
    {
        /// <summary>当前输入所有者（轴二只读投影）。</summary>
        InputOwner CurrentOwner { get; }

        /// <summary>Opening 索取声明（非门禁；门禁用 <see cref="CurrentOwner"/>）。</summary>
        IReadonlyBindableProperty<bool> OpeningPresentationActive { get; }
        /// <summary>场地覆层索取声明（非门禁；门禁用 <see cref="CurrentOwner"/>）。</summary>
        IReadonlyBindableProperty<bool> ChoiceOverlayActive { get; }
        /// <summary>棋盘选择索取声明（非门禁；门禁用 <see cref="CurrentOwner"/>）。</summary>
        IReadonlyBindableProperty<bool> BoardSelectModeActive { get; }

        bool MainlineBusy { get; }
        bool HasExternalHold { get; }

        void SetOpeningPresentationActive(bool active);
        void SetChoiceOverlayActive(bool active);
        void SetBoardSelectModeActive(bool active);
        void ResetGates(string reason = null);
    }
}
