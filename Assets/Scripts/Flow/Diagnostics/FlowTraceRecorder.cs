using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 全流程旁路 FlowTrace 记录器。打点失败一律吞掉，不影响游戏路径。
    /// </summary>
    public static class FlowTraceRecorder
    {
        private static FlowTraceSession sSession;
        private static bool sEnabled =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        public static bool Enabled
        {
            get => sEnabled;
            set => sEnabled = value;
        }

        public static FlowTraceSession CurrentSession => sSession;

        public static bool HasEvents =>
            sSession != null && sSession.events != null && sSession.events.Count > 0;

        /// <summary>
        /// 是否已有「上一局」内容（含 StartRun/胜负），失败重开时应轮转 session。
        /// </summary>
        public static bool HasPriorRunMarker()
        {
            if (!HasEvents)
            {
                return false;
            }

            for (var i = 0; i < sSession.events.Count; i++)
            {
                var e = sSession.events[i];
                if (e == null)
                {
                    continue;
                }

                if (e.name == FlowTraceNames.StartRun
                    || e.name == FlowTraceNames.Defeat
                    || e.name == FlowTraceNames.Victory)
                {
                    return true;
                }
            }

            return false;
        }

        public static void Clear()
        {
            try
            {
                sSession = null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FlowTrace] Clear failed: " + ex.Message);
            }
        }

        public static void BeginSessionIfNeeded(ulong seed = 0UL)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                DiagTraceShared.EnsureSessionIdentity(seed);
                if (sSession != null)
                {
                    // 对齐 Shared（Bootstrap 后 seed 可能从 0 变为真实值）
                    sSession.sessionId = DiagTraceShared.CurrentSessionId;
                    sSession.seed = DiagTraceShared.CurrentSeed;
                    DiagTraceShared.StampSessionRunMetadata(out sSession.runTag, out sSession.runTagNote);
                    return;
                }

                sSession = new FlowTraceSession
                {
                    schemaVersion = 3,
                    seed = DiagTraceShared.CurrentSeed,
                    sessionId = DiagTraceShared.CurrentSessionId,
                    events = new List<FlowTraceEvent>(),
                };
                DiagTraceShared.StampSessionRunMetadata(out sSession.runTag, out sSession.runTagNote);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FlowTrace] BeginSession failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 随时插桩：记一条流程事件。payload 可为 null。
        /// </summary>
        public static void Record(
            string category,
            string name,
            Dictionary<string, string> payload = null,
            string loopState = null,
            string phaseBefore = null,
            string phaseAfter = null,
            bool accepted = true,
            int refBattleOpIndex = -1)
        {
            if (!sEnabled)
            {
                return;
            }

            try
            {
                BeginSessionIfNeeded();
                if (sSession == null)
                {
                    return;
                }

                var ev = new FlowTraceEvent
                {
                    index = sSession.events.Count,
                    beatId = DiagBeatClock.ResolveBeatIdForEvent(),
                    category = category ?? string.Empty,
                    name = name ?? string.Empty,
                    loopState = loopState ?? string.Empty,
                    phaseBefore = phaseBefore ?? string.Empty,
                    phaseAfter = phaseAfter ?? string.Empty,
                    accepted = accepted,
                    refBattleOpIndex = refBattleOpIndex,
                    payload = payload != null
                        ? new Dictionary<string, string>(payload)
                        : new Dictionary<string, string>(),
                };
                sSession.events.Add(ev);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FlowTrace] Record failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 导出当前局 CoreLog。Editor → Assets/Notes/Logs/CoreLog。
        /// </summary>
        /// <param name="silentIfEmpty">无数据时不打 Warning（轮转/一并导出用）。</param>
        public static string ExportJson(bool silentIfEmpty = false)
        {
            try
            {
                if (sSession == null || sSession.events == null || sSession.events.Count == 0)
                {
                    if (!silentIfEmpty)
                    {
                        Debug.LogWarning("[FlowTrace] ExportJson: 无会话或无 events。");
                    }

                    return null;
                }

                var json = FlowTraceJson.Serialize(sSession);
                var dir = DiagTraceShared.ResolveNotesDir("Logs/CoreLog");
                var fileName = DiagTraceShared.BuildFileName(
                    "corelog",
                    sSession.sessionId,
                    sSession.seed);
                var path = DiagTraceShared.WriteUtf8File(dir, fileName, json);
                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log("[FlowTrace] Exported CoreLog: " + path + "\n" + BuildTailSummary(sSession));
                }

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FlowTrace] ExportJson failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Play 退出导出（与 BattleTrace 共用 DiagTraceShared 去重标记的「已尝试导出」由调用方协调）。
        /// 有 events 则导出；无数据静默跳过。
        /// </summary>
        public static string ExportOnPlayExit(string source)
        {
            try
            {
                if (!sEnabled)
                {
                    return null;
                }

                if (sSession == null || sSession.events == null || sSession.events.Count == 0)
                {
                    return null;
                }

                var path = ExportJson();
                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log("[FlowTrace] Play 结束已导出流程日志（" + source + "）：" + path);
                }

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FlowTrace] ExportOnPlayExit failed: " + ex.Message);
                return null;
            }
        }

        public static string ResolveExportDirectory()
        {
            return DiagTraceShared.ResolveNotesDir("Logs/CoreLog");
        }

        public static void RecordSessionChoreoSummary(Dictionary<string, string> payload)
        {
            Record(
                FlowTraceCategory.Presentation,
                FlowTraceNames.SessionChoreoSummary,
                payload,
                accepted: true);
        }

        private static string BuildTailSummary(FlowTraceSession session)
        {
            var sb = new StringBuilder();
            sb.Append("events=").Append(session.events.Count);
            var from = Math.Max(0, session.events.Count - 4);
            for (var i = from; i < session.events.Count; i++)
            {
                var e = session.events[i];
                if (e == null)
                {
                    continue;
                }

                sb.Append(" | [#").Append(e.index)
                    .Append(' ').Append(e.category)
                    .Append('/').Append(e.name)
                    .Append(" ok=").Append(e.accepted)
                    .Append(']');
            }

            return sb.ToString();
        }
    }
}
