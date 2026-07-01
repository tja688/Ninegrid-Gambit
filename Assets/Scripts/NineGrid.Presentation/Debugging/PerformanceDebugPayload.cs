using System.Collections.Generic;
using System.Text;
using NineGrid.Presentation.Shared;

namespace NineGrid.Presentation.Debugging
{
    public sealed class PerformanceDebugPayload
    {
        private readonly Dictionary<string, string> values = new();

        public IReadOnlyDictionary<string, string> Values => values;

        public void Set(string key, string value)
        {
            values[key] = value ?? string.Empty;
        }

        public string GetString(string key, string fallback = "")
        {
            return values.TryGetValue(key, out string value) ? value : fallback;
        }

        public int GetInt(string key, int fallback = 0)
        {
            return int.TryParse(GetString(key), out int parsed) ? parsed : fallback;
        }

        public float GetFloat(string key, float fallback = 0f)
        {
            return float.TryParse(GetString(key), out float parsed) ? parsed : fallback;
        }

        public bool GetBool(string key, bool fallback = false)
        {
            string raw = GetString(key);
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            return raw == "1"
                || raw.Equals("true", System.StringComparison.OrdinalIgnoreCase)
                || raw.Equals("yes", System.StringComparison.OrdinalIgnoreCase);
        }

        public CardBattleDirection GetDirection(string key, CardBattleDirection fallback = CardBattleDirection.Right)
        {
            string raw = GetString(key);
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            return System.Enum.TryParse(raw, true, out CardBattleDirection direction) ? direction : fallback;
        }

        public PerformanceDebugContextPreset GetContextPreset(string key)
        {
            string raw = GetString(key);
            return System.Enum.TryParse(raw, true, out PerformanceDebugContextPreset preset)
                ? preset
                : PerformanceDebugContextPreset.None;
        }

        public int GetBoardSlot(string key, int fallback = 5)
        {
            string raw = GetString(key);
            if (string.IsNullOrEmpty(raw))
            {
                return fallback;
            }

            return int.TryParse(raw, out int parsed)
                   && parsed >= NineGrid.Core.SlotId.MinBoardIndex
                   && parsed <= NineGrid.Core.SlotId.MaxBoardIndex
                ? parsed
                : fallback;
        }

        public PerformanceDebugPayload Clone()
        {
            var clone = new PerformanceDebugPayload();
            foreach (KeyValuePair<string, string> pair in values)
            {
                clone.Set(pair.Key, pair.Value);
            }

            return clone;
        }

        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append('{');
            var first = true;
            foreach (KeyValuePair<string, string> pair in values)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append('"').Append(pair.Key).Append("\":\"").Append(pair.Value).Append('"');
            }

            builder.Append('}');
            return builder.ToString();
        }
    }
}
