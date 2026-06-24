using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NineGrid.Core;
using NineGrid.Presentation.FSM;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Diagnostics
{
    public static class PresentationTrace
    {
        private static PresentationTraceConfig sConfig;
        private static PresentationTraceRingBuffer sBuffer;
        private static string sLastStallDumpPath = string.Empty;
        private static int sContextBatchId;
        private static string sContextPhase = string.Empty;
        private static string sContextScreen = string.Empty;

        public static event Action<PresentationTraceEntry> EntryRecorded;

        public static PresentationTraceConfig Config
        {
            get
            {
                EnsureInitialized();
                return sConfig;
            }
        }

        public static PresentationTraceRingBuffer Buffer
        {
            get
            {
                EnsureInitialized();
                return sBuffer;
            }
        }

        public static string LastStallDumpPath => sLastStallDumpPath;

        public static void Initialize(PresentationTraceConfig config)
        {
            sConfig = config != null ? config : CreateRuntimeDefaultConfig();
            sBuffer = new PresentationTraceRingBuffer(sConfig.RingBufferCapacity);
        }

        public static void SetContext(int batchId, string phase, string screen)
        {
            sContextBatchId = batchId;
            sContextPhase = phase ?? string.Empty;
            sContextScreen = screen ?? string.Empty;
        }

        public static void SetContextFromArchitecture(IArchitecture architecture)
        {
            if (architecture == null)
            {
                return;
            }

            var sync = architecture.GetSystem<IPresentationSyncSystem>();
            var phase = architecture.GetModel<RunModel>().Phase.Value;
            sContextBatchId = sync != null ? sync.ActiveBatchId : 0;
            sContextPhase = phase.ToString();
        }

        public static void Log(
            PresentationTraceChannel channel,
            PresentationTraceLevel level,
            string eventName,
            params (string key, object value)[] fields)
        {
            EnsureInitialized();
            if (!sConfig.IsChannelEnabled(channel) || !sConfig.IsLevelEnabled(level))
            {
                return;
            }

            var normalizedFields = NormalizeFields(fields);
            var entry = new PresentationTraceEntry(
                Time.realtimeSinceStartupAsDouble,
                channel,
                level,
                eventName,
                sContextBatchId,
                sContextPhase,
                sContextScreen,
                normalizedFields);

            sBuffer.Add(entry);
            EntryRecorded?.Invoke(entry);
            WriteToUnityConsole(entry);
        }

        public static void LogFsm(
            string fsmName,
            PresentationTraceLevel level,
            string eventName,
            params (string key, object value)[] fields)
        {
            var merged = new (string, object)[fields.Length + 1];
            merged[0] = ("fsm", fsmName);
            Array.Copy(fields, 0, merged, 1, fields.Length);
            Log(PresentationTraceChannel.Fsm, level, eventName, merged);
        }

        public static string DumpRingBufferJson()
        {
            EnsureInitialized();
            return sBuffer.ToJsonArray();
        }

        public static string WriteStallDump(string ruleId, string summary, string snapshotJson)
        {
            EnsureInitialized();
            if (!sConfig.WriteDumpOnStall)
            {
                return string.Empty;
            }

            try
            {
                var directory = Path.Combine(Application.dataPath, "..", "Logs");
                Directory.CreateDirectory(directory);
                var fileName = "presentation-trace-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json";
                var path = Path.Combine(directory, fileName);

                var builder = new StringBuilder(4096);
                builder.Append('{');
                builder.Append("\"rule\":\"").Append(ruleId).Append('"');
                builder.Append(",\"summary\":\"").Append(summary.Replace("\"", "\\\"")).Append('"');
                builder.Append(",\"snapshot\":").Append(string.IsNullOrEmpty(snapshotJson) ? "{}" : snapshotJson);
                builder.Append(",\"trace\":").Append(sBuffer.ToJsonArray());
                builder.Append('}');

                File.WriteAllText(path, builder.ToString());
                sLastStallDumpPath = path;
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogError("[PRES][Watchdog] Failed to write stall dump: " + ex.Message);
                return string.Empty;
            }
        }

        private static void EnsureInitialized()
        {
            if (sConfig != null && sBuffer != null)
            {
                return;
            }

            Initialize(null);
        }

        private static PresentationTraceConfig CreateRuntimeDefaultConfig()
        {
            var config = ScriptableObject.CreateInstance<PresentationTraceConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }

        private static IReadOnlyList<KeyValuePair<string, string>> NormalizeFields((string key, object value)[] fields)
        {
            if (fields == null || fields.Length == 0)
            {
                return Array.Empty<KeyValuePair<string, string>>();
            }

            var list = new List<KeyValuePair<string, string>>(fields.Length);
            for (var i = 0; i < fields.Length; i++)
            {
                var key = fields[i].key ?? string.Empty;
                var value = fields[i].value != null ? fields[i].value.ToString() : string.Empty;
                list.Add(new KeyValuePair<string, string>(key, value));
            }

            return list;
        }

        private static void WriteToUnityConsole(PresentationTraceEntry entry)
        {
            var line = entry.FormatConsoleLine();
            switch (entry.Level)
            {
                case PresentationTraceLevel.Warn:
                    Debug.LogWarning(line);
                    break;
                case PresentationTraceLevel.Error:
                case PresentationTraceLevel.Stall:
                    Debug.LogError(line);
                    break;
                default:
                    Debug.Log(line);
                    break;
            }
        }
    }
}
