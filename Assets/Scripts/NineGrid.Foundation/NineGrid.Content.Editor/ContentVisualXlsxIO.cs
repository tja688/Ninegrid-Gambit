using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public sealed class ContentVisualXlsxRow
    {
        public string ContentId { get; set; }
        public string ContentKind { get; set; }
        public string Description { get; set; }
        public int SheetRowIndex { get; set; } = -1;
    }

    /// <summary>
    /// 旧 content_visual.xlsx 桥（#69 Luban 工具链已退休）。脚本缺失时 Read 返回空列表，不抛。
    /// </summary>
    public static class ContentVisualXlsxIO
    {
        public const string RelativeXlsxPath = "Assets/Tools/Luban/Datas/content_visual.xlsx";
        public const string RelativeIoScriptPath = "Assets/Tools/Luban/content_visual_xlsx_io.py";

        public static string ResolveAbsolutePath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), RelativeXlsxPath);
        }

        public static bool CanWrite(string absolutePath, out string error)
        {
            error = null;
            if (!File.Exists(absolutePath))
            {
                error = "找不到权威表：" + absolutePath + "（Luban/xlsx 已退休，请改一卡一文件 JSON）";
                return false;
            }

            var attributes = File.GetAttributes(absolutePath);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                error = "xlsx 为只读，请关闭 Excel 或取消只读后重试。";
                return false;
            }

            try
            {
                using (var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                }
            }
            catch (IOException)
            {
                error = "xlsx 被占用（可能已在 Excel 中打开），请关闭后重试。";
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                error = "无权限写入 xlsx：" + absolutePath;
                return false;
            }

            return true;
        }

        public static List<ContentVisualXlsxRow> ReadAll(string absolutePath)
        {
            if (!File.Exists(absolutePath)
                || !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), RelativeIoScriptPath)))
            {
                return new List<ContentVisualXlsxRow>();
            }

            var json = RunPython("read", absolutePath, null);
            return ParseRows(json);
        }

        public static (string varRow, string typeRow) ReadHeaderRows(string absolutePath)
        {
            if (!File.Exists(absolutePath)
                || !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), RelativeIoScriptPath)))
            {
                return (string.Empty, string.Empty);
            }

            var json = RunPython("headers", absolutePath, null);
            var header = JsonUtility.FromJson<HeaderDto>(json);
            if (header == null)
            {
                return (string.Empty, string.Empty);
            }

            return (header.var_row ?? string.Empty, header.type_row ?? string.Empty);
        }

        public static void PatchDescriptions(string absolutePath, IReadOnlyList<ContentVisualXlsxRow> patches)
        {
            var payload = BuildDescriptionPatchJson(patches);
            RunPython("patch_descriptions", absolutePath, payload);
        }

        private static List<ContentVisualXlsxRow> ParseRows(string json)
        {
            var rows = new List<ContentVisualXlsxRow>();
            var list = JsonUtility.FromJson<RowListWrapper>("{\"items\":" + json + "}");
            if (list?.items == null)
            {
                return rows;
            }

            for (var i = 0; i < list.items.Length; i++)
            {
                var node = list.items[i];
                if (node == null)
                {
                    continue;
                }

                rows.Add(new ContentVisualXlsxRow
                {
                    ContentId = node.content_id,
                    ContentKind = node.content_kind,
                    Description = node.description,
                    SheetRowIndex = node.sheet_row_index
                });
            }

            return rows;
        }

        private static string BuildDescriptionPatchJson(IReadOnlyList<ContentVisualXlsxRow> patches)
        {
            var builder = new StringBuilder();
            builder.Append('[');
            for (var i = 0; i < patches.Count; i++)
            {
                var patch = patches[i];
                if (patch == null || string.IsNullOrEmpty(patch.ContentId))
                {
                    continue;
                }

                if (builder.Length > 1)
                {
                    builder.Append(',');
                }

                builder.Append('{');
                AppendJsonString(builder, "content_id", patch.ContentId);
                builder.Append(',');
                AppendJsonString(builder, "description", patch.Description ?? string.Empty);
                builder.Append(',');
                builder.Append("\"sheet_row_index\":").Append(patch.SheetRowIndex);
                builder.Append('}');
            }

            builder.Append(']');
            return builder.ToString();
        }

        private static void AppendJsonString(StringBuilder builder, string key, string value)
        {
            builder.Append('\"').Append(key).Append("\":\"").Append(EscapeJson(value)).Append('\"');
        }

        private static string EscapeJson(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        private static string RunPython(string mode, string absolutePath, string patchesJson)
        {
            return RunPythonScript(RelativeIoScriptPath, mode, absolutePath, patchesJson, "--patches");
        }

        public static string RunPythonScript(
            string relativeScriptPath,
            string mode,
            string absolutePath,
            string payloadJson,
            string payloadFlag)
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var scriptPath = Path.Combine(projectRoot, relativeScriptPath);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("xlsx io script not found（Luban 已退休）。", scriptPath);
            }

            var pythonExe = ResolvePythonExecutable(projectRoot);
            var arguments = new StringBuilder();
            arguments.Append('"').Append(scriptPath).Append("\" ");
            arguments.Append(mode).Append(' ');
            arguments.Append("--path \"").Append(absolutePath).Append('\"');
            if (!string.IsNullOrEmpty(payloadJson))
            {
                arguments.Append(' ').Append(payloadFlag).Append(" \"").Append(EscapeCommandLine(payloadJson)).Append('\"');
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = arguments.ToString(),
                WorkingDirectory = Path.GetDirectoryName(scriptPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start Python for xlsx IO.");
                }

                var stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrEmpty(stderr) ? stdout : stderr);
                }

                return stdout.Trim();
            }
        }

        private static string ResolvePythonExecutable(string projectRoot)
        {
            var venvPython = Path.Combine(projectRoot, "Assets/Tools/Luban/.venv/Scripts/python.exe");
            if (File.Exists(venvPython))
            {
                return venvPython;
            }

            return "python";
        }

        private static string EscapeCommandLine(string value)
        {
            return (value ?? string.Empty).Replace("\"", "\\\"");
        }

        [Serializable]
        private sealed class RowDto
        {
            public string content_id;
            public string content_kind;
            public string description;
            public int sheet_row_index;
        }

        [Serializable]
        private sealed class RowListWrapper
        {
            public RowDto[] items;
        }

        [Serializable]
        private sealed class HeaderDto
        {
            public string var_row;
            public string type_row;
        }
    }
}
