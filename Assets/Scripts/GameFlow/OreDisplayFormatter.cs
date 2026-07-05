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
            public string Effect;
        }

        static readonly TraitLine[] TraitLines =
        {
            new() { Flag = OreTrait.Ember, Label = "余烬", Effect = "回合结束留精炼盘，不返矿舱" },
            new() { Flag = OreTrait.Quench2, Label = "淬火2", Effect = "摆下永久+2点" },
            new() { Flag = OreTrait.Quench, Label = "淬火", Effect = "摆下永久+1点" },
            new() { Flag = OreTrait.Station, Label = "驻台", Effect = "撞击后留铸造台，下回合仍可用" },
            new() { Flag = OreTrait.Debris, Label = "碎屑", Effect = "摆下生成0点矿渣入精炼盘" },
            new() { Flag = OreTrait.Twin, Label = "双晶", Effect = "摆下复制自身入精炼盘" },
            new() { Flag = OreTrait.Preheat, Label = "预热", Effect = "下一块入同台矿获其半数点数" },
            new() { Flag = OreTrait.Symbiosis2, Label = "共生2", Effect = "摆下从矿舱抽2块" },
            new() { Flag = OreTrait.Symbiosis, Label = "共生", Effect = "摆下从矿舱抽1块" },
            new() { Flag = OreTrait.Core, Label = "熔核", Effect = "结算时点数翻倍" },
            new() { Flag = OreTrait.Unity, Label = "齐心", Effect = "同台每多1矿+1点" },
            new() { Flag = OreTrait.Sociable, Label = "合群", Effect = "邻台每有1矿+1点" },
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

            var header = BuildHeader(card);
            AddSegment(segments, header);

            var traits = card.Traits;
            if (traits != OreTrait.None)
            {
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

                    AddSegment(segments, $"{line.Label}：{line.Effect}");
                }
            }

            if (!string.IsNullOrWhiteSpace(card.Description))
            {
                AppendDescriptionChunks(segments, card.Description.Trim());
            }

            if (segments.Count == 0)
            {
                segments.Add(Truncate(header));
            }

            return segments;
        }

        static string BuildHeader(CardInstance card)
        {
            var sb = new StringBuilder();
            sb.Append(card.DisplayName).Append(' ').Append(card.GetBaseValue()).Append('\u70B9');

            if (card.Traits != OreTrait.None)
            {
                var tags = new List<string>(4);
                CollectTraitTags(card.Traits, tags);
                if (tags.Count > 0)
                {
                    sb.Append('\u00B7').Append(string.Join("\u00B7", tags));
                }
            }

            return sb.ToString();
        }

        static void CollectTraitTags(OreTrait traits, List<string> tags)
        {
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

                tags.Add(line.Label);
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
