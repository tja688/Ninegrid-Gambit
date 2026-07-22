using System;
using System.IO;
using System.Text;
using NineGrid.Core;
using QFramework;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// BattleTrace / FlowTrace 共享会话时钟与导出目录。保证同一次 Play 两边 sessionId/seed 一致。
    /// </summary>
    public static class DiagTraceShared
    {
        public const string QuickTestRunTag = "QuickTest";

        private static string sSessionId = string.Empty;
        private static string sSeed = "0";
        private static string sRunTag = string.Empty;
        private static string sRunTagNote = string.Empty;
        private static bool sExportedThisPlayExit;

        /// <summary>
        /// 进入 Play 时复位导出去重标记，并清空会话身份（下次 Begin 重新生成）。
        /// </summary>
        public static void NotifyEnteredPlayMode()
        {
            sExportedThisPlayExit = false;
            sSessionId = string.Empty;
            sSeed = "0";
            ClearRunTag();
            DiagBeatClock.Reset();
        }

        public static string RunTag => sRunTag ?? string.Empty;

        public static string RunTagNote => sRunTagNote ?? string.Empty;

        /// <summary>
        /// 标记本局诊断会话（如 DevTest 快速测试），写入各 Log session 与导出文件名前缀。
        /// </summary>
        public static void SetRunTag(string tag, string note = null)
        {
            sRunTag = tag ?? string.Empty;
            sRunTagNote = note ?? string.Empty;
        }

        public static void ClearRunTag()
        {
            sRunTag = string.Empty;
            sRunTagNote = string.Empty;
        }

        public static void StampSessionRunMetadata(
            out string runTag,
            out string runTagNote)
        {
            runTag = RunTag;
            runTagNote = RunTagNote;
        }

        public static bool AlreadyExportedThisPlayExit => sExportedThisPlayExit;

        public static void MarkExportedThisPlayExit()
        {
            sExportedThisPlayExit = true;
        }

        public static string CurrentSessionId => sSessionId;

        public static string CurrentSeed => sSeed;

        /// <summary>
        /// 确保本 Play 有统一 sessionId/seed。seed=0 时尝试读 RunModel。
        /// </summary>
        public static void EnsureSessionIdentity(ulong seed = 0UL)
        {
            try
            {
                if (seed == 0UL)
                {
                    seed = TryReadSeed();
                }

                if (string.IsNullOrEmpty(sSessionId))
                {
                    sSessionId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                }

                if (seed != 0UL || sSeed == "0")
                {
                    sSeed = seed.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DiagTrace] EnsureSessionIdentity failed: " + ex.Message);
            }
        }

        public static void ClearSessionIdentity()
        {
            sSessionId = string.Empty;
            sSeed = "0";
        }

        /// <summary>
        /// 强制新开一局诊断会话（失败重开 / 再点开始）。生成新的 sessionId。
        /// </summary>
        public static void ForceNewSessionIdentity(ulong seed = 0UL)
        {
            try
            {
                if (seed == 0UL)
                {
                    seed = TryReadSeed();
                }

                var next = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                if (!string.IsNullOrEmpty(sSessionId) && next == sSessionId)
                {
                    next = next + "-" + DateTime.Now.ToString("fff");
                }

                sSessionId = next;
                sSeed = seed != 0UL ? seed.ToString() : "0";
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DiagTrace] ForceNewSessionIdentity failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 解析 Notes 子目录。支持嵌套，如 <c>Logs/CoreLog</c>、<c>Logs/OtherLog/BattleLog</c>。
        /// </summary>
        public static string ResolveNotesDir(string subfolder)
        {
            var parts = string.IsNullOrEmpty(subfolder)
                ? Array.Empty<string>()
                : subfolder.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

#if UNITY_EDITOR
            var path = Path.Combine(Application.dataPath, "Notes");
#else
            var path = Application.persistentDataPath;
#endif
            for (var i = 0; i < parts.Length; i++)
            {
                path = Path.Combine(path, parts[i]);
            }

            return path;
        }

        /// <summary>
        /// UTF-8 写盘；返回完整路径。失败返回 null。
        /// </summary>
        public static string WriteUtf8File(string directory, string fileName, string contents)
        {
            try
            {
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, fileName);
                File.WriteAllText(path, contents ?? string.Empty, Encoding.UTF8);
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DiagTrace] WriteUtf8File failed: " + ex.Message);
                return null;
            }
        }

        public static string BuildFileName(string prefix, string sessionId, string seed)
        {
            var sid = string.IsNullOrEmpty(sessionId) ? DateTime.Now.ToString("yyyyMMdd-HHmmss") : sessionId;
            var s = string.IsNullOrEmpty(seed) ? "0" : seed;
            if (!string.IsNullOrEmpty(sRunTag))
            {
                prefix = prefix + "-" + sRunTag;
            }

            return prefix + "-" + sid + "-seed" + s + ".json";
        }

        private static ulong TryReadSeed()
        {
            try
            {
                return NineGridArchitecture.Current.GetModel<RunModel>().Seed.Value;
            }
            catch
            {
                return 0UL;
            }
        }
    }
}
