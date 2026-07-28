#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NineGrid.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 效果模板表编辑读写（Authoring Arts + StreamingAssets 同步）。本轮只改元数据，body 保持原样。
    /// </summary>
    public static class EffectTemplateEditorIO
    {
        [Serializable]
        public sealed class TemplateRow
        {
            public string id = string.Empty;
            public string state = string.Empty;
            public string design_text = string.Empty;
            public string requires_json = "[]";
            public string conditions_json = "[]";
            public string body = string.Empty;

            public string SavedFingerprint { get; set; } = string.Empty;

            public bool IsDirty =>
                !string.Equals(ComputeFingerprint(), SavedFingerprint ?? string.Empty, StringComparison.Ordinal);

            public void MarkSaved()
            {
                SavedFingerprint = ComputeFingerprint();
            }

            public string ComputeFingerprint()
            {
                return string.Join("|",
                    id ?? string.Empty,
                    state ?? string.Empty,
                    design_text ?? string.Empty,
                    requires_json ?? string.Empty,
                    conditions_json ?? string.Empty,
                    body ?? string.Empty);
            }
        }

        [Serializable]
        private sealed class JsonArrayWrapper
        {
            public TemplateRow[] items;
        }

        public static string GetAuthoringAbsolutePath()
        {
            return Path.Combine(
                Application.dataPath,
                ContentCatalogTableLoader.AuthoringRelativeFolder.Replace('/', Path.DirectorySeparatorChar),
                EffectTemplateCatalog.FileName);
        }

        public static string GetStreamingAbsolutePath()
        {
            return Path.Combine(
                Application.streamingAssetsPath,
                "ContentVisual",
                "tables",
                EffectTemplateCatalog.FileName);
        }

        public static List<TemplateRow> LoadAll(out string error)
        {
            error = null;
            var path = GetAuthoringAbsolutePath();
            if (!File.Exists(path))
            {
                path = GetStreamingAbsolutePath();
            }

            if (!File.Exists(path))
            {
                error = "找不到 " + EffectTemplateCatalog.FileName;
                return new List<TemplateRow>();
            }

            try
            {
                var raw = File.ReadAllText(path);
                var wrapped = "{\"items\":" + raw + "}";
                var list = JsonUtility.FromJson<JsonArrayWrapper>(wrapped);
                var result = new List<TemplateRow>();
                if (list?.items == null)
                {
                    return result;
                }

                for (var i = 0; i < list.items.Length; i++)
                {
                    var row = list.items[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.id))
                    {
                        continue;
                    }

                    row.id = row.id.Trim();
                    row.state = row.state ?? string.Empty;
                    row.design_text = row.design_text ?? string.Empty;
                    row.requires_json = string.IsNullOrWhiteSpace(row.requires_json) ? "[]" : row.requires_json;
                    row.conditions_json = string.IsNullOrWhiteSpace(row.conditions_json)
                        ? "[]"
                        : row.conditions_json;
                    row.body = row.body ?? string.Empty;
                    row.MarkSaved();
                    result.Add(row);
                }

                result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
                return result;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return new List<TemplateRow>();
            }
        }

        public static bool TrySaveAll(IReadOnlyList<TemplateRow> rows, out string error)
        {
            error = null;
            if (rows == null)
            {
                error = "rows is null";
                return false;
            }

            try
            {
                var sb = new StringBuilder(rows.Count * 256);
                sb.AppendLine("[");
                for (var i = 0; i < rows.Count; i++)
                {
                    var row = rows[i];
                    if (row == null || string.IsNullOrWhiteSpace(row.id))
                    {
                        continue;
                    }

                    var json = JsonUtility.ToJson(row, true);
                    var indented = IndentBlock(json, "    ");
                    sb.Append(indented);
                    if (i < rows.Count - 1)
                    {
                        sb.Append(',');
                    }

                    sb.AppendLine();
                }

                sb.Append(']');
                var text = sb.ToString();
                var utf8 = new UTF8Encoding(false);
                var authoring = GetAuthoringAbsolutePath();
                var streaming = GetStreamingAbsolutePath();
                Directory.CreateDirectory(Path.GetDirectoryName(authoring) ?? authoring);
                Directory.CreateDirectory(Path.GetDirectoryName(streaming) ?? streaming);
                File.WriteAllText(authoring, text, utf8);
                File.WriteAllText(streaming, text, utf8);

                for (var i = 0; i < rows.Count; i++)
                {
                    rows[i]?.MarkSaved();
                }

                EffectTemplateCatalog.Invalidate();
                AssetDatabase.ImportAsset(
                    "Assets/Arts/ContentVisual/tables/" + EffectTemplateCatalog.FileName);
                AssetDatabase.ImportAsset(
                    "Assets/StreamingAssets/ContentVisual/tables/" + EffectTemplateCatalog.FileName);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string IndentBlock(string json, string indent)
        {
            if (string.IsNullOrEmpty(json))
            {
                return indent + "{}";
            }

            var lines = json.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder(json.Length + lines.Length * indent.Length);
            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                {
                    sb.AppendLine();
                }

                sb.Append(indent);
                sb.Append(lines[i]);
            }

            return sb.ToString();
        }
    }
}
#endif
