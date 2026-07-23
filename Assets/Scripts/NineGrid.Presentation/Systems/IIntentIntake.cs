using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Presentation.Systems
{
    /// <summary>
    /// 唯一意图收口：两轴门禁 + Core 合法性 + Director 互斥/缓冲；忙时发冲动轻点。
    /// </summary>
    public interface IIntentIntake : ISystem
    {
        /// <summary>
        /// 提交玩家意图。目标表面须等于当前所有者；棋盘动作忙时 Buffer，模式/模态忙时 Reject。
        /// </summary>
        IntentDisposition Submit(
            InputIntent intent,
            InputOwner targetSurface,
            out bool uiPickPreview);
    }
}
