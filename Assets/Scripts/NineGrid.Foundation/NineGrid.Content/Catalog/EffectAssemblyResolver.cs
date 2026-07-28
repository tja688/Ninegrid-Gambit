using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;

namespace NineGrid.Content
{
    /// <summary>
    /// 效果参数化模板（ADR-0009 / #70）。不绑定具体卡；卡上经装配引用给实参。
    /// </summary>
    public sealed class EffectTemplateDefinition
    {
        private readonly string[] mRequires;
        private readonly string[] mConditions;

        public EffectTemplateDefinition(
            string id,
            IReadOnlyList<string> requires,
            IReadOnlyList<string> conditions,
            string bodyJson,
            ContentImplementationState state,
            string designText)
        {
            Id = id ?? string.Empty;
            mRequires = CopyTokens(requires);
            mConditions = CopyTokens(conditions);
            BodyJson = bodyJson ?? string.Empty;
            State = state;
            DesignText = designText ?? string.Empty;
        }

        public string Id { get; private set; }
        public IReadOnlyList<string> Requires { get { return mRequires; } }
        /// <summary>玩法条件自陈（供 #72）；通常与 body 内 conditions 对齐。</summary>
        public IReadOnlyList<string> Conditions { get { return mConditions; } }
        public string BodyJson { get; private set; }
        public ContentImplementationState State { get; private set; }
        public string DesignText { get; private set; }

        private static string[] CopyTokens(IReadOnlyList<string> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<string>();
            }

            var copy = new string[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                copy[i] = source[i] ?? string.Empty;
            }

