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

            view = new ContentVisualResolvedView
            {
                ContentId = contentId,
                Kind = visual.Kind,
                DisplayName = ResolveDisplayName(contentId, visual.Kind, coreCatalog),
                Description = visual.Description,
                FaceKey = ResolveFaceKey(contentId, visual, coreCatalog),
                FrameKey = ResolveFrameKey(contentId, visual, coreCatalog),
                IconKey = ResolveIconKey(visual),
                IconResourcePath = BuildIconConventionPath(visual.Kind, contentId)
            };
            return true;
        }

        public static string ResolveIconKey(ContentVisualDefinition visual)
        {
            if (visual == null)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(visual.IconKey)
                ? BuildIconConventionPath(visual.Kind, visual.ContentId)
                : visual.IconKey;
        }

        public static string BuildIconConventionPath(ContentVisualKind kind, string contentId)
        {
            if (string.IsNullOrEmpty(contentId) || kind == ContentVisualKind.Unknown)
            {
                return string.Empty;
            }

            return IconConventionRoot + "/" + kind + "/" + contentId;
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

        private static string ResolveFaceKey(string contentId, ContentVisualDefinition visual, GameContentCatalog coreCatalog)
        {
            if (!string.IsNullOrEmpty(visual.FaceKey))
            {
                return visual.FaceKey;
            }

            if (visual.Kind != ContentVisualKind.Monster)
            {
                return string.Empty;
            }

            CardContentDefinition card;
            if (coreCatalog.Cards.TryGetValue(contentId, out card) && !string.IsNullOrEmpty(card.DeckId))
            {
                return card.DeckId;
            }

            return string.Empty;
        }

        private static string ResolveFrameKey(string contentId, ContentVisualDefinition visual, GameContentCatalog coreCatalog)
        {
            if (!string.IsNullOrEmpty(visual.FrameKey))
            {
                return visual.FrameKey;
            }

            switch (visual.Kind)
            {
                case ContentVisualKind.HelpCard:
                case ContentVisualKind.Relic:
                    CardContentDefinition helpCard;
                    RelicContentDefinition relic;
                    if (visual.Kind == ContentVisualKind.HelpCard
                        && coreCatalog.Cards.TryGetValue(contentId, out helpCard))
                    {
                        return FrameKeyFromRarity(helpCard.Rarity);
                    }

                    if (visual.Kind == ContentVisualKind.Relic
                        && coreCatalog.Relics.TryGetValue(contentId, out relic))
                    {
                        return FrameKeyFromRarity(relic.Rarity);
                    }

                    break;
                case ContentVisualKind.Monster:
                    CardContentDefinition monster;
                    if (coreCatalog.Cards.TryGetValue(contentId, out monster))
                    {
                        if (monster.IsBoss)
                        {
                            return "boss_frame";
                        }

                        if (monster.IsElite)
                        {
                            return "elite_frame";
                        }

                        return "normal_frame";
                    }

                    break;
            }

            return string.Empty;
        }

        private static string FrameKeyFromRarity(ContentRarity rarity)
        {
            switch (rarity)
            {
                case ContentRarity.White:
                    return "white_frame";
                case ContentRarity.Blue:
                    return "blue_frame";
                case ContentRarity.Gold:
                    return "gold_frame";
                case ContentRarity.Red:
                    return "red_frame";
                default:
                    return "normal_frame";
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
