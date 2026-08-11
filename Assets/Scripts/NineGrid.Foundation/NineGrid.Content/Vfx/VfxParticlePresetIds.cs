using System;
using System.Collections.Generic;

namespace NineGrid.Content.Vfx
{
  /// <summary>
  /// particle 程序化粒子播放器的稳定预设 ID 表（内容真源侧）。
  /// 绑定 JSON 里 particle 播放器的 materialKey / variants.materialKey 填这里的 ID；
  /// 具体发射参数在表现层 VfxParticlePresetLibrary，两侧以契约测试保持一一对应。
  /// </summary>
  public static class VfxParticlePresetIds
  {
    // —— 一次性 Pulse 预设（战斗反馈） ——
    public const string HitSpark = "particle.hit_spark";
    public const string BloodSpray = "particle.blood_spray";
    public const string ArmorShatter = "particle.armor_shatter";
    public const string BlockSpark = "particle.block_spark";
    public const string HealMotes = "particle.heal_motes";
    public const string ArmorGainRise = "particle.armor_gain_rise";
    public const string DeathPuff = "particle.death_puff";
    public const string BoneChips = "particle.bone_chips";

    // —— 一次性 Pulse 预设（卡牌生命周期 / 交互） ——
    public const string ExitWisp = "particle.exit_wisp";
    public const string ItemFlash = "particle.item_flash";
    public const string FlipSparkle = "particle.flip_sparkle";
    public const string GoldBurst = "particle.gold_burst";

    // —— 一次性 Pulse 预设（效果 / 技能 / 机关 / 遗物触发） ——
    public const string EffectRing = "particle.effect_ring";
    public const string SkillSurge = "particle.skill_surge";
    public const string TrapSnap = "particle.trap_snap";
    public const string RelicGleam = "particle.relic_gleam";

    // —— 一次性 Pulse 预设（主题元素） ——
    public const string FlameBurst = "particle.flame_burst";
    public const string VoidBurst = "particle.void_burst";
    public const string SummonPuff = "particle.summon_puff";

    // —— 一次性 Pulse 预设（流程级） ——
    public const string VictoryConfetti = "particle.victory_confetti";
    public const string DefeatEmbers = "particle.defeat_embers";

    // —— 持续 State 预设（loop） ——
    public const string LoopEmber = "particle.loop.ember";
    public const string LoopHealSpring = "particle.loop.heal_spring";
    public const string LoopMiasma = "particle.loop.miasma";
    public const string LoopSparkle = "particle.loop.sparkle";
    public const string LoopVoidVeil = "particle.loop.void_veil";
    public const string LoopDust = "particle.loop.dust";

    private static readonly string[] sAll =
    {
      HitSpark,
      BloodSpray,
      ArmorShatter,
      BlockSpark,
      HealMotes,
      ArmorGainRise,
      DeathPuff,
      BoneChips,
      ExitWisp,
      ItemFlash,
      FlipSparkle,
      GoldBurst,
      EffectRing,
      SkillSurge,
      TrapSnap,
      RelicGleam,
      FlameBurst,
      VoidBurst,
      SummonPuff,
      VictoryConfetti,
      DefeatEmbers,
      LoopEmber,
      LoopHealSpring,
      LoopMiasma,
      LoopSparkle,
      LoopVoidVeil,
      LoopDust,
    };

    private static readonly HashSet<string> sKnown = new HashSet<string>(sAll, StringComparer.Ordinal);

    public static IReadOnlyList<string> All => sAll;

    public static bool IsKnown(string presetId)
    {
      return !string.IsNullOrWhiteSpace(presetId) && sKnown.Contains(presetId.Trim());
    }

    /// <summary>loop 前缀的预设只供 State 绑定；其余只供 Pulse 绑定。</summary>
    public static bool IsLoopPreset(string presetId)
    {
      return !string.IsNullOrWhiteSpace(presetId)
          && presetId.Trim().StartsWith("particle.loop.", StringComparison.Ordinal);
    }
  }
}
