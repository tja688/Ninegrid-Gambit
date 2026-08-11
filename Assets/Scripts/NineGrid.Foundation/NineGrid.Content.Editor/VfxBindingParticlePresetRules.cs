#if UNITY_EDITOR
using NineGrid.Content.Vfx;

namespace NineGrid.Content.Editor
{
  /// <summary>particle 播放器绑定的预设键判定（编辑器/卫生共用小工具）。</summary>
  public static class VfxBindingParticlePresetRules
  {
    /// <summary>顶层 materialKey 或任一有效变体命中已知粒子预设即认为可用。</summary>
    public static bool HasAnyKnownPresetKey(string materialKey, VfxMaterialVariantDto[] variants)
    {
      if (VfxParticlePresetIds.IsKnown(materialKey))
      {
        return true;
      }

      if (variants == null)
      {
        return false;
      }

      for (var i = 0; i < variants.Length; i++)
      {
        var variant = variants[i];
        if (variant != null
            && variant.weight > 0f
            && VfxParticlePresetIds.IsKnown(variant.materialKey))
        {
          return true;
        }
      }

      return false;
    }
  }
}
#endif
