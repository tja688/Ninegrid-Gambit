using NineGrid.Core;
using NineGrid.Core.Content;
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
            Sprite icon = null;
            Sprite face = null;
            if (spriteProvider != null)
            {
                spriteProvider.TryGet(visual.Kind, contentId, out icon, out face);
            }

            view = new ContentVisualResolvedView
            {
                ContentId = contentId,
                Kind = visual.Kind,
                DisplayName = ResolveDisplayName(contentId, visual.Kind, coreCatalog),
                Description = string.IsNullOrWhiteSpace(visual.Description)
                    ? ResolveDisplayName(contentId, visual.Kind, coreCatalog)
                    : visual.Description,
                Icon = icon,
                Face = face,
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
                return ContentColor.White;
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

            return ContentColor.White;
        }

        private static string FrameStyleIdFromRarity(ContentRarity rarity)
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
                    return "玩家";
            }

            return contentId;
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
