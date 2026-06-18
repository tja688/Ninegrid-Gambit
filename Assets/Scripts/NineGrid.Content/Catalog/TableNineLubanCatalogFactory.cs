using System;
using System.Collections.Generic;
using System.IO;
using Luban.SimpleJSON;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;

namespace NineGrid.Content
{
    public static class TableNineLubanCatalogFactory
    {
        public const string DefaultDataRelativePath = "Assets/StreamingAssets/TableNine/LubanData";

        public static GameContentCatalog CreateFromDirectory(string dataDirectory)
        {
            if (string.IsNullOrEmpty(dataDirectory))
            {
                throw new ArgumentException("Luban data directory is required.", "dataDirectory");
            }

            var tables = new cfg.Tables(file => LoadJson(dataDirectory, file));
            return CreateFromTables(tables);
        }

        public static GameContentCatalog CreateFromTables(cfg.Tables tables)
        {
            if (tables == null)
            {
                throw new ArgumentNullException("tables");
            }

            var catalog = new GameContentCatalog();
            AddEffects(catalog, tables);
            AddCards(catalog, tables);
            AddSkills(catalog, tables);
            AddRelics(catalog, tables);
            AddRewards(catalog, tables);
            AddMonsterDecks(catalog, tables);
            AddNodeDeckRules(catalog, tables);
            AddRooms(catalog, tables);
            ApplyEconomy(catalog, tables);
            return catalog;
        }

        private static JSONNode LoadJson(string dataDirectory, string file)
        {
            var path = Path.Combine(dataDirectory, file + ".json");
            return JSON.Parse(File.ReadAllText(path));
        }

        private static void AddEffects(GameContentCatalog catalog, cfg.Tables tables)
        {
            foreach (var row in tables.TbEffect.DataList)
            {
                catalog.AddEffect(new ContentEffectDefinition(
                    row.Id,
                    ParseEnum(row.ContainerType, EffectContainerType.Unknown),
                    row.Json,
                    ParseEnum(row.State, ContentImplementationState.RawDesignOnly),
                    row.DesignText));
            }
        }

        private static void AddCards(GameContentCatalog catalog, cfg.Tables tables)
        {
            foreach (var row in tables.TbCard.DataList)
            {
                var card = new CardContentDefinition(row.DefId, row.DisplayName, ParseEnum(row.Kind, CardKind.Unknown))
                {
                    Rarity = ParseEnum(row.Rarity, ContentRarity.None),
                    Price = row.Price,
                    Level = row.Level,
                    IsElite = row.IsElite,
                    IsBoss = row.IsBoss,
                    IsReserve = row.IsReserve,
                    DeckId = row.DeckId
                };

                card.Stats.MaxHp = row.MaxHp;
                card.Stats.Hp = row.MaxHp;
                card.Stats.Attack = row.Attack;
                card.Stats.Armor = row.Armor;
                card.Stats.Recovery = row.Recovery;

                AddTokens(row.Tags, value => card.AddTag(value));
                AddTokens(row.EffectIds, value => card.AddEffect(value));
                AddTokens(row.SkillIds, value => card.AddSkill(value));
                catalog.AddCard(card);
            }
        }

        private static void AddSkills(GameContentCatalog catalog, cfg.Tables tables)
        {
            foreach (var row in tables.TbSkill.DataList)
            {
                var skill = new SkillContentDefinition(
                    row.DefId,
                    row.DisplayName,
                    ParseEnum(row.ContainerType, EffectContainerType.Unknown),
                    row.DesignText);

                AddTokens(row.EffectIds, value => skill.AddEffect(value));
                catalog.AddSkill(skill);
            }
        }

        private static void AddRelics(GameContentCatalog catalog, cfg.Tables tables)
        {
            foreach (var row in tables.TbRelic.DataList)
            {
                var relic = new RelicContentDefinition(
                    row.DefId,
                    row.DisplayName,
                    ParseEnum(row.Rarity, ContentRarity.None),
                    row.DesignText);

                AddTokens(row.Tags, value => relic.AddTag(value));
                AddTokens(row.EffectIds, value => relic.AddEffect(value));
                catalog.AddRelic(relic);
            }
        }

