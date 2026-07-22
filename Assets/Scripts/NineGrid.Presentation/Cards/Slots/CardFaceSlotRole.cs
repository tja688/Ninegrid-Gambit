using System;

namespace NineGrid.Cards.Slots
{
    /// <summary>
    /// 卡面装配槽角色标记（同一槽可组合多角色）。
    /// </summary>
    [Flags]
    public enum CardFaceSlotRole
    {
        None = 0,

        /// <summary>编辑器默认展开的高频装配入口。</summary>
        DirectExpose = 1 << 0,

        /// <summary>默认收纳；收纳不等于不渲染终态。</summary>
        Collapsed = 1 << 1,

        /// <summary>可被基础描述 `[SlotCode]` 引用插入。</summary>
        InsertableInDescription = 1 << 2,

        /// <summary>数值槽；缺省统一为 0。</summary>
        Numeric = 1 << 3,

        /// <summary>图标/贴图槽；缺省回退源模板自带 Sprite。</summary>
        Icon = 1 << 4,
    }
}
