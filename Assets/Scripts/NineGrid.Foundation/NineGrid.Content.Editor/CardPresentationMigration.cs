#if UNITY_EDITOR
using System;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 从 ContentVisual Catalog SO / xlsx / Core Catalog 填充缺失的卡牌表现 JSON。
    /// 仅旧→JSON 单向 seed；禁止把 JSON 重叠字段回写 Luban / xlsx。
    /// </summary>
    public static class CardPresentationMigration
    {
        public static bool IsCardLikeKind(string contentKind)
        {
            if (string.IsNullOrWhiteSpace(contentKind))
            {
                return false;
            }

            if (Enum.TryParse(contentKind, true, out ContentVisualKind kind))
            {
                switch (kind)
                {
                    case ContentVisualKind.Avatar:
                    case ContentVisualKind.Monster:
                    case ContentVisualKind.HelpCard:
                    case ContentVisualKind.Relic:
                    case ContentVisualKind.Skill:
                        return true;
                }
            }

            var k = contentKind.Trim();
            return string.Equals(k, "PlayerCard", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(k, "Item", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(k, "HelpCard", StringComparison.OrdinalIgnoreCase);
        }

        public static CardPresentationConfigDto CreateFilledDefault(
            string contentId,
            string contentKind,
            string description,
            ContentVisualDirectSlotSprites slots,
            GameContentCatalog coreCatalog,
            Func<string, string, string> getDisplayName)
        {
            var kind = string.IsNullOrWhiteSpace(contentKind) ? string.Empty : contentKind.Trim();
            var dto = CardPresentationJsonIO.CreateDefault(contentId ?? string.Empty, kind);
            dto.description = description ?? string.Empty;
            dto.displayName = getDisplayName != null
                ? (getDisplayName(contentId, kind) ?? contentId)
                : (contentId ?? string.Empty);

            ApplySpritePaths(dto, slots);
            ApplyCoreFields(dto, coreCatalog);
            return dto;
        }

        public static void ApplySpritePaths(CardPresentationConfigDto dto, ContentVisualDirectSlotSprites slots)
        {
            if (dto == null)
            {
                return;
            }

            if (dto.sprites == null)
            {
                dto.sprites = new CardPresentationSpritesDto();
            }

            if (slots.MainIcon != null)
            {
                dto.sprites.mainIcon = AssetPathOrEmpty(slots.MainIcon);
            }

            if (slots.FaceBackground != null)
            {
                dto.sprites.faceBackground = AssetPathOrEmpty(slots.FaceBackground);
            }

            if (slots.BackBorder != null)
            {
                dto.sprites.backBorder = AssetPathOrEmpty(slots.BackBorder);
            }

            if (slots.BackShirt != null)
            {
                dto.sprites.backShirt = AssetPathOrEmpty(slots.BackShirt);
            }

            if (slots.BackLogo != null)
            {
                dto.sprites.backLogo = AssetPathOrEmpty(slots.BackLogo);
            }
        }

        public static void ApplyCoreFields(CardPresentationConfigDto dto, GameContentCatalog coreCatalog)
        {
            if (dto == null || coreCatalog == null || string.IsNullOrWhiteSpace(dto.contentId))
            {
                return;
            }

            if (coreCatalog.Cards != null
                && coreCatalog.Cards.TryGetValue(dto.contentId, out var card)
                && card != null)
            {
                if (string.IsNullOrWhiteSpace(dto.displayName))
                {
                    dto.displayName = card.DisplayName ?? dto.contentId;
                }

                if (string.IsNullOrWhiteSpace(dto.deckId) && !string.IsNullOrWhiteSpace(card.DeckId))
                {
                    dto.deckId = card.DeckId;
                }

                if (dto.stats == null)
                {
                    dto.stats = new CardPresentationStatsDto();
                }

                if (dto.stats.hp == 0 && dto.stats.attack == 0 && dto.stats.armor == 0)
                {
                    dto.stats.hp = card.Stats.Hp > 0 ? card.Stats.Hp : card.Stats.MaxHp;
                    dto.stats.attack = card.Stats.Attack;
                    dto.stats.armor = card.Stats.Armor;
                }

                if (IsHelpLikeKind(dto.kind) && dto.gold == 0 && card.Price > 0)
                {
                    dto.gold = card.Price;
                }

                if (IsMonsterKind(dto.kind) && dto.gold == 0)
                {
                    if (card.KillGold > 0)
                    {
                        dto.gold = card.KillGold;
                    }
                    else if (coreCatalog.Economy != null && coreCatalog.Economy.MonsterRemovedGold > 0)
                    {
                        dto.gold = coreCatalog.Economy.MonsterRemovedGold;
                    }
                }

                return;
            }

            if (coreCatalog.Relics != null
                && coreCatalog.Relics.TryGetValue(dto.contentId, out var relic)
                && relic != null)
            {
                if (string.IsNullOrWhiteSpace(dto.displayName))
                {
                    dto.displayName = relic.DisplayName ?? dto.contentId;
                }

                // RelicContentDefinition 无 Price；gold 保持 0 除非作者已写。
            }
        }

        public static void FillEmptyFromCore(
            CardPresentationConfigDto dto,
            GameContentCatalog coreCatalog,
            Func<string, string, string> getDisplayName)
        {
            if (dto == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(dto.displayName) && getDisplayName != null)
            {
                dto.displayName = getDisplayName(dto.contentId, dto.kind) ?? dto.contentId;
            }

            ApplyCoreFields(dto, coreCatalog);
        }

        public static bool AuthoringFileExists(string contentId)
        {
            var abs = CardPresentationJsonIO.GetAuthoringAbsolutePath(contentId);
            return !string.IsNullOrEmpty(abs) && System.IO.File.Exists(abs);
        }

        public static string AssetPathOrEmpty(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static bool IsHelpLikeKind(string kind)
        {
            if (string.IsNullOrWhiteSpace(kind))
            {
                return false;
            }

            return string.Equals(kind, "HelpCard", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(kind, "Skill", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(kind, "Item", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(kind, "PlayerCard", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(kind, "Relic", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMonsterKind(string kind)
        {
            return !string.IsNullOrWhiteSpace(kind)
                   && string.Equals(kind, "Monster", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
