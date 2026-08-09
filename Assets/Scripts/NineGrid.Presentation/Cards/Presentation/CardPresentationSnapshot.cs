using System.Collections.Generic;
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
        /// 攻击模式（ADR-0011）；局内怪物由 Core 投影，用于卡面攻击模式槽图标切换（见
        /// <see cref="CardFaceAttackPatternIconResolver"/>，当前仅三档近战）。
        /// </summary>
        public NineGrid.Core.AttackPattern AttackPattern;

        /// <summary>是否挂有技能同步触发（ADR-0038）。</summary>
        public bool HasSyncRhythmSkills;

        /// <summary>是否有活跃卡级节奏（应显示行动计数数值）。</summary>
        public bool HasActiveRhythm;

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
        /// </summary>
        public string DetailDescription = string.Empty;

        /// <summary>
        /// 卡面背景介绍（JSON <c>faceIntro</c>）；供右键详述面板，不进卡面 <c>Basic_Description</c> 槽。
        /// </summary>
        public string FaceIntro = string.Empty;

        /// <summary>稀有度驱动的卡框染色；a=0 表示未接线（Binder/底盘不改色）。</summary>
        public Color FrameColor = new Color(0f, 0f, 0f, 0f);

        /// <summary>
        /// 已提交倒计时投影值（ADR-0035）：键为完整「装配id.键」（如 <c>trap.flame.remove.every</c>），
        /// 只经 Settled 结算指令（<c>UpdateCountdownRemaining</c>）写入；null = 无已提交剩余。
        /// 仅实例/预览表面消费；Inspect 模式由投影缝恒忽略。
        /// </summary>
        public IReadOnlyDictionary<string, string> CommittedCountdownRemaining;
    }
}
