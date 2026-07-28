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
    /// </summary>
    public static class CardFaceDescriptionParamFiller
    {
        private static readonly Regex ParamToken =
            new Regex(@"\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string FillFromAssemblies(string description, EffectAssemblyDto[] assemblies)
        {
            if (string.IsNullOrEmpty(description) || assemblies == null || assemblies.Length == 0)
            {
                return description ?? string.Empty;
            }

            var args = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
                if (assembly == null)
                {
                    continue;
                }

                foreach (var pair in EffectAssemblyResolver.ParseArgsJson(assembly.argsJson))
                {
                    if (!args.ContainsKey(pair.Key))
                    {
                        args[pair.Key] = pair.Value;
                    }
                }
            }

            return Fill(description, args);
        }

        public static string Fill(string description, IReadOnlyDictionary<string, object> args)
        {
            if (string.IsNullOrEmpty(description) || args == null || args.Count == 0)
            {
                return description ?? string.Empty;
            }

            return ParamToken.Replace(description, match =>
            {
                var key = match.Groups[1].Value;
                if (!args.TryGetValue(key, out var value) || value == null)
                {
                    return match.Value;
                }

                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? match.Value;
            });
        }
    }
}
