namespace NineGrid.Cards.Convergence
{
    /// <summary>
    /// 卡牌变换塔层号。编译期定死，不可事后插层。
    /// </summary>
    public enum TowerLayer
    {
        CardRoot = 0,
        BoardFrame = 1,
        SlotFrame = 2,
        EffectFrame = 3,
        CardVisual = 4,
    }
}
