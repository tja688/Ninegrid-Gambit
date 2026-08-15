using System;

namespace NineGrid.Core.Effects
{
    /// <summary>
    /// 卡店道具数值强化（<see cref="Models.PlayerModel.ItemStatBonus"/>）变更通知。
    /// Presentation 注册后刷新已生成道具卡描述；Core 不依赖 Presentation 程序集。
    /// </summary>
    public static class ItemStatBonusChangedSink
    {
        public static Action NotifyChanged;

        internal static void RaiseChanged()
        {
            NotifyChanged?.Invoke();
        }
    }
}
