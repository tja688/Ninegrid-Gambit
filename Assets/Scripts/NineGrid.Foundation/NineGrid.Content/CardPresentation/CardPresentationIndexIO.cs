using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡面索引文件（<c>_index.json</c>）读写与一致性校验。
    /// 磁盘 JSON 是权威；索引是磁盘的完整快照（#140）：导出索引必须扫盘而非依赖 Editor 会话状态，
    /// 否则新增技能/遗物/房间选项会漂移漏抄。
    /// </summary>
    [Serializable]
    public sealed class CardPresentationIndexFileDto
    {
        public int schemaVersion = 1;
        public string[] contentIds = Array.Empty<string>();
    }

    public static class CardPresentationIndexIO
    {
        /// <summary>
        /// 扫描某目录全部卡牌 JSON 的 contentId（跳过 _index.json），去重、升序。
        /// </summary>
        public static string[] ScanContentIds(string absoluteFolder)
        {
            var distinct = new List<string>();
            foreach (var id in EnumerateContentIds(absoluteFolder))
            {
                if (!distinct.Contains(id))
                {
                    distinct.Add(id);
                }
            }

            distinct.Sort(StringComparer.Ordinal);
            return distinct.ToArray();
        }

        /// <summary>
        /// 磁盘上重复的 contentId（多文件同 id 属装配错误，索引无法表达）。升序。
        /// </summary>
        public static string[] FindDuplicateContentIds(string absoluteFolder)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in EnumerateContentIds(absoluteFolder))
            {
                if (!seen.Add(id))
                {
                    duplicates.Add(id);
                }
            }

            var result = new List<string>(duplicates);
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static IEnumerable<string> EnumerateContentIds(string absoluteFolder)
        {
            if (string.IsNullOrWhiteSpace(absoluteFolder) || !Directory.Exists(absoluteFolder))
            {
                yield break;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(absoluteFolder, "*.json", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CardPresentationIndexIO] Failed to list " + absoluteFolder + ": " + ex.Message);
                yield break;
            }

            for (var i = 0; i < files.Length; i++)
            {
                var name = Path.GetFileName(files[i]);
                if (string.Equals(name, CardPresentationJsonIO.IndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out _) || dto == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(dto.contentId))
                {
                    yield return dto.contentId;
                }
            }
        }

        public static bool TryLoadIndex(string absoluteFolder, out string[] contentIds, out string error)
        {
            contentIds = null;
            error = null;
            if (string.IsNullOrWhiteSpace(absoluteFolder))
            {
                error = "Folder is empty.";
                return false;
            }

            var path = Path.Combine(absoluteFolder, CardPresentationJsonIO.IndexFileName);
            if (!File.Exists(path))
            {
                error = "Index file missing: " + path;
                return false;
            }

            try
            {
                var dto = JsonUtility.FromJson<CardPresentationIndexFileDto>(
                    File.ReadAllText(path, Encoding.UTF8));
                contentIds = dto != null && dto.contentIds != null
                    ? dto.contentIds
                    : Array.Empty<string>();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool WriteIndex(string absoluteFolder, string[] contentIds, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(absoluteFolder);
                var dto = new CardPresentationIndexFileDto
                {
                    schemaVersion = 1,
                    contentIds = contentIds ?? Array.Empty<string>(),
                };
                File.WriteAllText(
                    Path.Combine(absoluteFolder, CardPresentationJsonIO.IndexFileName),
                    JsonUtility.ToJson(dto, true),
                    new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 校验索引与磁盘一致（#140）：索引条目 ↔ 磁盘文件双向覆盖、索引无重复、
        /// 两侧索引文件字节一致。返回空列表 = 一致。
        /// </summary>
        public static List<string> ValidateIndexVsDisk(string authoringFolder, string streamingFolder)
        {
            var issues = new List<string>();
            var duplicates = FindDuplicateContentIds(authoringFolder);
            for (var i = 0; i < duplicates.Length; i++)
            {
                issues.Add("index: duplicate contentId on disk " + duplicates[i]);
            }

            var diskIds = ScanContentIds(authoringFolder);
            var diskSet = new HashSet<string>(diskIds, StringComparer.Ordinal);

            if (!TryLoadIndex(authoringFolder, out var indexIds, out var error))
            {
                issues.Add("index: " + error);
                return issues;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < indexIds.Length; i++)
            {
                var id = indexIds[i];
                if (string.IsNullOrWhiteSpace(id))
                {
                    issues.Add("index: empty contentId at " + i);
                    continue;
                }

                if (!seen.Add(id))
                {
                    issues.Add("index: duplicate contentId " + id);
                }

                if (!diskSet.Contains(id))
                {
                    issues.Add("index: no disk file for " + id);
                }
            }

            for (var i = 0; i < diskIds.Length; i++)
            {
                if (!seen.Contains(diskIds[i]))
                {
                    issues.Add("index: missing entry for " + diskIds[i]);
                }
            }

            var streamingIndex = Path.Combine(streamingFolder, CardPresentationJsonIO.IndexFileName);
            var authoringIndex = Path.Combine(authoringFolder, CardPresentationJsonIO.IndexFileName);
            if (File.Exists(authoringIndex)
                && (!File.Exists(streamingIndex)
                    || !FileEquals(authoringIndex, streamingIndex)))
            {
                issues.Add("index: authoring/streaming index files differ");
            }

            return issues;
        }

        /// <summary>
        /// 校验 Authoring / Streaming 双写一致（#140）：文件名字集合双向相等、内容字节一致。
        /// 返回空列表 = 一致。
        /// </summary>
        public static List<string> ValidateMirror(string authoringFolder, string streamingFolder)
        {
            var issues = new List<string>();
            if (!Directory.Exists(authoringFolder))
            {
                issues.Add("mirror: authoring folder missing " + authoringFolder);
                return issues;
            }

            if (!Directory.Exists(streamingFolder))
            {
                issues.Add("mirror: streaming folder missing " + streamingFolder);
                return issues;
            }

            var authoringFiles = ListJsonFiles(authoringFolder);
            var streamingFiles = ListJsonFiles(streamingFolder);
            for (var i = 0; i < authoringFiles.Count; i++)
            {
                if (!streamingFiles.Contains(authoringFiles[i]))
                {
                    issues.Add("mirror: streaming missing " + authoringFiles[i]);
                }
            }

            for (var i = 0; i < streamingFiles.Count; i++)
            {
                if (!authoringFiles.Contains(streamingFiles[i]))
                {
                    issues.Add("mirror: authoring missing " + streamingFiles[i]);
                }
            }

            for (var i = 0; i < authoringFiles.Count; i++)
            {
                var name = authoringFiles[i];
                var a = Path.Combine(authoringFolder, name);
                var s = Path.Combine(streamingFolder, name);
                if (streamingFiles.Contains(name) && !FileEquals(a, s))
                {
                    issues.Add("mirror: content differs " + name);
                }
            }

            return issues;
        }

        public static string GetAuthoringFolderAbsolute()
        {
            return ToAbsoluteUnderProject(CardPresentationJsonIO.AuthoringFolder);
        }

        public static string GetStreamingFolderAbsolute()
        {
            return Path.Combine(Application.streamingAssetsPath, "ContentVisual", "cards");
        }

        private static List<string> ListJsonFiles(string absoluteFolder)
        {
            var names = new List<string>();
            try
            {
                var files = Directory.GetFiles(absoluteFolder, "*.json", SearchOption.TopDirectoryOnly);
                for (var i = 0; i < files.Length; i++)
                {
                    names.Add(Path.GetFileName(files[i]));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CardPresentationIndexIO] Failed to list " + absoluteFolder + ": " + ex.Message);
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private static bool FileEquals(string pathA, string pathB)
        {
            try
            {
                var bytesA = File.ReadAllBytes(pathA);
                var bytesB = File.ReadAllBytes(pathB);
                if (bytesA.Length != bytesB.Length)
                {
                    return false;
                }

                for (var i = 0; i < bytesA.Length; i++)
                {
                    if (bytesA[i] != bytesB[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string ToAbsoluteUnderProject(string assetRelativePath)
        {
            var normalized = (assetRelativePath ?? string.Empty).Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("Assets/".Length);
            }

            return Path.GetFullPath(Path.Combine(Application.dataPath, normalized.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
