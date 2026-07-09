using System;

namespace NineGrid.Cards
{
    /// <summary>
    /// 参战双方内容 ID（对应 <see cref="ManagedCard.DefId"/>）。通配符为 <see cref="Wildcard"/>。
    /// </summary>
    public readonly struct BattleParticipantIds : IEquatable<BattleParticipantIds>
    {
        public const string Wildcard = "*";

        public BattleParticipantIds(string playerContentId, string monsterContentId)
        {
            PlayerContentId = Normalize(playerContentId);
            MonsterContentId = Normalize(monsterContentId);
        }

        public string PlayerContentId { get; }

        public string MonsterContentId { get; }

        public static BattleParticipantIds Any => new(Wildcard, Wildcard);

        public static string Normalize(string contentId)
        {
            return string.IsNullOrWhiteSpace(contentId) ? Wildcard : contentId.Trim();
        }

        public static bool Matches(string pattern, string actual)
        {
            var normalizedPattern = Normalize(pattern);
            if (normalizedPattern == Wildcard)
            {
                return true;
            }

            return string.Equals(normalizedPattern, Normalize(actual), StringComparison.Ordinal);
        }

        public bool Equals(BattleParticipantIds other)
        {
            return string.Equals(PlayerContentId, other.PlayerContentId, StringComparison.Ordinal)
                && string.Equals(MonsterContentId, other.MonsterContentId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is BattleParticipantIds other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerContentId, MonsterContentId);
        }

        public override string ToString()
        {
            return $"{PlayerContentId} x {MonsterContentId}";
        }
    }
}
