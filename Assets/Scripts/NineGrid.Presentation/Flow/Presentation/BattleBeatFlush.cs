using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 与 PresentStep 锁步同构的 Impact→Settled 冲刷；供就位回执前与非锁步 OpenBatch 路径共用。
    /// </summary>
    public static class BattleBeatFlush
    {
        /// <summary>先冲刷 Impact，再报 Settled（空操作可叠加以幂等）。</summary>
        public static void FlushBeats()
        {
            BattleBeatHook.NotifyBeat(PresentationBeat.Impact);
            BattleBeatHook.NotifyBeat(PresentationBeat.Settled);
        }

        /// <summary>
        /// 非锁步：将事件日志切片经排期器消费。
        /// 无打开批次时 OpenBatch → FlushBeats → FinishBatch；
        /// 已有打开批次时走旁路 PresentStandalone，避免清掉当批 pending。
        /// </summary>
        public static void PresentEventLogSlice(IArchitecture architecture, int startIndex)
        {
            if (architecture == null || startIndex < 0)
            {
                return;
            }

            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var sync = architecture.GetSystem<IPresentationSyncSystem>();
            if (pipeline?.EventLog == null || sync == null)
            {
                return;
            }

            var entries = pipeline.EventLog.Entries;
            if (entries == null || startIndex >= entries.Count)
            {
                return;
            }

            var batchId = sync.ActiveBatchId > 0
                // 旁路批号避开 ActiveBatchId，避免与锁步当批撞号；不经 sync.OpenBatch。
                ? sync.ActiveBatchId + 1_000_000
                : System.Math.Max(1, startIndex + 1);
            var batch = PresentationBatchFactory.FromEventLog(
                pipeline.EventLog,
                startIndex,
                batchId,
                snapshot: null);

            if (sync.ActiveBatchId <= 0)
            {
                sync.OpenBatch(batch);
                FlushBeats();
                sync.FinishBatch(batch.BatchId);
                return;
            }

            BattleBeatHook.NotifyPresentStandalone(batch);
        }
    }
}
