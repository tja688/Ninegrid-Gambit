using NineGrid.Core;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 表演锚点处理器：认领并消费归属当前报点的结算指令。
    /// 装饰类实现不得占用主线就位回执（旁路装饰道）。
    /// </summary>
    public interface IBattleBeatHandler
    {
        /// <summary>
        /// 若本处理器认领该指令则应用并返回 true；否则返回 false 交由后续处理器。
        /// </summary>
        bool TryApply(PresentationInstruction instruction);
    }
}
