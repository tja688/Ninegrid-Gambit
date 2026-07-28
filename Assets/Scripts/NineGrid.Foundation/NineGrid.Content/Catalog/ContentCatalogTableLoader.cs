using System;
using System.Collections.Generic;
using System.IO;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using UnityEngine;

namespace NineGrid.Content
{
    /// <summary>
    /// 加载效果 DSL / 奖励池 / 经济 / 节点牌组规则表（非一卡一文件部分）进
    /// <see cref="GameContentCatalog"/>。权威目录：Arts/ContentVisual/tables（Editor）与
    /// StreamingAssets/ContentVisual/tables（Player）。不依赖 Luban.Runtime。
    /// </summary>
    public static class ContentCatalogTableLoader
    {
        public const string AuthoringRelativeFolder = "Arts/ContentVisual/tables";
        public const string StreamingRelativeFolder = "ContentVisual/tables";

        public static int ApplyToCatalog(GameContentCatalog catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            var folder = ResolveTablesDirectory();
            if (string.IsNullOrEmpty(folder))
            {
                Debug.LogWarning("[ContentCatalogTableLoader] tables directory not found.");
                return 0;
            }

            var applied = 0;
            applied += ApplyEffects(catalog, Path.Combine(folder, "effects.json"));
            applied += ApplyRewardPools(catalog, Path.Combine(folder, "reward_pools.json"));
            applied += ApplyRewardEntries(catalog, Path.Combine(folder, "reward_entries.json"));
            applied += ApplyEconomy(catalog, Path.Combine(folder, "economy.json"));
            applied += ApplyNodeDeckRules(catalog, Path.Combine(folder, "node_deck_rules.json"));
            return applied;
        }

        public static string ResolveTablesDirectory()
        {
#if UNITY_EDITOR
            var authoring = Path.Combine(Application.dataPath, AuthoringRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(authoring) && File.Exists(Path.Combine(authoring, "effects.json")))
            {
                return authoring;
            }
#endif
            var streaming = Path.Combine(Application.streamingAssetsPath, StreamingRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(streaming) && File.Exists(Path.Combine(streaming, "effects.json")))
            {
                return streaming;
            }

            return string.Empty;
        }

        private static int ApplyEffects(GameContentCatalog catalog, string path)
        {
            if (!TryReadArray(path, out EffectRowDto[] rows) || rows == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrEmpty(row.id))
                {
                    continue;
                }

                catalog.AddEffect(new ContentEffectDefinition(
                    row.id,
                    ParseEnum(row.container_type, EffectContainerType.Unknown),
                    row.json,
                    ParseEnum(row.state, ContentImplementationState.RawDesignOnly),
                    row.design_text));
                count++;
            }

            return count;
        }

        private static int ApplyRewardPools(GameContentCatalog catalog, string path)
        {
            if (!TryReadArray(path, out RewardPoolRowDto[] rows) || rows == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrEmpty(row.id))
                {
                    continue;
                }

                catalog.Rewards.AddPool(new RewardPoolDefinition(row.id, row.pick_count));
                count++;
            }

            return count;
        }

        private static int ApplyRewardEntries(GameContentCatalog catalog, string path)
        {
            if (!TryReadArray(path, out RewardEntryRowDto[] rows) || rows == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrEmpty(row.pool_id) || string.IsNullOrEmpty(row.def_id))
                {
                    continue;
                }

                if (!catalog.Rewards.TryGetPool(row.pool_id, out var pool) || pool == null)
                {
                    continue;
                }

                pool.Add(row.def_id, ParseEnum(row.kind, CardKind.Unknown), row.weight, row.count);
                count++;
            }

            return count;
        }

        private static int ApplyEconomy(GameContentCatalog catalog, string path)
        {
            if (!TryReadArray(path, out EconomyRowDto[] rows) || rows == null || rows.Length == 0)
            {
                return 0;
            }

            EconomyRowDto row = null;
            for (var i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && string.Equals(rows[i].id, "default", StringComparison.Ordinal))
                {
                    row = rows[i];
                    break;
                }
            }

            row ??= rows[0];
            if (row == null)
            {
                return 0;
            }

            catalog.Economy.MonsterRemovedGold = row.monster_removed_gold;
            catalog.Economy.UnusedHelpCardGold = row.unused_help_card_gold;
            catalog.Economy.SkipHelpChoiceGold = row.skip_help_choice_gold;
            catalog.Economy.SkipRelicChoiceGold = row.skip_relic_choice_gold;
            catalog.Economy.DiscardRelicGold = row.discard_relic_gold;
            catalog.Economy.ShopDeleteHelpCardGold = row.shop_delete_help_card_gold;
            return 1;
        }

        private static int ApplyNodeDeckRules(GameContentCatalog catalog, string path)
        {
            if (!TryReadArray(path, out NodeDeckRuleRowDto[] rows) || rows == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row == null)
                {
                    continue;
                }

                catalog.Rewards.AddNodeRule(new NodeDeckRule
                {
                    NodeIndex = row.node_index,
                    TotalMonsterCount = row.total_monster_count,
                    Level1Min = row.level1_min,
                    Level1Max = row.level1_max,
                    Level2Min = row.level2_min,
                    Level2Max = row.level2_max,
                    Level3Min = row.level3_min,
                    Level3Max = row.level3_max,
                    EliteCount = row.elite_count,
                    BossCount = row.boss_count,
                    DeckKind = ParseEnum(row.deck_kind, MonsterDeckKind.Unknown)
                });
                count++;
            }

            return count;
        }

        private static bool TryReadArray<T>(string path, out T[] items)
        {
            items = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning("[ContentCatalogTableLoader] Missing table: " + path);
                return false;
            }

            try
            {
                var raw = File.ReadAllText(path);
                var wrapped = "{\"items\":" + raw + "}";
                var list = JsonUtility.FromJson<JsonArrayWrapper<T>>(wrapped);
                items = list != null ? list.items : null;
                return items != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ContentCatalogTableLoader] Failed reading " + path + ": " + ex.Message);
                return false;
            }
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

        [Serializable]
        private sealed class JsonArrayWrapper<T>
        {
            public T[] items;
        }

        [Serializable]
        private sealed class EffectRowDto
        {
            public string id;
            public string container_type;
            public string state;
            public string json;
            public string design_text;
        }

        [Serializable]
        private sealed class RewardPoolRowDto
        {
            public string id;
            public int pick_count;
        }

        [Serializable]
        private sealed class RewardEntryRowDto
        {
            public string id;
            public string pool_id;
            public string def_id;
            public string kind;
            public int weight;
            public int count;
        }

        [Serializable]
        private sealed class EconomyRowDto
        {
            public string id;
            public int monster_removed_gold;
            public int unused_help_card_gold;
            public int skip_help_choice_gold;
            public int skip_relic_choice_gold;
            public int discard_relic_gold;
            public int shop_delete_help_card_gold;
        }

        [Serializable]
        private sealed class NodeDeckRuleRowDto
        {
            public int node_index;
            public int total_monster_count;
            public int level1_min;
            public int level1_max;
            public int level2_min;
            public int level2_max;
            public int level3_min;
            public int level3_max;
            public int elite_count;
            public int boss_count;
            public string deck_kind;
        }
    }
}
