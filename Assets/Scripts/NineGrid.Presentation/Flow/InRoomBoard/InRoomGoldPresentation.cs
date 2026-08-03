using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using QFramework;

namespace NineGrid.Flow.InRoomBoard
{
    /// <summary>
    /// 商店/卡店等非战斗房内扣金：无战斗拍，须直接把 EventLog 金币变更推到 HUD。
    /// </summary>
    public static class InRoomGoldPresentation
    {
        public static int CaptureEventLogCount(IArchitecture arch)
        {
            var pipeline = arch?.GetSystem<IActionPipelineSystem>();
            return pipeline?.EventLog?.Entries != null ? pipeline.EventLog.Entries.Count : 0;
        }

        /// <summary>把 startIndex 起的 GoldModified 推到飞币/HUD（含扣费 Snap）。</summary>
        public static int PresentGoldChangesSince(IArchitecture arch, int startIndex)
        {
            if (arch == null || startIndex < 0)
            {
                return 0;
            }

            GoldGainPresentationBinder.EnsureInstalled();
            return new GoldGainPresentationScheduler().PresentFromEventLog(arch, startIndex);
        }
    }
}
