using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 某一格的认领者：静止可点实体声明的激活与简要解释文案（ADR-0023）。
    /// </summary>
    public sealed class SlotClaimant
    {
        public SlotClaimant(
            object owner,
            string briefTipText,
            Action activate,
            Action hoverEnter = null,
            Action hoverExit = null)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            BriefTipText = briefTipText ?? string.Empty;
            Activate = activate;
            HoverEnter = hoverEnter;
            HoverExit = hoverExit;
        }

        /// <summary>认领身份；Release 时须匹配，冲突时用于日志。</summary>
        public object Owner { get; }

        /// <summary>悬停声明文案；能否显示由布局构型决定。</summary>
        public string BriefTipText { get; }

        /// <summary>单击激活（提交意图或房内动作）。</summary>
        public Action Activate { get; }

        /// <summary>悬停进入附加反馈（如卡面 Hover 视觉）。</summary>
        public Action HoverEnter { get; }

        /// <summary>悬停离开附加反馈。</summary>
        public Action HoverExit { get; }
    }
}
