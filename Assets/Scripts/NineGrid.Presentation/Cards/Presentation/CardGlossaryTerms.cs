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
            public ResolvedTerm(
                string displayName,
                string explanation,
                bool matched,
                bool hasColor,
                UnityEngine.Color color,
                string lookupName = null,
                string inlineCode = null)
            {
                DisplayName = displayName ?? string.Empty;
                Explanation = explanation ?? string.Empty;
                Matched = matched;
                HasColor = hasColor;
                Color = color;
                LookupName = string.IsNullOrWhiteSpace(lookupName)
                    ? (displayName ?? string.Empty).Trim()
                    : lookupName.Trim();
                InlineCode = inlineCode ?? string.Empty;
            }

            public string DisplayName { get; }
            public string Explanation { get; }
            public bool Matched { get; }
            public bool HasColor { get; }
            public UnityEngine.Color Color { get; }
            /// <summary>词条库 zh 名（localization 表键）；未命中时等于名字明文。用于跨源去重。</summary>
            public string LookupName { get; }

            /// <summary>有内联图标时为词条表 <c>code</c>；空 = 纯文字行。</summary>
            public string InlineCode { get; }

            public ResolvedTerm WithInlineCode(string code)
            {
                return new ResolvedTerm(
                    DisplayName,
                    Explanation,
                    Matched,
                    HasColor,
                    Color,
                    LookupName,
                    code);
            }
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

                ResolveTerm(name, catalog, out var resolved);
                result.Add(AttachInlineCode(resolved, catalog));
            }

            return result;
        }

        /// <summary>
        /// 右键详情词条装配：检查描述自动抽取（基础填充）+ 配置的额外词条（去重保序）。
        /// 额外词条按词条库解析（缺库/未命中仍返回名字行）；与描述词条重名、或自身重名时丢弃。
        /// </summary>
        public static List<ResolvedTerm> BuildInspectTerms(
            string description,
            IReadOnlyList<string> extraNames,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            var result = ExtractExplicitTerms(description, catalog);

            if (extraNames == null || extraNames.Count == 0)
            {
                return result;
            }

            // 描述词条名（去括号明文）与词条库 zh 名（localization 表键）都作为占用键。
            var occupied = new HashSet<string>(StringComparer.Ordinal);
            if (!string.IsNullOrEmpty(description))
            {
                foreach (Match match in DoubleBracketToken.Matches(description))
                {
                    var name = match.Groups[1].Value.Trim();
                    if (name.Length > 0)
                    {
                        occupied.Add(name);
                    }
                }
            }

            for (var i = 0; i < result.Count; i++)
            {
                var title = result[i].DisplayName?.Trim() ?? string.Empty;
                if (title.Length > 0)
                {
                    occupied.Add(title);
                }
            }

            for (var i = 0; i < extraNames.Count; i++)
            {
                var name = (extraNames[i] ?? string.Empty).Trim();
                if (name.Length == 0)
                {
                    continue;
                }

                ResolveTerm(name, catalog, out var resolved);
                resolved = AttachInlineCode(resolved, catalog);
                var key = resolved.LookupName.Length > 0
                    ? resolved.LookupName
                    : name;
                if (!occupied.Add(key))
                {
                    continue;
                }

                result.Add(resolved);
            }

            return result;
        }

        /// <summary>
        /// 从检查描述抽出 <c>[code]</c> 图标词条（去重保序）。先去掉 <c>[[…]]</c>，
        /// 未命中词条表或无 sprite 的语义前缀（如 <c>[使用时]</c>）丢弃。
        /// </summary>
        public static List<ResolvedTerm> ExtractInlineIconTerms(
            string description,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            var result = new List<ResolvedTerm>();
            if (string.IsNullOrEmpty(description) || catalog == null)
            {
                return result;
            }

            var stripped = DoubleBracketToken.Replace(description, " ");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var matches = Regex.Matches(stripped, @"\[([^\]]+)\]");
            for (var i = 0; i < matches.Count; i++)
            {
                var code = matches[i].Groups[1].Value.Trim();
                if (code.Length == 0
                    || CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(code)
                    || !seen.Add(code))
                {
                    continue;
                }

                if (!catalog.TryGetByCode(code, out var entry) || entry == null || !entry.HasInlineIcon)
                {
                    continue;
                }

                ResolveTerm(entry.displayNameZh, catalog, out var resolved);
                result.Add(AttachInlineCode(resolved, catalog, code));
            }

            return result;
        }

        public static void ResolveTerm(
            string name,
            CardFaceDescriptionIconCatalogSO catalog,
            out ResolvedTerm resolved)
        {
            if (catalog != null)
            {
                if (catalog.TryGetByDisplayName(name, out var entry) && entry != null)
                {
                    // 行标题与解释按当前语言取 glossary 表（键 = zh 名），缺翻译回中文（ADR-0046）。
                    var zhName = string.IsNullOrWhiteSpace(entry.displayNameZh)
                        ? name
                        : entry.displayNameZh.Trim();
                    var title = NineGrid.Core.Localization.LocalizationCatalog
                        .ResolveGlossaryDisplayName(zhName);
                    var explanation = NineGrid.Core.Localization.LocalizationCatalog
                        .ResolveGlossaryIntro(zhName, entry.explanation ?? string.Empty);
                    resolved = new ResolvedTerm(
                        title,
                        explanation,
                        matched: true,
                        hasColor: entry.HasColorOverride,
                        color: entry.color,
                        lookupName: zhName,
                        inlineCode: entry.HasInlineIcon ? entry.code.Trim() : string.Empty);
                    return;
                }

                var canonical = CardInspectGlossaryAssembler.CanonicalAttackRangeName(name);
                if (!string.IsNullOrEmpty(canonical)
                    && !string.Equals(canonical, name, StringComparison.Ordinal)
                    && catalog.TryGetByDisplayName(canonical, out var canonicalEntry)
                    && canonicalEntry != null)
                {
                    var zhName = string.IsNullOrWhiteSpace(canonicalEntry.displayNameZh)
                        ? canonical
                        : canonicalEntry.displayNameZh.Trim();
                    var title = NineGrid.Core.Localization.LocalizationCatalog
                        .ResolveGlossaryDisplayName(zhName);
                    var explanation = NineGrid.Core.Localization.LocalizationCatalog
                        .ResolveGlossaryIntro(zhName, canonicalEntry.explanation ?? string.Empty);
                    resolved = new ResolvedTerm(
                        title,
                        explanation,
                        matched: true,
                        hasColor: canonicalEntry.HasColorOverride,
                        color: canonicalEntry.color,
                        lookupName: zhName,
                        inlineCode: canonicalEntry.HasInlineIcon ? canonicalEntry.code.Trim() : string.Empty);
                    return;
                }
            }

            resolved = new ResolvedTerm(
                name,
                string.Empty,
                matched: false,
                hasColor: false,
                color: default,
                lookupName: name);
        }

        /// <summary>
        /// 卡专属首条回退：<c>[[词条]]</c> 去括号留明文；有 sprite 的 <c>[code]</c> 换成展示名；
        /// <c>[使用时]</c> 等语义前缀原样保留。
        /// </summary>
        public static string StripMarkupForReadableFallback(
            string description,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            if (string.IsNullOrEmpty(description))
            {
                return string.Empty;
            }

            var afterTerms = DoubleBracketToken.Replace(description, "$1");
            return Regex.Replace(
                afterTerms,
                @"\[([^\]]+)\]",
                match =>
                {
                    var code = match.Groups[1].Value.Trim();
                    if (code.Length == 0
                        || CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(code)
                        || catalog == null
                        || !catalog.TryGetByCode(code, out var entry)
                        || entry == null
                        || !entry.HasInlineIcon)
                    {
                        return match.Value;
                    }

                    return string.IsNullOrWhiteSpace(entry.displayNameZh)
                        ? code
                        : entry.displayNameZh.Trim();
                });
        }

        public static ResolvedTerm AttachInlineCode(
            ResolvedTerm term,
            CardFaceDescriptionIconCatalogSO catalog,
            string codeOverride = null)
        {
            if (!string.IsNullOrWhiteSpace(codeOverride))
            {
                return term.WithInlineCode(codeOverride.Trim());
            }

            if (!string.IsNullOrWhiteSpace(term.InlineCode) || catalog == null)
            {
                return term;
            }

            var key = !string.IsNullOrWhiteSpace(term.LookupName) ? term.LookupName : term.DisplayName;
            if (catalog.TryGetByDisplayName(key, out var entry)
                && entry != null
                && entry.HasInlineIcon)
            {
                return term.WithInlineCode(entry.code.Trim());
            }

            return term;
        }
    }
}
