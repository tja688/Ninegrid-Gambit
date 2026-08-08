using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 词条抽取 seam：从检查描述抽出 <c>[[展示名]]</c>（去重保序），供右键详情默认行装配。
    /// </summary>
    public static class CardGlossaryTerms
    {
        private static readonly Regex DoubleBracketToken =
            new Regex(@"\[\[([^\]]+)\]\]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public readonly struct ResolvedTerm
        {
            public ResolvedTerm(string displayName, string explanation, bool matched, bool hasColor, UnityEngine.Color color)
            {
                DisplayName = displayName ?? string.Empty;
                Explanation = explanation ?? string.Empty;
                Matched = matched;
                HasColor = hasColor;
                Color = color;
            }

            public string DisplayName { get; }
            public string Explanation { get; }
            public bool Matched { get; }
            public bool HasColor { get; }
            public UnityEngine.Color Color { get; }
        }

        /// <summary>
        /// 按出现顺序抽出 <c>[[名字]]</c>；同名只保留首次。未命中词条库时仍返回名字行（Matched=false）。
        /// </summary>
        public static List<ResolvedTerm> ExtractExplicitTerms(
            string description,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            var result = new List<ResolvedTerm>();
            if (string.IsNullOrEmpty(description))
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in DoubleBracketToken.Matches(description))
            {
                var name = match.Groups[1].Value;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                name = name.Trim();
                if (!seen.Add(name))
                {
                    continue;
                }

                if (catalog != null && catalog.TryGetByDisplayName(name, out var entry) && entry != null)
                {
                    var title = string.IsNullOrWhiteSpace(entry.displayNameZh)
                        ? name
                        : entry.displayNameZh.Trim();
                    result.Add(new ResolvedTerm(
                        title,
                        entry.explanation ?? string.Empty,
                        matched: true,
                        hasColor: entry.HasColorOverride,
                        color: entry.color));
                }
                else
                {
                    result.Add(new ResolvedTerm(name, string.Empty, matched: false, hasColor: false, color: default));
                }
            }

            return result;
        }
    }
}
