using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 面向卡面消费的胖投影快照（不按 Kind 裁剪真相；卡面按模板路由，不用则不绑）。
    /// FaceUp = Core 牌面朝向的镜像，非 DisplayMode 推导；朝向规则门闩属后续专题。
    /// </summary>
    public sealed class CardPresentationSnapshot
    {
        public CardPresentationKind Kind;
        public string DefId = string.Empty;
        public string DisplayName = string.Empty;

        /// <summary>主图标；null 时 Binder 回退卡面模板默认。</summary>
        public Sprite MainIcon;

        public Sprite FaceBackground;
        public Sprite BackBorder;
        public Sprite BackShirt;
        public Sprite BackLogo;

        public int Attack;
        public int Armor;
        public int Hp;

        /// <summary>未接线时保持 0。</summary>
        public int ActionCount;

        /// <summary>
        /// Core 牌面朝向镜像（明/暗）。权威在 Core；表现仅 Commit 镜像。
        /// 本波默认 true 即可玩；规则门闩 / 揭牌演出属后续专题。
        /// </summary>
        public bool FaceUp = true;

        /// <summary>
        /// 静态基础描述（可含 `[SlotCode]` 图标引用）。
        /// 文案本身不随战中数值跳动；不进数值旁路 Set*。详细描述见 <see cref="DetailDescription"/>。
        /// </summary>
        public string BasicDescription = string.Empty;

        /// <summary>详细描述字段契约留口；右键面板 / 词条排版 Out of Scope。</summary>
        public string DetailDescription = string.Empty;
    }
}
