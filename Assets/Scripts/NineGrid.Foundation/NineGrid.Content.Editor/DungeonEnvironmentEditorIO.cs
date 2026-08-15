#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using NineGrid.Content;
using NineGrid.Core.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>地下城虚构环境表读写（Authoring Arts + StreamingAssets 双写）。</summary>
    public static class DungeonEnvironmentEditorIO
    {
        public static string GetAuthoringAbsolutePath()
        {
            return Path.Combine(
                Application.dataPath,
                ContentCatalogTableLoader.AuthoringRelativeFolder.Replace('/', Path.DirectorySeparatorChar),
                DungeonEnvironmentCatalog.FileName);
        }

        public static string GetStreamingAbsolutePath()
        {
            return Path.Combine(
                Application.streamingAssetsPath,
                ContentCatalogTableLoader.StreamingRelativeFolder.Replace('/', Path.DirectorySeparatorChar),
                DungeonEnvironmentCatalog.FileName);
        }

        public static DungeonEnvironmentEditorDocument Load(out string error)
        {
            error = null;
            var path = GetAuthoringAbsolutePath();
            if (!File.Exists(path))
            {
                path = GetStreamingAbsolutePath();
            }

            DungeonEnvironmentTableDto table = null;
            if (File.Exists(path))
            {
                try
                {
                    table = JsonUtility.FromJson<DungeonEnvironmentTableDto>(File.ReadAllText(path));
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            }

            if (table == null || table.variants == null || table.variants.Length == 0)
            {
                table = DungeonEnvironmentCatalog.CreateDefaultTable();
            }

            if (table.roomSplit < 1)
            {
                table.roomSplit = 4;
            }

            Normalize(table);
            var document = new DungeonEnvironmentEditorDocument(table);
            document.MarkSaved();
            DungeonEnvironmentCatalog.SetTable(table);
            return document;
        }

        public static bool TrySave(DungeonEnvironmentEditorDocument document, out string error)
        {
            error = null;
            if (document?.Table == null)
            {
                error = "地下城环境表为空。";
                return false;
            }

            try
            {
                Normalize(document.Table);
                var text = JsonUtility.ToJson(document.Table, true);
                var utf8 = new UTF8Encoding(false);
                var authoring = GetAuthoringAbsolutePath();
                var streaming = GetStreamingAbsolutePath();
                Directory.CreateDirectory(Path.GetDirectoryName(authoring) ?? authoring);
                Directory.CreateDirectory(Path.GetDirectoryName(streaming) ?? streaming);
                File.WriteAllText(authoring, text, utf8);
                File.WriteAllText(streaming, text, utf8);
                document.MarkSaved();
                DungeonEnvironmentCatalog.SetTable(document.Table);
                AssetDatabase.ImportAsset("Assets/Arts/ContentVisual/tables/" + DungeonEnvironmentCatalog.FileName);
                AssetDatabase.ImportAsset(
                    "Assets/StreamingAssets/ContentVisual/tables/" + DungeonEnvironmentCatalog.FileName);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void Normalize(DungeonEnvironmentTableDto table)
        {
            if (table.variants == null)
            {
                table.variants = Array.Empty<DungeonEnvironmentVariantDto>();
                return;
            }

            for (var i = 0; i < table.variants.Length; i++)
            {
                var row = table.variants[i];
                if (row == null)
                {
                    continue;
                }

                row.id = (row.id ?? string.Empty).Trim();
                row.layerName = row.layerName ?? string.Empty;
                row.variantName = row.variantName ?? string.Empty;
                row.displayName = row.displayName ?? string.Empty;
                row.faceBackground = (row.faceBackground ?? string.Empty).Replace('\\', '/');
                row.groundPanel = (row.groundPanel ?? string.Empty).Replace('\\', '/');
                row.mainBackgroundHex = NormalizeHex(row.mainBackgroundHex);
                row.notes = row.notes ?? string.Empty;
                if (row.roomMin < 1)
                {
                    row.roomMin = 1;
                }

                if (row.roomMax < row.roomMin)
                {
                    row.roomMax = row.roomMin;
                }
            }
        }

        private static string NormalizeHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return "#241527";
            }

            hex = hex.Trim();
            if (hex[0] != '#')
            {
                hex = "#" + hex;
            }

            return hex.ToLowerInvariant();
        }
    }

    public sealed class DungeonEnvironmentEditorDocument
    {
        public DungeonEnvironmentEditorDocument(DungeonEnvironmentTableDto table)
        {
            Table = table ?? DungeonEnvironmentCatalog.CreateDefaultTable();
        }

        public DungeonEnvironmentTableDto Table { get; }

        public string SavedFingerprint { get; private set; } = string.Empty;

        public bool IsDirty =>
            !string.Equals(ComputeFingerprint(), SavedFingerprint ?? string.Empty, StringComparison.Ordinal);

        public void MarkSaved()
        {
            SavedFingerprint = ComputeFingerprint();
        }

        public string ComputeFingerprint()
        {
            return JsonUtility.ToJson(Table);
        }

        public DungeonEnvironmentVariantDto FindVariant(string id)
        {
            var variants = Table?.variants;
            if (variants == null || string.IsNullOrEmpty(id))
            {
                return null;
            }

            for (var i = 0; i < variants.Length; i++)
            {
                var row = variants[i];
                if (row != null && string.Equals(row.id, id, StringComparison.Ordinal))
                {
                    return row;
                }
            }

            return null;
        }
    }
}
#endif
