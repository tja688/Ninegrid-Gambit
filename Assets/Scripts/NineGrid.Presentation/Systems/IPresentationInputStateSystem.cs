using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 分型只读输入门禁投影：聚合 Opening / Overlay / BoardSelect / MainlineBusy。
    /// Opening 与 Overlay 由本 System 暂持（后续迁 BattleSession / Choice）；
    /// BoardSelect 镜像 <see cref="NineGrid.Cards.BoardCardSelectModeController"/>；
    /// MainlineBusy / ExternalHold 只读投影 Runtime，不产生第二份可写真相。
    /// </summary>
    public interface IPresentationInputStateSystem : ISystem
    {
        IReadonlyBindableProperty<bool> OpeningPresentationActive { get; }
        IReadonlyBindableProperty<bool> ChoiceOverlayActive { get; }
        IReadonlyBindableProperty<bool> BoardSelectModeActive { get; }

        bool MainlineBusy { get; }
        bool HasExternalHold { get; }

        void SetOpeningPresentationActive(bool active);
        void SetChoiceOverlayActive(bool active);
        void SetBoardSelectModeActive(bool active);
        void ResetGates(string reason = null);

        PresentationInputGateResult EvaluateExplore();
        PresentationInputGateResult EvaluateAttack();
        PresentationInputGateResult EvaluatePickup();
        PresentationInputGateResult EvaluateUseItem();
        PresentationInputGateResult EvaluateBoardSelectionBegin();
    }
}
