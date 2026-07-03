using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NineGrid.Presentation.Debugging.Trace
{
    internal static class BattleTraceJsonWriter
    {
        public static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(value.Length + 8);
            for (var i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        public static void AppendNumber(StringBuilder builder, string key, int value)
        {
            builder.Append('"').Append(key).Append("\":").Append(value);
        }

        public static void AppendNumber(StringBuilder builder, string key, long value)
        {
            builder.Append('"').Append(key).Append("\":").Append(value);
        }

        public static void AppendFloat(StringBuilder builder, string key, float value)
        {
            builder.Append('"')
                .Append(key)
                .Append("\":")
                .Append(value.ToString("F3", CultureInfo.InvariantCulture));
        }

        public static void AppendBool(StringBuilder builder, string key, bool value)
        {
            builder.Append('"').Append(key).Append("\":").Append(value ? "true" : "false");
        }

        public static void AppendString(StringBuilder builder, string key, string value)
        {
            builder.Append('"').Append(key).Append("\":\"").Append(Escape(value ?? string.Empty)).Append('"');
        }

        public static void AppendNullableInt(StringBuilder builder, string key, int? value)
        {
            if (value.HasValue)
            {
                AppendNumber(builder, key, value.Value);
            }
            else
            {
                builder.Append('"').Append(key).Append("\":null");
            }
        }

        public static void AppendIntArray(StringBuilder builder, string key, IReadOnlyList<int> values)
        {
            builder.Append('"').Append(key).Append("\":[");
            if (values != null)
            {
                for (var i = 0; i < values.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append(values[i]);
                }
            }

            builder.Append(']');
        }

        public static void AppendBoardMap(StringBuilder builder, string key, IReadOnlyDictionary<int, int> board)
        {
            builder.Append('"').Append(key).Append("\":{");
            if (board != null)
            {
                var first = true;
                foreach (var pair in board)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }

                    first = false;
                    builder.Append('"').Append(pair.Key).Append("\":").Append(pair.Value);
                }
            }

            builder.Append('}');
        }
    }
}
