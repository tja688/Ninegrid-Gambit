using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NineGrid.Core.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public static class TableNineLubanDataExporter
    {
        private const string MenuPath = "TableNine/Content/Export Hardcoded Catalog To Luban Datas";
        private const string DataDirectory = "Assets/Tools/Luban/Datas";

        [MenuItem(MenuPath)]
        public static void ExportHardcodedCatalogToLubanDatas()
        {
            var projectRoot = Directory.GetCurrentDirectory();
            var outputDirectory = Path.Combine(projectRoot, DataDirectory);
            Export(TableNineContentCatalog.CreateDefault(), outputDirectory);
            AssetDatabase.Refresh();
            Debug.Log("TableNine Luban source data exported from hardcoded catalog.");
        }

        public static void Export(GameContentCatalog catalog, string outputDirectory)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException("catalog");
            }

            if (string.IsNullOrEmpty(outputDirectory))
            {
                throw new ArgumentException("Output directory is required.", "outputDirectory");
            }

            Directory.CreateDirectory(outputDirectory);
            WriteRows(Path.Combine(outputDirectory, "effects.json"), catalog.Effects.Values.OrderBy(row => row.Id), WriteEffectRow);
            WriteRows(Path.Combine(outputDirectory, "cards.json"), catalog.Cards.Values.OrderBy(row => row.DefId), WriteCardRow);
            WriteRows(Path.Combine(outputDirectory, "skills.json"), catalog.Skills.Values.OrderBy(row => row.DefId), WriteSkillRow);
            WriteRows(Path.Combine(outputDirectory, "relics.json"), catalog.Relics.Values.OrderBy(row => row.DefId), WriteRelicRow);
            WriteRows(Path.Combine(outputDirectory, "reward_pools.json"), catalog.Rewards.Pools.Values.OrderBy(row => row.Id), WriteRewardPoolRow);
            WriteRows(Path.Combine(outputDirectory, "reward_entries.json"), RewardEntries(catalog).OrderBy(row => row.Id), WriteRewardEntryRow);
            WriteRows(Path.Combine(outputDirectory, "monster_decks.json"), catalog.MonsterDecks.Values.OrderBy(row => row.Id), WriteMonsterDeckRow);
            WriteRows(Path.Combine(outputDirectory, "node_deck_rules.json"), catalog.Rewards.NodeDeckRules.OrderBy(row => row.NodeIndex), WriteNodeDeckRuleRow);
            WriteRows(Path.Combine(outputDirectory, "rooms.json"), catalog.Rewards.Rooms.Values.OrderBy(row => row.Kind.ToString()), WriteRoomRow);
            WriteRows(Path.Combine(outputDirectory, "economy.json"), new[] { catalog.Economy }, WriteEconomyRow);
        }

        private static IEnumerable<RewardEntryRow> RewardEntries(GameContentCatalog catalog)
        {
            foreach (var pool in catalog.Rewards.Pools.Values.OrderBy(row => row.Id))
            {
                for (var i = 0; i < pool.Entries.Count; i++)
                {
                    var entry = pool.Entries[i];
                    yield return new RewardEntryRow(
                        pool.Id + "." + i.ToString("00") + "." + entry.DefId.Replace(".", "_"),
                        pool.Id,
                        entry.DefId,
                        entry.Kind.ToString(),
                        entry.Weight,
                        entry.Count);
                }
            }
        }

        private static void WriteEffectRow(StringBuilder builder, ContentEffectDefinition row)
        {
            Property(builder, "id", row.Id, true);
            Property(builder, "container_type", row.ContainerType.ToString(), true);
            Property(builder, "state", row.State.ToString(), true);
            Property(builder, "json", row.Json, true);
            Property(builder, "design_text", row.DesignText, false);
        }

        private static void WriteCardRow(StringBuilder builder, CardContentDefinition row)
        {
            Property(builder, "def_id", row.DefId, true);
            Property(builder, "display_name", row.DisplayName, true);
            Property(builder, "kind", row.Kind.ToString(), true);
            Property(builder, "rarity", row.Rarity.ToString(), true);
            Property(builder, "price", row.Price, true);
            Property(builder, "level", row.Level, true);
            Property(builder, "is_elite", row.IsElite, true);
            Property(builder, "is_boss", row.IsBoss, true);
            Property(builder, "is_reserve", row.IsReserve, true);
            Property(builder, "deck_id", row.DeckId, true);
            Property(builder, "max_hp", row.Stats.MaxHp, true);
            Property(builder, "attack", row.Stats.Attack, true);
            Property(builder, "armor", row.Stats.Armor, true);
            Property(builder, "recovery", row.Stats.Recovery, true);
            Property(builder, "tags", Join(row.Tags), true);
            Property(builder, "effect_ids", Join(row.EffectIds), true);
            Property(builder, "skill_ids", Join(row.SkillIds), false);
        }

        private static void WriteSkillRow(StringBuilder builder, SkillContentDefinition row)
        {
            Property(builder, "def_id", row.DefId, true);
            Property(builder, "display_name", row.DisplayName, true);
            Property(builder, "container_type", row.ContainerType.ToString(), true);
            Property(builder, "design_text", row.DesignText, true);
            Property(builder, "effect_ids", Join(row.EffectIds), false);
        }

        private static void WriteRelicRow(StringBuilder builder, RelicContentDefinition row)
        {
            Property(builder, "def_id", row.DefId, true);
            Property(builder, "display_name", row.DisplayName, true);
            Property(builder, "rarity", row.Rarity.ToString(), true);
            Property(builder, "design_text", row.DesignText, true);
            Property(builder, "tags", Join(row.Tags), true);
            Property(builder, "effect_ids", Join(row.EffectIds), false);
        }

        private static void WriteRewardPoolRow(StringBuilder builder, RewardPoolDefinition row)
        {
            Property(builder, "id", row.Id, true);
            Property(builder, "pick_count", row.PickCount, false);
        }

        private static void WriteRewardEntryRow(StringBuilder builder, RewardEntryRow row)
        {
            Property(builder, "id", row.Id, true);
            Property(builder, "pool_id", row.PoolId, true);
            Property(builder, "def_id", row.DefId, true);
            Property(builder, "kind", row.Kind, true);
            Property(builder, "weight", row.Weight, true);
            Property(builder, "count", row.Count, false);
        }

        private static void WriteMonsterDeckRow(StringBuilder builder, MonsterDeckDefinition row)
        {
            Property(builder, "id", row.Id, true);
            Property(builder, "display_name", row.DisplayName, true);
            Property(builder, "kind", row.Kind.ToString(), true);
            Property(builder, "monster_def_ids", Join(row.MonsterDefIds), false);
        }

        private static void WriteNodeDeckRuleRow(StringBuilder builder, NodeDeckRule row)
        {
            Property(builder, "node_index", row.NodeIndex, true);
            Property(builder, "total_monster_count", row.TotalMonsterCount, true);
            Property(builder, "level1_min", row.Level1Min, true);
            Property(builder, "level1_max", row.Level1Max, true);
            Property(builder, "level2_min", row.Level2Min, true);
            Property(builder, "level2_max", row.Level2Max, true);
            Property(builder, "level3_min", row.Level3Min, true);
            Property(builder, "level3_max", row.Level3Max, true);
            Property(builder, "elite_count", row.EliteCount, true);
            Property(builder, "boss_count", row.BossCount, true);
            Property(builder, "deck_kind", row.DeckKind.ToString(), false);
        }

        private static void WriteRoomRow(StringBuilder builder, RoomDefinition row)
        {
            Property(builder, "kind", row.Kind.ToString(), true);
            Property(builder, "display_name", row.DisplayName, true);
            Property(builder, "weight", row.Weight, true);
            Property(builder, "gold_delta", row.GoldDelta, true);
            Property(builder, "max_hp_delta", row.MaxHpDelta, true);
            Property(builder, "heal_to_full", row.HealToFull, true);
            Property(builder, "reward_pool_id", row.RewardPoolId, true);
            Property(builder, "shop_offer_count", row.ShopOfferCount, false);
        }

        private static void WriteEconomyRow(StringBuilder builder, EconomyConfig row)
        {
            Property(builder, "id", "default", true);
            Property(builder, "monster_removed_gold", row.MonsterRemovedGold, true);
            Property(builder, "unused_help_card_gold", row.UnusedHelpCardGold, true);
            Property(builder, "skip_help_choice_gold", row.SkipHelpChoiceGold, true);
            Property(builder, "skip_relic_choice_gold", row.SkipRelicChoiceGold, true);
            Property(builder, "discard_relic_gold", row.DiscardRelicGold, true);
            Property(builder, "shop_delete_help_card_gold", row.ShopDeleteHelpCardGold, false);
        }

        private static void WriteRows<T>(string path, IEnumerable<T> rows, Action<StringBuilder, T> writeRow)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[");
            var list = rows.ToList();
            for (var i = 0; i < list.Count; i++)
            {
                builder.AppendLine("  {");
                writeRow(builder, list[i]);
                builder.Append("  }");
                if (i < list.Count - 1)
                {
                    builder.Append(",");
                }

                builder.AppendLine();
            }

            builder.AppendLine("]");
            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
        }

        private static string Join(IReadOnlyList<string> values)
        {
            return values == null || values.Count == 0 ? string.Empty : string.Join("|", values);
        }

        private static void Property(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("    \"");
            builder.Append(name);
            builder.Append("\": \"");
            builder.Append(Escape(value));
            builder.Append("\"");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void Property(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("    \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value);
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static void Property(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("    \"");
            builder.Append(name);
            builder.Append("\": ");
            builder.Append(value ? "true" : "false");
            if (comma)
            {
                builder.Append(",");
            }

            builder.AppendLine();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 16);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        private sealed class RewardEntryRow
        {
            public RewardEntryRow(string id, string poolId, string defId, string kind, int weight, int count)
            {
                Id = id;
                PoolId = poolId;
                DefId = defId;
                Kind = kind;
                Weight = weight;
                Count = count;
            }

            public string Id { get; private set; }
            public string PoolId { get; private set; }
            public string DefId { get; private set; }
            public string Kind { get; private set; }
            public int Weight { get; private set; }
            public int Count { get; private set; }
        }
    }
}
