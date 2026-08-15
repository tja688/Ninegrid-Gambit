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
    /// 加载奖励池 / 经济 / 节点牌组规则 / 遭遇牌组表进 <see cref="GameContentCatalog"/>。
    /// 效果改为模板表 + 卡上装配引用解析（ADR-0009 / #70）；不再从 effects.json 灌全量实例。
    /// 权威目录：Arts/ContentVisual/tables（Editor）与 StreamingAssets/ContentVisual/tables（Player）。
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

            EffectTemplateCatalog.Invalidate();
            DungeonEnvironmentCatalog.Invalidate();
            var applied = 0;
            applied += EffectTemplateCatalog.Count; // ensure templates load; mounts resolved in projector
            applied += ApplyDungeonEnvironments(Path.Combine(folder, DungeonEnvironmentCatalog.FileName));
            applied += ApplyRewardPools(catalog, Path.Combine(folder, "reward_pools.json"));
            // legacy whitelist（若仍存在则合并）；生产以查询规则为主（#71）。
            var entriesPath = Path.Combine(folder, "reward_entries.json");
            if (File.Exists(entriesPath))
            {
                applied += ApplyRewardEntries(catalog, entriesPath);
            }

            applied += ApplyEconomy(catalog, Path.Combine(folder, "economy.json"));
            applied += ApplyNodeDeckRules(catalog, Path.Combine(folder, "node_deck_rules.json"));
            return applied;
        }

        public static string ResolveTablesDirectory()
        {
#if UNITY_EDITOR
            var authoring = Path.Combine(Application.dataPath, AuthoringRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(authoring)
                && (File.Exists(Path.Combine(authoring, EffectTemplateCatalog.FileName))
                    || File.Exists(Path.Combine(authoring, "effects.json"))))
            {
                return authoring;
            }
#endif
            var streaming = Path.Combine(Application.streamingAssetsPath, StreamingRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(streaming)
                && (File.Exists(Path.Combine(streaming, EffectTemplateCatalog.FileName))
                    || File.Exists(Path.Combine(streaming, "effects.json"))))
            {
                return streaming;
            }

            return string.Empty;
        }

        private static int ApplyDungeonEnvironments(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return 0;
            }

            try
            {
                var table = JsonUtility.FromJson<DungeonEnvironmentTableDto>(File.ReadAllText(path));
                DungeonEnvironmentCatalog.SetTable(table);
                return table?.variants != null ? 1 : 0;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[ContentCatalogTableLoader] Failed reading " + path + ": " + ex.Message);
                return 0;
            }
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

                var pool = new RewardPoolDefinition(row.id, row.pick_count);
                if (!string.IsNullOrWhiteSpace(row.kind))
                {
                    var query = new RewardPoolQueryRule
                    {
                        Kind = ParseEnum(row.kind, CardKind.Unknown),
                        DefaultWeight = row.default_weight > 0 ? row.default_weight : 1,
                        RarityWeightWhite = row.rarity_weight_white,
                        RarityWeightBlue = row.rarity_weight_blue,
                        RarityWeightGold = row.rarity_weight_gold,
                        RarityWeightRed = row.rarity_weight_red,
                        BalanceMinAttack = row.balance_min_attack,
                        BalanceMinDefense = row.balance_min_defense,
                    };

                    AddRarityTokens(row.rarities, query);
                    AddRoleTokens(row.roles, query);
                    AddTagTokens(row.tags_any, query);
                    pool.WithQuery(query);
                }

                catalog.Rewards.AddPool(pool);
                count++;
            }

            return count;
        }

        private static void AddRarityTokens(string[] tokens, RewardPoolQueryRule query)
        {
            if (tokens == null || query == null)
            {
                return;
            }

            for (var i = 0; i < tokens.Length; i++)
            {
                var rarity = ParseEnum(tokens[i], ContentRarity.None);
                if (rarity != ContentRarity.None)
                {
                    query.AllowRarity(rarity);
                }
            }
        }

        private static void AddRoleTokens(string[] tokens, RewardPoolQueryRule query)
        {
            if (tokens == null || query == null)
            {
                return;
            }

            for (var i = 0; i < tokens.Length; i++)
            {
                var role = ParseEnum(tokens[i], ContentRole.None);
                if (role != ContentRole.None)
                {
                    query.AllowRole(role);
                }
            }
        }

        private static void AddTagTokens(string[] tokens, RewardPoolQueryRule query)
        {
            if (tokens == null || query == null)
            {
                return;
            }

            for (var i = 0; i < tokens.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(tokens[i]))
                {
                    query.AllowTag(tokens[i].Trim());
                }
            }
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
                    Seq1Count = row.seq1,
                    Seq2Count = row.seq2,
                    Seq3Count = row.seq3,
                    Seq4Count = row.seq4,
                    Seq5Count = row.seq5
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
        private sealed class RewardPoolRowDto
        {
            public string id;
            public int pick_count;
            public string kind;
            public string[] rarities;
            public string[] roles;
            public string[] tags_any;
            public int default_weight;
            public int rarity_weight_white;
            public int rarity_weight_blue;
            public int rarity_weight_gold;
            public int rarity_weight_red;
            public int balance_min_attack;
            public int balance_min_defense;
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
            public int seq1;
            public int seq2;
            public int seq3;
            public int seq4;
            public int seq5;
        }
    }
}
