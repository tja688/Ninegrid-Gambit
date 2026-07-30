#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NineGrid.Content.CardPresentation;
using NineGrid.Core.Content;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    public enum MonsterDeckWiringStatus
    {
        PresentationOnly,
        Wired,
        UnwiredNoTable,
        UnwiredEmpty,
        UnwiredNoNodePool,
    }

    public sealed class MonsterDeckUsageSummary
    {
        public string DeckId { get; set; } = string.Empty;
        public MonsterDeckWiringStatus Status { get; set; }
        public bool IsInEncounterTable { get; set; }
        public string DeckKindLabel { get; set; } = string.Empty;
        public int MemberCount { get; set; }
        public int EncounterEligibleCount { get; set; }
        public int ReserveMemberCount { get; set; }
        public string NodePoolSummary { get; set; } = string.Empty;
        public string StatusHeadline { get; set; } = string.Empty;
        public string DetailLines { get; set; } = string.Empty;
    }

    /// <summary>
    /// 只读诊断：表现层卡组在遭遇编排中的接线状态（表 + 卡面 deckId 归属 + 节点规则）。
    /// </summary>
    public static class MonsterDeckUsageInspector
    {
        public static MonsterDeckUsageSummary InspectDeck(string deckContentId, CardPresentationEditorSession session)
        {
            var summary = new MonsterDeckUsageSummary
            {
                DeckId = deckContentId ?? string.Empty,
            };

            if (string.IsNullOrWhiteSpace(deckContentId))
            {
                summary.Status = MonsterDeckWiringStatus.PresentationOnly;
                summary.StatusHeadline = "无效卡组 id";
                return summary;
            }

            CountMembers(session, deckContentId, out var total, out var eligible, out var reserve);
            summary.MemberCount = total;
            summary.EncounterEligibleCount = eligible;
            summary.ReserveMemberCount = reserve;

            MonsterDeckTableCatalog.EnsureLoaded();
            if (!MonsterDeckTableCatalog.TryGet(deckContentId, out var row) || row == null)
            {
                summary.IsInEncounterTable = false;
                summary.Status = eligible > 0
                    ? MonsterDeckWiringStatus.UnwiredNoTable
                    : MonsterDeckWiringStatus.PresentationOnly;
                summary.StatusHeadline = eligible > 0
                    ? "未接线：有怪物归属但未列入遭遇表"
                    : "仅表现层（卡背分组）";
                summary.DetailLines = eligible > 0
                    ? "怪物卡已设置 deckId 指向本组，但 monster_decks.json 无登记。\n"
                      + "编辑：Assets/Arts/ContentVisual/tables/monster_decks.json"
                    : "本组仅用于卡面 deckId 归属与卡背三槽；不参与关卡遭遇抽选。";
                return summary;
            }

            summary.IsInEncounterTable = true;
            summary.DeckKindLabel = string.IsNullOrWhiteSpace(row.deck_kind)
                ? "Unknown"
                : row.deck_kind.Trim();
            summary.NodePoolSummary = BuildNodePoolSummary(summary.DeckKindLabel);

            if (eligible <= 0)
            {
                summary.Status = MonsterDeckWiringStatus.UnwiredEmpty;
                summary.StatusHeadline = "未接线：遭遇表已登记但无可用怪物";
                summary.DetailLines = "档位 " + summary.DeckKindLabel
                    + "；遭遇成员 0（需怪物卡 deckId 指向本组，isReserve 不计入）。\n"
                    + "节点池：" + summary.NodePoolSummary;
                return summary;
            }

            if (string.IsNullOrEmpty(summary.NodePoolSummary))
            {
                summary.Status = MonsterDeckWiringStatus.UnwiredNoNodePool;
                summary.StatusHeadline = "未接线：无节点使用档位 " + summary.DeckKindLabel;
                summary.DetailLines = "遭遇成员 " + eligible + " 张"
                    + (reserve > 0 ? "（另有 " + reserve + " 张 isReserve 储备怪）" : string.Empty)
                    + "；当前 node_deck_rules 未引用该档位。";
                return summary;
            }

            summary.Status = MonsterDeckWiringStatus.Wired;
            summary.StatusHeadline = "已接线";
            summary.DetailLines = "档位 " + summary.DeckKindLabel
                + "；遭遇成员 " + eligible + " 张"
                + (reserve > 0 ? "（另有 " + reserve + " 张 isReserve 储备怪）" : string.Empty)
                + "\n节点池：" + summary.NodePoolSummary
                + "\n成员来源：卡面 deckId 归属（非本编辑器维护）";
            return summary;
        }

        public static List<MonsterDeckUsageSummary> ListUnwiredDecks(CardPresentationEditorSession session)
        {
            var result = new List<MonsterDeckUsageSummary>();
            if (session == null)
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < session.DeckEntries.Count; i++)
            {
                var deckId = session.DeckEntries[i]?.ContentId;
                if (string.IsNullOrWhiteSpace(deckId) || !seen.Add(deckId))
                {
                    continue;
                }

                var summary = InspectDeck(deckId, session);
                if (summary.Status != MonsterDeckWiringStatus.Wired
                    && summary.Status != MonsterDeckWiringStatus.PresentationOnly)
                {
                    result.Add(summary);
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.DeckId, b.DeckId));
            return result;
        }

        private static void CountMembers(
            CardPresentationEditorSession session,
            string deckId,
            out int total,
            out int eligible,
            out int reserve)
        {
            total = 0;
            eligible = 0;
            reserve = 0;
            if (session == null || string.IsNullOrWhiteSpace(deckId))
            {
                return;
            }

            for (var i = 0; i < session.FaceEntries.Count; i++)
            {
                var entry = session.FaceEntries[i];
                if (entry?.Dto == null
                    || !string.Equals(entry.Dto.kind, "Monster", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(entry.DeckId, deckId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                total++;
                if (entry.Dto.isReserve)
                {
                    reserve++;
                }
                else
                {
                    eligible++;
                }
            }
        }

        private static string BuildNodePoolSummary(string deckKindLabel)
        {
            var folder = ContentCatalogTableLoader.ResolveTablesDirectory();
            if (string.IsNullOrEmpty(folder))
            {
                return string.Empty;
            }

            var path = Path.Combine(folder, "node_deck_rules.json");
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            try
            {
                var raw = File.ReadAllText(path, Encoding.UTF8);
                var wrapped = "{\"items\":" + raw + "}";
                var list = JsonUtility.FromJson<NodeDeckRuleRowList>(wrapped);
                if (list?.items == null || list.items.Length == 0)
                {
                    return string.Empty;
                }

                var nodes = new List<int>();
                for (var i = 0; i < list.items.Length; i++)
                {
                    var row = list.items[i];
                    if (row != null
                        && string.Equals(row.deck_kind, deckKindLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        nodes.Add(row.node_index);
                    }
                }

                if (nodes.Count == 0)
                {
                    return string.Empty;
                }

                nodes.Sort();
                if (nodes.Count == 1)
                {
                    return "节点 " + nodes[0];
                }

                return "节点 " + nodes[0] + "–" + nodes[nodes.Count - 1]
                    + "（共 " + nodes.Count + " 个）";
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[MonsterDeckUsageInspector] node_deck_rules: " + ex.Message);
                return string.Empty;
            }
        }

        [Serializable]
        private sealed class NodeDeckRuleRowList
        {
            public NodeDeckRuleRow[] items;
        }

        [Serializable]
        private sealed class NodeDeckRuleRow
        {
            public int node_index;
            public string deck_kind;
        }
    }
}
#endif
