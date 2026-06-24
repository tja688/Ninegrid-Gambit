using System;
using System.Collections.Generic;
using System.Text;

namespace NineGrid.Presentation.Diagnostics
{
    public sealed class PresentationTraceEntry
    {
        public PresentationTraceEntry(
            double timeSinceStartup,
            PresentationTraceChannel channel,
            PresentationTraceLevel level,
            string eventName,
            int batchId,
            string phase,
            string screen,
            IReadOnlyList<KeyValuePair<string, string>> fields)
        {
            TimeSinceStartup = timeSinceStartup;
            Channel = channel;
            Level = level;
            EventName = eventName ?? string.Empty;
            BatchId = batchId;
            Phase = phase ?? string.Empty;
            Screen = screen ?? string.Empty;
            Fields = fields ?? Array.Empty<KeyValuePair<string, string>>();
        }

        public double TimeSinceStartup { get; private set; }
        public PresentationTraceChannel Channel { get; private set; }
        public PresentationTraceLevel Level { get; private set; }
        public string EventName { get; private set; }
        public int BatchId { get; private set; }
        public string Phase { get; private set; }
        public string Screen { get; private set; }
        public IReadOnlyList<KeyValuePair<string, string>> Fields { get; private set; }

        public string FormatConsoleLine()
        {
            var builder = new StringBuilder(128);
            builder.Append("[PRES][");
            builder.Append(Channel);
            builder.Append(']');
            if (BatchId > 0)
            {
                builder.Append("[B=");
                builder.Append(BatchId);
                builder.Append(']');
            }

            builder.Append(' ');
            builder.Append(EventName);

            for (var i = 0; i < Fields.Count; i++)
            {
                builder.Append(' ');
                builder.Append(Fields[i].Key);
                builder.Append('=');
                builder.Append(Fields[i].Value);
            }

            return builder.ToString();
        }

        public void AppendJson(StringBuilder builder, bool trailingComma)
        {
            builder.Append('{');
            builder.Append("\"t\":").Append(TimeSinceStartup.ToString("F3"));
            builder.Append(",\"channel\":\"").Append(Channel).Append('"');
            builder.Append(",\"level\":\"").Append(Level).Append('"');
            builder.Append(",\"event\":\"").Append(EscapeJson(EventName)).Append('"');
            if (BatchId > 0)
            {
                builder.Append(",\"batchId\":").Append(BatchId);
            }

            if (!string.IsNullOrEmpty(Phase))
            {
                builder.Append(",\"phase\":\"").Append(EscapeJson(Phase)).Append('"');
            }

            if (!string.IsNullOrEmpty(Screen))
            {
                builder.Append(",\"screen\":\"").Append(EscapeJson(Screen)).Append('"');
            }

            if (Fields.Count > 0)
            {
                builder.Append(",\"fields\":{");
                for (var i = 0; i < Fields.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }

                    builder.Append('"').Append(EscapeJson(Fields[i].Key)).Append("\":\"");
                    builder.Append(EscapeJson(Fields[i].Value)).Append('"');
                }

                builder.Append('}');
            }

            builder.Append('}');
            if (trailingComma)
            {
                builder.Append(',');
            }
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}
