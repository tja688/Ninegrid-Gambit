using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// 试玩者手动 bug 快照：把当前四轨诊断日志物理写到磁盘，并附带给 AI 的醒目标识。
    /// Editor → Assets/Notes/Logs/ManualBugSnapshots；
    /// Player → 可执行文件旁 ManualBugSnapshots（非临时缓存）。
    /// </summary>
    public static class DiagTraceManualSnapshot
    {
        public const string RootFolderName = "ManualBugSnapshots";
        public const string AiReadmeFileName = "!!!AI_READ_THIS_FIRST!!!.md";
        public const string AiFileNamePrefix = "!!!AI-BUG-REPORT!!!-";
        public const string AiAttentionMarker = "!!!AI_ATTENTION_MANUAL_BUG_REPORT!!!";

        public readonly struct Result
        {
            public readonly bool Success;
            public readonly string FolderPath;
            public readonly string ReadmePath;
            public readonly string UserTag;
            public readonly string Message;
            public readonly int FileCount;

            public Result(
                bool success,
                string folderPath,
                string readmePath,
                string userTag,
                string message,
                int fileCount)
            {
                Success = success;
                FolderPath = folderPath ?? string.Empty;
                ReadmePath = readmePath ?? string.Empty;
                UserTag = userTag ?? string.Empty;
                Message = message ?? string.Empty;
                FileCount = fileCount;
            }
        }

        /// <summary>
        /// 戳 UserMark → 把当前内存中的四轨日志写入独立快照目录（含 AI 必读说明）。
        /// </summary>
        public static Result Save(string userTag)
        {
            var tag = string.IsNullOrWhiteSpace(userTag)
                ? "未填写问题描述"
                : userTag.Trim();

            try
            {
                DiagTraceShared.EnsureSessionIdentity();
                PerfTraceRecorder.StampUserObservation(tag);

                var root = ResolveRootDirectory();
                Directory.CreateDirectory(root);

                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var folderName = AiFileNamePrefix + stamp + "_" + SanitizeForFileName(tag, 48);
                var folder = Path.Combine(root, folderName);
                Directory.CreateDirectory(folder);

                var written = new List<string>();
                TryWriteTrack(
                    folder,
                    "battlelog",
                    () =>
                    {
                        var session = BattleTraceRecorder.CurrentSession;
                        return session == null ? null : BattleTraceJson.Serialize(session);
                    },
                    written);
                TryWriteTrack(
                    folder,
                    "corelog",
                    () =>
                    {
                        var session = FlowTraceRecorder.CurrentSession;
                        return session == null ? null : FlowTraceJson.Serialize(session);
                    },
                    written);
                TryWriteTrack(
                    folder,
                    "perflog",
                    () =>
                    {
                        var session = PerfTraceRecorder.CurrentSession;
                        return session == null ? null : PerfTraceJson.Serialize(session);
                    },
                    written);
                TryWriteTrack(
                    folder,
                    "registrylog",
                    () =>
                    {
                        var session = RegistryTraceRecorder.CurrentSession;
                        return session == null ? null : RegistryTraceJson.Serialize(session);
                    },
                    written);
                TryWriteTrack(
                    folder,
                    "consolelog",
                    ConsoleTraceRecorder.SerializeCurrent,
                    written);

                var readmePath = Path.Combine(folder, AiReadmeFileName);
                File.WriteAllText(readmePath, BuildAiReadme(tag, folder, written), Encoding.UTF8);
                written.Add(AiReadmeFileName);

                if (written.Count <= 1)
                {
                    var emptyMsg = "已创建快照目录，但当前没有可导出的诊断数据（请先进入对局/战斗后再记录）。\n路径：" + folder;
                    Debug.LogWarning("[DiagTraceManualSnapshot] " + emptyMsg);
                    return new Result(false, folder, readmePath, tag, emptyMsg, written.Count);
                }

                var okMsg = "log 记录成功！\n已保存 " + written.Count + " 个文件到：\n" + folder;
                Debug.Log("[DiagTraceManualSnapshot] " + okMsg);
                return new Result(true, folder, readmePath, tag, okMsg, written.Count);
            }
            catch (Exception ex)
            {
                var fail = "log 保存失败：" + ex.Message;
                Debug.LogWarning("[DiagTraceManualSnapshot] " + fail);
                return new Result(false, string.Empty, string.Empty, tag, fail, 0);
            }
        }

        public static string ResolveRootDirectory()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "Notes", "Logs", RootFolderName);
#else
            // 打包体：可执行文件同级 ManualBugSnapshots（试玩者可直接找到并打包发给开发者）
            var dataParent = Directory.GetParent(Application.dataPath);
            var baseDir = dataParent != null ? dataParent.FullName : Application.persistentDataPath;
            return Path.Combine(baseDir, RootFolderName);
