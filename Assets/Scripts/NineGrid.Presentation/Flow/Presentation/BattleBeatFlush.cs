using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
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

        /// <summary>只冲刷 pending UpdateFaceUp（通道 Begin 前；经 Hook 注入排期器）。</summary>
        public static void FlushUpdateFaceUp()
        {
            BattleBeatHook.NotifyFlushUpdateFaceUp();
        }

        /// <summary>
        /// 冲刷 Impact 但跳过指定 Kind（ADR-0018：Vacate 前飘字，TriggerEffect 留待运动后）。
        /// </summary>
        public static void FlushImpactExcept(PresentationInstructionKind excludedKind)
        {
            BattleBeatHook.NotifyFlushImpactExcept(excludedKind);
        }

        /// <summary>
        /// 只冲刷指定 Kind 的 Impact，返回派发条数（ADR-0048：运动落地后先演触发脉冲）。
        /// </summary>
        public static int FlushImpactOnly(PresentationInstructionKind onlyKind)
        {
            return BattleBeatHook.NotifyFlushImpactOnly(onlyKind);
        }

        /// <summary>
        /// 非锁步：将事件日志切片经排期器消费。
        /// 无打开批次时 OpenBatch → FlushBeats → FinishBatch；
        /// 已有打开批次时走旁路 PresentStandalone，避免清掉当批 pending。
        /// </summary>
        public static void PresentEventLogSlice(IArchitecture architecture, int startIndex)
        {
            PresentEventLogSliceFiltered(architecture, startIndex, excludeKind: null, onlyKind: null);
        }

        /// <summary>
        /// 同 <see cref="PresentEventLogSlice"/>，但跳过指定 Kind（ADR-0018：有盘面运动时先 Present 非触发类）。
        /// </summary>
        public static void PresentEventLogSliceExcluding(
            IArchitecture architecture,
            int startIndex,
            PresentationInstructionKind excludeKind)
        {
            PresentEventLogSliceFiltered(architecture, startIndex, excludeKind, onlyKind: null);
        }

        /// <summary>
        /// 同 <see cref="PresentEventLogSlice"/>，但只消费指定 Kind（ADR-0018：运动落地后再 Present TriggerEffect）。
        /// </summary>
        public static void PresentEventLogSliceOnly(
            IArchitecture architecture,
            int startIndex,
            PresentationInstructionKind onlyKind)
        {
            PresentEventLogSliceFiltered(architecture, startIndex, excludeKind: null, onlyKind);
        }

        private static void PresentEventLogSliceFiltered(
            IArchitecture architecture,
            int startIndex,
            PresentationInstructionKind? excludeKind,
            PresentationInstructionKind? onlyKind)
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
            batch = FilterBatch(batch, excludeKind, onlyKind);
            if (batch.Instructions.Count == 0)
            {
                return;
            }

            if (sync.ActiveBatchId <= 0)
            {
                sync.OpenBatch(batch);
                FlushBeats();
                sync.FinishBatch(batch.BatchId);
                return;
            }

            BattleBeatHook.NotifyPresentStandalone(batch);
        }

        private static PresentationBatch FilterBatch(
            PresentationBatch batch,
            PresentationInstructionKind? excludeKind,
            PresentationInstructionKind? onlyKind)
        {
            if (batch == null || (excludeKind == null && onlyKind == null))
            {
                return batch;
            }

            var source = batch.Instructions;
            var filtered = new List<PresentationInstruction>(source.Count);
            for (var i = 0; i < source.Count; i++)
            {
                var instruction = source[i];
                if (instruction == null)
                {
                    continue;
                }

                if (onlyKind != null && instruction.Kind != onlyKind.Value)
                {
                    continue;
                }

                if (excludeKind != null && instruction.Kind == excludeKind.Value)
                {
                    continue;
                }

                filtered.Add(instruction);
            }

            return new PresentationBatch(batch.BatchId, filtered, batch.Snapshot);
        }

        /// <summary>
        /// 非锁步 + 效果打击编排（ADR-0050 拾取补丁）：盘面运动已由调用方 Drain 落地后调用，
        /// 与 PresentStep 两相同构——OpenBatch（触发打击计划构建/暂扣）→ 触发脉冲 →
        /// 串行效果打击 → FlushBeats（其余 Impact + Settled）→ FinishBatch。
        /// 已有打开批次时退回 <see cref="PresentEventLogSlice"/> 旁路（不搅乱锁步当批）。
        /// </summary>
        public static async UniTask PresentEventLogSliceWithStrikesAsync(
            IArchitecture architecture,
            int startIndex,
            CancellationToken cancellationToken = default)
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

            if (sync.ActiveBatchId > 0)
            {
                // 锁步批打开中：该情形不应由本路径编排打击，维持旧旁路语义。
                PresentEventLogSlice(architecture, startIndex);
                return;
            }

            var batch = PresentationBatchFactory.FromEventLog(
                pipeline.EventLog,
                startIndex,
                System.Math.Max(1, startIndex + 1),
                snapshot: null);
            if (batch.Instructions.Count == 0)
            {
                return;
            }

            // OpenBatch 经 Evt_PresentationBatchOpened 同时驱动排期器装载与 EffectStrikePlan 构建/暂扣。
            sync.OpenBatch(batch);
            try
            {
                var dispatched = FlushImpactOnly(PresentationInstructionKind.TriggerEffect);
                if (dispatched > 0)
                {
                    await UniTask.Delay(
                        System.TimeSpan.FromSeconds(PresentStep.TriggerCadenceSec),
                        cancellationToken: cancellationToken);
                }

                await EffectStrikeHook.NotifyPlayAllPendingStrikesAsync(cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                // 取消也必须收批；残留暂扣由 FlushBeats 兜底放行。
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    "[BattleBeatFlush] 切片效果打击编排失败（残留由 FlushBeats 兜底）: " + ex.Message);
            }
            finally
            {
                FlushBeats();
                sync.FinishBatch(batch.BatchId);
            }
        }

        /// <summary>
        /// 非锁步：只冲刷单条结算指令（Bounce spawn 后二次提交 OfferReward）。
        /// </summary>
        public static void PresentSingleEvent(CoreGameEvent entry, int batchIdHint = 0)
        {
            if (entry == null)
            {
                return;
            }

            var map = PresentationEventMap.Get(entry.Type);
            if (map.Beat == PresentationBeat.None)
            {
                return;
            }

            var batchId = batchIdHint > 0 ? batchIdHint : System.Math.Max(1, (int)(entry.Sequence % int.MaxValue) + 1);
            var batch = new PresentationBatch(
                batchId,
                new[] { new PresentationInstruction(entry, map) },
                snapshot: null);
            BattleBeatHook.NotifyPresentStandalone(batch);
        }

        /// <summary>
        /// 从事件日志末尾向前找最近一条指定类型并 PresentSingleEvent。
        /// </summary>
        public static bool PresentLatestEventOfType(IArchitecture architecture, CoreEventType type)
        {
            var pipeline = architecture != null
                ? architecture.GetSystem<IActionPipelineSystem>()
                : null;
            var entries = pipeline?.EventLog?.Entries;
            if (entries == null || entries.Count == 0)
            {
                return false;
            }

            for (var i = entries.Count - 1; i >= 0; i--)
            {
                var entry = entries[i];
                if (entry == null || entry.Type != type)
                {
                    continue;
                }

                PresentSingleEvent(entry, batchIdHint: 1_000_000 + i);
                return true;
            }

            return false;
        }
    }
}