            return copy;
        }
    }

    /// <summary>
    /// 将模板 + 实参解析为可执行的 <see cref="ContentEffectDefinition"/>（喂给既有 EffectSystem）。
    /// 占位符形如 <c>{{amount}}</c>；无模板默认值间接层。
    /// </summary>
    public static class EffectAssemblyResolver
    {
        public static ContentEffectDefinition Resolve(
            EffectTemplateDefinition template,
            string mountId,
            EffectContainerType containerType,
            IReadOnlyDictionary<string, object> args)
        {
            if (template == null)
            {
                throw new ArgumentNullException("template");
            }

            if (string.IsNullOrEmpty(mountId))
            {
                throw new ArgumentException("mountId is required.", "mountId");
            }

            if (containerType == EffectContainerType.Unknown)
            {
                throw new ArgumentException("containerType is required.", "containerType");
            }

            var substituted = SubstitutePlaceholders(template.BodyJson, args);
            if (HasUnresolvedPlaceholders(substituted))
            {
                throw new FormatException(
                    "Effect assembly missing args for placeholders in template "
                    + template.Id
                    + " (mountId="
                    + mountId
                    + "). args must fill every {{name}} in the body.");
            }

            var body = EffectJson.Parse(substituted);
            var bodyObj = body != null ? body.RawValue as Dictionary<string, object> : null;
            if (bodyObj == null)
            {
                throw new FormatException("Effect template body must be a JSON object: " + template.Id);
            }

            var root = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in bodyObj)
            {
                if (IsIdentityKey(pair.Key))
                {
                    continue;
                }

                root[pair.Key] = CloneJsonValue(pair.Value);
            }

            root["id"] = mountId;
            root["containerType"] = containerType.ToString();
            root["requires"] = ToObjectList(template.Requires);

            var json = EffectJsonWriter.Write(root);
            return new ContentEffectDefinition(
                mountId,
                containerType,
                json,
                template.State,
                template.DesignText);
        }

        public static IReadOnlyDictionary<string, object> ParseArgsJson(string argsJson)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(argsJson))
            {
                return result;
            }

            var node = EffectJson.Parse(argsJson);
            var obj = node != null ? node.RawValue as Dictionary<string, object> : null;
            if (obj == null)
            {
                return result;
            }

            foreach (var pair in obj)
            {
                result[pair.Key] = pair.Value;
            }

            return result;
        }

        /// <summary>
        /// 模板 body 中尚未被实参替换的 <c>{{name}}</c> 占位符名（去重、保序）。
        /// </summary>
        public static List<string> ExtractPlaceholderNames(string bodyJson)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(bodyJson))
            {
                return names;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            while (index < bodyJson.Length)
            {
                var open = bodyJson.IndexOf("{{", index, StringComparison.Ordinal);
                if (open < 0)
                {
                    break;
                }

                var close = bodyJson.IndexOf("}}", open + 2, StringComparison.Ordinal);
                if (close < 0)
                {
                    break;
                }

                var name = bodyJson.Substring(open + 2, close - open - 2).Trim();
                if (!string.IsNullOrEmpty(name) && seen.Add(name))
                {
                    names.Add(name);
                }

                index = close + 2;
            }

            return names;
        }

        public static bool HasUnresolvedPlaceholders(string json)
        {
            return !string.IsNullOrEmpty(json) && json.IndexOf("{{", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// 装配 UI 用：优先复用同模板既有实参；否则按占位符名生成最小 JSON（数值键默认 0）。
        /// </summary>
        public static string SuggestArgsJson(string bodyJson, string peerArgsJson)
        {
            if (!string.IsNullOrWhiteSpace(peerArgsJson))
            {
                var trimmed = peerArgsJson.Trim();
                if (trimmed.Length > 2 && trimmed != "{}")
                {
                    return trimmed;
                }
            }

            var names = ExtractPlaceholderNames(bodyJson);
            if (names.Count == 0)
            {
                return "{}";
            }

            var builder = new StringBuilder();
            builder.Append('{');
            for (var i = 0; i < names.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var key = names[i];
                builder.Append('"').Append(Escape(key)).Append('"').Append(':');
                builder.Append(IsNumericArgKey(key) ? "0" : "\"\"");
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static bool IsNumericArgKey(string key)
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

        private static object CloneJsonValue(object value)
        {
            var dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in dict)
                {
                    copy[pair.Key] = CloneJsonValue(pair.Value);
                }

                return copy;
            }

            var list = value as List<object>;
            if (list != null)
            {
                var copy = new List<object>(list.Count);
                for (var i = 0; i < list.Count; i++)
                {
                    copy.Add(CloneJsonValue(list[i]));
                }

                return copy;
            }

            return value;
        }

        private static string SubstitutePlaceholders(string bodyJson, IReadOnlyDictionary<string, object> args)
        {
            if (string.IsNullOrEmpty(bodyJson) || args == null || args.Count == 0)
            {
                return bodyJson ?? string.Empty;
            }

            var text = bodyJson;
            foreach (var pair in args)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                var token = "{{" + pair.Key + "}}";
                var quoted = "\"" + token + "\"";
                var literal = FormatJsonLiteral(pair.Value);
                // Prefer replacing JSON string placeholders with typed literals.
                text = text.Replace(quoted, literal);
                text = text.Replace(token, StripQuotes(literal));
            }

            return text;
        }

        private static string FormatJsonLiteral(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            if (value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (value is float || value is double || value is decimal)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return "\"" + Escape((value as string) ?? Convert.ToString(value, CultureInfo.InvariantCulture)) + "\"";
        }

        private static string StripQuotes(string literal)
        {
            if (literal != null && literal.Length >= 2 && literal[0] == '"' && literal[literal.Length - 1] == '"')
            {
                return literal.Substring(1, literal.Length - 2);
            }

            return literal;
        }

        private static string Escape(string text)
        {
            return (text ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static bool IsIdentityKey(string key)
        {
            return string.Equals(key, "id", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "typeTag", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "verb", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "containerType", StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, "requires", StringComparison.OrdinalIgnoreCase);
        }

        private static List<object> ToObjectList(IReadOnlyList<string> tokens)
        {
            var list = new List<object>();
            if (tokens == null)
            {
                return list;
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                if (!string.IsNullOrEmpty(tokens[i]))
                {
                    list.Add(tokens[i]);
                }
            }

            return list;
        }
    }

    /// <summary>最小 JSON 写出（供装配解析落盘/入 Catalog）。</summary>
    public static class EffectJsonWriter
    {
        public static string Write(object value)
        {
            var builder = new StringBuilder();
            WriteValue(builder, value);
            return builder.ToString();
        }

        private static void WriteValue(StringBuilder builder, object value)
        {
            if (value == null)
            {
                builder.Append("null");
                return;
            }

            if (value is bool)
            {
                builder.Append((bool)value ? "true" : "false");
                return;
            }

            if (value is string)
            {
                builder.Append('"');
                builder.Append(Escape((string)value));
                builder.Append('"');
                return;
            }

            if (value is byte || value is sbyte || value is short || value is ushort
                || value is int || value is uint || value is long || value is ulong
                || value is float || value is double || value is decimal)
            {
                builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }

            var dict = value as Dictionary<string, object>;
            if (dict != null)
            {
                builder.Append('{');
                var first = true;
                foreach (var pair in dict)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    builder.Append('"');
                    builder.Append(Escape(pair.Key));
                    builder.Append('"');
                    builder.Append(':');
                    WriteValue(builder, pair.Value);
                }

                builder.Append('}');
                return;
            }

            var list = value as List<object>;
            if (list != null)
            {
                builder.Append('[');
                for (var i = 0; i < list.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    WriteValue(builder, list[i]);
                }

                builder.Append(']');
                return;
            }

            var node = value as EffectDslNode;
            if (node != null)
            {
                WriteValue(builder, node.RawValue);
                return;
            }

            builder.Append('"');
            builder.Append(Escape(Convert.ToString(value, CultureInfo.InvariantCulture)));
            builder.Append('"');
        }

        private static string Escape(string text)
        {
            return (text ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
