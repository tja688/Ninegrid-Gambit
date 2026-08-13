using System;
using System.Collections.Generic;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 效果打击表演的按卡回退登记（ADR-0050 补记）。
    /// 默认：所有场上卡造成的伤害/破坏都走「攻击动作 + 受击反馈」打击表演；
    /// 备选：确认互殴观感不佳的卡，把其**效果容器 defId**（事件 SourceDefId，
    /// 如 "trap.rolling_stone"，非效果实例 defId）登记进 <see cref="DirectFlushSources"/>，
    /// 该来源的伤害/移除即回退为旧「直接掉血/直接破坏」——指令不进打击暂扣区，
    /// 在常规 Impact 锚点冲刷，不编排打击动作。
    /// </summary>
    public static class EffectStrikePresentationRules
    {
        /// <summary>
        /// 直伤回退清单（容器 defId）。默认空 = 全部卡走打击表演。
        /// 示例：
        ///   "trap.rolling_stone",   // 滚石改回瞬间破坏
        ///   "skill.some_monster",   // 某怪技能改回直接掉血
        /// </summary>
        private static readonly HashSet<string> DirectFlushSources = new HashSet<string>(StringComparer.Ordinal)
        {
        };

        private static HashSet<string> sOverrideForTests;

        /// <summary>该效果来源是否回退为直伤冲刷（true = 不建打击组、不暂扣）。</summary>
        public static bool UseDirectFlush(string sourceDefId)
        {
            if (string.IsNullOrEmpty(sourceDefId))
            {
                return false;
            }

            var set = sOverrideForTests ?? DirectFlushSources;
            return set.Contains(sourceDefId);
        }

        public static void SetOverrideForTests(IEnumerable<string> sources)
        {
            sOverrideForTests = sources != null
                ? new HashSet<string>(sources, StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);
        }

        public static void ResetForTests()
        {
            sOverrideForTests = null;
        }
    }
}
