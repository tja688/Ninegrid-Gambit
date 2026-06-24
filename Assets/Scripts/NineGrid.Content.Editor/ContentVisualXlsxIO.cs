using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Luban.SimpleJSON;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public sealed class ContentVisualXlsxRow
    {
        public string ContentId { get; set; }
        public string ContentKind { get; set; }
        public string Description { get; set; }
        public string FaceKey { get; set; }
        public string FrameKey { get; set; }
        public string IconKey { get; set; }
        public int SheetRowIndex { get; set; } = -1;
    }

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
                error = "找不到权威表：" + absolutePath;
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
            var json = RunPython("read", absolutePath, null);
            return ParseRows(json);
        }

        public static void PatchVisualKeys(string absolutePath, IReadOnlyList<ContentVisualXlsxRow> patches)
        {
            string error;
            if (!CanWrite(absolutePath, out error))
            {
                throw new IOException(error);
            }

            var payload = BuildPatchJson(patches);
            RunPython("patch", absolutePath, payload);
        }

        public static (string varRow, string typeRow) ReadHeaderRows(string absolutePath)
        {
            var json = RunPython("headers", absolutePath, null);
            var node = JSON.Parse(json);
            return (node["var_row"].Value, node["type_row"].Value);
        }

        private static List<ContentVisualXlsxRow> ParseRows(string json)
        {
            var rows = new List<ContentVisualXlsxRow>();
            var array = JSON.Parse(json).AsArray;
            for (var i = 0; i < array.Count; i++)
            {
                var node = array[i];
                rows.Add(new ContentVisualXlsxRow
                {
                    ContentId = node["content_id"].Value,
                    ContentKind = node["content_kind"].Value,
                    Description = node["description"].Value,
                    FaceKey = node["face_key"].Value,
                    FrameKey = node["frame_key"].Value,
                    IconKey = node["icon_key"].Value,
                    SheetRowIndex = node["sheet_row_index"].AsInt
                });
            }

            return rows;
        }

        private static string BuildPatchJson(IReadOnlyList<ContentVisualXlsxRow> patches)
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
                builder.Append("\"sheet_row_index\":").Append(patch.SheetRowIndex).Append(',');
                AppendJsonString(builder, "face_key", patch.FaceKey ?? string.Empty);
                builder.Append(',');
                AppendJsonString(builder, "frame_key", patch.FrameKey ?? string.Empty);
                builder.Append(',');
                AppendJsonString(builder, "icon_key", patch.IconKey ?? string.Empty);
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
            var projectRoot = Directory.GetCurrentDirectory();
            var scriptPath = Path.Combine(projectRoot, RelativeIoScriptPath);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("content_visual xlsx io script not found.", scriptPath);
            }

            var pythonExe = ResolvePythonExecutable(projectRoot);
            var arguments = new StringBuilder();
            arguments.Append('"').Append(scriptPath).Append("\" ");
            arguments.Append(mode).Append(' ');
            arguments.Append("--path \"").Append(absolutePath).Append('\"');
            if (!string.IsNullOrEmpty(patchesJson))
            {
                arguments.Append(" --patches \"").Append(EscapeCommandLine(patchesJson)).Append('\"');
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
    }
}
