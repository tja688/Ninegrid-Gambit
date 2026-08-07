using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NineGrid.Content;
using NineGrid.Content.CardPresentation;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 卡面 `{参数}` 插值：用装配实参填充人手写概括；填初始配置值，不接战中数值管线。
    /// token 语法：
    /// - `{value}`：简单式，取「第一个含该键的装配」的值（同键跨装配异值时语义不唯一，仅兼容旧文案）。
    /// - `{装配id.value}` / `{模板id.value}`：限定式，精确定位某个装配（id 或 templateId 精确匹配）。
    /// - `{卡defId.value}`：限定式前缀匹配（装配 id / templateId 以 `defId.` 开头）；
    ///   所有命中装配该键取值一致才填，不一致视为歧义保留字面量（作者应改写成精确装配限定式）。
    /// </summary>
    public static class CardFaceDescriptionParamFiller
    {
        private static readonly Regex ParamToken =
            new Regex(@"\{([A-Za-z_][A-Za-z0-9_.]*)\}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly struct AssemblyArgs
        {
            public AssemblyArgs(string id, string templateId, IReadOnlyDictionary<string, object> args)
            {
                Id = id ?? string.Empty;
                TemplateId = templateId ?? string.Empty;
                Args = args;
            }

            public string Id { get; }
            public string TemplateId { get; }
            public IReadOnlyDictionary<string, object> Args { get; }
        }

        public static string FillFromAssemblies(string description, EffectAssemblyDto[] assemblies)
        {
            return FillFromAssemblies(description, assemblies, remainingOverrides: null);
        }

        /// <summary>
        /// 投影缝填充（ADR-0035 / #155）：装配实参填初始配置值；<paramref name="remainingOverrides"/>
        /// 是已提交的卡面投影值（Settled 倒计时剩余，键为完整 <c>装配id.键</c>），命中时优先于初始实参。
        /// 键不命中的令牌仍回退装配初始实参。
        /// </summary>
        public static string FillFromAssemblies(
            string description,
            EffectAssemblyDto[] assemblies,
            IReadOnlyDictionary<string, string> remainingOverrides)
        {
            if (string.IsNullOrEmpty(description))
            {
                return description ?? string.Empty;
            }

            if (assemblies == null || assemblies.Length == 0)
            {
                return description;
            }

            var items = new List<AssemblyArgs>(assemblies.Length);
            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                items.Add(new AssemblyArgs(
                    assembly.id,
                    assembly.templateId,
                    EffectAssemblyResolver.ParseArgsJson(assembly.argsJson)));
            }

            return FillFromAssemblyItems(description, items, remainingOverrides);
        }

        public static string Fill(string description, IReadOnlyDictionary<string, object> args)
        {
            if (string.IsNullOrEmpty(description) || args == null || args.Count == 0)
            {
                return description ?? string.Empty;
            }

            return ParamToken.Replace(description, match =>
            {
                var token = match.Groups[1].Value;
                // 限定式（含点）在无装配上下文时一律保留字面量，不拆键兜底。
                if (token.IndexOf('.') >= 0)
                {
                    return match.Value;
                }

                if (!TryGetArg(args, token, out var value) || value == null)
                {
                    return match.Value;
                }

                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? match.Value;
            });
        }

        private static string FillFromAssemblyItems(
            string description,
            IReadOnlyList<AssemblyArgs> items,
            IReadOnlyDictionary<string, string> remainingOverrides)
        {
            return ParamToken.Replace(description, match =>
            {
                var token = match.Groups[1].Value;
                var lastDot = token.LastIndexOf('.');
                if (lastDot < 0)
                {
                    // 简单式（旧行为）：第一个含该键的装配。
                    for (var i = 0; i < items.Count; i++)
                    {
                        var args = items[i].Args;
                        if (args != null && TryGetArg(args, token, out var value) && value != null)
                        {
                            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? match.Value;
                        }
                    }

                    return match.Value;
                }

                var qualifier = token.Substring(0, lastDot);
                var key = token.Substring(lastDot + 1);
                if (string.IsNullOrEmpty(qualifier) || string.IsNullOrEmpty(key))
                {
                    return match.Value;
                }

                // 已提交投影值（Settled 倒计时剩余）优先于初始装配实参；键为完整「装配id.键」。
                if (TryGetRemaining(remainingOverrides, token, out var remaining))
                {
                    return remaining;
                }

                return ResolveQualified(items, qualifier, key, match.Value);
            });
        }

        private static bool TryGetRemaining(
            IReadOnlyDictionary<string, string> remainingOverrides,
            string fullToken,
            out string value)
        {
            if (remainingOverrides != null && remainingOverrides.TryGetValue(fullToken, out value))
            {
                return true;
            }

            if (remainingOverrides != null)
            {
                foreach (var pair in remainingOverrides)
                {
                    if (string.Equals(pair.Key, fullToken, StringComparison.OrdinalIgnoreCase))
                    {
                        value = pair.Value;
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// 限定式解析：精确 id / 精确 templateId → 前缀 id / 前缀 templateId 的装配集合。
        /// 命中装配中「含该键者」的取值必须全部一致，否则视为歧义保留原样。
        /// </summary>
        private static string ResolveQualified(
            IReadOnlyList<AssemblyArgs> items,
            string qualifier,
            string key,
            string fallback)
        {
            var prefix = qualifier + ".";
            string resolved = null;
            var found = false;
            var ambiguous = false;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var matches = string.Equals(item.Id, qualifier, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(item.TemplateId, qualifier, StringComparison.OrdinalIgnoreCase)
                              || (!string.IsNullOrEmpty(item.Id)
                                  && item.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                              || (!string.IsNullOrEmpty(item.TemplateId)
                                  && item.TemplateId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (!matches)
                {
                    continue;
                }

                if (item.Args == null || !TryGetArg(item.Args, key, out var value) || value == null)
                {
                    continue;
                }

                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                if (!found)
                {
                    resolved = text;
                    found = true;
                }
                else if (!string.Equals(resolved, text, StringComparison.Ordinal))
                {
                    ambiguous = true;
                    break;
                }
            }

            return found && !ambiguous ? resolved : fallback;
        }

        private static bool TryGetArg(
            IReadOnlyDictionary<string, object> args,
            string key,
            out object value)
        {
            if (args.TryGetValue(key, out value))
            {
                return true;
            }

            foreach (var pair in args)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }
}
