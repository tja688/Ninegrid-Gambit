#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NineGrid.Content.CardPresentation;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 主要内容卡牌描述字段批量导出/导入（垂直测试工具）。
    /// 范围：怪物 / 遗物 / 道具 / 机关，排除归档卡组与储备怪。
    /// </summary>
    public static class CardDescriptionBulkIO
    {
        public const string Schema = "table-nine.card-descriptions.v1";
        public const string ExportFolderAsset = "Assets/Notes/exports/card-descriptions";

        [Serializable]
        private sealed class BulkFileDto
        {
            public string schema;
            public string exportedAt;
            public string[] fields;
            public BulkEntryDto[] entries;
        }

        [Serializable]
        private sealed class BulkEntryDto
        {
            public string contentId;
            public string deckId;
            public string deckDisplayName;
            public string displayName;
            public string kind;
            public string faceIntro;
            public string description;
        }

        [MenuItem("NineGrid/Content/导出卡面描述（测试）")]
        public static void ExportMenu()
        {
            var message = TryExport(out var path);
            if (path != null)
            {
                EditorUtility.DisplayDialog("导出卡面描述", message, "确定");
                EditorUtility.RevealInFinder(path);
            }
            else
            {
                EditorUtility.DisplayDialog("导出卡面描述", message, "确定");
            }
        }

        [MenuItem("NineGrid/Content/导入卡面描述（测试）")]
        public static void ImportMenu()
        {
            var defaultDir = Path.GetFullPath(ExportFolderAsset);
            if (!Directory.Exists(defaultDir))
            {
                defaultDir = Application.dataPath;
            }

            var path = EditorUtility.OpenFilePanel(
                "选择卡面描述 JSON",
                defaultDir,
                "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var message = TryImport(path);
            EditorUtility.DisplayDialog("导入卡面描述", message, "确定");
        }

        public static string TryExport(out string absolutePath)
        {
            absolutePath = null;
            try
            {
                var deckNames = LoadDeckDisplayNames();
                var entries = CollectWiredEntries(deckNames);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var folderAbs = Path.GetFullPath(ExportFolderAsset);
                if (!Directory.Exists(folderAbs))
                {
                    Directory.CreateDirectory(folderAbs);
                }

                absolutePath = Path.Combine(folderAbs, "card-descriptions-" + stamp + ".json");
                var dto = new BulkFileDto
                {
                    schema = Schema,
                    exportedAt = DateTime.Now.ToString("o"),
                    fields = new[]
                    {
                        "displayName",
                        "kind",
                        "faceIntro",
                        "description",
                    },
                    entries = entries,
                };

                var json = JsonUtility.ToJson(dto, true);
                File.WriteAllText(absolutePath, json, new UTF8Encoding(false));
                AssetDatabase.Refresh();
                return "已导出 " + entries.Length + " 张卡 → " + absolutePath;
            }
            catch (Exception ex)
            {
                return "导出失败：" + ex.Message;
            }
        }

        public static string TryImport(string absolutePath)
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                return "文件不存在：" + absolutePath;
            }

            try
            {
                var json = File.ReadAllText(absolutePath, Encoding.UTF8);
                var bulk = JsonUtility.FromJson<BulkFileDto>(json);
                if (bulk?.entries == null || bulk.entries.Length == 0)
                {
                    return "JSON 无 entries。";
                }

                if (!string.Equals(bulk.schema, Schema, StringComparison.Ordinal))
                {
                    Debug.LogWarning(
                        "[CardDescriptionBulkIO] schema 不匹配："
                        + bulk.schema
                        + "（期望 "
                        + Schema
                        + "），仍尝试导入。");
                }

                var updated = 0;
                var skipped = 0;
                var errors = new List<string>();
                for (var i = 0; i < bulk.entries.Length; i++)
                {
                    var entry = bulk.entries[i];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.contentId))
                    {
                        skipped++;
                        continue;
                    }

                    var contentId = entry.contentId.Trim();
                    var authoringAbs = CardPresentationJsonIO.GetAuthoringAbsolutePath(contentId);
                    if (!CardPresentationJsonIO.TryLoad(authoringAbs, out var dto, out var loadError))
                    {
                        errors.Add(contentId + ": " + loadError);
                        continue;
                    }

                    dto.displayName = entry.displayName ?? string.Empty;
                    dto.faceIntro = entry.faceIntro ?? string.Empty;
                    dto.description = entry.description ?? string.Empty;
                    CardPresentationJsonIO.SaveAuthoring(dto);
                    updated++;
                }

                AssetDatabase.SaveAssets();
                var sb = new StringBuilder();
                sb.Append("已回填 ").Append(updated).Append(" 张");
                if (skipped > 0)
                {
                    sb.Append("；跳过 ").Append(skipped).Append(" 条空主键");
                }

                if (errors.Count > 0)
                {
                    sb.AppendLine();
                    sb.Append("失败 ").Append(errors.Count).Append(" 条：");
                    for (var e = 0; e < errors.Count && e < 5; e++)
                    {
                        sb.AppendLine().Append(errors[e]);
                    }

                    if (errors.Count > 5)
                    {
                        sb.AppendLine().Append("…");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return "导入失败：" + ex.Message;
            }
        }

        private static BulkEntryDto[] CollectWiredEntries(Dictionary<string, string> deckNames)
        {
            var list = new List<BulkEntryDto>();
            var folder = CardPresentationIndexIO.GetAuthoringFolderAbsolute();
            if (!Directory.Exists(folder))
            {
                return Array.Empty<BulkEntryDto>();
            }

            var files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < files.Length; i++)
            {
                var name = Path.GetFileName(files[i]);
                if (string.Equals(name, CardPresentationJsonIO.IndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out _))
                {
                    continue;
                }

                if (!CardPresentationPrimaryCardRules.IsWiredPrimaryCard(dto, deckNames))
                {
                    continue;
                }

                var deckId = dto.deckId?.Trim() ?? string.Empty;
                list.Add(new BulkEntryDto
                {
                    contentId = dto.contentId ?? string.Empty,
                    deckId = deckId,
                    deckDisplayName = ResolveDeckDisplayName(deckId, deckNames),
                    displayName = dto.displayName ?? string.Empty,
                    kind = dto.kind ?? string.Empty,
                    faceIntro = dto.faceIntro ?? string.Empty,
                    description = dto.description ?? string.Empty,
                });
            }

            list.Sort((a, b) => string.CompareOrdinal(a.contentId, b.contentId));
            return list.ToArray();
        }

        private static Dictionary<string, string> LoadDeckDisplayNames()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            var folder = CardPresentationIndexIO.GetAuthoringFolderAbsolute();
            if (!Directory.Exists(folder))
            {
                return map;
            }

            var files = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < files.Length; i++)
            {
                if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out _))
                {
                    continue;
                }

                if (!string.Equals(dto.kind, "Deck", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(dto.contentId))
                {
                    continue;
                }

                map[dto.contentId.Trim()] = dto.displayName?.Trim() ?? string.Empty;
            }

            return map;
        }

        private static string ResolveDeckDisplayName(string deckId, Dictionary<string, string> deckNames)
        {
            if (string.IsNullOrWhiteSpace(deckId))
            {
                return string.Empty;
            }

            return deckNames.TryGetValue(deckId.Trim(), out var name) ? name : deckId.Trim();
        }
    }
}
#endif
