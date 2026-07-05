using System.Collections.Generic;
using System.Text;
using NineGrid.Battle.Combat;
using NineGrid.Data;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 锻造显示屏（≤51 字）矿石信息格式化：名称/点数/词条效果/描述，超长时分段轮播。
    /// </summary>
    public static class OreDisplayFormatter
    {
        public const int MaxChars = 51;

        struct TraitLine
        {
            public OreTrait Flag;
            public string Label;
            public string Summary;
            public string Effect;
        }

        static readonly TraitLine[] TraitLines =
        {
            new() { Flag = OreTrait.Ember, Label = "余烬", Summary = "余烬:回合后留盘", Effect = "回合结束留精炼盘，不返矿舱" },
            new() { Flag = OreTrait.Quench2, Label = "淬火2", Summary = "淬火2:摆下+2", Effect = "摆下永久+2点" },
            new() { Flag = OreTrait.Quench, Label = "淬火", Summary = "淬火:摆下+1", Effect = "摆下永久+1点" },
            new() { Flag = OreTrait.Station, Label = "驻台", Summary = "驻台:撞后留台", Effect = "撞击后留铸造台，下回合仍可用" },
            new() { Flag = OreTrait.Debris, Label = "碎屑", Summary = "碎屑:摆下生0渣", Effect = "摆下生成0点矿渣入精炼盘" },
            new() { Flag = OreTrait.Twin, Label = "双晶", Summary = "双晶:摆下复制", Effect = "摆下复制自身入精炼盘" },
            new() { Flag = OreTrait.Preheat, Label = "预热", Summary = "预热:下块获半值", Effect = "下一块入同台矿获其半数点数" },
            new() { Flag = OreTrait.Symbiosis2, Label = "共生2", Summary = "共生2:摆下抽2", Effect = "摆下从矿舱抽2块" },
            new() { Flag = OreTrait.Symbiosis, Label = "共生", Summary = "共生:摆下抽1", Effect = "摆下从矿舱抽1块" },
            new() { Flag = OreTrait.Core, Label = "熔核", Summary = "熔核:结算翻倍", Effect = "结算时点数翻倍" },
            new() { Flag = OreTrait.Unity, Label = "齐心", Summary = "齐心:同台每矿+1", Effect = "同台每多1矿+1点" },
            new() { Flag = OreTrait.Sociable, Label = "合群", Summary = "合群:邻台每矿+1", Effect = "邻台每有1矿+1点" },
        };

        /// <summary>生成锻造显示屏轮播段（每段 ≤51 字）。</summary>
        public static List<string> BuildForgeSegments(CardInstance card)
        {
            var segments = new List<string>(6);
            if (card == null)
            {
                segments.Add("矿石信息：未知");
                return segments;
            }

            var traits = card.Traits;
            var traitLines = CollectTraitLines(traits);
            var consumedSummaryCount = AddPrimarySegment(segments, card, traitLines);

            if (traitLines.Count > 0)
            {
                var summaries = new List<string>(traitLines.Count);
                for (var i = consumedSummaryCount; i < traitLines.Count; i++)
                {
                    summaries.Add(traitLines[i].Summary);
                }

                AppendSummaryChunks(segments, summaries);
            }

            if (!string.IsNullOrWhiteSpace(card.Description))
            {
                AppendDescriptionChunks(segments, card.Description.Trim());
            }

            if (segments.Count == 0)
            {
                segments.Add(Truncate(BuildHeaderPrefix(card)));
            }

            return segments;
        }

        static string BuildHeaderPrefix(CardInstance card)
        {
            var sb = new StringBuilder();
            sb.Append(card.DisplayName).Append(' ').Append(card.GetBaseValue()).Append('\u70B9');
            return sb.ToString();
        }

        static List<TraitLine> CollectTraitLines(OreTrait traits)
        {
            var lines = new List<TraitLine>(4);
            for (var i = 0; i < TraitLines.Length; i++)
            {
                var line = TraitLines[i];
                if ((traits & line.Flag) == 0)
                {
                    continue;
                }

                if (line.Flag == OreTrait.Quench && (traits & OreTrait.Quench2) != 0)
                {
                    continue;
                }

                if (line.Flag == OreTrait.Symbiosis && (traits & OreTrait.Symbiosis2) != 0)
                {
                    continue;
                }

                lines.Add(line);
            }

            return lines;
        }

        static int AddPrimarySegment(List<string> segments, CardInstance card, List<TraitLine> traitLines)
        {
            var prefix = BuildHeaderPrefix(card);
            if (traitLines == null || traitLines.Count == 0)
            {
                AddSegment(segments, prefix);
                return 0;
            }

            var sb = new StringBuilder(prefix);
            var consumed = 0;
            for (var i = 0; i < traitLines.Count; i++)
            {
                var summary = traitLines[i].Summary;
                var separator = sb.Length == prefix.Length ? " " : "；";
                if (sb.Length + separator.Length + summary.Length > MaxChars)
                {
                    break;
                }

                sb.Append(separator).Append(summary);
                consumed++;
            }

            AddSegment(segments, sb.ToString());
            return consumed;
        }

        static void AppendSummaryChunks(List<string> segments, List<string> summaries)
        {
            if (summaries == null || summaries.Count == 0)
            {
                return;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < summaries.Count; i++)
            {
                var summary = summaries[i];
                if (string.IsNullOrWhiteSpace(summary))
                {
                    continue;
                }

                if (sb.Length == 0)
                {
                    sb.Append(summary);
                    continue;
                }

                if (sb.Length + 1 + summary.Length > MaxChars)
                {
                    AddSegment(segments, sb.ToString());
                    sb.Clear();
                    sb.Append(summary);
                    continue;
                }

                sb.Append('；').Append(summary);
            }

            if (sb.Length > 0)
            {
                AddSegment(segments, sb.ToString());
            }
        }

        static void AppendDescriptionChunks(List<string> segments, string description)
        {
            var chunks = SplitToChunks(description, MaxChars);
            for (var i = 0; i < chunks.Count; i++)
            {
                AddSegment(segments, chunks[i]);
            }
        }

        static void AddSegment(List<string> segments, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var trimmed = Truncate(text.Trim());
            if (string.IsNullOrEmpty(trimmed))
            {
                return;
            }

            if (segments.Count > 0 && segments[^1] == trimmed)
            {
                return;
            }

            segments.Add(trimmed);
        }

        static List<string> SplitToChunks(string text, int maxLen)
        {
            var chunks = new List<string>(4);
            if (string.IsNullOrEmpty(text))
            {
                return chunks;
            }

            var start = 0;
            while (start < text.Length)
            {
                var remaining = text.Length - start;
                if (remaining <= maxLen)
                {
                    chunks.Add(text.Substring(start));
                    break;
                }

                var window = text.Substring(start, maxLen);
                var splitAt = FindSplitIndex(window);
                if (splitAt <= 0)
                {
                    splitAt = maxLen;
                }

                chunks.Add(text.Substring(start, splitAt).Trim());
                start += splitAt;
                while (start < text.Length && char.IsWhiteSpace(text[start]))
                {
                    start++;
                }
            }

            return chunks;
        }

        static int FindSplitIndex(string window)
        {
            for (var i = window.Length - 1; i >= 0; i--)
            {
                switch (window[i])
                {
                    case '\uFF0C':
                    case '\u3002':
                    case '\uFF1B':
                    case '\u3001':
                    case '\u2014':
                    case '\uFF01':
                    case '\uFF1F':
                    case ' ':
                        return i + 1;
                }
            }

            return window.Length;
        }

        public static string Truncate(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= MaxChars)
            {
                return text ?? string.Empty;
            }

            return text.Substring(0, MaxChars);
        }
    }
}
