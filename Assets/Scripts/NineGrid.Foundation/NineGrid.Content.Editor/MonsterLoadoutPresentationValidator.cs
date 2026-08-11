#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using NineGrid.Core.Content;
using UnityEditor;
using UnityEngine;

namespace NineGrid.Content.Editor
{
    /// <summary>
    /// 怪物遭遇表现配置一键校验：过渡期 / 交付就绪两档。
    /// 交付就绪通过后即可接入已落盘的主题卡组 + 序列抽卡流程（ADR-0022）。
    /// </summary>
    public static class MonsterLoadoutPresentationValidator
    {
        public const string TransitionDeckId = "deck.transition";
        public const int MinThemeDecksForDelivery = 3;

        public enum ValidationMode
        {
            /// <summary>过渡期：允许怪物全在过渡卡组；过渡组须能按序列抽卡。</summary>
            Staging,

            /// <summary>交付就绪：过渡组清空；每套非 Reserve 主题卡组覆盖序列 1–5。</summary>
            DeliveryReady,
        }

        public sealed class Issue
        {
            public string Severity;
            public string Code;
            public string Message;
        }

        public sealed class Report
        {
            public ValidationMode Mode;
            public bool Passed;
            public List<Issue> Issues { get; } = new List<Issue>();

            public void Error(string code, string message)
            {
                Issues.Add(new Issue { Severity = "error", Code = code, Message = message });
            }

            public void Warn(string code, string message)
            {
                Issues.Add(new Issue { Severity = "warn", Code = code, Message = message });
            }

