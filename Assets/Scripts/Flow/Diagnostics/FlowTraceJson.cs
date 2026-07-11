using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NineGrid.Flow.Diagnostics
{
    /// <summary>
    /// FlowTrace 手写 JSON（零第三方依赖）。
    /// </summary>
    public static class FlowTraceJson
    {
        public static string Serialize(FlowTraceSession session)
        {
            if (session == null)
            {
                return "{}";
            }

            var sb = new StringBuilder(2048);
            sb.Append('{');
            AppendNumber(sb, "schemaVersion", session.schemaVersion);
            sb.Append(',');
            AppendString(sb, "seed", session.seed ?? "0");
            sb.Append(',');
            AppendString(sb, "sessionId", session.sessionId ?? string.Empty);
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

        private static void AppendEvent(StringBuilder sb, FlowTraceEvent e)
        {
            if (e == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            AppendNumber(sb, "index", e.index);
            sb.Append(',');
            AppendString(sb, "category", e.category);
            sb.Append(',');
            AppendString(sb, "name", e.name);
            sb.Append(',');
            AppendString(sb, "loopState", e.loopState);
            sb.Append(',');
            AppendString(sb, "phaseBefore", e.phaseBefore);
            sb.Append(',');
            AppendString(sb, "phaseAfter", e.phaseAfter);
            sb.Append(',');
            AppendBool(sb, "accepted", e.accepted);
            sb.Append(',');
            AppendNumber(sb, "refBattleOpIndex", e.refBattleOpIndex);
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

        private static void AppendBool(StringBuilder sb, string key, bool value)
        {
            sb.Append('"');
            sb.Append(key);
            sb.Append("\":");
            sb.Append(value ? "true" : "false");
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
