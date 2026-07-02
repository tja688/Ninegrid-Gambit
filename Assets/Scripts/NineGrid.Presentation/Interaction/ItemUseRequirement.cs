using System;
using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Presentation.Interaction
{
    public enum ItemUseRequirementKind
    {
        None = 0,
        Option,
        BoardTarget,
    }

    /// <summary>
    /// 道具使用前置条件：由 <see cref="ItemUseRequirementResolver"/> 根据 defId 解析。
    /// </summary>
    public sealed class ItemUseRequirement
    {
        public static readonly ItemUseRequirement Direct = new(ItemUseRequirementKind.None);

        private static readonly string[] StatBoostOptionIds = { "Attack", "Armor", "Hp" };
        private static readonly string[] StatBoostOptionLabels = { "攻击+1", "护甲+1", "生命+2" };

        private ItemUseRequirement(
            ItemUseRequirementKind kind,
            int targetCount = 0,
            CardKind targetKind = CardKind.Unknown,
            bool excludeElite = false,
            bool excludeBoss = false,
            IReadOnlyList<string> optionIds = null,
            IReadOnlyList<string> optionLabels = null)
        {
            Kind = kind;
            TargetCount = targetCount;
            TargetKind = targetKind;
            ExcludeElite = excludeElite;
            ExcludeBoss = excludeBoss;
            OptionIds = optionIds ?? Array.Empty<string>();
            OptionLabels = optionLabels ?? Array.Empty<string>();
        }

        public ItemUseRequirementKind Kind { get; }
        public int TargetCount { get; }
        public CardKind TargetKind { get; }
        public bool ExcludeElite { get; }
        public bool ExcludeBoss { get; }
        public IReadOnlyList<string> OptionIds { get; }
        public IReadOnlyList<string> OptionLabels { get; }

        public static ItemUseRequirement StatBoostOptions()
        {
            return new ItemUseRequirement(
                ItemUseRequirementKind.Option,
                optionIds: StatBoostOptionIds,
                optionLabels: StatBoostOptionLabels);
        }

        public static ItemUseRequirement BoardTargets(
            int count,
            CardKind kind = CardKind.Unknown,
            bool excludeElite = false,
            bool excludeBoss = false)
        {
            return new ItemUseRequirement(
                ItemUseRequirementKind.BoardTarget,
                targetCount: count,
                targetKind: kind,
                excludeElite: excludeElite,
                excludeBoss: excludeBoss);
        }

        public string ResolveOptionId(int optionIndex)
        {
            if (optionIndex < 0 || optionIndex >= OptionIds.Count)
            {
                return string.Empty;
            }

            return OptionIds[optionIndex];
        }
    }
}