            public bool HasErrors
            {
                get
                {
                    for (var i = 0; i < Issues.Count; i++)
                    {
                        if (string.Equals(Issues[i].Severity, "error", StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
        }

        [MenuItem("NineGrid/Content/校验怪物遭遇配置（过渡期）")]
        public static void ValidateStagingMenu()
        {
            RunAndDialog(ValidationMode.Staging);
        }

        [MenuItem("NineGrid/Content/校验怪物遭遇配置（交付就绪）")]
        public static void ValidateDeliveryMenu()
        {
            RunAndDialog(ValidationMode.DeliveryReady);
        }

        public static void RunAndDialog(ValidationMode mode)
        {
            var report = Validate(mode);
            var sb = new StringBuilder();
            sb.AppendLine(mode == ValidationMode.DeliveryReady ? "【交付就绪】" : "【过渡期】");
            sb.AppendLine(report.Passed ? "通过" : "未通过");
            sb.AppendLine();
            if (report.Issues.Count == 0)
            {
                sb.AppendLine("无问题。");
            }
            else
            {
                for (var i = 0; i < report.Issues.Count; i++)
                {
                    var issue = report.Issues[i];
                    sb.Append('[').Append(issue.Severity).Append("] ")
                        .Append(issue.Code).Append(": ")
                        .AppendLine(issue.Message);
                }
            }

            var text = sb.ToString();
            if (report.Passed)
            {
                Debug.Log("[MonsterLoadoutPresentationValidator]\n" + text);
            }
            else
            {
                Debug.LogWarning("[MonsterLoadoutPresentationValidator]\n" + text);
            }

            EditorUtility.DisplayDialog("怪物遭遇配置校验", text, "OK");
        }

        public static Report Validate(ValidationMode mode)
        {
            var report = new Report { Mode = mode };
            CardPresentationConfigCatalog.Invalidate();
            MonsterDeckTableCatalog.Invalidate();

            var monsters = LoadMonsterDtos(report);
            var tableRows = LoadTableRows(report);
            if (monsters.Count == 0)
            {
                report.Error("no_monsters", "未读到任何 Monster JSON。");
                report.Passed = false;
                return report;
            }

            ValidateShared(monsters, tableRows, report);
            if (mode == ValidationMode.Staging)
            {
                ValidateStaging(monsters, tableRows, report);
            }
            else
            {
                ValidateDeliveryReady(monsters, tableRows, report);
            }

            report.Passed = !report.HasErrors;
            return report;
        }

        private static void ValidateShared(
            List<CardPresentationConfigDto> monsters,
            Dictionary<string, MonsterDeckTableRow> tableRows,
            Report report)
        {
            for (var i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                var id = m.contentId ?? "(missing)";
                var deckId = (m.deckId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(deckId))
                {
                    report.Error("missing_deck", id + " 未设置 deckId。");
                }
                else if (!tableRows.ContainsKey(deckId))
                {
                    report.Error(
                        "deck_not_in_table",
                        id + " 的 deckId=" + deckId + " 未登记在 monster_decks.json。");
                }

                if (m.sequence < 0 || m.sequence > 5)
                {
                    report.Error("sequence_range", id + " sequence=" + m.sequence + " 超出 0–5。");
                }

                if (m.isReserve)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(m.attackPattern)
                    || string.Equals(m.attackPattern.Trim(), "Unspecified", StringComparison.OrdinalIgnoreCase))
                {
                    report.Error("attack_pattern", id + " 缺少合法 attackPattern。");
                }

                var needsRhythm = AttackPatternRules.TryParse(m.attackPattern, out var pattern)
                    && AttackPatternRules.ParticipatesInEnemyAction(pattern);
                // 同步技能检测从简：快递模板挂载即视为有节奏需求。
                if (!needsRhythm && m.effectAssemblies != null)
                {
                    for (var ai = 0; ai < m.effectAssemblies.Length; ai++)
                    {
                        var tid = m.effectAssemblies[ai] != null ? m.effectAssemblies[ai].templateId : null;
                        if (string.IsNullOrEmpty(tid))
                        {
                            continue;
                        }

                        if (tid.IndexOf("delivery.move", StringComparison.OrdinalIgnoreCase) >= 0
                            || tid.IndexOf("gear_delivery", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            needsRhythm = true;
                            break;
                        }
                    }
                }

                if (needsRhythm)
                {
                    if (!CardRhythmRules.TryParse(m.rhythmSource, out _))
                    {
                        report.Error("rhythm_source", id + " 缺少合法 rhythmSource。");
                    }

                    if (m.rhythmPeriod <= 0)
                    {
                        report.Error("rhythm_period", id + " 缺少合法 rhythmPeriod（须 > 0）。");
                    }
                }

                if (string.IsNullOrWhiteSpace(m.displayName))
                {
                    report.Warn("display_name", id + " 显示名为空。");
                }

                if (string.IsNullOrWhiteSpace(m.designSlotName))
                {
                    report.Warn("design_slot", id + " 策划槽位名为空（建议填近战1/远程2…）。");
                }
            }

            if (!tableRows.ContainsKey(TransitionDeckId))
            {
                report.Warn("no_transition_row", "monster_decks.json 未登记 " + TransitionDeckId + "。");
            }
        }

        private static void ValidateStaging(
            List<CardPresentationConfigDto> monsters,
            Dictionary<string, MonsterDeckTableRow> tableRows,
            Report report)
        {
            if (tableRows.TryGetValue(TransitionDeckId, out var transitionRow)
                && string.Equals(transitionRow.deck_kind, "Reserve", StringComparison.OrdinalIgnoreCase))
            {
                report.Error(
                    "transition_reserve",
                    TransitionDeckId + " 当前为 Reserve，过渡期无法被每层主题绑定。请改为 Unknown。");
            }

            var inTransition = 0;
            var seqCover = new HashSet<int>();
            for (var i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m.isReserve)
                {
                    continue;
                }

                if (!string.Equals(m.deckId, TransitionDeckId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                inTransition++;
                if (m.sequence >= 1 && m.sequence <= 5)
                {
                    seqCover.Add(m.sequence);
                }
            }

            if (inTransition == 0)
            {
                report.Warn(
                    "transition_empty",
                    "过渡卡组暂无可用怪物。若已开始交付到主题卡组，请改跑「交付就绪」校验。");
                return;
            }

            for (var seq = 1; seq <= 5; seq++)
            {
                if (!seqCover.Contains(seq))
                {
                    report.Error(
                        "transition_seq_gap",
                        TransitionDeckId + " 缺少 sequence=" + seq
                        + " 的可用怪物（过渡期抽卡会缺档）。");
                }
            }
        }

        private static void ValidateDeliveryReady(
            List<CardPresentationConfigDto> monsters,
            Dictionary<string, MonsterDeckTableRow> tableRows,
            Report report)
        {
            var stillInTransition = 0;
            for (var i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m.isReserve)
                {
                    continue;
                }

                if (string.Equals(m.deckId, TransitionDeckId, StringComparison.OrdinalIgnoreCase))
                {
                    stillInTransition++;
                    report.Error(
                        "still_in_transition",
                        (m.contentId ?? "?") + " 仍在过渡卡组；交付前请改到主题卡组并设好序列。");
                }

                if (m.sequence < 1 || m.sequence > 5)
                {
                    report.Error(
                        "delivery_sequence",
                        (m.contentId ?? "?") + " 交付要求 sequence 为 1–5（当前 " + m.sequence + "）。");
                }
            }

            if (stillInTransition > 0)
            {
                report.Error(
                    "transition_not_cleared",
                    "过渡卡组仍有 " + stillInTransition + " 张可用怪物。");
            }

            var themeCount = 0;
            foreach (var pair in tableRows)
            {
                var row = pair.Value;
                if (row == null || string.IsNullOrWhiteSpace(row.deck_id))
                {
                    continue;
                }

                if (string.Equals(row.deck_id, TransitionDeckId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(row.deck_kind, "Reserve", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                themeCount++;
                var cover = new HashSet<int>();
                var members = 0;
                for (var i = 0; i < monsters.Count; i++)
                {
                    var m = monsters[i];
                    if (m.isReserve)
                    {
                        continue;
                    }

                    if (!string.Equals(m.deckId, row.deck_id, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    members++;
                    if (m.sequence >= 1 && m.sequence <= 5)
                    {
                        cover.Add(m.sequence);
                    }
                }

                if (members == 0)
                {
                    report.Error(
                        "theme_empty",
                        row.deck_id + "（" + (row.display_name ?? "") + "）已非 Reserve 但无可用怪物。");
                    continue;
                }

                for (var seq = 1; seq <= 5; seq++)
                {
                    if (!cover.Contains(seq))
                    {
                        report.Error(
                            "theme_seq_gap",
                            row.deck_id + " 缺少 sequence=" + seq + "（主题卡组交付要求覆盖 1–5）。");
                    }
                }
            }

            if (themeCount < MinThemeDecksForDelivery)
            {
                report.Error(
                    "theme_count",
                    "非 Reserve 主题卡组仅 " + themeCount + " 套，交付至少需要 "
                    + MinThemeDecksForDelivery
                    + " 套（建议 7）。请为已填满的主题卡组设置 deck_kind（WeakElite/StrongElite/Boss）。");
            }
        }

        private static List<CardPresentationConfigDto> LoadMonsterDtos(Report report)
        {
            var result = new List<CardPresentationConfigDto>();
            var folder = Path.GetFullPath(
                Path.Combine(Application.dataPath, "Arts", "ContentVisual", "cards"));
            if (!Directory.Exists(folder))
            {
                report.Error("cards_dir", "找不到 Cards 目录：" + folder);
                return result;
            }

            var files = Directory.GetFiles(folder, "monster_*.json", SearchOption.TopDirectoryOnly);
            for (var i = 0; i < files.Length; i++)
            {
                if (!CardPresentationJsonIO.TryLoad(files[i], out var dto, out var error))
                {
                    report.Error("json_parse", Path.GetFileName(files[i]) + ": " + error);
                    continue;
                }

                if (dto == null
                    || !string.Equals(dto.kind, "Monster", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(dto);
            }

            return result;
        }

        private static Dictionary<string, MonsterDeckTableRow> LoadTableRows(Report report)
        {
            var map = new Dictionary<string, MonsterDeckTableRow>(StringComparer.OrdinalIgnoreCase);
            MonsterDeckTableCatalog.EnsureLoaded();
            var rows = MonsterDeckTableCatalog.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                if (row == null || string.IsNullOrWhiteSpace(row.deck_id))
                {
                    continue;
                }

                map[row.deck_id.Trim()] = row;
            }

            if (map.Count == 0)
            {
                report.Error("empty_table", "monster_decks.json 为空或未加载。");
            }

            return map;
        }
    }
}
#endif