        private static void AddRewards(GameContentCatalog catalog, cfg.Tables tables)
        {
            var pools = new Dictionary<string, RewardPoolDefinition>();
            foreach (var row in tables.TbRewardPool.DataList)
            {
                var pool = new RewardPoolDefinition(row.Id, row.PickCount);
                pools[row.Id] = pool;
                catalog.Rewards.AddPool(pool);
            }

            foreach (var row in tables.TbRewardEntry.DataList)
            {
                RewardPoolDefinition pool;
                if (!pools.TryGetValue(row.PoolId, out pool))
                {
                    continue;
                }

                pool.Add(row.DefId, ParseEnum(row.Kind, CardKind.Unknown), row.Weight, row.Count);
            }
        }

        private static void AddMonsterDecks(GameContentCatalog catalog, cfg.Tables tables)
        {
            foreach (var row in tables.TbMonsterDeck.DataList)
            {
                var deck = new MonsterDeckDefinition(
                    row.Id,
                    row.DisplayName,
                    ParseEnum(row.Kind, MonsterDeckKind.Unknown));

                AddTokens(row.MonsterDefIds, value => deck.AddMonster(value));
                catalog.AddMonsterDeck(deck);
            }
        }

        private static void AddNodeDeckRules(GameContentCatalog catalog, cfg.Tables tables)
        {
            foreach (var row in tables.TbNodeDeckRule.DataList)
            {
                catalog.Rewards.AddNodeRule(new NodeDeckRule
                {
                    NodeIndex = row.NodeIndex,
                    TotalMonsterCount = row.TotalMonsterCount,
                    Level1Min = row.Level1Min,
                    Level1Max = row.Level1Max,
                    Level2Min = row.Level2Min,
                    Level2Max = row.Level2Max,
                    Level3Min = row.Level3Min,
                    Level3Max = row.Level3Max,
                    EliteCount = row.EliteCount,
                    BossCount = row.BossCount,
                    DeckKind = ParseEnum(row.DeckKind, MonsterDeckKind.Unknown)
                });
            }
        }

        private static void AddRooms(GameContentCatalog catalog, cfg.Tables tables)
        {
            foreach (var row in tables.TbRoom.DataList)
            {
                var room = new RoomDefinition(ParseEnum(row.Kind, RoomKind.None), row.DisplayName)
                {
                    Weight = row.Weight,
                    GoldDelta = row.GoldDelta,
                    MaxHpDelta = row.MaxHpDelta,
                    HealToFull = row.HealToFull,
                    RewardPoolId = row.RewardPoolId,
                    ShopOfferCount = row.ShopOfferCount
                };
                catalog.Rewards.AddRoom(room);
            }
        }

        private static void ApplyEconomy(GameContentCatalog catalog, cfg.Tables tables)
        {
            var row = tables.TbEconomy.GetOrDefault("default");
            if (row == null)
            {
                return;
            }

            catalog.Economy.MonsterRemovedGold = row.MonsterRemovedGold;
            catalog.Economy.UnusedHelpCardGold = row.UnusedHelpCardGold;
            catalog.Economy.SkipHelpChoiceGold = row.SkipHelpChoiceGold;
            catalog.Economy.SkipRelicChoiceGold = row.SkipRelicChoiceGold;
            catalog.Economy.DiscardRelicGold = row.DiscardRelicGold;
            catalog.Economy.ShopDeleteHelpCardGold = row.ShopDeleteHelpCardGold;
        }

        private static T ParseEnum<T>(string value, T fallback) where T : struct
        {
            if (string.IsNullOrEmpty(value))
            {
                return fallback;
            }

            try
            {
                return (T)Enum.Parse(typeof(T), value, true);
            }
            catch (ArgumentException)
            {
                return fallback;
            }
        }

        private static void AddTokens(string raw, Action<string> add)
        {
            if (string.IsNullOrEmpty(raw) || add == null)
            {
                return;
            }

            var tokens = raw.Split(new[] { ';', '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                add(tokens[i].Trim());
            }
        }
    }
}
