using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// Debug Console 抓取轨（第五轨）：订阅 <c>Application.logMessageReceivedThreaded</c>，
    /// 缓存 Warning / Error / Exception / Assert（主抓报错，普通 Log 不进轨），
    /// 随四轨一起自动落盘（胜负 / 重开轮转 / 退出）并进 F12 手动快照。
    /// Editor → Assets/Notes/Logs/OtherLog/ConsoleLog；Player → exe 旁 GameLogs/Logs/OtherLog/ConsoleLog。
    /// </summary>
    public static class ConsoleTraceRecorder
    {
        private const int MaxEntries = 2000;
        private const int MaxMessageChars = 2000;
        private const int MaxStackChars = 3000;

        private static readonly object sLock = new object();
        private static readonly List<Entry> sEntries = new List<Entry>(256);
        private static int sDroppedCount;
        private static bool sHooked;

        private static readonly bool sEnabled =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        public sealed class Entry
        {
            public string timeLocal;
            public string type;
            public string message;
            public string stackTrace;
        }

        public static bool Enabled => sEnabled;

        public static bool HasEntries
        {
            get
            {
                lock (sLock)
                {
                    return sEntries.Count > 0;
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void HookOnLoad()
        {
            Hook();
#if !UNITY_EDITOR && DEVELOPMENT_BUILD
            // Player 中途退出兜底：随四轨一起落盘（胜负点导出照常，同会话同名文件覆盖写）。
            Application.quitting -= OnPlayerQuitting;
            Application.quitting += OnPlayerQuitting;
#endif
        }

        public static void Hook()
        {
            if (!sEnabled || sHooked)
            {
                return;
            }

            Application.logMessageReceivedThreaded -= OnLogMessage;
            Application.logMessageReceivedThreaded += OnLogMessage;
            sHooked = true;
        }

#if !UNITY_EDITOR && DEVELOPMENT_BUILD
        private static void OnPlayerQuitting()
        {
            try
            {
                BattleTraceRecorder.ExportOnPlayExit("ApplicationQuitting");
            }
            catch
            {
                // ignore
            }
        }
#endif

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            // 可能在工作线程回调：不得触碰 Unity 主线程 API（Time / Architecture 等）。
            if (!sEnabled || type == LogType.Log)
            {
                return;
            }

            try
            {
                var entry = new Entry
                {
                    timeLocal = DateTime.Now.ToString("HH:mm:ss.fff"),
                    type = type.ToString(),
                    message = Truncate(condition, MaxMessageChars),
                    stackTrace = type == LogType.Warning
                        ? string.Empty
                        : Truncate(stackTrace, MaxStackChars),
                };

                lock (sLock)
                {
                    if (sEntries.Count >= MaxEntries)
                    {
                        sEntries.RemoveAt(0);
                        sDroppedCount++;
                    }

                    sEntries.Add(entry);
                }
            }
            catch
            {
                // 抓取失败一律吞掉，不影响游戏路径。
            }
        }

        public static void Clear()
        {
            lock (sLock)
            {
                sEntries.Clear();
                sDroppedCount = 0;
            }
        }

        /// <summary>
        /// 序列化当前缓冲为 JSON（含会话身份）；空缓冲返回 null（供手动快照判空）。
        /// </summary>
        public static string SerializeCurrent()
        {
            Entry[] snapshot;
            int dropped;
            lock (sLock)
            {
                if (sEntries.Count == 0)
                {
                    return null;
                }

                snapshot = sEntries.ToArray();
                dropped = sDroppedCount;
            }

            var sb = new StringBuilder(4096);
            sb.Append('{');
            sb.Append("\"schemaVersion\":1,");
            AppendString(sb, "seed", DiagTraceShared.CurrentSeed ?? "0");
            sb.Append(',');
            AppendString(sb, "sessionId", DiagTraceShared.CurrentSessionId ?? string.Empty);
            sb.Append(',');
            AppendString(sb, "runTag", DiagTraceShared.RunTag ?? string.Empty);
            sb.Append(',');
            sb.Append("\"droppedOldest\":").Append(dropped).Append(',');
            sb.Append("\"entries\":[");
            for (var i = 0; i < snapshot.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var e = snapshot[i];
                sb.Append('{');
                AppendString(sb, "time", e.timeLocal);
                sb.Append(',');
                AppendString(sb, "type", e.type);
                sb.Append(',');
                AppendString(sb, "message", e.message);
                sb.Append(',');
                AppendString(sb, "stackTrace", e.stackTrace);
                sb.Append('}');
            }

            sb.Append("]}");
            return sb.ToString();
        }

        /// <summary>
        /// 导出当前缓冲（随四轨一并调用）。空缓冲静默跳过。
        /// </summary>
        /// <param name="automatic">自动落盘时受 <see cref="DiagTraceExportPreferences.AutoExportEnabled"/> 总开关约束。</param>
        public static string ExportJson(bool silentIfEmpty = false, bool automatic = true)
        {
            try
            {
                if (!sEnabled)
                {
                    return null;
                }

                if (automatic && !DiagTraceExportPreferences.AutoExportEnabled)
                {
                    return null;
                }

                var json = SerializeCurrent();
                if (json == null)
                {
                    if (!silentIfEmpty)
                    {
                        Debug.LogWarning("[ConsoleTrace] ExportJson: 无可导出的 Console 记录。");
                    }

                    return null;
                }

                var dir = DiagTraceShared.ResolveNotesDir("Logs/OtherLog/ConsoleLog");
                var fileName = DiagTraceShared.BuildFileName(
                    "consolelog",
                    DiagTraceShared.CurrentSessionId,
                    DiagTraceShared.CurrentSeed);
                var path = DiagTraceShared.WriteUtf8File(dir, fileName, json);
                if (!string.IsNullOrEmpty(path))
                {
                    Debug.Log("[ConsoleTrace] Exported ConsoleLog: " + path);
                }

                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ConsoleTrace] ExportJson failed: " + ex.Message);
                return null;
            }
        }

        private static string Truncate(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= maxChars ? value : value.Substring(0, maxChars) + "…(truncated)";
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append('"').Append(key).Append("\":\"");
            AppendEscaped(sb, value);
            sb.Append('"');
        }

        private static void AppendEscaped(StringBuilder sb, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }
        }
    }
}