#endif
        }

        public static string SanitizeForFileName(string raw, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "untitled";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(raw.Length);
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                var bad = false;
                for (var j = 0; j < invalid.Length; j++)
                {
                    if (c == invalid[j])
                    {
                        bad = true;
                        break;
                    }
                }

                if (bad || c < 32)
                {
                    sb.Append('_');
                }
                else if (char.IsWhiteSpace(c))
                {
                    sb.Append('_');
                }
                else
                {
                    sb.Append(c);
                }
            }

            var s = sb.ToString().Trim('_');
            if (string.IsNullOrEmpty(s))
            {
                s = "untitled";
            }

            if (maxLen > 0 && s.Length > maxLen)
            {
                s = s.Substring(0, maxLen).TrimEnd('_');
            }

            return s;
        }

        private static void TryWriteTrack(
            string folder,
            string trackPrefix,
            Func<string> serialize,
            List<string> written)
        {
            try
            {
                var json = serialize();
                if (string.IsNullOrEmpty(json) || json == "{}")
                {
                    return;
                }

                // 空会话常见形态：只有 schema 壳、无 ops/events —— 仍写出便于 AI 对照，但文件名照常打标记
                var sessionId = DiagTraceShared.CurrentSessionId;
                var seed = DiagTraceShared.CurrentSeed;
                var fileName = AiFileNamePrefix + DiagTraceShared.BuildFileName(trackPrefix, sessionId, seed);
                var path = DiagTraceShared.WriteUtf8File(folder, fileName, json);
                if (!string.IsNullOrEmpty(path))
                {
                    written.Add(Path.GetFileName(path));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DiagTraceManualSnapshot] write " + trackPrefix + " failed: " + ex.Message);
            }
        }

        private static string BuildAiReadme(string userTag, string folder, List<string> files)
        {
            var sb = new StringBuilder(2048);
            sb.AppendLine("################################################################################");
            sb.AppendLine("# " + AiAttentionMarker);
            sb.AppendLine("# !!! AI：这是试玩者手动保存的 BUG 证据包，请优先阅读本文件再分析同目录 JSON !!!");
            sb.AppendLine("################################################################################");
            sb.AppendLine();
            sb.AppendLine("## USER_PROBLEM_TAG（试玩者填写的问题描述 —— 排查必须以它为准）");
            sb.AppendLine();
            sb.AppendLine("> " + userTag);
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("USER_PROBLEM_TAG=" + userTag);
            sb.AppendLine("AI_ATTENTION_MARKER=" + AiAttentionMarker);
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("## Snapshot metadata");
            sb.AppendLine();
            sb.AppendLine("- savedAtLocal: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("- sessionId: " + (DiagTraceShared.CurrentSessionId ?? string.Empty));
            sb.AppendLine("- seed: " + (DiagTraceShared.CurrentSeed ?? "0"));
            sb.AppendLine("- runTag: " + (DiagTraceShared.RunTag ?? string.Empty));
            sb.AppendLine("- runTagNote: " + (DiagTraceShared.RunTagNote ?? string.Empty));
            sb.AppendLine("- floor: " + DiagTraceShared.ResolveFloor());
            sb.AppendLine("- nodeIndex: " + DiagTraceShared.ResolveNodeIndex());
            sb.AppendLine("- folder: `" + folder + "`");
            sb.AppendLine("- unityVersion: " + Application.unityVersion);
            sb.AppendLine("- productName: " + Application.productName);
            sb.AppendLine("- platform: " + Application.platform);
            sb.AppendLine("- isEditor: " + Application.isEditor);
            sb.AppendLine();
            sb.AppendLine("## How AI should investigate");
            sb.AppendLine();
            sb.AppendLine("1. Treat `USER_PROBLEM_TAG` as the reproduced symptom description.");
            sb.AppendLine("2. Open sibling `!!!AI-BUG-REPORT!!!-*.json` (battle / core / perf / registry).");
            sb.AppendLine("3. Correlate Run(sessionId/seed/runTag) → Floor → Node → Battle(opIndex).");
            sb.AppendLine("4. Search Error / Exception / Assert / Reject / UserMark near the symptom time.");
            sb.AppendLine("5. Prefer evidence in these files over guesses; cite opIndex / event sites in the reply.");
            sb.AppendLine();
            sb.AppendLine("## Files in this snapshot");
            sb.AppendLine();
            for (var i = 0; i < files.Count; i++)
            {
                sb.AppendLine("- `" + files[i] + "`");
            }

            sb.AppendLine();
            sb.AppendLine("################################################################################");
            sb.AppendLine("# END " + AiAttentionMarker);
            sb.AppendLine("################################################################################");
            return sb.ToString();
        }
    }
}
