using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Presentation
{
    /// <summary>冲击原点：持有卡自身，或棋盘几何中心（道具槽发动的离场效果）。</summary>
    public enum BoardImpactOrigin
    {
        Holder = 0,
        BoardCenter = 1,
    }

    /// <summary>
    /// 一次签名冲击的档位。Shake = 抖屏 trauma 强度；Shock = 场地卡径向推开强度。任一为 0 即该通道静默。
    /// </summary>
    public readonly struct BoardImpactProfile
    {
        public readonly float Shake;
        public readonly float Shock;
        public readonly BoardImpactOrigin Origin;

        public BoardImpactProfile(float shake, float shock, BoardImpactOrigin origin = BoardImpactOrigin.BoardCenter)
        {
            Shake = shake;
            Shock = shock;
            Origin = origin;
        }

        public bool IsSilent => Shake <= 0.001f && Shock <= 0.001f;
    }

    /// <summary>
    /// 效果触发瞬间的"签名冲击"登记表：按**效果容器 defId**（事件 SourceDefId）给抖屏 / 冲击波档位。
    ///
    /// 只登记「触发瞬间就是可见爆点」的效果（爆弹、手雷一类道具槽发动的即时爆裂）。
    /// 走打击表演（ADR-0050）的伤害类效果**不要**登记：它们的反馈由命中帧冲刷出的 ShowDamage
    /// 驱动抖屏，登记在此会比可见命中提前一拍。
    ///
    /// 默认静默：表内没有的来源不抖不推。宁缺毋滥。
    /// </summary>
    public static class BoardImpactSignatures
    {
        private static readonly Dictionary<string, BoardImpactProfile> Table =
            new Dictionary<string, BoardImpactProfile>(StringComparer.Ordinal)
            {
                // 道具槽即时爆裂：爆点在触发帧，冲击波从棋盘中心向四周炸开。
                ["help.bomb"] = new BoardImpactProfile(1f, 1f),
                ["help.sunder_grenade"] = new BoardImpactProfile(0.72f, 0.62f),
                ["help.flash_bomb"] = new BoardImpactProfile(0.5f, 0.42f),
                ["help.fireball"] = new BoardImpactProfile(0.48f, 0.3f),
                ["help.armor_breaking_hammer"] = new BoardImpactProfile(0.6f, 0.24f),
                ["help.rolling_stone"] = new BoardImpactProfile(0.46f, 0.3f),
                ["help.charge_horn"] = new BoardImpactProfile(0.34f, 0.14f),
                ["help.flame"] = new BoardImpactProfile(0.3f, 0.16f),
                ["help.dismantle_kit"] = new BoardImpactProfile(0.26f, 0f),
                ["help.bear_trap"] = new BoardImpactProfile(0.3f, 0f),

                // 场上机关：原点取持有卡，涟漪由该格向外。
                ["trap.storm_totem"] = new BoardImpactProfile(0.4f, 0.3f, BoardImpactOrigin.Holder),
                ["trap.revive_stone"] = new BoardImpactProfile(0.32f, 0.2f, BoardImpactOrigin.Holder),
                ["trap.echo_bell"] = new BoardImpactProfile(0.22f, 0.12f, BoardImpactOrigin.Holder),
                ["trap.bear_trap"] = new BoardImpactProfile(0.3f, 0f, BoardImpactOrigin.Holder),
                ["trap.flame"] = new BoardImpactProfile(0.24f, 0.13f, BoardImpactOrigin.Holder),
                ["trap.offering_altar"] = new BoardImpactProfile(0.22f, 0f, BoardImpactOrigin.Holder),
            };

        private static Dictionary<string, BoardImpactProfile> sOverrideForTests;

        public static bool TryResolve(string sourceDefId, out BoardImpactProfile profile)
        {
            profile = default;
            if (string.IsNullOrEmpty(sourceDefId))
            {
                return false;
            }

            var table = sOverrideForTests ?? Table;
            return table.TryGetValue(sourceDefId, out profile) && !profile.IsSilent;
        }

        public static void SetOverrideForTests(IReadOnlyDictionary<string, BoardImpactProfile> entries)
        {
            if (entries == null)
            {
                sOverrideForTests = null;
                return;
            }

            sOverrideForTests = new Dictionary<string, BoardImpactProfile>(StringComparer.Ordinal);
            foreach (var pair in entries)
            {
                sOverrideForTests[pair.Key] = pair.Value;
            }
        }

        public static void ResetForTests()
        {
            sOverrideForTests = null;
        }
    }
}
