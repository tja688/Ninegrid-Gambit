using NineGrid.Core;
using NineGrid.Core.Content;
using System;
using UnityEngine;

namespace NineGrid.Content
{
    public static class ContentVisualResolver
    {
        public static bool TryResolve(
            string contentId,
            GameContentCatalog coreCatalog,
            ContentVisualCatalog visualCatalog,
            CardFrameStyleCatalog frameStyleCatalog,
            IContentVisualSpriteProvider spriteProvider,
            out ContentVisualResolvedView view)
        {
            view = null;
            if (string.IsNullOrEmpty(contentId) || coreCatalog == null || visualCatalog == null)
            {
                return false;
            }

            ContentVisualDefinition visual;
            if (!visualCatalog.TryGet(contentId, out visual))
            {
                return false;
            }

            var frameStyleId = ResolveFrameStyleId(contentId, visual, coreCatalog);
            ContentVisualDirectSlotSprites slots = default;
            if (spriteProvider != null)
            {
                spriteProvider.TryGetDirectSlots(visual.Kind, contentId, out slots);
            }

            view = new ContentVisualResolvedView
            {
                ContentId = contentId,
                Kind = visual.Kind,
                DisplayName = ResolveDisplayName(contentId, visual.Kind, coreCatalog),
                Description = string.IsNullOrWhiteSpace(visual.Description)
                    ? ResolveDisplayName(contentId, visual.Kind, coreCatalog)
                    : visual.Description,
                Icon = slots.MainIcon,
                Face = slots.FaceBackground,
                BackBorder = slots.BackBorder,
                BackShirt = slots.BackShirt,
                BackLogo = slots.BackLogo,
                FrameStyleId = frameStyleId,
                FrameColor = ResolveFrameColor(frameStyleId, frameStyleCatalog)
            };
            return true;
        }

        public static string ResolveFrameStyleId(
            string contentId,
            ContentVisualDefinition visual,
            GameContentCatalog coreCatalog)
        {
            if (visual == null)
            {
                return CardFrameStyleCatalog.StyleNormal;
            }

            switch (visual.Kind)
            {
                case ContentVisualKind.HelpCard:
                    CardContentDefinition helpCard;
                    if (coreCatalog.Cards.TryGetValue(contentId, out helpCard))
                    {
                        return FrameStyleIdFromRarity(helpCard.Rarity);
                    }

                    break;
                case ContentVisualKind.Relic:
                    RelicContentDefinition relic;
                    if (coreCatalog.Relics.TryGetValue(contentId, out relic))
                    {
                        return FrameStyleIdFromRarity(relic.Rarity);
                    }

                    break;
                case ContentVisualKind.Monster:
                    CardContentDefinition monster;
                    if (coreCatalog.Cards.TryGetValue(contentId, out monster))
                    {
                        if (monster.IsBoss)
                        {
                            return CardFrameStyleCatalog.StyleBoss;
                        }

                        if (monster.IsElite)
                        {
                            return CardFrameStyleCatalog.StyleElite;
                        }

                        return CardFrameStyleCatalog.StyleNormal;
                    }

                    break;
            }

            return CardFrameStyleCatalog.StyleNormal;
        }

        public static ContentColor ResolveFrameColor(string frameStyleId, CardFrameStyleCatalog frameStyleCatalog)
        {
            if (frameStyleCatalog == null || string.IsNullOrEmpty(frameStyleId))
            {
                return ResolveDefaultFrameColor(frameStyleId);
            }

            CardFrameStyleDefinition style;
            if (frameStyleCatalog.TryGet(frameStyleId, out style))
            {
                return style.Color;
            }

            CardFrameStyleDefinition fallback;
            if (frameStyleCatalog.TryGet(CardFrameStyleCatalog.StyleNormal, out fallback))
            {
                return fallback.Color;
            }

            return ResolveDefaultFrameColor(frameStyleId);
        }

        /// <summary>稀有度 → 框色（无 Catalog 时用内建默认色）。</summary>
        public static ContentColor ResolveFrameColorForRarity(ContentRarity rarity)
        {
            return ResolveDefaultFrameColor(FrameStyleIdFromRarity(rarity));
        }

