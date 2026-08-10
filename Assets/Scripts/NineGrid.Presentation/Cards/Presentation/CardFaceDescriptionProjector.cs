using NineGrid.Content.CardPresentation;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 卡面描述投影统一渲染通路（ADR-0035 唯一主缝 / #155）：
    /// 输入检查描述、装配实参与表面模式，输出填好的静态描述字符串。
    /// Instance 与 Inspect 均只填检查描述 + 装配初始实参，永不消费已提交倒计时剩余。
    /// </summary>
    public enum CardDescriptionProjectionMode
    {
        /// <summary>右键检查：静态检查描述 + 装配初始实参插值。</summary>
        Inspect,

        /// <summary>实例/预览表面：与 Inspect 同文（效果倒计时改走 ActionCount / 遗物栏计数）。</summary>
        Instance,
    }

    public static class CardFaceDescriptionProjector
    {
        /// <summary>投影缝唯一入口。</summary>
        public static string Project(
            CardDescriptionProjectionMode mode,
            string checkDescription,
            EffectAssemblyDto[] assemblies)
        {
            _ = mode;
            return CardFaceDescriptionParamFiller.FillFromAssemblies(checkDescription, assemblies);
        }
    }
}
