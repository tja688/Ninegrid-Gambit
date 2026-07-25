namespace NineGrid.Cards.Anim
{
    /// <summary>
    /// 主视图相对「主视图Mask」的摆放锚点语义（切槽时算一次，播帧不改 transform）。
    /// </summary>
    public enum CardMainVisualAnchorMode
    {
        /// <summary>水平居中 + 参考帧底边对齐 Mask 底边。</summary>
        BottomCenter = 0,

        /// <summary>参考帧包围盒中心对齐 Mask 中心。</summary>
        BoundsCenter = 1,

        /// <summary>Transform 原点对齐 Mask 中心（旧行为）。</summary>
        TransformOrigin = 2,
    }
}
