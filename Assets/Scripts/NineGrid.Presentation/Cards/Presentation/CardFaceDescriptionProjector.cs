using System.Collections.Generic;
using NineGrid.Content.CardPresentation;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 卡面描述投影的表面模式（ADR-0035 / #155）。
    /// </summary>
    public enum CardDescriptionProjectionMode
    {
        /// <summary>
        /// 右键检查：恒输出静态检查描述（装配初始实参插值）；
        /// 永不使用局内模板，永不消费已提交的倒计时剩余投影值。
        /// </summary>
        Inspect,

        /// <summary>
        /// 实例/预览表面（场上/手牌/道具格/遗物栏及店/奖/鉴预览）：
        /// 有局内模板时用模板并消费已提交剩余；无模板时与检查描述同文。
        /// </summary>
        Instance,
    }

    /// <summary>
    /// 卡面描述投影统一渲染通路（ADR-0035 唯一主缝 / #155）：
    /// 输入检查描述、局内模板、装配实参、已提交倒计时投影值与表面模式，
    /// 输出填好的描述字符串。检查面板与卡面 <c>Basic_Description</c> / 预览都只消费本出口，
    /// 禁止 View 直读内核计数器填卡面描述。
    /// 模式选择：Inspect 恒走检查描述；Instance 无模板时与检查描述同文。
    /// </summary>
    public static class CardFaceDescriptionProjector
    {
        /// <summary>
        /// 投影缝唯一入口。
        /// </summary>
        /// <param name="mode">表面模式（检查 vs 实例/预览）。</param>
        /// <param name="checkDescription">卡面基础描述（检查描述，可含 <c>{装配id.键}</c>）。</param>
        /// <param name="liveTemplate">局内描述投影模板；空 = 无动态，与检查描述同文。</param>
        /// <param name="assemblies">装配实参（初始配置值填充）。</param>
        /// <param name="committedRemaining">
        /// 已提交倒计时投影值（键为完整 <c>装配id.键</c>，仅 Settled 写入）；
        /// 仅 Instance 模式消费，Inspect 模式恒忽略。
        /// </param>
        public static string Project(
            CardDescriptionProjectionMode mode,
            string checkDescription,
            string liveTemplate,
            EffectAssemblyDto[] assemblies,
            IReadOnlyDictionary<string, string> committedRemaining = null)
        {
            var useTemplate = mode != CardDescriptionProjectionMode.Inspect
                              && !string.IsNullOrWhiteSpace(liveTemplate);
            var source = useTemplate ? liveTemplate : checkDescription;
            var remaining = mode == CardDescriptionProjectionMode.Inspect ? null : committedRemaining;
            return CardFaceDescriptionParamFiller.FillFromAssemblies(source, assemblies, remaining);
        }
    }
}
