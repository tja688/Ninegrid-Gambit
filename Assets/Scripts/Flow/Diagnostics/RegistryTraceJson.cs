using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// RegistryTrace 手写 JSON（零第三方依赖）。
    /// </summary>
    public static class RegistryTraceJson
    {
        public static string Serialize(RegistryTraceSession session)
        {
            if (session == null)
            {
                return "{}";
            }

            var sb = new StringBuilder(4096);
            sb.Append('{');
            AppendNumber(sb, "schemaVersion", session.schemaVersion);
            sb.Append(',');
            AppendString(sb, "seed", session.seed ?? "0");
            sb.Append(',');
            AppendString(sb, "sessionId", session.sessionId ?? string.Empty);
            sb.Append(',');
            AppendString(sb, "runTag", session.runTag ?? string.Empty);
            sb.Append(',');
            AppendString(sb, "runTagNote", session.runTagNote ?? string.Empty);
            sb.Append(',');
            sb.Append("\"events\":[");
            var events = session.events;
            if (events != null)
            {
                for (var i = 0; i < events.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(',');
                    }

                    AppendEvent(sb, events[i]);
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private static void AppendEvent(StringBuilder sb, RegistryTraceEvent e)
        {
            if (e == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            AppendNumber(sb, "index", e.index);
            sb.Append(',');
            AppendNumber(sb, "beatId", e.beatId);
            sb.Append(',');
            AppendNumber(sb, "tMs", e.tMs);
            sb.Append(',');
            AppendString(sb, "kind", e.kind);
            sb.Append(',');
            AppendNumber(sb, "uid", e.uid);
            sb.Append(',');
            AppendString(sb, "site", e.site);
            sb.Append(',');
            sb.Append("\"payload\":{");
            AppendPayload(sb, e.payload);
            sb.Append("}}");
        }

        private static void AppendPayload(StringBuilder sb, Dictionary<string, string> payload)
        {
            if (payload == null || payload.Count == 0)
            {
                return;
            }

            var first = true;
            foreach (var kv in payload)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                first = false;
                AppendString(sb, kv.Key ?? string.Empty, kv.Value ?? string.Empty);
            }
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append('"');
            sb.Append(Escape(key));
            sb.Append("\":\"");
            sb.Append(Escape(value));
            sb.Append('"');
        }

        private static void AppendNumber(StringBuilder sb, string key, int value)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":");
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }
    }
}
