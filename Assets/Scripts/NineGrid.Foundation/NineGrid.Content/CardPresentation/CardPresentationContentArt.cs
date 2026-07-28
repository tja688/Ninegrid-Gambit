using System;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// 卡牌内容引用的美术单一 Resources 根（ADR-0008）。
    /// JSON 路径须落在 <see cref="RootAssetFolder"/> 下，Player 通过 Resources 相对键解析。
    /// </summary>
    public static class CardPresentationContentArt
    {
        public const string RootAssetFolder = "Assets/Resources/ContentArt";
        public const string ResourcesRelativeRoot = "ContentArt";
        public const string LegacyArtsImagesPrefix = "Assets/Arts/Images/";

        /// <summary>
        /// 将完整资产路径（可含 <c>#spriteName</c>）转为 Resources 相对键（无扩展名、无 #）。
        /// 仅当路径落在任一 Resources 目录下时成功。
        /// </summary>
        public static bool TryGetResourcesRelativeKey(string assetPathOrKey, out string relativeKey)
        {
            relativeKey = null;
            CardPresentationSpritePath.SplitPath(assetPathOrKey, out var path, out _);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            const string marker = "/Resources/";
            var index = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var relative = path.Substring(index + marker.Length);
            if (relative.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || relative.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                var dot = relative.LastIndexOf('.');
                relative = relative.Substring(0, dot);
            }

            relativeKey = relative;
            return !string.IsNullOrEmpty(relativeKey);
        }

        public static bool IsUnderContentArtRoot(string assetPathOrKey)
        {
            CardPresentationSpritePath.SplitPath(assetPathOrKey, out var path, out _);
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var normalized = path.Replace('\\', '/');
            return normalized.StartsWith(RootAssetFolder + "/", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalized, RootAssetFolder, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 把历史 <c>Assets/Arts/Images/...</c> 路径改写到 ContentArt 根；已在根下或非该前缀则原样返回。
        /// 保留 <c>#spriteName</c>。
        /// </summary>
        public static string RewriteLegacyArtsImagesPath(string assetPathOrKey)
        {
            if (string.IsNullOrWhiteSpace(assetPathOrKey))
            {
                return assetPathOrKey ?? string.Empty;
            }

            CardPresentationSpritePath.SplitPath(assetPathOrKey, out var path, out var spriteName);
            if (string.IsNullOrEmpty(path))
            {
                return assetPathOrKey.Trim();
            }

            var normalized = path.Replace('\\', '/');
            if (IsUnderContentArtRoot(normalized))
            {
                return CardPresentationSpritePath.ComposePath(normalized, spriteName);
            }

            if (normalized.StartsWith(LegacyArtsImagesPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var tail = normalized.Substring(LegacyArtsImagesPrefix.Length);
                var rewritten = RootAssetFolder + "/" + tail;
                return CardPresentationSpritePath.ComposePath(rewritten, spriteName);
            }

            return CardPresentationSpritePath.ComposePath(normalized, spriteName);
        }

        /// <summary>
        /// 收集一张卡 JSON 里所有非空资产路径字段（sprites + anim slots + extraSlots）。
        /// </summary>
        public static void CollectAssetPathEntries(
            CardPresentationConfigDto dto,
            System.Collections.Generic.List<CardPresentationAssetPathEntry> into)
        {
            if (dto == null || into == null)
            {
                return;
            }

            if (dto.sprites != null)
            {
                AddEntry(into, dto.contentId, "sprites.mainIcon", "sprite", dto.sprites.mainIcon);
                AddEntry(into, dto.contentId, "sprites.faceBackground", "sprite", dto.sprites.faceBackground);
                AddEntry(into, dto.contentId, "sprites.cardFrame", "sprite", dto.sprites.cardFrame);
                AddEntry(into, dto.contentId, "sprites.banner", "sprite", dto.sprites.banner);
                AddEntry(into, dto.contentId, "sprites.backBorder", "sprite", dto.sprites.backBorder);
                AddEntry(into, dto.contentId, "sprites.backShirt", "sprite", dto.sprites.backShirt);
                AddEntry(into, dto.contentId, "sprites.backLogo", "sprite", dto.sprites.backLogo);
            }

            if (dto.animations?.slots != null)
            {
                for (var i = 0; i < dto.animations.slots.Length; i++)
                {
                    var slot = dto.animations.slots[i];
                    if (slot == null)
                    {
                        continue;
                    }

                    var type = CardPresentationAnimResolve.NormalizeSourceType(slot.sourceType);
                    if (type == "none" || string.IsNullOrWhiteSpace(slot.path))
                    {
                        continue;
                    }

                    AddEntry(
                        into,
                        dto.contentId,
                        "animations.slots[" + i + "]." + (slot.id ?? string.Empty),
                        type,
                        slot.path);
                }
            }

            if (dto.extraSlots != null)
            {
                for (var i = 0; i < dto.extraSlots.Length; i++)
                {
                    var extra = dto.extraSlots[i];
                    if (extra == null || string.IsNullOrWhiteSpace(extra.path))
                    {
                        continue;
                    }

                    AddEntry(
                        into,
                        dto.contentId,
                        "extraSlots[" + i + "]." + (extra.code ?? string.Empty),
                        "sprite",
                        extra.path);
                }
            }
        }

        private static void AddEntry(
            System.Collections.Generic.List<CardPresentationAssetPathEntry> into,
            string contentId,
            string field,
            string kind,
            string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            into.Add(new CardPresentationAssetPathEntry
            {
                ContentId = contentId ?? string.Empty,
                Field = field,
                Kind = kind,
                Path = path.Replace('\\', '/').Trim()
            });
        }
    }

    public sealed class CardPresentationAssetPathEntry
    {
        public string ContentId;
        public string Field;
        public string Kind;
        public string Path;
    }
}
