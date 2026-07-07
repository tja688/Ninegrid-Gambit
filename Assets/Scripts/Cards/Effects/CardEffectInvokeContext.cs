namespace NineGrid.Cards
{
    /// <summary>
    /// 外部编排层传入的效果调用上下文（方向、格位、关联卡等）。
    /// </summary>
    public readonly struct CardEffectInvokeContext
    {
        public CardEffectInvokeContext(
            CardEffectKind kind,
            CardBoardDirection selfDirection = CardBoardDirection.None,
            int selfSlot = 0,
            int? otherSlot = null,
            int? sourceUid = null,
            int? targetUid = null,
            float magnitude = 0f,
            CardDisplayMode displayMode = CardDisplayMode.GroundCardMode)
        {
            Kind = kind;
            SelfDirection = selfDirection;
            SelfSlot = selfSlot;
            OtherSlot = otherSlot;
            SourceUid = sourceUid;
            TargetUid = targetUid;
            Magnitude = magnitude;
            DisplayMode = displayMode;
        }

        public CardEffectKind Kind { get; }

        public CardBoardDirection SelfDirection { get; }

        public int SelfSlot { get; }

        public int? OtherSlot { get; }

        public int? SourceUid { get; }

        public int? TargetUid { get; }

        public float Magnitude { get; }

        public CardDisplayMode DisplayMode { get; }

        public static CardEffectInvokeContext ForAttack(
            CardBoardDirection selfDirection,
            int selfSlot = 0,
            int? otherSlot = null,
            int? sourceUid = null,
            int? targetUid = null,
            float magnitude = 0f,
            CardDisplayMode displayMode = CardDisplayMode.GroundCardMode)
        {
            return new CardEffectInvokeContext(
                CardEffectKind.Attack,
                selfDirection,
                selfSlot,
                otherSlot,
                sourceUid,
                targetUid,
                magnitude,
                displayMode);
        }

        public static CardEffectInvokeContext ForHit(
            CardBoardDirection selfDirection,
            int selfSlot = 0,
            int? otherSlot = null,
            int? sourceUid = null,
            int? targetUid = null,
            float magnitude = 0f,
            CardDisplayMode displayMode = CardDisplayMode.GroundCardMode)
        {
            return new CardEffectInvokeContext(
                CardEffectKind.Hit,
                selfDirection,
                selfSlot,
                otherSlot,
                sourceUid,
                targetUid,
                magnitude,
                displayMode);
        }

        public static CardEffectInvokeContext ForDeath(
            int selfSlot = 0,
            CardBoardDirection selfDirection = CardBoardDirection.None,
            CardDisplayMode displayMode = CardDisplayMode.GroundCardMode)
        {
            return new CardEffectInvokeContext(
                CardEffectKind.Death,
                selfDirection,
                selfSlot,
                displayMode: displayMode);
        }

        public static CardEffectInvokeContext ForUse(
            CardBoardDirection selfDirection = CardBoardDirection.None,
            CardDisplayMode displayMode = CardDisplayMode.HandCardMode)
        {
            return new CardEffectInvokeContext(
                CardEffectKind.Use,
                selfDirection,
                displayMode: displayMode);
        }
    }
}
