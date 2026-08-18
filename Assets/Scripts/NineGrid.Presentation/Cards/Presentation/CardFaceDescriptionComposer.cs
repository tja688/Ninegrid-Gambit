using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NineGrid.Cards.Slots;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 旁路纯数据 seam：检查描述双语法 → TMP 富文本。
    /// <c>[[展示名]]</c> → 去括号明文（可选着色）；<c>[code]</c> → 内联 sprite
    ///（装配 Insertable 优先，其余走词条表）。普通语义单括号（如 <c>[使用时]</c>）原样保留。
    /// </summary>
    public static class CardFaceDescriptionComposer
    {
        private static readonly Regex DoubleBracketToken =
            new Regex(@"\[\[([^\]]+)\]\]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex BracketToken =
            new Regex(@"\[([^\]]+)\]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex SpriteTagToken =
            new Regex(@"<sprite\s+name=""([^""]+)""[^>]*>", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public readonly struct InlineIcon
        {
            public InlineIcon(string slotCode, Sprite sprite)
            {
                SlotCode = slotCode ?? string.Empty;
                Sprite = sprite;
            }

            public string SlotCode { get; }
            public Sprite Sprite { get; }
        }

        public readonly struct Result
        {
            public Result(
                string tmpRichText,
                IReadOnlyList<InlineIcon> icons,
                IReadOnlyList<CardGlossaryTerms.ResolvedTerm> explicitTerms)
            {
                TmpRichText = tmpRichText ?? string.Empty;
                Icons = icons ?? Array.Empty<InlineIcon>();
                ExplicitTerms = explicitTerms ?? Array.Empty<CardGlossaryTerms.ResolvedTerm>();
            }

            public string TmpRichText { get; }
            public IReadOnlyList<InlineIcon> Icons { get; }
            public IReadOnlyList<CardGlossaryTerms.ResolvedTerm> ExplicitTerms { get; }
        }

        public static Result Compose(
            string basicDescription,
            IReadOnlyDictionary<string, Sprite> assembledIcons,
            CardFaceSlotRegistrySO registry)
        {
            return Compose(basicDescription, assembledIcons, registry, catalog: null);
        }

        public static Result Compose(
            string basicDescription,
            IReadOnlyDictionary<string, Sprite> assembledIcons,
            CardFaceSlotRegistrySO registry,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            if (string.IsNullOrWhiteSpace(basicDescription))
            {
                return new Result(
                    string.Empty,
                    Array.Empty<InlineIcon>(),
                    Array.Empty<CardGlossaryTerms.ResolvedTerm>());
            }

            var explicitTerms = CardGlossaryTerms.ExtractExplicitTerms(basicDescription, catalog);
            var afterTerms = ExpandDoubleBrackets(basicDescription, catalog);
            var icons = new List<InlineIcon>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var builder = new StringBuilder(afterTerms.Length + 16);
            var offset = 0;
            foreach (Match match in BracketToken.Matches(afterTerms))
            {
                builder.Append(afterTerms, offset, match.Index - offset);
                var code = match.Groups[1].Value;
                if (TryResolve(code, assembledIcons, registry, catalog, out var sprite))
                {
                    builder.Append("<sprite name=\"").Append(code).Append("\">");
                    if (seen.Add(code))
                    {
                        icons.Add(new InlineIcon(code, sprite));
                    }
                }
                else
                {
                    builder.Append(match.Value);
                }

                offset = match.Index + match.Length;
            }

            builder.Append(afterTerms, offset, afterTerms.Length - offset);

            // 补充扫描文本中已有的 <sprite name="..."> 标签，保证外部预转译文案也能提取图集
            foreach (Match match in SpriteTagToken.Matches(afterTerms))
            {
                var code = match.Groups[1].Value;
                if (seen.Add(code) && TryResolve(code, assembledIcons, registry, catalog, out var sprite))
                {
                    icons.Add(new InlineIcon(code, sprite));
                }
            }

            return new Result(builder.ToString(), icons, explicitTerms);
        }

        /// <summary>
        /// 将 <c>[[名字]]</c> 换成去括号明文（命中词条且有色则包 TMP color）。
        /// </summary>
        private static string ExpandDoubleBrackets(
            string text,
            CardFaceDescriptionIconCatalogSO catalog)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("[[", StringComparison.Ordinal) < 0)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length + 16);
            var offset = 0;
            foreach (Match match in DoubleBracketToken.Matches(text))
            {
                builder.Append(text, offset, match.Index - offset);
                var name = match.Groups[1].Value.Trim();
                if (catalog != null
                    && catalog.TryGetByDisplayName(name, out var entry)
                    && entry != null)
                {
                    // 显示名按当前语言取 glossary 表（键 = zh 名），缺翻译回中文（ADR-0046）。
                    var display = string.IsNullOrWhiteSpace(entry.displayNameZh)
                        ? name
                        : NineGrid.Core.Localization.LocalizationCatalog
                            .ResolveGlossaryDisplayName(entry.displayNameZh.Trim());
                    if (entry.HasColorOverride)
                    {
                        builder.Append("<color=#")
                            .Append(ColorUtility.ToHtmlStringRGBA(entry.color))
                            .Append('>')
                            .Append(display)
                            .Append("</color>");
                    }
                    else
                    {
                        builder.Append(display);
                    }
                }
                else
                {
                    if (catalog != null && !string.IsNullOrEmpty(name))
                    {
                        Debug.LogWarning(
                            "[CardFaceDescriptionComposer] 未命中词条名字 [[" + name + "]]，已去括号显示原文。");
                    }

                    builder.Append(name);
                }

                offset = match.Index + match.Length;
            }

            builder.Append(text, offset, text.Length - offset);
            return builder.ToString();
        }

        private static bool TryResolve(
            string slotCode,
            IReadOnlyDictionary<string, Sprite> assembledIcons,
            CardFaceSlotRegistrySO registry,
            CardFaceDescriptionIconCatalogSO catalog,
            out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(slotCode))
            {
                return false;
            }

            // 装配 Insertable：只看高层 assembled，catalog 不可反向覆盖。
            if (registry != null
                && registry.TryGet(slotCode, out var definition)
                && definition != null
                && definition.HasRole(CardFaceSlotRole.InsertableInDescription))
            {
                if (assembledIcons == null
                    || !assembledIcons.TryGetValue(slotCode, out sprite)
                    || sprite == null)
                {
                    sprite = null;
                    return false;
                }

                return true;
            }

            // 描述专用表：自定义代号；保留装配槽名时 TryGet 会拒绝。
            if (catalog != null && catalog.TryGet(slotCode, out sprite) && sprite != null)
            {
                return true;
            }

            sprite = null;
            return false;
        }
    }
}
