using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Luban.SimpleJSON;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public sealed class VisualAssetXlsxRow
    {
        public string VisualId { get; set; }
        public string Kind { get; set; } = "sprite";
        public string AssetKey { get; set; }
        public string FallbackId { get; set; }
        public int SheetRowIndex { get; set; } = -1;
    }

    public static class VisualAssetXlsxIO
    {
        public const string RelativeXlsxPath = "Assets/Tools/Luban/Datas/visual_asset.xlsx";
        public const string RelativeIoScriptPath = "Assets/Tools/Luban/visual_asset_xlsx_io.py";

        public static string ResolveAbsolutePath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), RelativeXlsxPath);
        }

        public static List<VisualAssetXlsxRow> ReadAll(string absolutePath)
        {
            var json = RunPython("read", absolutePath, null);
            return ParseRows(json);
        }

        public static void UpsertRows(string absolutePath, IReadOnlyList<VisualAssetXlsxRow> upserts)
        {
            var payload = BuildUpsertJson(upserts);
            RunPython("upsert", absolutePath, payload);
        }

        private static List<VisualAssetXlsxRow> ParseRows(string json)
        {
            var rows = new List<VisualAssetXlsxRow>();
            var array = JSON.Parse(json).AsArray;
            for (var i = 0; i < array.Count; i++)
            {
                var node = array[i];
                rows.Add(new VisualAssetXlsxRow
                {
                    VisualId = node["visual_id"].Value,
                    Kind = node["kind"].Value,
                    AssetKey = node["asset_key"].Value,
                    FallbackId = node["fallback_id"].Value,
                    SheetRowIndex = node["sheet_row_index"].AsInt
                });
            }

            return rows;
        }

        private static string BuildUpsertJson(IReadOnlyList<VisualAssetXlsxRow> upserts)
        {
            var builder = new StringBuilder();
            builder.Append('[');
            for (var i = 0; i < upserts.Count; i++)
            {
                var row = upserts[i];
                if (row == null || string.IsNullOrEmpty(row.VisualId))
                {
                    continue;
                }

                if (builder.Length > 1)
                {
                    builder.Append(',');
                }

                builder.Append('{');
                AppendJsonString(builder, "visual_id", row.VisualId);
                builder.Append(',');
                AppendJsonString(builder, "kind", row.Kind ?? "sprite");
                builder.Append(',');
                AppendJsonString(builder, "asset_key", row.AssetKey ?? string.Empty);
                builder.Append(',');
                AppendJsonString(builder, "fallback_id", row.FallbackId ?? string.Empty);
                builder.Append(',');
                builder.Append("\"sheet_row_index\":").Append(row.SheetRowIndex);
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
            return ContentVisualXlsxIO.RunPythonScript(RelativeIoScriptPath, mode, absolutePath, payloadJson, "--upserts");
        }
    }
}
