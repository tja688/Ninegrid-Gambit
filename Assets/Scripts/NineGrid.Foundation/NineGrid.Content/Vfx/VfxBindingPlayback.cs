using UnityEngine;

namespace NineGrid.Content.Vfx
{
  /// <summary>绑定层播放参数解析；JSON 缺省与运行时默认值在此统一。</summary>
  public static class VfxBindingPlayback
  {
    public const string TimeBaseScaled = "scaled";
    public const string TimeBaseUnscaled = "unscaled";

    public static float ResolveSpeed(float speed)
    {
      return speed <= 0f ? 1f : speed;
    }

    public static Color ResolveTint(float r, float g, float b, float a)
    {
      if (r == 0f && g == 0f && b == 0f && a == 0f)
      {
        return Color.white;
      }

      return new Color(r, g, b, a <= 0f ? 1f : a);
    }

    public static bool IsUnscaledTimeBase(string timeBase)
    {
      return string.Equals(timeBase, TimeBaseUnscaled, System.StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatTimeBase(bool useUnscaledTime)
    {
      return useUnscaledTime ? TimeBaseUnscaled : TimeBaseScaled;
    }
  }
}
