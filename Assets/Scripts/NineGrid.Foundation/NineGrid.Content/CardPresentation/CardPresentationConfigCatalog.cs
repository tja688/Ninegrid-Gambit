using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌表现 JSON 目录缓存：Editor 优先 Authoring；运行时读 StreamingAssets。
    /// </summary>
    public static class CardPresentationConfigCatalog
    {
        private static readonly Dictionary<string, CardPresentationConfigDto> ByContentId =
            new Dictionary<string, CardPresentationConfigDto>(StringComparer.Ordinal);

        private static bool _loaded;

        public static IEnumerable<string> AllContentIds
        {
            get
            {
                EnsureLoaded();
                return ByContentId.Keys;
            }
        }

        public static bool TryGet(string contentId, out CardPresentationConfigDto dto)
        {
            dto = null;
            if (string.IsNullOrWhiteSpace(contentId))
            {
                return false;
            }

            EnsureLoaded();
            return ByContentId.TryGetValue(contentId, out dto) && dto != null;
        }

        public static void Invalidate()
        {
            ByContentId.Clear();
            _loaded = false;
        }

        public static void Reload()
        {
            Invalidate();
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            ByContentId.Clear();

            var folders = ResolveLoadFolders();
            for (var i = 0; i < folders.Count; i++)
            {
                LoadFolder(folders[i], overwriteExisting: i == 0);
            }
        }

        private static List<string> ResolveLoadFolders()
        {
            var folders = new List<string>(2);

#if UNITY_EDITOR
            // Editor / Play Mode in Editor：优先 Authoring，再补 Streaming 缺失项。
            var authoringAbs = Path.Combine(
                Application.dataPath,
                "Arts",
                "ContentVisual",
                "cards");
            if (Directory.Exists(authoringAbs))
            {
                folders.Add(authoringAbs);
            }
#endif

            var streamingAbs = Path.Combine(
                Application.streamingAssetsPath,
                "ContentVisual",
                "cards");
            if (Directory.Exists(streamingAbs))
            {
                folders.Add(streamingAbs);
            }

            return folders;
        }

        private static void LoadFolder(string absoluteFolder, bool overwriteExisting)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(absoluteFolder, "*.json", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex)
            {
                Debug.LogWarning(
                    "[CardPresentationConfigCatalog] Failed to list " + absoluteFolder + ": " + ex.Message);
                return;
            }

            for (var i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var name = Path.GetFileName(file);
                if (string.Equals(name, CardPresentationJsonIO.IndexFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!CardPresentationJsonIO.TryLoad(file, out var dto, out var error))
                {
                    Debug.LogWarning(
                        "[CardPresentationConfigCatalog] Skip " + file + ": " + error);
                    continue;
                }

                var key = string.IsNullOrEmpty(dto.contentId)
                    ? Path.GetFileNameWithoutExtension(file).Replace('_', '.')
                    : dto.contentId;
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (!overwriteExisting && ByContentId.ContainsKey(key))
                {
                    continue;
                }

                ByContentId[key] = dto;
            }
        }
    }
}
