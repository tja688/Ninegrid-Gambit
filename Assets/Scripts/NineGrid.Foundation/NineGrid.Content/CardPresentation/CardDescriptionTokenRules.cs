using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NineGrid.Content;

namespace NineGrid.Content.CardPresentation
{
    /// <summary>
    /// ADR-0035：卡面描述投影的令牌契约与描述格计量（静态通路 / #154）。
    /// 检查描述与卡面介绍里的玩家可见数值一律 <c>{装配id.键}</c>
    /// （限定符必须精确等于某条装配的 id）；简单式 <c>{value}</c>/<c>{amount}</c> 与
    /// <c>{卡defId.键}</c> 前缀式在范围内卡（机关 / 遗物 / 道具）上为错误。
    /// 描述格：普通字符、每个 <c>{…}</c>、每个 <c>[…]</c>、每个 <c>[[…]]</c> 各算 1 格；硬上限 26。
    /// 校验是纯函数，供内容卫生校验（磁盘）与 EditMode 边界测试共用。
    /// </summary>
    public static class CardDescriptionTokenRules
    {
        /// <summary>描述格硬上限（ADR-0035 #5）。</summary>
        public const int MaxUnits = 26;

        private static readonly Regex ParamToken =
            new Regex(@"\{([A-Za-z_][A-Za-z0-9_.]*)\}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>本契约约束的范围：真实接线的机关 / 遗物 / 道具卡。</summary>
        public static bool IsInScopeKind(string kind)
        {
            return string.Equals(kind, "Trap", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "Relic", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kind, "HelpCard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>归档弃用内容不迁移、不受本契约约束（ADR-0035 #6）。</summary>
        public static bool IsArchivedDeck(string deckId)
        {
            if (string.IsNullOrWhiteSpace(deckId))
            {
                return false;
            }

            var deck = deckId.Trim();
            return string.Equals(deck, "deck.relic_archive", StringComparison.OrdinalIgnoreCase)
                || string.Equals(deck, "deck.help_archive", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 描述格计数：普通字符各 1 格；每个 <c>{…}</c> / <c>[…]</c> / <c>[[…]]</c> 各 1 格。
        /// <c>[[…]]</c> 优先于单括号；未闭合的 <c>{</c> / <c>[</c> 按 1 格计并继续。
        /// </summary>
        public static int CountUnits(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            var units = 0;
            var index = 0;
            while (index < text.Length)
            {
                var c = text[index];
                if (c == '{')
                {
                    var end = text.IndexOf('}', index + 1);
                    if (end >= 0)
                    {
                        index = end + 1;
                        units++;
                        continue;
                    }
                }
                else if (c == '[')
                {
                    // [[展示名]] 整块计 1 格（ADR-0037）。
                    if (index + 1 < text.Length && text[index + 1] == '[')
                    {
                        var close = text.IndexOf("]]", index + 2, StringComparison.Ordinal);
                        if (close >= 0)
                        {
                            index = close + 2;
                            units++;
                            continue;
                        }
                    }

                    var end = text.IndexOf(']', index + 1);
                    if (end >= 0)
                    {
                        index = end + 1;
                        units++;
                        continue;
                    }
                }

                units++;
                index++;
            }

            return units;
        }

        /// <summary>
        /// 单卡校验（ADR-0035 静态通路契约，#154 / #155）。范围外卡（非机关/遗物/道具，或归档卡组）恒返回空；
        /// 范围内卡报告：装配缺稳定 id、简单式 / defId 前缀式 / templateId 限定式令牌、令牌键不存在、
        /// 检查描述 / 介绍超 26 格。
        /// 倒计时投影契约（#156）：装配实参 <c>projectKey</c>（投影令牌键）必须等于「本装配id.键」限定式；
        /// 剩余改走机关 ActionCount 槽 / 遗物栏计数，不再要求局内描述模板。
        /// </summary>
        public static List<string> ValidateCard(CardPresentationConfigDto dto)
        {
            var errors = new List<string>();
            if (dto == null || !IsInScopeKind(dto.kind) || IsArchivedDeck(dto.deckId))
            {
                return errors;
            }

            var assemblies = dto.effectAssemblies;
            if (assemblies != null)
            {
                for (var i = 0; i < assemblies.Length; i++)
                {
                    var assembly = assemblies[i];
                    if (assembly == null || string.IsNullOrWhiteSpace(assembly.id))
                    {
                        errors.Add("assembly[" + i + "] missing id（描述契约要求装配有稳定 id）");
                        continue;
                    }

                    ValidateProjectKey(errors, "assembly[" + i + "]", assembly);
                }
            }

            ValidateText(errors, "description", dto.description, assemblies);
            ValidateText(errors, "faceIntro", dto.faceIntro, assemblies);

            return errors;
        }

        /// <summary>
        /// 装配实参 projectKey 契约：非空时必须等于「本装配id.键」限定式；合法键登记供倒计时 UI 路由。
        /// </summary>
        private static void ValidateProjectKey(
            List<string> errors,
            string prefix,
            EffectAssemblyDto assembly)
        {
            var args = EffectAssemblyResolver.ParseArgsJson(assembly.argsJson);
            if (!args.TryGetValue("projectKey", out var raw) || raw == null)
            {
                return;
            }

            var projectKey = Convert.ToString(raw).Trim();
            if (projectKey.Length == 0)
            {
                return;
            }

            var lastDot = projectKey.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= projectKey.Length - 1)
            {
                errors.Add(prefix + " projectKey 必须为「装配id.键」限定式，实际 {" + projectKey + "}");
                return;
            }

            var qualifier = projectKey.Substring(0, lastDot);
            if (!string.Equals(qualifier, assembly.id.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(prefix + " projectKey 限定符必须等于本装配 id（{" + projectKey + "} ≠ " + assembly.id + "）");
            }
        }

        private static void ValidateText(
            List<string> errors,
            string field,
            string text,
            EffectAssemblyDto[] assemblies)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            foreach (Match match in ParamToken.Matches(text))
            {
                var token = match.Groups[1].Value;
                var lastDot = token.LastIndexOf('.');
                if (lastDot < 0)
                {
                    errors.Add(field + " 简单式令牌 {" + token + "}：须 {装配id.键} 限定式");
                    continue;
                }

                var qualifier = token.Substring(0, lastDot);
                var key = token.Substring(lastDot + 1);
                if (string.IsNullOrEmpty(qualifier))
                {
                    errors.Add(field + " 令牌 {" + token + "} 限定符为空");
                    continue;
                }

                if (string.IsNullOrEmpty(key))
                {
                    errors.Add(field + " 令牌 {" + token + "} 键为空");
                    continue;
                }

                var assembly = FindAssemblyById(assemblies, qualifier);
                if (assembly == null)
                {
                    errors.Add(field + " 令牌 {" + token + "} 限定符不是本卡装配 id（简单式/defId 前缀/templateId 限定退役）");
                    continue;
                }

                if (!HasArgKey(assembly, key))
                {
                    errors.Add(field + " 令牌 {" + token + "} 键 " + key + " 不在装配 " + qualifier + " 实参中");
                }
            }

            var units = CountUnits(text);
            if (units > MaxUnits)
            {
                errors.Add(field + " 超描述格 " + MaxUnits + "（实际 " + units + "）");
            }
        }

        private static EffectAssemblyDto FindAssemblyById(EffectAssemblyDto[] assemblies, string qualifier)
        {
            if (assemblies == null || assemblies.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (assembly != null
                    && !string.IsNullOrWhiteSpace(assembly.id)
                    && string.Equals(assembly.id.Trim(), qualifier, StringComparison.OrdinalIgnoreCase))
                {
                    return assembly;
                }
            }

            return null;
        }

        private static bool HasArgKey(EffectAssemblyDto assembly, string key)
        {
            var args = EffectAssemblyResolver.ParseArgsJson(assembly.argsJson);
            foreach (var pair in args)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
