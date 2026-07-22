namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// Kind 卡面消费端：只消费已提交投影，不裁决规则、不回写 Core。
    /// </summary>
    public interface ICardFaceBinder
    {
        void ApplyPresentation(CardPresentationSnapshot snapshot);

        /// <summary>
        /// 消费已提交朝向。本波可恒正面 / no-op 视觉，但入口须存在且不抛错。
        /// 禁止从 DisplayMode 隐式推导朝向。见 ADR-0002 / #15。
        /// </summary>
        void ApplyFaceOrientation(bool faceUp);
    }
}
