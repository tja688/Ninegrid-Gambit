using System;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    public static class ContentVisualResolver
    {
        public const string IconConventionRoot = "Sprites/Content";

        public static bool TryResolve(
            string contentId,
            GameContentCatalog coreCatalog,
            ContentVisualCatalog visualCatalog,
            CardFrameStyleCatalog frameStyleCatalog,
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

            var iconVisualId = ResolveIconVisualId(visual);
            var faceVisualId = ResolveFaceVisualId(contentId, visual, coreCatalog);
            var frameStyleId = ResolveFrameStyleId(contentId, visual, coreCatalog);

            view = new ContentVisualResolvedView
            {
                ContentId = contentId,
                Kind = visual.Kind,
                DisplayName = ResolveDisplayName(contentId, visual.Kind, coreCatalog),
                Description = visual.Description,
                IconVisualId = iconVisualId,
                IconAssetKey = ResolveIconAssetKey(visual.Kind, iconVisualId, contentId),
                FaceVisualId = faceVisualId,
                FaceAssetKey = ResolveFaceAssetKey(contentId, visual, coreCatalog, faceVisualId),
                FrameStyleId = frameStyleId,
                FrameColor = ResolveFrameColor(frameStyleId, frameStyleCatalog)
            };
            return true;
        }

        public static string ResolveIconVisualId(ContentVisualDefinition visual)
        {
            if (visual == null)
            {
                return string.Empty;
            }

            if (VisualIdNaming.IsVisualId(visual.IconKey))
            {
                return visual.IconKey;
            }

            if (!string.IsNullOrEmpty(visual.IconKey))
            {
                return visual.IconKey;
            }

            return VisualIdNaming.ForIcon(visual.ContentId);
        }

        public static string ResolveIconAssetKey(ContentVisualKind kind, string iconVisualId, string contentId)
        {
            if (string.IsNullOrEmpty(iconVisualId))
            {
                return string.Empty;
            }

            return VisualAssetKeyNaming.FromConvention(kind, VisualAssetSlot.Icon, contentId);
        }

        public static string BuildIconConventionPath(ContentVisualKind kind, string contentId)
        {
            return VisualAssetKeyNaming.ToResourcesPath(
                VisualAssetKeyNaming.FromConvention(kind, VisualAssetSlot.Icon, contentId));
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

        private static string ResolveFaceVisualId(
            string contentId,
            ContentVisualDefinition visual,
            GameContentCatalog coreCatalog)
        {
            if (visual == null)
            {
                return string.Empty;
            }

            if (VisualIdNaming.IsVisualId(visual.FaceKey))
            {
                return visual.FaceKey;
            }

            if (!string.IsNullOrEmpty(visual.FaceKey))
            {
                if (visual.Kind == ContentVisualKind.Monster)
                {
                    CardContentDefinition card;
                    if (coreCatalog.Cards.TryGetValue(contentId, out card)
                        && string.Equals(visual.FaceKey, card.DeckId, StringComparison.Ordinal))
                    {
                        return VisualIdNaming.ForFace(contentId);
                    }
                }

                if (VisualIdNaming.IsLegacyPathKey(visual.FaceKey))
                {
                    return visual.FaceKey;
                }
            }

            if (visual.Kind == ContentVisualKind.Monster)
            {
                CardContentDefinition card;
                if (coreCatalog.Cards.TryGetValue(contentId, out card) && !string.IsNullOrEmpty(card.DeckId))
                {
                    return VisualIdNaming.ForFace(contentId);
                }
            }

            return VisualIdNaming.ForFace(contentId);
        }

        private static string ResolveFaceAssetKey(
            string contentId,
            ContentVisualDefinition visual,
            GameContentCatalog coreCatalog,
            string faceVisualId)
        {
            if (string.IsNullOrEmpty(faceVisualId) || visual == null)
            {
                return string.Empty;
            }

            if (visual.Kind == ContentVisualKind.Monster && string.IsNullOrEmpty(visual.FaceKey))
            {
                CardContentDefinition card;
                if (coreCatalog.Cards.TryGetValue(contentId, out card) && !string.IsNullOrEmpty(card.DeckId))
                {
                    return VisualAssetKeyNaming.FromConventionFaceDeck(card.DeckId);
                }
            }

            if (visual.Kind == ContentVisualKind.Monster)
            {
                CardContentDefinition deckCard;
                if (coreCatalog.Cards.TryGetValue(contentId, out deckCard)
                    && !string.IsNullOrEmpty(visual.FaceKey)
                    && string.Equals(visual.FaceKey, deckCard.DeckId, StringComparison.Ordinal))
                {
                    return VisualAssetKeyNaming.FromConventionFaceDeck(deckCard.DeckId);
                }
            }

            return VisualAssetKeyNaming.FromConvention(visual.Kind, VisualAssetSlot.Face, contentId);
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
