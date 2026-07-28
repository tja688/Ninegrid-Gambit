using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NineGrid.Content
{
    /// <summary>
    /// 把模板 <c>design_text</c> 里与装配实参对应的「预设字面量」换成 <c>{param}</c>，
    /// 避免编辑器下拉/自动描述被 peer 默认数值误导；卡面仍可由
    /// Presentation 层按 argsJson 填回。
    /// </summary>
    public static class EffectDesignTextParameterizer
    {
        private static readonly string[] ChineseDigits =
        {
            null, "一", "二", "三", "四", "五", "六", "七", "八", "九", "十",
        };

        /// <summary>
        /// 用 body 占位符 + 实参，把 design_text 中对应字面量换成 <c>{name}</c>。
        /// 已是 <c>{name}</c> 的片段保持不变；标识类实参（reason/cause 等）不替换。
        /// 实参对不上时，按占位顺序吃掉文案里剩余的数字字面量。
        /// </summary>
        public static string Parameterize(
            string designText,
            string bodyJson,
            IReadOnlyDictionary<string, object> args)
        {
            if (string.IsNullOrWhiteSpace(designText))
            {
                return designText ?? string.Empty;
            }

            var text = designText.Trim();
            var names = EffectAssemblyResolver.ExtractPlaceholderNames(bodyJson);
            if (names.Count == 0)
            {
                return text;
            }

            var pendingNumeric = new List<string>();
            var replacements = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < names.Count; i++)
            {
                var name = names[i];
                if (string.IsNullOrEmpty(name) || IsOpaqueArgKey(name))
                {
                    continue;
                }

                var token = "{" + name + "}";
                if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                if (!IsNumericArgKey(name))
                {
                    continue;
                }

                if (args != null
                    && TryGetArg(args, name, out var value)
                    && value != null
                    && !IsDefaultNumericPlaceholder(value))
                {
                    AddLiteralCandidates(replacements, value, token);
                }
                else
                {
                    pendingNumeric.Add(name);
                }
            }

            replacements.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            for (var i = 0; i < replacements.Count; i++)
            {
                var next = ReplaceFirstOutsideBraces(text, replacements[i].Key, replacements[i].Value);
                if (!string.Equals(next, text, StringComparison.Ordinal))
                {
                    text = next;
                }
                else
                {
                    var name = UnwrapToken(replacements[i].Value);
                    if (!string.IsNullOrEmpty(name) && !pendingNumeric.Contains(name))
                    {
                        pendingNumeric.Add(name);
                    }
                }
            }

            for (var i = 0; i < pendingNumeric.Count; i++)
            {
                var name = pendingNumeric[i];
                var token = "{" + name + "}";
                if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                if (!TryFindNextNumberLiteral(text, out var start, out var length))
                {
                    break;
                }

                text = text.Substring(0, start) + token + text.Substring(start + length);
            }

            return text;
        }

        public static string Parameterize(
            string designText,
            string bodyJson,
            string argsJson)
        {
            return Parameterize(designText, bodyJson, EffectAssemblyResolver.ParseArgsJson(argsJson));
        }

        /// <summary>多条装配简要描述拼接（自动卡面描述用）。</summary>
        public static string JoinBriefs(IReadOnlyList<string> briefs)
        {
            if (briefs == null || briefs.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < briefs.Count; i++)
            {
                var brief = briefs[i];
                if (string.IsNullOrWhiteSpace(brief))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append('；');
                }

                builder.Append(brief.Trim());
            }

            return builder.ToString();
        }

        public static bool IsOpaqueArgKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return true;
            }

            return string.Equals(key, "reason", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "cause", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "source", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "sourceAction", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "actor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "defId", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "kind", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "zone", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "targetKind", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "stat", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "op", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "atom", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "typeTag", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "verb", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "containerType", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNumericArgKey(string key)
        {
            return string.Equals(key, "value", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "amount", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "delta", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "count", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "weight", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "every", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "threshold", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "min", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "max", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "slot", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDefaultNumericPlaceholder(object value)
        {
            if (value is string s)
            {
                return string.IsNullOrEmpty(s)
                    || string.Equals(s, "0", StringComparison.Ordinal);
            }

            return TryToInt(value, out var n) && n == 0;
        }

        private static string UnwrapToken(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length < 3 || token[0] != '{' || token[token.Length - 1] != '}')
            {
                return string.Empty;
            }

            return token.Substring(1, token.Length - 2);
        }

        private static bool TryGetArg(
            IReadOnlyDictionary<string, object> args,
            string name,
            out object value)
        {
            if (args.TryGetValue(name, out value))
            {
                return true;
            }

            foreach (var pair in args)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static void AddLiteralCandidates(
            List<KeyValuePair<string, string>> replacements,
            object value,
            string token)
        {
            var arabic = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(arabic))
            {
                replacements.Add(new KeyValuePair<string, string>(arabic, token));
            }

            if (!TryToInt(value, out var number) || number < 1 || number > 10)
            {
                return;
            }

            var cn = ChineseDigits[number];
            if (!string.IsNullOrEmpty(cn))
            {
                replacements.Add(new KeyValuePair<string, string>(cn, token));
            }

            if (number == 2)
            {
                replacements.Add(new KeyValuePair<string, string>("两", token));
            }
        }

        private static bool TryToInt(object value, out int number)
        {
            number = 0;
            if (value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong)
            {
                number = Convert.ToInt32(value, CultureInfo.InvariantCulture);
                return true;
            }

            if (value is float || value is double || value is decimal)
            {
                var d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (Math.Abs(d - Math.Round(d)) > 0.0001)
                {
                    return false;
                }

                number = (int)Math.Round(d);
                return true;
            }

            return value is string s
                && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out number);
        }

        private static bool TryFindNextNumberLiteral(string text, out int start, out int length)
        {
            start = -1;
            length = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            // 优先阿拉伯数字，避免把「一张/格三」等量词误当成 {amount}。
            if (TryFindNextArabicLiteral(text, out start, out length))
            {
                return true;
            }

            return TryFindNextChineseQuantityLiteral(text, out start, out length);
        }

        private static bool TryFindNextArabicLiteral(string text, out int start, out int length)
        {
            start = -1;
            length = 0;
            var depth = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}' && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (depth > 0 || !char.IsDigit(c))
                {
                    continue;
                }

                var end = i + 1;
                while (end < text.Length && char.IsDigit(text[end]))
                {
                    end++;
                }

                start = i;
                length = end - i;
                return true;
            }

            return false;
        }

        /// <summary>仅匹配「两点/五金币」这类数量用法，跳过「一张/格二」。</summary>
        private static bool TryFindNextChineseQuantityLiteral(string text, out int start, out int length)
        {
            start = -1;
            length = 0;
            var depth = 0;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}' && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (depth > 0)
                {
                    continue;
                }

                string matched = null;
                if (text[i] == '两')
                {
                    matched = "两";
                }
                else
                {
                    for (var n = 10; n >= 1; n--)
                    {
                        var cn = ChineseDigits[n];
                        if (!string.IsNullOrEmpty(cn)
                            && string.CompareOrdinal(text, i, cn, 0, cn.Length) == 0)
                        {
                            matched = cn;
                            break;
                        }
                    }
                }

                if (matched == null)
                {
                    continue;
                }

                var after = i + matched.Length;
                if (after >= text.Length || !IsQuantityUnit(text[after]))
                {
                    continue;
                }

                start = i;
                length = matched.Length;
                return true;
            }

            return false;
        }

        private static bool IsQuantityUnit(char c)
        {
            return c == '点' || c == '金' || c == '次' || c == '层'
                || c == '张' || c == '个' || c == '枚';
        }

        /// <summary>只替换不在 <c>{...}</c> 内的首次字面量，避免破坏已有占位符。</summary>
        private static string ReplaceFirstOutsideBraces(string text, string literal, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(literal))
            {
                return text;
            }

            var depth = 0;
            for (var i = 0; i <= text.Length - literal.Length; i++)
            {
                var c = text[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}' && depth > 0)
                {
                    depth--;
                    continue;
                }

                if (depth > 0)
                {
                    continue;
                }

                if (string.CompareOrdinal(text, i, literal, 0, literal.Length) != 0)
                {
                    continue;
                }

                return text.Substring(0, i) + token + text.Substring(i + literal.Length);
            }

            return text;
        }
    }
}
