using System;
using System.Collections.Generic;
using System.IO;
using NineGrid.Core.Localization;
using UnityEngine;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌表现 JSON 目录缓存：Editor 优先 Authoring；运行时读 StreamingAssets。
    /// 本地化覆盖缝（ADR-0046）：<see cref="TryGet"/> 是卡面文本唯一读口——非源语言时按
    /// contentId 从 <see cref="LocalizationCatalog"/> cards 表覆盖 displayName/description/faceIntro
    /// （返回覆盖副本，不污染中文原 DTO；缺字段回退中文），覆盖发生在令牌投影之前。
    /// </summary>
    public static class CardPresentationConfigCatalog
    {
        private static readonly Dictionary<string, CardPresentationConfigDto> ByContentId =
            new Dictionary<string, CardPresentationConfigDto>(StringComparer.Ordinal);

        private static readonly Dictionary<string, CardPresentationConfigDto> LocalizedOverlays =
            new Dictionary<string, CardPresentationConfigDto>(StringComparer.Ordinal);

        private static bool _loaded;
        private static int _overlayTablesVersion;

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
            if (!ByContentId.TryGetValue(contentId, out dto) || dto == null)
            {
                return false;
            }

            dto = ApplyLocalizationOverlay(contentId, dto);
            return true;
        }

        public static void Invalidate()
        {
            ByContentId.Clear();
            LocalizedOverlays.Clear();
            _loaded = false;
        }

        /// <summary>
        /// 非源语言时返回文本覆盖副本（浅拷贝 + 三个文本字段替换）；zh / 无翻译条目时原样返回。
        /// 覆盖副本按 contentId 缓存，语言切换 / 表重载（TablesVersion 变化）与 Invalidate 时失效。
        /// </summary>
        private static CardPresentationConfigDto ApplyLocalizationOverlay(
            string contentId,
            CardPresentationConfigDto source)
        {
            if (LocalizationCatalog.IsSourceLanguage)
            {
                return source;
            }

            if (_overlayTablesVersion != LocalizationCatalog.TablesVersion)
            {
                LocalizedOverlays.Clear();
                _overlayTablesVersion = LocalizationCatalog.TablesVersion;
            }

            if (LocalizedOverlays.TryGetValue(contentId, out var cached) && cached != null)
            {
                return cached;
            }

            if (!LocalizationCatalog.TryGetCardText(contentId, out var text))
            {
                LocalizedOverlays[contentId] = source;
                return source;
            }

            var overlay = CloneForTextOverlay(source);
            if (!string.IsNullOrWhiteSpace(text.DisplayName))
            {
                overlay.displayName = text.DisplayName;
            }

            if (!string.IsNullOrWhiteSpace(text.Description))
            {
                overlay.description = text.Description;
            }

            if (!string.IsNullOrWhiteSpace(text.FaceIntro))
            {
                overlay.faceIntro = text.FaceIntro;
            }

            LocalizedOverlays[contentId] = overlay;
            return overlay;
        }

        private static CardPresentationConfigDto CloneForTextOverlay(CardPresentationConfigDto source)
        {
            // 浅拷贝：非文本字段（sprites/effectAssemblies/…）与中文 DTO 共享引用，只有文本被替换。
            return source.ShallowClone();
        }

        public static void Reload()
        {
            Invalidate();
            EnsureLoaded();
        }

        /// <summary>
        /// EditMode / 单测注入：不经磁盘加载，写入内存目录（TearDown 须 <see cref="Invalidate"/>）。
        /// </summary>
        public static void UpsertForTests(CardPresentationConfigDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.contentId))
            {
                return;
            }

            _loaded = true;
            ByContentId[dto.contentId] = dto;
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