        public static string FrameStyleIdFromRarity(ContentRarity rarity)
        {
            switch (rarity)
            {
                case ContentRarity.White:
                    return CardFrameStyleCatalog.StyleWhite;
                case ContentRarity.Blue:
                    return CardFrameStyleCatalog.StyleBlue;
                case ContentRarity.Gold:
                    return CardFrameStyleCatalog.StyleGold;
                case ContentRarity.Red:
                    return CardFrameStyleCatalog.StyleRed;
                default:
                    return CardFrameStyleCatalog.StyleNormal;
            }
        }

        private static ContentColor ResolveDefaultFrameColor(string frameStyleId)
        {
            if (string.Equals(frameStyleId, CardFrameStyleCatalog.StyleBlue, StringComparison.Ordinal))
            {
                return new ContentColor(0.45f, 0.65f, 1f, 1f);
            }

            if (string.Equals(frameStyleId, CardFrameStyleCatalog.StyleGold, StringComparison.Ordinal))
            {
                return new ContentColor(1f, 0.84f, 0.3f, 1f);
            }

            if (string.Equals(frameStyleId, CardFrameStyleCatalog.StyleRed, StringComparison.Ordinal))
            {
                return new ContentColor(1f, 0.35f, 0.35f, 1f);
            }

            if (string.Equals(frameStyleId, CardFrameStyleCatalog.StyleElite, StringComparison.Ordinal))
            {
                return new ContentColor(0.85f, 0.55f, 1f, 1f);
            }

            if (string.Equals(frameStyleId, CardFrameStyleCatalog.StyleBoss, StringComparison.Ordinal))
            {
                return new ContentColor(1f, 0.45f, 0.2f, 1f);
            }

            return ContentColor.White;
        }

        private static string ResolveDisplayName(string contentId, ContentVisualKind kind, GameContentCatalog coreCatalog)
        {
            switch (kind)
            {
                case ContentVisualKind.HelpCard:
                case ContentVisualKind.Monster:
                    CardContentDefinition card;
                    if (coreCatalog.Cards.TryGetValue(contentId, out card))
                    {
                        return card.DisplayName;
                    }

                    break;
                case ContentVisualKind.Relic:
                    RelicContentDefinition relic;
                    if (coreCatalog.Relics.TryGetValue(contentId, out relic))
                    {
                        return relic.DisplayName;
                    }

                    break;
                case ContentVisualKind.Skill:
                    SkillContentDefinition skill;
                    if (coreCatalog.Skills.TryGetValue(contentId, out skill))
                    {
                        return skill.DisplayName;
                    }

                    break;
                case ContentVisualKind.Room:
                    RoomKind roomKind;
                    if (TryParseRoomKind(contentId, out roomKind))
                    {
                        RoomDefinition room;
                        if (coreCatalog.Rewards.Rooms.TryGetValue(roomKind, out room))
                        {
                            return room.DisplayName;
                        }
                    }

                    break;
                case ContentVisualKind.MonsterDeck:
                    MonsterDeckDefinition deck;
                    if (coreCatalog.MonsterDecks.TryGetValue(contentId, out deck))
                    {
                        return deck.DisplayName;
                    }

                    break;
                case ContentVisualKind.Avatar:
                    return NineGrid.Core.Localization.L10n.Tr("hud.avatar_fallback_name", "玩家");
                case ContentVisualKind.ChoiceOption:
                    return ResolveChoiceOptionDisplayName(contentId);
            }

            return contentId;
        }

        private static string ResolveChoiceOptionDisplayName(string contentId)
        {
            switch (contentId)
            {
                case "Attack":
                    return NineGrid.Core.Localization.L10n.Tr("choice.attack_plus", "攻击+1");
                case "Armor":
                    return NineGrid.Core.Localization.L10n.Tr("choice.armor_plus", "护甲+1");
                case "Hp":
                    return NineGrid.Core.Localization.L10n.Tr("choice.hp_plus", "血量+2");
                default:
                    return contentId;
            }
        }

        private static bool TryParseRoomKind(string contentId, out RoomKind roomKind)
        {
            try
            {
                roomKind = (RoomKind)System.Enum.Parse(typeof(RoomKind), contentId, true);
                return true;
            }
            catch (System.ArgumentException)
            {
                roomKind = RoomKind.None;
                return false;
            }
        }
    }
}
