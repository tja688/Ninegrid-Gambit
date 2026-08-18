using System;
using System.Collections.Generic;
using NineGrid.Content.Vfx;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using UnityEngine;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 快速测试模式下顺劈斧程序化弹道粒子效果切换状态。
    /// 仅在 <see cref="IGameFlowShellSystem.IsQuickTestMode"/> 为真时生效。
    /// 数字键 0~9 映射到 8 个已落地的程序化弹道预设。
    /// </summary>
    public static class QuickTestProjectileEffectState
    {
        public readonly struct PresetEntry
        {
            public PresetEntry(string id, string name, string description)
            {
                Id = id;
                Name = name;
                Description = description;
            }

            public string Id { get; }
            public string Name { get; }
            public string Description { get; }
        }

        private static readonly PresetEntry[] sPresets =
        {
            new PresetEntry(VfxProjectilePresetIds.MagicBolt, "魔法飞弹", "蓝紫发光弹头 + 光尘拖尾 + 星爆命中"),
            new PresetEntry(VfxProjectilePresetIds.ArrowShot, "飞刀直射", "白亮拉伸弹头极速直线 + 稀疏碎屑尾 + 火花命中"),
            new PresetEntry(VfxProjectilePresetIds.Fireball, "火球", "橙红光团低弧线 + 火星尾 + 爆燃命中"),
            new PresetEntry(VfxProjectilePresetIds.VoidOrb, "虚空法球", "暗紫球体高抛缓落 + 暗烟尾 + 虚空爆散"),
            new PresetEntry(VfxProjectilePresetIds.SparkZip, "电光急蹿", "白青光点锯齿疾驰 + 电花尾 + 脆响火星命中"),
            new PresetEntry(VfxProjectilePresetIds.BoneShard, "骨刺三连", "骨白碎片旋转齐射 + 骨屑命中"),
            new PresetEntry(VfxProjectilePresetIds.VenomGlob, "毒液抛射", "黄绿液团高抛 + 滴落尾 + 毒雾泼溅命中"),
            new PresetEntry(VfxProjectilePresetIds.GleamStreak, "金色流光", "暖金光点柔和 S 线 + 星尘尾 + 金光爆点"),
            new PresetEntry(VfxProjectilePresetIds.Fireball, "火球 (强化)", "烈焰主题重击弹道"),
            new PresetEntry(VfxProjectilePresetIds.SparkZip, "电光 (疾驰)", "雷电主题疾驰弹道"),
        };

        private static int sActiveIndex = 0;

        public static int ActiveIndex => sActiveIndex;

        public static string ActivePresetId => GetPresetId(sActiveIndex);

        public static string ActivePresetDisplayName => GetDisplayName(sActiveIndex);

        public static IReadOnlyList<PresetEntry> AllPresets => sPresets;

        public static string GetPresetId(int index)
        {
            if (index >= 0 && index < sPresets.Length)
            {
                return sPresets[index].Id;
            }

            return VfxProjectilePresetIds.MagicBolt;
        }

        public static string GetDisplayName(int index)
        {
            if (index >= 0 && index < sPresets.Length)
            {
                return sPresets[index].Name;
            }

            return "魔法飞弹";
        }

        public static bool TrySetActiveIndex(int index, out string presetId, out string displayName)
        {
            if (index < 0 || index >= sPresets.Length)
            {
                presetId = ActivePresetId;
                displayName = ActivePresetDisplayName;
                return false;
            }

            sActiveIndex = index;
            presetId = sPresets[index].Id;
            displayName = sPresets[index].Name;
            return true;
        }

        /// <summary>离开快速测试时清掉数字键选择，避免残留状态串到下一局。</summary>
        public static void Reset()
        {
            sActiveIndex = 0;
        }

        /// <summary>
        /// 若处于快速测试模式，获取当前选中的测试弹道预设 ID；若非快速测试模式返回 false。
        /// 调用方必须把此返回值当作发射门闩：false 时不得 Pulse 任何遗物弹道。
        /// </summary>
        public static bool TryGetActivePresetForQuickTest(out string presetId)
        {
            var arch = NineGridArchitecture.Interface ?? NineGridArchitecture.Current;
            if (arch == null)
            {
                presetId = null;
                return false;
            }

            var shell = arch.GetSystem<IGameFlowShellSystem>();
            if (shell == null || !shell.IsQuickTestMode)
            {
                presetId = null;
                return false;
            }

            presetId = ActivePresetId;
            return true;
        }
    }
}
