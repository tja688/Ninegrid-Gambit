using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace NineGrid.Presentation.Editor
{
    /// <summary>
    /// One-shot: load a Profiler .data capture and dump spike frames + hierarchy tops to JSON.
    /// </summary>
    public static class ProfilerCaptureAnalyzer
    {
        private const string DefaultCapture =
            @"C:\Users\jinji\Documents\GitHub\Ninegrid Gambit\ProfilerCaptures\Ninegrid Gambit_2026-07-27_19-32-25.data";

        private static string _pendingOutPath;
        private static bool _waiting;

        [MenuItem("NineGrid/Diagnostics/Analyze Latest Profiler Capture")]
        public static void AnalyzeDefaultFromMenu()
        {
            Analyze(DefaultCapture);
        }

        public static string Analyze(string capturePath)
        {
            if (_waiting)
            {
                return "busy";
            }

            if (string.IsNullOrEmpty(capturePath) || !File.Exists(capturePath))
            {
                return "missing:" + capturePath;
            }

            _pendingOutPath = Path.ChangeExtension(capturePath, ".analysis.json");
            _waiting = true;
            ProfilerDriver.profileLoaded -= OnProfileLoaded;
            ProfilerDriver.profileLoaded += OnProfileLoaded;

            var ok = ProfilerDriver.LoadProfile(capturePath, false);
            if (!ok)
            {
                _waiting = false;
                ProfilerDriver.profileLoaded -= OnProfileLoaded;
                return "load-failed";
            }

            // Some Unity versions load sync enough that event already fired; also poll once.
            EditorApplication.delayCall += () =>
            {
                if (_waiting)
                {
                    TryAnalyzeLoaded("delayCall");
                }
            };

            return "loading:" + _pendingOutPath;
        }

        public static string Status()
        {
            if (_waiting)
            {
                return "waiting";
            }

            if (!string.IsNullOrEmpty(_pendingOutPath) && File.Exists(_pendingOutPath))
            {
                return "done:" + _pendingOutPath;
            }

            return "idle";
        }

        private static void OnProfileLoaded()
        {
            TryAnalyzeLoaded("profileLoaded");
        }

        private static void TryAnalyzeLoaded(string trigger)
        {
            if (!_waiting)
            {
                return;
            }

            var first = ProfilerDriver.firstFrameIndex;
            var last = ProfilerDriver.lastFrameIndex;
            if (last < first)
            {
                return;
            }

            _waiting = false;
            ProfilerDriver.profileLoaded -= OnProfileLoaded;

            try
            {
                WriteAnalysis(_pendingOutPath, first, last, trigger);
                Debug.Log("[ProfilerCaptureAnalyzer] Wrote " + _pendingOutPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[ProfilerCaptureAnalyzer] " + ex);
                File.WriteAllText(
                    _pendingOutPath ?? "profiler-analysis-error.txt",
                    "{\"error\":" + JsonEscape(ex.ToString()) + "}",
                    Encoding.UTF8);
            }
        }

        private static void WriteAnalysis(string outPath, int first, int last, string trigger)
        {
            var frames = new List<FrameSummary>(Math.Max(0, last - first + 1));
            for (var frame = first; frame <= last; frame++)
            {
                using var raw = ProfilerDriver.GetRawFrameDataView(frame, 0);
                if (raw == null || !raw.valid)
                {
                    continue;
                }

                frames.Add(new FrameSummary
                {
                    Frame = frame,
                    TimeMs = raw.frameTimeMs,
                    Fps = raw.frameFps,
                    GpuMs = raw.frameGpuTimeMs,
                    SampleCount = raw.sampleCount
                });
            }

            if (frames.Count == 0)
            {
                File.WriteAllText(outPath, "{\"error\":\"no-valid-frames\",\"trigger\":" + JsonEscape(trigger) + "}", Encoding.UTF8);
                return;
            }

            var sortedByTime = frames.OrderByDescending(f => f.TimeMs).ToList();
            var baseline = frames.OrderBy(f => f.TimeMs).Skip(frames.Count / 10).Take(Math.Max(1, frames.Count / 5)).ToList();
            var baselineMs = baseline.Count > 0 ? baseline.Average(f => f.TimeMs) : frames.Average(f => f.TimeMs);

            var spikeCount = Math.Min(12, sortedByTime.Count);
            var spikes = sortedByTime.Take(spikeCount).ToList();

            var interestingNames = new[]
            {
                "OnMouseEnter", "OnMouseExit", "OnMouseOver", "OnMouseDown",
                "DOTween", "DOKill", "Tween", "Punch",
                "TMP", "TextMeshPro", "Mesh", "Canvas", "Rebuild",
                "FindObject", "SendMessage", "Physics2D", "Physics.",
                "GC.Alloc", "GC.Collect", "Hover", "Description",
                "CardVisual", "GroundCard", "SetTarget", "Update"
            };

            var sb = new StringBuilder(256 * 1024);
            sb.Append("{\n");
            sb.Append("  \"trigger\": ").Append(JsonEscape(trigger)).Append(",\n");
            sb.Append("  \"firstFrame\": ").Append(first).Append(",\n");
            sb.Append("  \"lastFrame\": ").Append(last).Append(",\n");
            sb.Append("  \"frameCount\": ").Append(frames.Count).Append(",\n");
            sb.Append("  \"baselineMsApprox\": ").Append(F(baselineMs)).Append(",\n");
            sb.Append("  \"maxMs\": ").Append(F(sortedByTime[0].TimeMs)).Append(",\n");
            sb.Append("  \"maxFrame\": ").Append(sortedByTime[0].Frame).Append(",\n");
            sb.Append("  \"p95Ms\": ").Append(F(Percentile(sortedByTime.Select(f => f.TimeMs).Reverse().ToList(), 0.95))).Append(",\n");
            sb.Append("  \"p99Ms\": ").Append(F(Percentile(sortedByTime.Select(f => f.TimeMs).Reverse().ToList(), 0.99))).Append(",\n");
            sb.Append("  \"spikes\": [\n");

            for (var i = 0; i < spikes.Count; i++)
            {
                var spike = spikes[i];
                sb.Append("    {\n");
                sb.Append("      \"frame\": ").Append(spike.Frame).Append(",\n");
                sb.Append("      \"timeMs\": ").Append(F(spike.TimeMs)).Append(",\n");
                sb.Append("      \"fps\": ").Append(F(spike.Fps)).Append(",\n");
                sb.Append("      \"gpuMs\": ").Append(F(spike.GpuMs)).Append(",\n");
                sb.Append("      \"sampleCount\": ").Append(spike.SampleCount).Append(",\n");
                sb.Append("      \"topSelf\": [\n");
                AppendTopHierarchy(sb, spike.Frame, sortByTotal: false, limit: 20, indent: "        ");
                sb.Append("\n      ],\n");
                sb.Append("      \"topTotal\": [\n");
                AppendTopHierarchy(sb, spike.Frame, sortByTotal: true, limit: 20, indent: "        ");
                sb.Append("\n      ],\n");
                sb.Append("      \"interesting\": [\n");
                AppendInteresting(sb, spike.Frame, interestingNames, indent: "        ");
                sb.Append("\n      ]\n");
                sb.Append(i + 1 < spikes.Count ? "    },\n" : "    }\n");
            }

            sb.Append("  ],\n");
            sb.Append("  \"nameTotalsAcrossSpikes\": [\n");
            AppendNameAggregates(sb, spikes.Select(s => s.Frame).ToList(), interestingNames, indent: "    ");
            sb.Append("\n  ],\n");

            // Fast raw scan: correlate mouse / wait / GC markers with frame time across the session.
            sb.Append("  \"rawMarkerSummary\": ");
            AppendRawMarkerSummary(sb, first, last);
            sb.Append("\n}\n");

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        private static void AppendRawMarkerSummary(StringBuilder sb, int first, int last)
        {
            var needles = new[]
            {
                "OnMouseEnter", "OnMouseExit", "OnMouseOver",
                "QuickTestEntryInputHandler",
                "DOTween", "CardVisualDriver",
                "TMP", "TextMeshPro",
                "FindObject", "GC.Collect",
                "WaitForTargetFPS", "Gfx.WaitForPresent", "Gfx.WaitForPresentOnGfxThread",
                "Semaphore.WaitForSignal", "Profiler.RequestScreenshot", "Profiler.Screenshot"
            };

            var totals = new Dictionary<string, (int frames, int samples, double timeMsSum)>(StringComparer.OrdinalIgnoreCase);
            var mouseFrames = new List<(int frame, float frameMs, int enter, int exit, double mouseSampleMs)>();
            var waitHeavy = new List<(int frame, float frameMs, double waitMs, double cpuSampleMs)>();

            for (var frame = first; frame <= last; frame++)
            {
                using var raw = ProfilerDriver.GetRawFrameDataView(frame, 0);
                if (raw == null || !raw.valid)
                {
                    continue;
                }

                var enter = 0;
                var exit = 0;
                double mouseSampleMs = 0;
                double waitMs = 0;
                double cpuSampleMs = 0;
                var frameNeedlesHit = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var count = raw.sampleCount;
                for (var i = 0; i < count; i++)
                {
                    var name = raw.GetSampleName(i);
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    var sampleMs = raw.GetSampleTimeMs(i);
                    cpuSampleMs += sampleMs;

                    if (name.IndexOf("OnMouseEnter", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        enter++;
                        mouseSampleMs += sampleMs;
                    }
                    else if (name.IndexOf("OnMouseExit", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        exit++;
                        mouseSampleMs += sampleMs;
                    }
                    else if (name.IndexOf("OnMouseOver", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mouseSampleMs += sampleMs;
                    }

                    if (name.IndexOf("WaitForTargetFPS", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Gfx.WaitForPresent", StringComparison.OrdinalIgnoreCase) >= 0
                        || name.IndexOf("Semaphore.WaitForSignal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        waitMs += sampleMs;
                    }

                    foreach (var needle in needles)
                    {
                        if (name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            continue;
                        }

                        if (!frameNeedlesHit.Add(needle + "::" + name))
                        {
                            // still accumulate samples/time below via totals key=name
                        }

                        if (totals.TryGetValue(name, out var cur))
                        {
                            totals[name] = (cur.frames, cur.samples + 1, cur.timeMsSum + sampleMs);
                        }
                        else
                        {
                            totals[name] = (0, 1, sampleMs);
                        }
                    }
                }

                // Fix frames count: recount unique names per frame.
                // Simpler second pass for frame flags:
                if (enter + exit > 0)
                {
                    mouseFrames.Add((frame, raw.frameTimeMs, enter, exit, mouseSampleMs));
                }

                if (waitMs > 5f || (raw.frameTimeMs > 30f && cpuSampleMs < raw.frameTimeMs * 0.25f))
                {
                    waitHeavy.Add((frame, raw.frameTimeMs, waitMs, cpuSampleMs));
                }
            }

            // Recompute frame counts properly.
            var frameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var frame = first; frame <= last; frame++)
            {
                using var raw = ProfilerDriver.GetRawFrameDataView(frame, 0);
                if (raw == null || !raw.valid) continue;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < raw.sampleCount; i++)
                {
                    var name = raw.GetSampleName(i);
                    if (string.IsNullOrEmpty(name) || !totals.ContainsKey(name) || !seen.Add(name))
                    {
                        continue;
                    }

                    frameCounts[name] = frameCounts.TryGetValue(name, out var c) ? c + 1 : 1;
                }
            }

            sb.Append("{\n");
            sb.Append("    \"mouseFramesCount\": ").Append(mouseFrames.Count).Append(",\n");
            sb.Append("    \"mouseFramesTop\": [\n");
            var mouseTop = mouseFrames.OrderByDescending(m => m.frameMs).Take(25).ToList();
            for (var i = 0; i < mouseTop.Count; i++)
            {
                var m = mouseTop[i];
                sb.Append("      {\"frame\":").Append(m.frame)
                    .Append(",\"frameMs\":").Append(F(m.frameMs))
                    .Append(",\"enter\":").Append(m.enter)
                    .Append(",\"exit\":").Append(m.exit)
                    .Append(",\"mouseSampleMs\":").Append(F((float)m.mouseSampleMs))
                    .Append("}");
                if (i + 1 < mouseTop.Count) sb.Append(",");
                sb.Append("\n");
            }

            sb.Append("    ],\n");
            sb.Append("    \"waitOrUnaccountedTop\": [\n");
            var waitTop = waitHeavy.OrderByDescending(w => w.frameMs).Take(20).ToList();
            for (var i = 0; i < waitTop.Count; i++)
            {
                var w = waitTop[i];
                sb.Append("      {\"frame\":").Append(w.frame)
                    .Append(",\"frameMs\":").Append(F(w.frameMs))
                    .Append(",\"waitMs\":").Append(F((float)w.waitMs))
                    .Append(",\"sumSampleMs\":").Append(F((float)w.cpuSampleMs))
                    .Append("}");
                if (i + 1 < waitTop.Count) sb.Append(",");
                sb.Append("\n");
            }

            sb.Append("    ],\n");
            sb.Append("    \"markers\": [\n");
            var ordered = totals
                .Select(kv => (
                    name: kv.Key,
                    frames: frameCounts.TryGetValue(kv.Key, out var fc) ? fc : 0,
                    samples: kv.Value.samples,
                    timeMsSum: kv.Value.timeMsSum))
                .OrderByDescending(x => x.timeMsSum)
                .Take(40)
                .ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var o = ordered[i];
                sb.Append("      {\"name\":").Append(JsonEscape(o.name))
                    .Append(",\"frames\":").Append(o.frames)
                    .Append(",\"samples\":").Append(o.samples)
                    .Append(",\"timeMsSum\":").Append(F((float)o.timeMsSum))
                    .Append("}");
                if (i + 1 < ordered.Count) sb.Append(",");
                sb.Append("\n");
            }

            sb.Append("    ]\n");
            sb.Append("  }");
        }

        private static void AppendTopHierarchy(StringBuilder sb, int frame, bool sortByTotal, int limit, string indent)
        {
            var column = sortByTotal
                ? HierarchyFrameDataView.columnTotalTime
                : HierarchyFrameDataView.columnSelfTime;

            using var view = ProfilerDriver.GetHierarchyFrameDataView(
                frame,
                0,
                HierarchyFrameDataView.ViewModes.Default,
                column,
                false);

            if (view == null || !view.valid)
            {
                return;
            }

            var items = new List<(string name, float selfMs, float totalMs, int calls, float gcKb)>();
            CollectLeavesOrAll(view, view.GetRootItemID(), items, depth: 0, maxDepth: 8);

            IEnumerable<(string name, float selfMs, float totalMs, int calls, float gcKb)> ordered = sortByTotal
                ? items.OrderByDescending(i => i.totalMs)
                : items.OrderByDescending(i => i.selfMs);

            var top = ordered
                .Where(i => !string.IsNullOrEmpty(i.name) && i.name != "PlayerLoop" && i.name != "EditorLoop")
                .Take(limit)
                .ToList();

            for (var i = 0; i < top.Count; i++)
            {
                var t = top[i];
                sb.Append(indent).Append("{");
                sb.Append("\"name\":").Append(JsonEscape(t.name)).Append(",");
                sb.Append("\"selfMs\":").Append(F(t.selfMs)).Append(",");
                sb.Append("\"totalMs\":").Append(F(t.totalMs)).Append(",");
                sb.Append("\"calls\":").Append(t.calls).Append(",");
                sb.Append("\"gcKb\":").Append(F(t.gcKb));
                sb.Append("}");
                if (i + 1 < top.Count)
                {
                    sb.Append(",");
                }

                sb.Append("\n");
            }
        }

        private static void AppendInteresting(StringBuilder sb, int frame, string[] needles, string indent)
        {
            using var view = ProfilerDriver.GetHierarchyFrameDataView(
                frame,
                0,
                HierarchyFrameDataView.ViewModes.Default,
                HierarchyFrameDataView.columnTotalTime,
                false);

            if (view == null || !view.valid)
            {
                return;
            }

            var items = new List<(string name, float selfMs, float totalMs, int calls, float gcKb)>();
            CollectLeavesOrAll(view, view.GetRootItemID(), items, depth: 0, maxDepth: 12);

            var hits = items
                .Where(i => needles.Any(n => i.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderByDescending(i => i.totalMs)
                .Take(40)
                .ToList();

            for (var i = 0; i < hits.Count; i++)
            {
                var t = hits[i];
                sb.Append(indent).Append("{");
                sb.Append("\"name\":").Append(JsonEscape(t.name)).Append(",");
                sb.Append("\"selfMs\":").Append(F(t.selfMs)).Append(",");
                sb.Append("\"totalMs\":").Append(F(t.totalMs)).Append(",");
                sb.Append("\"calls\":").Append(t.calls).Append(",");
                sb.Append("\"gcKb\":").Append(F(t.gcKb));
                sb.Append("}");
                if (i + 1 < hits.Count)
                {
                    sb.Append(",");
                }

                sb.Append("\n");
            }
        }

        private static void AppendNameAggregates(StringBuilder sb, List<int> frames, string[] needles, string indent)
        {
            var agg = new Dictionary<string, (float self, float total, int calls, float gc)>(StringComparer.Ordinal);

            foreach (var frame in frames)
            {
                using var view = ProfilerDriver.GetHierarchyFrameDataView(
                    frame,
                    0,
                    HierarchyFrameDataView.ViewModes.Default,
                    HierarchyFrameDataView.columnTotalTime,
                    false);
                if (view == null || !view.valid)
                {
                    continue;
                }

                var items = new List<(string name, float selfMs, float totalMs, int calls, float gcKb)>();
                CollectLeavesOrAll(view, view.GetRootItemID(), items, depth: 0, maxDepth: 12);
                foreach (var item in items)
                {
                    if (!needles.Any(n => item.name.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        continue;
                    }

                    if (agg.TryGetValue(item.name, out var cur))
                    {
                        agg[item.name] = (cur.self + item.selfMs, cur.total + item.totalMs, cur.calls + item.calls, cur.gc + item.gcKb);
                    }
                    else
                    {
                        agg[item.name] = (item.selfMs, item.totalMs, item.calls, item.gcKb);
                    }
                }
            }

            var ordered = agg.OrderByDescending(kv => kv.Value.total).Take(40).ToList();
            for (var i = 0; i < ordered.Count; i++)
            {
                var kv = ordered[i];
                sb.Append(indent).Append("{");
                sb.Append("\"name\":").Append(JsonEscape(kv.Key)).Append(",");
                sb.Append("\"selfMsSum\":").Append(F(kv.Value.self)).Append(",");
                sb.Append("\"totalMsSum\":").Append(F(kv.Value.total)).Append(",");
                sb.Append("\"callsSum\":").Append(kv.Value.calls).Append(",");
                sb.Append("\"gcKbSum\":").Append(F(kv.Value.gc));
                sb.Append("}");
                if (i + 1 < ordered.Count)
                {
                    sb.Append(",");
                }

                sb.Append("\n");
            }
        }

        private static void CollectLeavesOrAll(
            HierarchyFrameDataView view,
            int itemId,
            List<(string name, float selfMs, float totalMs, int calls, float gcKb)> sink,
            int depth,
            int maxDepth)
        {
            if (itemId < 0 || depth > maxDepth)
            {
                return;
            }

            var name = view.GetItemName(itemId);
            var self = view.GetItemColumnDataAsSingle(itemId, HierarchyFrameDataView.columnSelfTime);
            var total = view.GetItemColumnDataAsSingle(itemId, HierarchyFrameDataView.columnTotalTime);
            var calls = (int)view.GetItemColumnDataAsSingle(itemId, HierarchyFrameDataView.columnCalls);
            var gc = view.GetItemColumnDataAsSingle(itemId, HierarchyFrameDataView.columnGcMemory) / 1024f;

            if (!string.IsNullOrEmpty(name) && (self > 0.01f || total > 0.05f || gc > 1f))
            {
                sink.Add((name, self, total, calls, gc));
            }

            var children = new List<int>(16);
            view.GetItemChildren(itemId, children);
            foreach (var child in children)
            {
                CollectLeavesOrAll(view, child, sink, depth + 1, maxDepth);
            }
        }

        private static float Percentile(List<float> ascendingSortedUniqueSource, double p)
        {
            // ascendingSortedUniqueSource expected ascending
            if (ascendingSortedUniqueSource == null || ascendingSortedUniqueSource.Count == 0)
            {
                return 0f;
            }

            var sorted = ascendingSortedUniqueSource.OrderBy(v => v).ToList();
            var idx = (int)Math.Clamp(Math.Round((sorted.Count - 1) * p), 0, sorted.Count - 1);
            return sorted[idx];
        }

        private static string F(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);

        private static string JsonEscape(string s)
        {
            if (s == null)
            {
                return "null";
            }

            return "\"" + s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t") + "\"";
        }

        private sealed class FrameSummary
        {
            public int Frame;
            public float TimeMs;
            public float Fps;
            public float GpuMs;
            public int SampleCount;
        }
    }
}
