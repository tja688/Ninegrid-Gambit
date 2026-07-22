using System;
using NineGrid.Core;

namespace NineGrid.Flow
{
    /// <summary>
    /// 奖励点选 / 跳过 Core 写入：由 Presentation RewardChoiceInputController 注册。
    /// </summary>
    public static class RewardChoiceCoreHook
    {
        public static Func<int, CoreCommandResult> SelectReward;
        public static Func<CoreCommandResult> SkipHelpChoice;

        public static Action WireController;

        public static void RequestWire()
        {
            WireController?.Invoke();
        }
    }
}
