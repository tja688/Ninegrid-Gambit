using System;
using System.Collections;
using UnityEngine;

namespace NineGrid.Presentation.Diagnostics
{
    public static class AdaptorPlaybackTrace
    {
        public static IEnumerator WaitWhilePlaying(
            string adaptorName,
            string performanceName,
            Func<bool> isPlaying,
            float timeoutSeconds)
        {
            float elapsed = 0f;
            while (isPlaying() && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (isPlaying())
            {
                PresentationTrace.Log(
                    PresentationTraceChannel.Performance,
                    PresentationTraceLevel.Warn,
                    "PERF_TIMEOUT",
                    ("adaptor", adaptorName),
                    ("performance", performanceName),
                    ("timeout", timeoutSeconds),
                    ("elapsed", elapsed));
            }
        }

        public static IEnumerator WaitUntilOrTimeout(
            string adaptorName,
            string performanceName,
            Func<bool> condition,
            float timeoutSeconds)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!condition())
            {
                PresentationTrace.Log(
                    PresentationTraceChannel.Performance,
                    PresentationTraceLevel.Warn,
                    "PERF_TIMEOUT",
                    ("adaptor", adaptorName),
                    ("performance", performanceName),
                    ("mode", "until"),
                    ("timeout", timeoutSeconds),
                    ("elapsed", elapsed));
            }
        }

        public static void InstructionBegin(string adaptorName, int batchId, int index, int total, string kind)
        {
            PresentationTrace.SetContext(batchId, string.Empty, string.Empty);
            PresentationTrace.Log(
                PresentationTraceChannel.Adaptor,
                PresentationTraceLevel.Trace,
                "INSTR_BEGIN",
                ("adaptor", adaptorName),
                ("idx", index + 1),
                ("total", total),
                ("kind", kind));
        }

        public static void InstructionEnd(string adaptorName, int batchId, int index, int total, string kind, float durationMs)
        {
            PresentationTrace.SetContext(batchId, string.Empty, string.Empty);
            PresentationTrace.Log(
                PresentationTraceChannel.Adaptor,
                PresentationTraceLevel.Trace,
                "INSTR_END",
                ("adaptor", adaptorName),
                ("idx", index + 1),
                ("total", total),
                ("kind", kind),
                ("durationMs", durationMs.ToString("F0")));
        }
    }
}
