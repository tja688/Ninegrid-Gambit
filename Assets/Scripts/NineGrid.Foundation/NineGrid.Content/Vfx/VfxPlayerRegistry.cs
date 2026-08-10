using System;

namespace NineGrid.Content.Vfx
{
  /// <summary>首版已知播放器登记表；程序化播放器仅校验稳定 playerId 与能力位。</summary>
  public static class VfxPlayerRegistry
  {
    public const string SpriteSheet = "sprite-sheet";
    public const string GoldFlight = "gold-flight";

    public static bool IsKnownPlayerId(string playerId)
    {
      if (string.IsNullOrWhiteSpace(playerId))
      {
        return false;
      }

      return string.Equals(playerId, SpriteSheet, StringComparison.Ordinal)
          || string.Equals(playerId, GoldFlight, StringComparison.Ordinal);
    }

    public static bool IsMaterialPlayer(string playerId)
    {
      return string.Equals(playerId, SpriteSheet, StringComparison.Ordinal);
    }

    public static bool SupportsPulse(string playerId)
    {
      return IsKnownPlayerId(playerId);
    }

    public static bool SupportsState(string playerId)
    {
      return string.Equals(playerId, SpriteSheet, StringComparison.Ordinal);
    }

    public static bool AllowsParamOverride(string playerId, string paramName)
    {
      if (!IsMaterialPlayer(playerId) || string.IsNullOrWhiteSpace(paramName))
      {
        return false;
      }

      switch (paramName.Trim())
      {
        case "materialKey":
        case "fps":
        case "scale":
        case "startOffsetSeconds":
          return true;
        default:
          return false;
      }
    }
  }
}
