using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 详情合成 MVP：人手概括 + 从文案中出现的词条代号自动展开解释（与卡面图标同源 catalog）。
    /// </summary>
    public static class CardDetailDescriptionComposer
    {
        private static readonly Regex BracketToken =
            new Regex(@"\[([^\]]+)\]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string Compose(string summary, CardFaceDescriptionIconCatalogSO glossary)
        {
            var body = summary ?? string.Empty;
            if (glossary == null)
            {
                return body;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var expansions = new List<string>();
            foreach (Match match in BracketToken.Matches(body))
            {
                var code = match.Groups[1].Value;
                if (string.IsNullOrEmpty(code)
                    || !seen.Add(code)
                    || CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(code))
                {
                    continue;
                }

                if (!glossary.TryGetEntry(code, out var entry) || entry == null)
                {
                    continue;
                }

                var name = string.IsNullOrWhiteSpace(entry.displayNameZh) ? code : entry.displayNameZh.Trim();
                var explanation = entry.explanation ?? string.Empty;
                if (string.IsNullOrWhiteSpace(explanation))
                {
                    expansions.Add(name);
                }
                else
                {
                    expansions.Add(name + "：" + explanation.Trim());
                }
            }

            if (expansions.Count == 0)
            {
                return body;
            }

            var builder = new StringBuilder(body.Length + 64);
            builder.Append(body);
            if (!string.IsNullOrWhiteSpace(body))
            {
                builder.Append("\n\n");
            }

            builder.Append("词条");
            for (var i = 0; i < expansions.Count; i++)
            {
                builder.Append('\n').Append("· ").Append(expansions[i]);
            }

            return builder.ToString();
        }
    }
}
