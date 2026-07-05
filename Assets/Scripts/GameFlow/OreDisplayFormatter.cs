using System.Collections.Generic;
using NineGrid.Battle.Combat;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 锻造显示屏（≤51 字）矿石信息 / 空闲预览格式化：机制描述分段轮播。
    /// </summary>
    public static class OreDisplayFormatter
    {
        public const int MaxChars = 51;

        /// <summary>无聚焦时轮播段：伤害预览、熔炼加成现状、叠矿规则说明。</summary>
        public static List<string> BuildIdleForgeSegments(
            int frontDamage, int midDamage, int backDamage, int totalDamage,
            int frontSmelt, int midSmelt, int backSmelt,
            bool combatReady)
        {
            var segments = new List<string>(4);
            if (combatReady)
            {
                AddSegment(segments, $"\u524D{frontDamage} \u4E2D{midDamage} \u540E{backDamage} | \u5408\u8BA1{totalDamage}");
            }
            else
            {
                AddSegment(segments, $"\u524D{frontDamage} \u4E2D{midDamage} \u540E{backDamage} | \u5F85\u63A5\u5165");
            }

            AddSegment(segments,
                $"\u7194\u70BC\u52A0\u6210\uFF0C\u5F53\u524D\u7194\u70BC{frontSmelt}-{midSmelt}-{backSmelt}");
            AddSegment(segments, "\u6BCF\u53F02\u5757\uFF1A\u5947\u6570\u5C42\u672C\u53F0\u500D\u7387+1");
            AddSegment(segments, "\u5076\u6570\u5C42\u672C\u53F0\u6240\u6709\u77FF\u77F3\u57FA\u7840+1");
            return segments;
        }

        /// <summary>砧台矿石数对应的熔炼周期（每 2 块解锁一层叠矿加成）。</summary>
        public static int GetSmeltCycleCount(int oreCount) => oreCount / 2;

        /// <summary>生成锻造显示屏轮播段（每段 ≤51 字）。</summary>
        public static List<string> BuildForgeSegments(CardInstance card)
        {
            var segments = new List<string>(6);
            if (card == null)
            {
                segments.Add("矿石信息：未知");
                return segments;
            }

            var fullText = OreMechanicalText.Build(card);
            AppendDescriptionChunks(segments, fullText);

            if (segments.Count == 0)
            {
                segments.Add(Truncate(fullText));
            }

            return segments;
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
