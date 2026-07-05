using System.Collections.Generic;
using NineGrid.Battle.Combat;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 锻造显示屏：矿石信息（≤51 字轮播）；空闲态单行展示完整伤害与熔炼说明。
    /// </summary>
    public static class OreDisplayFormatter
    {
        public const int MaxChars = 51;
        public const int MaxIdleChars = 256;

        /// <summary>无聚焦时单行展示完整文案。</summary>
        public static string BuildIdleForgeText(
            int frontDamage, int midDamage, int backDamage, int totalDamage,
            int frontSmelt, int midSmelt, int backSmelt,
            bool combatReady)
        {
            var totalPart = combatReady
                ? $"\u5408\u8BA1{totalDamage}"
                : "\u5408\u8BA1\u5F85\u63A5\u5165";

            return
                $"\u9884\u8BA1\u4F24\u5BB3\uFF1A\u524D{frontDamage} \u4E2D{midDamage} \u540E{backDamage}|{totalPart}\u3002" +
                $"\u7194\u70BC\u52A0\u6210\uFF1A\u5F53\u524D\u7194\u70BC{frontSmelt}-{midSmelt}-{backSmelt}\uFF0C" +
                "\u5F53\u67D0\u94F8\u9020\u53F0\u7194\u70BC\uFF1A" +
                "2\u5757\u77FF\u77F3\uFF1A\u94BB\u5934\u4F24\u5BB3\u500D\u7387+1\uFF0C" +
                "4\u5757\u77FF\u77F3\uFF1A\u672C\u53F0\u6240\u6709\u77FF\u77F3\u57FA\u7840\u4F24\u5BB3\u6570\u503C+1\uFF0C" +
                "\u4EE5\u6B64\u7C7B\u63A8";
        }

        /// <summary>指定砧台当前矿石块数（与伤害预览同源）。</summary>
        public static int GetAnvilOreCount(HydraulicMaterialBoard board, int anvilIndex)
        {
            if (board == null)
            {
                return 0;
            }

            var stacks = board.GetAnvilStacks();
            if (stacks == null || anvilIndex < 0 || anvilIndex >= stacks.Length || stacks[anvilIndex] == null)
            {
                return 0;
            }

            return stacks[anvilIndex].Count;
        }

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
