using System;
using NineGrid.Core.Systems;

namespace NineGrid.Core.Stats
{
    /// <summary>
    /// 基础护甲 (Armor base) / 有效护甲 (pipeline) / 当前护甲 (CurrentArmor base) 三层语义。
    /// </summary>
    public static class StatArmorUtility
    {
        public static int GetBaseArmor(CardInstance card)
        {
            return card == null ? 0 : Math.Max(0, (int)Math.Round(card.Stats.GetBase(StatId.Armor)));
        }

        public static int GetEffectiveArmor(IStatSystem statSystem, CardInstance card)
        {
            return card == null || statSystem == null
                ? 0
                : Math.Max(0, statSystem.GetEffectiveInt(card, StatId.Armor));
        }

        public static int GetCurrentArmor(CardInstance card)
        {
            if (card == null)
            {
                return 0;
            }

            if (card.Stats.BaseValues.ContainsKey(StatId.CurrentArmor))
            {
                return Math.Max(0, (int)Math.Round(card.Stats.GetBase(StatId.CurrentArmor)));
            }

            return GetBaseArmor(card);
        }

        public static void SetCurrentArmor(CardInstance card, int value)
        {
            if (card != null)
            {
                card.Stats.SetBase(StatId.CurrentArmor, Math.Max(0, value));
            }
        }

        public static void InitializeCurrentFromBase(CardInstance card)
        {
            if (card != null)
            {
                SetCurrentArmor(card, GetBaseArmor(card));
            }
        }

        public static void ResetCurrentToEffective(IStatSystem statSystem, CardInstance card)
        {
            if (card != null && statSystem != null)
            {
                SetCurrentArmor(card, GetEffectiveArmor(statSystem, card));
            }
        }
    }
}
