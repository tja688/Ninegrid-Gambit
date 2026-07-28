using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public sealed class CardFrameStyleXlsxRow
    {
        public string StyleId { get; set; }
        public float ColorR { get; set; } = 1f;
        public float ColorG { get; set; } = 1f;
        public float ColorB { get; set; } = 1f;
        public float ColorA { get; set; } = 1f;
        public int SheetRowIndex { get; set; } = -1;
    }

    /// <summary>
    /// 旧 card_frame_style.xlsx 桥（#69 Luban 工具链已退休）。脚本缺失时 Read 返回空列表。
    /// </summary>
    public static class CardFrameStyleXlsxIO
    {
        public const string RelativeXlsxPath = "Assets/Tools/Luban/Datas/card_frame_style.xlsx";
        public const string RelativeIoScriptPath = "Assets/Tools/Luban/card_frame_style_xlsx_io.py";

        public static string ResolveAbsolutePath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), RelativeXlsxPath);
        }

        public static List<CardFrameStyleXlsxRow> ReadAll(string absolutePath)
        {
            if (!File.Exists(absolutePath)
                || !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), RelativeIoScriptPath)))
            {
                return new List<CardFrameStyleXlsxRow>();
            }

            var json = RunPython("read", absolutePath, null);
            return ParseRows(json);
        }

        public static void PatchColors(string absolutePath, IReadOnlyList<CardFrameStyleXlsxRow> patches)
        {
            var payload = BuildPatchJson(patches);
            RunPython("patch", absolutePath, payload);
        }

        private static List<CardFrameStyleXlsxRow> ParseRows(string json)
        {
            var rows = new List<CardFrameStyleXlsxRow>();
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

                rows.Add(new CardFrameStyleXlsxRow
                {
                    StyleId = node.style_id,
                    ColorR = node.color_r,
                    ColorG = node.color_g,
                    ColorB = node.color_b,
                    ColorA = node.color_a,
                    SheetRowIndex = node.sheet_row_index
                });
            }

            return rows;
        }

        private static string BuildPatchJson(IReadOnlyList<CardFrameStyleXlsxRow> patches)
        {
            var builder = new StringBuilder();
            builder.Append('[');
            for (var i = 0; i < patches.Count; i++)
            {
                var patch = patches[i];
                if (patch == null || string.IsNullOrEmpty(patch.StyleId))
                {
                    continue;
                }

                if (builder.Length > 1)
                {
                    builder.Append(',');
                }

                builder.Append('{');
                AppendJsonString(builder, "style_id", patch.StyleId);
                builder.Append(',');
                builder.Append("\"color_r\":").Append(patch.ColorR.ToString("R"));
                builder.Append(',');
                builder.Append("\"color_g\":").Append(patch.ColorG.ToString("R"));
                builder.Append(',');
                builder.Append("\"color_b\":").Append(patch.ColorB.ToString("R"));
                builder.Append(',');
                builder.Append("\"color_a\":").Append(patch.ColorA.ToString("R"));
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

        private static string RunPython(string mode, string absolutePath, string payloadJson)
        {
            return ContentVisualXlsxIO.RunPythonScript(RelativeIoScriptPath, mode, absolutePath, payloadJson, "--patches");
        }

        [Serializable]
        private sealed class RowDto
        {
            public string style_id;
            public float color_r;
            public float color_g;
            public float color_b;
            public float color_a;
            public int sheet_row_index;
        }

        [Serializable]
        private sealed class RowListWrapper
        {
            public RowDto[] items;
        }
    }
}
