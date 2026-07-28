using System;
using System.IO;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌表现 JSON 读写：Authoring（Arts）为编辑权威，并同步 StreamingAssets 运行时副本。
    /// </summary>
    public static class CardPresentationJsonIO
    {
        public const string AuthoringFolder = "Assets/Arts/ContentVisual/cards";
        public const string StreamingFolder = "Assets/StreamingAssets/ContentVisual/cards";
        public const string IndexFileName = "_index.json";

        public static string ToSafeFileName(string contentId)
        {
            if (string.IsNullOrEmpty(contentId))
            {
                return string.Empty;
            }

            return contentId.Replace('.', '_');
        }

        public static string GetAuthoringPath(string contentId)
        {
            var safe = ToSafeFileName(contentId);
            if (string.IsNullOrEmpty(safe))
            {
                return AuthoringFolder + "/";
            }

            return AuthoringFolder + "/" + safe + ".json";
        }

        public static string GetStreamingPath(string contentId)
        {
            var safe = ToSafeFileName(contentId);
            if (string.IsNullOrEmpty(safe))
            {
                return StreamingFolder + "/";
            }

            return StreamingFolder + "/" + safe + ".json";
        }

        public static string GetAuthoringAbsolutePath(string contentId)
        {
            return ToAbsoluteUnderProject(GetAuthoringPath(contentId));
        }

        public static string GetStreamingAbsolutePath(string contentId)
        {
            return ToAbsoluteUnderProject(GetStreamingPath(contentId));
        }

        public static bool TryLoad(
            string absoluteOrAssetPath,
            out CardPresentationConfigDto dto,
            out string error)
        {
            dto = null;
            error = null;
            if (string.IsNullOrWhiteSpace(absoluteOrAssetPath))
            {
                error = "Path is empty.";
                return false;
            }

            string absolute;
            try
            {
                absolute = ResolveAbsolutePath(absoluteOrAssetPath);
            }
            catch (Exception ex)
            {
                error = "Failed to resolve path: " + ex.Message;
                return false;
            }

            if (!File.Exists(absolute))
            {
                error = "File not found: " + absolute;
                return false;
            }

            string json;
            try
            {
                json = File.ReadAllText(absolute, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                error = "Read failed: " + ex.Message;
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON is empty.";
                return false;
            }

            try
            {
                dto = JsonUtility.FromJson<CardPresentationConfigDto>(json);
            }
            catch (Exception ex)
            {
                error = "JsonUtility.FromJson failed: " + ex.Message;
                dto = null;
                return false;
            }

            if (dto == null)
            {
                error = "JsonUtility returned null.";
                return false;
            }

            EnsureNestedDefaults(dto);
            return true;
        }

        public static string ToJson(CardPresentationConfigDto dto)
        {
            if (dto == null)
            {
                return "{}";
            }

            EnsureNestedDefaults(dto);
            return JsonUtility.ToJson(dto, true);
        }

        public static void SaveAuthoring(CardPresentationConfigDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }

            if (string.IsNullOrWhiteSpace(dto.contentId))
            {
                throw new ArgumentException("contentId is required.", nameof(dto));
            }

            EnsureNestedDefaults(dto);
            EnsureDirectoriesExist();

            var json = ToJson(dto);
            var authoringAbs = GetAuthoringAbsolutePath(dto.contentId);
            var streamingAbs = GetStreamingAbsolutePath(dto.contentId);

            File.WriteAllText(authoringAbs, json, new UTF8Encoding(false));
            File.WriteAllText(streamingAbs, json, new UTF8Encoding(false));

#if UNITY_EDITOR
            AssetDatabase.ImportAsset(GetAuthoringPath(dto.contentId));
            AssetDatabase.ImportAsset(GetStreamingPath(dto.contentId));
#endif
        }

        public static CardPresentationConfigDto CreateDefault(string contentId, string kind)
        {
            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 1,
                contentId = contentId ?? string.Empty,
                kind = kind ?? string.Empty,
                deckId = string.Empty,
                displayName = string.Empty,
                description = string.Empty,
                gold = 0,
                stats = new CardPresentationStatsDto(),
                sprites = new CardPresentationSpritesDto(),
                mainVisual = new CardPresentationMainVisualDto
                {
                    offsetX = 0f,
                    offsetY = 0f,
                    uniformScale = 1f,
                },
                animations = new CardPresentationAnimationsDto
                {
                    defaultFps = 8f,
                    slots = CreateDefaultAnimSlots(),
                },
                extraSlots = Array.Empty<CardPresentationExtraSlotDto>(),
            };
            return dto;
        }

        public static void EnsureDirectoriesExist()
        {
            EnsureDirectoryForAssetFolder(AuthoringFolder);
            EnsureDirectoryForAssetFolder(StreamingFolder);
        }

        private static CardPresentationAnimSlotDto[] CreateDefaultAnimSlots()
        {
            var all = CardAnimSlotIds.All;
            var slots = new CardPresentationAnimSlotDto[all.Length];
            for (var i = 0; i < all.Length; i++)
            {
                slots[i] = new CardPresentationAnimSlotDto
                {
                    id = all[i],
                    sourceType = "none",
                    path = string.Empty,
                    offsetX = 0f,
                    offsetY = 0f,
                };
            }

            return slots;
        }

        private static void EnsureNestedDefaults(CardPresentationConfigDto dto)
        {
            if (dto.stats == null)
            {
                dto.stats = new CardPresentationStatsDto();
            }

            if (dto.sprites == null)
            {
                dto.sprites = new CardPresentationSpritesDto();
            }

            if (dto.mainVisual == null)
            {
                dto.mainVisual = new CardPresentationMainVisualDto { uniformScale = 1f };
            }

            if (dto.animations == null)
            {
                dto.animations = new CardPresentationAnimationsDto
                {
                    defaultFps = 8f,
                    slots = CreateDefaultAnimSlots(),
                };
            }
            else if (dto.animations.slots == null)
            {
                dto.animations.slots = CreateDefaultAnimSlots();
            }

            if (dto.extraSlots == null)
            {
                dto.extraSlots = Array.Empty<CardPresentationExtraSlotDto>();
            }

            if (dto.tags == null)
            {
                dto.tags = Array.Empty<string>();
            }

            if (dto.effectAssemblies == null)
            {
                dto.effectAssemblies = Array.Empty<EffectAssemblyDto>();
            }

            if (dto.effectIds == null)
            {
                dto.effectIds = Array.Empty<string>();
            }

            if (dto.skillIds == null)
            {
                dto.skillIds = Array.Empty<string>();
            }

            if (dto.monsterDefIds == null)
            {
                dto.monsterDefIds = Array.Empty<string>();
            }

            if (dto.rarity == null)
            {
                dto.rarity = string.Empty;
            }

            if (dto.containerType == null)
            {
                dto.containerType = string.Empty;
            }

            if (dto.deckKind == null)
            {
                dto.deckKind = string.Empty;
            }

            if (dto.rewardPoolId == null)
            {
                dto.rewardPoolId = string.Empty;
            }

            if (dto.contentId == null)
            {
                dto.contentId = string.Empty;
            }

            if (dto.kind == null)
            {
                dto.kind = string.Empty;
            }
        }

        private static void EnsureDirectoryForAssetFolder(string assetFolder)
        {
            var absolute = ToAbsoluteUnderProject(assetFolder);
            if (!Directory.Exists(absolute))
            {
                Directory.CreateDirectory(absolute);
            }
        }

        private static string ResolveAbsolutePath(string absoluteOrAssetPath)
        {
            var normalized = absoluteOrAssetPath.Replace('\\', '/').Trim();
            if (Path.IsPathRooted(absoluteOrAssetPath)
                && !normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFullPath(absoluteOrAssetPath);
            }

            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Assets\\", StringComparison.OrdinalIgnoreCase))
            {
                return ToAbsoluteUnderProject(normalized);
            }

            return Path.GetFullPath(absoluteOrAssetPath);
        }

        private static string ToAbsoluteUnderProject(string assetRelativePath)
        {
            var normalized = (assetRelativePath ?? string.Empty).Replace('\\', '/');
            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.Substring("Assets/".Length);
            }

            var combined = Path.Combine(Application.dataPath, normalized.Replace('/', Path.DirectorySeparatorChar));
            return Path.GetFullPath(combined);
        }
    }
}
