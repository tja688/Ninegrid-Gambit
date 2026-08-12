using System;

namespace NineGrid.Content.Vfx
{
  /// <summary>首版已知播放器登记表；程序化播放器仅校验稳定 playerId 与能力位。</summary>
  public static class VfxPlayerRegistry
  {
    public const string SpriteSheet = "sprite-sheet";
    public const string GoldFlight = "gold-flight";
    public const string Particle = "particle";
    public const string Projectile = "projectile";

    public static bool IsKnownPlayerId(string playerId)
    {
      if (string.IsNullOrWhiteSpace(playerId))
      {
        return false;
      }

      return string.Equals(playerId, SpriteSheet, StringComparison.Ordinal)
          || string.Equals(playerId, GoldFlight, StringComparison.Ordinal)
          || string.Equals(playerId, Particle, StringComparison.Ordinal)
          || string.Equals(playerId, Projectile, StringComparison.Ordinal);
    }

    public static bool IsMaterialPlayer(string playerId)
    {
      return string.Equals(playerId, SpriteSheet, StringComparison.Ordinal);
    }

    /// <summary>
    /// particle 程序化粒子播放器：materialKey 携带 VfxParticlePresetIds 预设 ID，
    /// 走与素材播放器相同的变体池解析，但素材卫生按预设表校验而非 visual_effects 索引。
    /// </summary>
    public static bool IsParticlePlayer(string playerId)
    {
      return string.Equals(playerId, Particle, StringComparison.Ordinal);
    }

    /// <summary>
    /// projectile 程序化弹道播放器：materialKey 携带 VfxProjectilePresetIds 预设 ID，
    /// 源→靶双坐标经 VfxSpatialContext（PositionSnapshot / TargetPositionSnapshot）传入，仅支持 Pulse。
    /// </summary>
    public static bool IsProjectilePlayer(string playerId)
    {
      return string.Equals(playerId, Projectile, StringComparison.Ordinal);
    }

    /// <summary>该播放器是否消费 materialKey / variants 变体池（素材键或预设键）。</summary>
    public static bool UsesMaterialVariantSelection(string playerId)
    {
      return IsMaterialPlayer(playerId) || IsParticlePlayer(playerId) || IsProjectilePlayer(playerId);
    }

    public static bool SupportsPulse(string playerId)
    {
      return IsKnownPlayerId(playerId);
    }

    public static bool SupportsState(string playerId)
    {
      return string.Equals(playerId, SpriteSheet, StringComparison.Ordinal)
          || string.Equals(playerId, Particle, StringComparison.Ordinal);
    }

    public static bool AllowsParamOverride(string playerId, string paramName)
    {
      if (!UsesMaterialVariantSelection(playerId) || string.IsNullOrWhiteSpace(paramName))
      {
        return false;
      }

      switch (paramName.Trim())
      {
        case "materialKey":
        case "fps":
        case "speed":
        case "scale":
        case "startOffsetSeconds":
        case "timeBase":
        case "tintR":
        case "tintG":
        case "tintB":
        case "tintA":
          return true;
        default:
          return false;
      }
    }
  }
}
