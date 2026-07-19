using System;
using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Diagnostics;
using QFramework;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// #8 洗回牌库：解算批投影后入导演 sink，Present 前缀通道 Flush；旁路 Forget 泵不可达。
    /// </summary>
    public static class ShuffleIntoDeckLockstep
    {
        public static int EnqueueFromEventLog(
            ShuffleIntoDeckPresentSink sink,
            IArchitecture architecture,
            int startIndex,
            Func<int, bool> isAlreadyInDeck = null)
        {
            if (sink == null)
            {
                throw new ArgumentNullException("sink");
            }

            if (architecture == null)
            {
                throw new ArgumentNullException("architecture");
            }

            if (startIndex < 0)
            {
                return 0;
            }

            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var entries = pipeline.EventLog.Entries;
            if (startIndex >= entries.Count)
            {
                return 0;
            }

            var collected = ShuffleIntoDeckPresentationScanner.Collect(
                entries,
                startIndex,
                isAlreadyInDeck ?? (_ => false));
            if (collected.Count == 0)
            {
                return 0;
            }

            sink.EnqueueRange(collected);
            PerfTraceRecorder.Record(
                "ShuffleIntoDeck",
                -1,
                "EnqueuedForDirector",
                new Dictionary<string, string>
                {
                    ["count"] = collected.Count.ToString(),
                    ["path"] = "director",
                });
            return collected.Count;
        }

        public static bool HasShuffleExistingSince(IActionPipelineSystem pipeline, int startIndex)
        {
            if (pipeline == null || startIndex < 0)
            {
                return false;
            }

            var entries = pipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (ShuffleIntoDeckPresentationScanner.TryParseShuffleIntoEvent(
                        entries[i],
                        out var kind,
                        out _)
                    && kind == ShuffleIntoDeckEventKind.ExistingCard)
                {
                    return true;
                }
            }

            return false;
        }

        public static void RecordPresentBegin(int count)
        {
            PerfTraceRecorder.Record(
                "ShuffleIntoDeck",
                -1,
                "PresentBegin",
                new Dictionary<string, string>
                {
                    ["count"] = count.ToString(),
                    ["path"] = "director",
                });
        }

        public static void RecordPresentEnd(int count)
        {
            PerfTraceRecorder.Record(
                "ShuffleIntoDeck",
                -1,
                "PresentEnd",
                new Dictionary<string, string>
                {
                    ["count"] = count.ToString(),
                    ["path"] = "director",
                });
        }

        public static void RecordBurstScatterBegin(int actionId, int count)
        {
            PerfTraceRecorder.Record(
                "ShuffleBurstScatter",
                -1,
                "PresentBegin",
                new Dictionary<string, string>
                {
                    ["actionId"] = actionId.ToString(),
                    ["count"] = count.ToString(),
                    ["path"] = "director",
                });
        }

        public static void RecordBurstScatterEnd(int actionId, int count)
        {
            PerfTraceRecorder.Record(
                "ShuffleBurstScatter",
                -1,
                "PresentEnd",
                new Dictionary<string, string>
                {
                    ["actionId"] = actionId.ToString(),
                    ["count"] = count.ToString(),
                    ["path"] = "director",
                });
        }
    }
}
