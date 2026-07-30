using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 面向卡面消费的胖投影快照（不按 Kind 裁剪真相；卡面按模板路由，不用则不绑）。
    /// FaceUp = Core 牌面朝向的镜像，非 DisplayMode 推导。
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
        public Sprite CardFrame;
        public Sprite Banner;

        public int Attack;
        public int Armor;
        public int Hp;

        /// <summary>行动倒计时；经 UpdateActionCount 指令 Commit，缺省 0。</summary>
        public int ActionCount;

        /// <summary>
        /// Core 牌面朝向镜像（明/暗）。权威在 Core；表现仅 Commit 镜像。
        /// </summary>
        public bool FaceUp = true;

        /// <summary>
        /// 静态基础描述（可含 `[SlotCode]` 图标引用与 `{param}` 装配实参占位）。
        /// 文案本身不随战中数值跳动；不进数值旁路 Set*。详细描述见 <see cref="DetailDescription"/>。
        /// </summary>
        public string BasicDescription = string.Empty;

        /// <summary>
        /// 详细描述（MVP）：人手概括（已插值）+ 词条自动展开。
        /// 卡面 JSON <c>faceIntro</c> 尚未接入本字段（右键详述面板后续专题）。
        /// </summary>
        public string DetailDescription = string.Empty;

        /// <summary>稀有度驱动的卡框染色；a=0 表示未接线（Binder/底盘不改色）。</summary>
        public Color FrameColor = new Color(0f, 0f, 0f, 0f);
    }
}
