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
        private static string sSessionId = string.Empty;
        private static string sSeed = "0";
        private static bool sExportedThisPlayExit;

        /// <summary>
        /// 进入 Play 时复位导出去重标记，并清空会话身份（下次 Begin 重新生成）。
        /// </summary>
        public static void NotifyEnteredPlayMode()
        {
            sExportedThisPlayExit = false;
            sSessionId = string.Empty;
            sSeed = "0";
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

        public static string ResolveNotesDir(string subfolder)
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "Notes", subfolder);
#else
            return Path.Combine(Application.persistentDataPath, subfolder);
#endif
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
