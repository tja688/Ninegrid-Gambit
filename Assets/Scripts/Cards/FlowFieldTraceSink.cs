using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// Cards → Flow 占格/发牌/手牌诊断旁路。由 Flow <c>FieldTraceHelper</c> 注册，失败不影响游戏路径。
    /// Cards 程序集不可引用 Flow，故用静态 Action 解耦。
    /// </summary>
    public static class FlowFieldTraceSink
    {
        /// <summary>当前表现批标签（postKill / opening / sync …），由 Flow 设置。</summary>
        public static string CurrentBatchTag = string.Empty;

        /// <summary>slot, existingUid, incomingUid, caller</summary>
        public static Action<int, int, int, string> OccupancyConflict;

        /// <summary>plans 摘要字符串，如 "26:1→2;32:2→3"</summary>
        public static Action<string> HopPlan;

        /// <summary>uid, slot, placeable, ok, rollback, caller</summary>
        public static Action<int, int, bool, bool, bool, string> DealResult;

        /// <summary>uid, phase, handContains, displayMode</summary>
        public static Action<int, string, bool, string> HandLifecycle;

        public static void ClearHandlers()
        {
            OccupancyConflict = null;
            HopPlan = null;
            DealResult = null;
            HandLifecycle = null;
            CurrentBatchTag = string.Empty;
        }
    }
}
