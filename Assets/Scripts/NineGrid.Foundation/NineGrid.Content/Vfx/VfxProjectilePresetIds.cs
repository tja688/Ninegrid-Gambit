using System;
using System.Collections.Generic;

namespace NineGrid.Content.Vfx
{
  /// <summary>
  /// projectile 程序化弹道播放器的稳定预设 ID 表（内容真源侧）。
  /// 绑定 JSON 里 projectile 播放器的 materialKey / variants.materialKey 填这里的 ID；
  /// 具体飞行与拖尾参数在表现层 VfxProjectilePresetLibrary，两侧以契约测试保持一一对应。
  /// 弹道预设全部为一次性 Pulse（A 点飞向 B 点 + 命中爆点），不提供 State 形态。
  /// </summary>
  public static class VfxProjectilePresetIds
  {
    /// <summary>蓝紫魔法飞弹：发光弹头 + 光尘拖尾 + 星爆命中；通用「效果对某某造成伤害」默认语言。</summary>
    public const string MagicBolt = "projectile.magic_bolt";

    /// <summary>飞刀直射：白亮拉伸弹头极速直线 + 稀疏碎屑尾 + 命中火花；物理攻击语言。</summary>
    public const string ArrowShot = "projectile.arrow_shot";

    /// <summary>火球：橙红光团低弧线 + 火星尾 + 火焰爆燃命中；烈焰主题主击。</summary>
    public const string Fireball = "projectile.fireball";

    /// <summary>虚空法球：暗紫球体高抛缓落 + 暗烟尾 + 虚空爆散；决斗/暗系主题。</summary>
    public const string VoidOrb = "projectile.void_orb";

    /// <summary>电光急蹿：白青光点锯齿疾驰 + 电花尾 + 脆响火星命中；速攻/雷电语言。</summary>
    public const string SparkZip = "projectile.spark_zip";

    /// <summary>骨刺三连：骨白碎片旋转齐射（默认 3 发错峰）+ 骨屑命中；骷髅主题。</summary>
    public const string BoneShard = "projectile.bone_shard";

    /// <summary>毒液抛射：黄绿液团高抛 + 滴落尾 + 毒雾泼溅命中；虫群/中毒主题。</summary>
    public const string VenomGlob = "projectile.venom_glob";

    /// <summary>金色流光：暖金光点柔和 S 线 + 星尘尾 + 金光爆点；遗物/道具触发语言。</summary>
    public const string GleamStreak = "projectile.gleam_streak";

    private static readonly string[] sAll =
    {
      MagicBolt,
      ArrowShot,
      Fireball,
      VoidOrb,
      SparkZip,
      BoneShard,
      VenomGlob,
      GleamStreak,
    };

    private static readonly HashSet<string> sKnown = new HashSet<string>(sAll, StringComparer.Ordinal);

    public static IReadOnlyList<string> All => sAll;

    public static bool IsKnown(string presetId)
    {
      return !string.IsNullOrWhiteSpace(presetId) && sKnown.Contains(presetId.Trim());
    }
  }
}
