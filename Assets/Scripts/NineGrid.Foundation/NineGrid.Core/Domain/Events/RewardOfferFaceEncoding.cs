using System.Collections.Generic;
using System.Text;
using NineGrid.Core.Content;

namespace NineGrid.Core
{
    /// <summary>
    /// 奖励提供类事件 Message 编解码：poolId|defId:Kind:count:atk:armor:hp,...
    /// </summary>
    public static class RewardOfferFaceEncoding
    {
        public static string Format(string poolId, IReadOnlyList<RewardEntry> offered)
        {
            var builder = new StringBuilder(poolId ?? string.Empty);
            builder.Append('|');
            if (offered == null)
            {
                return builder.ToString();
            }

            for (var i = 0; i < offered.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                var entry = offered[i];
                if (entry == null)
                {
                    continue;
                }

                builder.Append(entry.DefId);
                builder.Append(':');
                builder.Append(entry.Kind);
                builder.Append(':');
                builder.Append(entry.Count);
                builder.Append(':');
                builder.Append(entry.Attack);
                builder.Append(':');
                builder.Append(entry.Armor);
                builder.Append(':');
                builder.Append(entry.Hp);
            }

            return builder.ToString();
        }

        public static bool TryParse(string message, out string poolId, out List<RewardEntry> entries)
        {
            poolId = string.Empty;
            entries = new List<RewardEntry>();
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            var pipe = message.IndexOf('|');
            if (pipe < 0)
            {
                poolId = message;
                return false;
            }

            poolId = message.Substring(0, pipe);
            var payload = pipe + 1 < message.Length ? message.Substring(pipe + 1) : string.Empty;
            if (string.IsNullOrEmpty(payload))
            {
                return entries.Count > 0;
            }

            var segments = payload.Split(',');
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (string.IsNullOrEmpty(segment))
                {
                    continue;
                }

                var parts = segment.Split(':');
                if (parts.Length < 3)
                {
                    return false;
                }

                if (!System.Enum.TryParse(parts[1], out CardKind kind))
                {
                    return false;
                }

                if (!int.TryParse(parts[2], out var count))
                {
                    return false;
                }

                var attack = 0;
                var armor = 0;
                var hp = 0;
                if (parts.Length >= 6)
                {
                    if (!int.TryParse(parts[3], out attack)
                        || !int.TryParse(parts[4], out armor)
                        || !int.TryParse(parts[5], out hp))
                    {
                        return false;
                    }
                }

                entries.Add(new RewardEntry(parts[0], kind, weight: 0, count, attack, armor, hp));
            }

            return entries.Count > 0;
        }
    }
}
