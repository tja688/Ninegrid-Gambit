using System.Collections.Generic;
using NineGrid.Data;
using UnityEngine;

namespace NineGrid.Battle.Combat
{
    /// <summary>
    /// 伤害计算器 —— 忠实移植 web 端 board.js / strategy.js 的数值链。
    ///
    /// 公式：totalDamage = floor( Σ_slots [ (Σ_cards finalValue) × slotMultiplier ] × extraMultiplier )
    ///
    /// 数值链（每张矿）：
    ///   baseValue = basePoints + permanentBonus + battleBonus + tempBonus
    ///   → [ON_CALC_VALUE] 预热/合群/齐心 光环加成
    ///   → [ON_CALC_FINAL] 熔核翻倍
    ///   → + 叠牌 cardBonus
    ///
    /// 倍率链（每个铸造台）：
    ///   multiplier(1) + roundBonus + 叠牌 slotBonus
    /// </summary>
    public static class CombatCalculator
    {
        public const int HandLimit = 10;
        public const int DrawCount = 5;
        public const int SlotCount = 3;

        /// <summary>
        /// 叠牌加成。每 2 张为一个周期：奇数周期 slotBonus+1，偶数周期 cardBonus+1。
        /// 对应 web strategy.js getStackingBonus。
        /// </summary>
        public static void GetStackingBonus(int cardCount, out int slotBonus, out int cardBonus)
        {
            var cycles = cardCount / 2;
            slotBonus = 0;
            cardBonus = 0;
            if (cycles <= 0) return;

            for (var c = 1; c <= cycles; c++)
            {
                if (c % 2 == 1) slotBonus++;
                else cardBonus++;
            }
        }

        /// <summary>基础点数 = base + permanent + battle + temp。对应 web getCardBaseValue。</summary>
        public static int GetCardBaseValue(CardInstance card)
            => card.BaseValue + card.PermanentBonus + card.BattleBonus + card.TempBonus;

        /// <summary>
        /// 有效点数（ON_CALC_VALUE 阶段）。对应 web getCardEffectiveValue。
        /// 应用光环：预热（dedicate）、合群（social）、齐心（unison）。
        /// </summary>
        public static int GetCardEffectiveValue(CardInstance card, CombatState state, int slotIndex)
        {
            var val = GetCardBaseValue(card);
            var slot = state.Slots[slotIndex];
            var myIdx = slot.Cards.IndexOf(card);

            // 预热（dedicate）：正下方（index-1）有预热矿 → 获得其源点数一半（向下取整）
            // 对应 web calc.js dedicate_aura。下方=先放置=低 Y=低 index。
            if (myIdx > 0)
            {
                var below = slot.Cards[myIdx - 1];
                if (below.HasTrait(OreTrait.Preheat))
                {
                    var belowVal = below.GetSourceValue();
                    val += belowVal / 2;
                }
            }

            // 合群（social）：相邻铸造台每有一张其他矿 +1
            if (card.HasTrait(OreTrait.Sociable))
            {
                var bonus = 0;
                for (var i = 0; i < SlotCount; i++)
                {
                    if (Mathf.Abs(i - slotIndex) == 1)
                        bonus += state.Slots[i].Cards.Count;
                }
                val += bonus;
            }

            // 齐心（unison）：同铸造台每有一张其他矿 +1
            if (card.HasTrait(OreTrait.Unity))
            {
                val += slot.Cards.Count - 1;
            }

            return val;
        }

        /// <summary>
        /// 最终点数（ON_CALC_FINAL 阶段）。对应 web getCardFinalValue。
        /// 熔核翻倍 → + 叠牌 cardBonus。
        /// 注意：叠牌 cardBonus 在熔核之后追加（不被翻倍），与 web 一致。
        /// </summary>
        public static int GetCardFinalValue(CardInstance card, CombatState state, int slotIndex)
        {
            var val = GetCardEffectiveValue(card, state, slotIndex);

            // 熔核（mighty）：翻倍
            if (card.HasTrait(OreTrait.Core))
                val *= 2;

            // 叠牌 cardBonus（在翻倍之后追加）
            var slot = state.Slots[slotIndex];
            GetStackingBonus(slot.Cards.Count, out _, out var cardBonus);
            val += cardBonus;

            return val;
        }

        /// <summary>
        /// 铸造台有效倍率。对应 web getSlotEffectiveMultiplier。
        /// multiplier(1) + roundBonus + 叠牌 slotBonus。
        /// </summary>
        public static int GetSlotEffectiveMultiplier(CombatState state, int slotIndex)
        {
            var slot = state.Slots[slotIndex];
            var mul = slot.Multiplier + slot.RoundMultiplierBonus;
            GetStackingBonus(slot.Cards.Count, out var slotBonus, out _);
            mul += slotBonus;
            return mul;
        }

        /// <summary>
        /// 全场总伤害。对应 web calculateTotalBoardDamage。
        /// floor( Σ_slots [ (Σ_cards finalValue) × slotMultiplier ] × extraMultiplier )
        /// </summary>
        public static int CalculateTotalBoardDamage(CombatState state)
        {
            var total = 0;
            for (var i = 0; i < SlotCount; i++)
            {
                var slot = state.Slots[i];
                var slotMul = GetSlotEffectiveMultiplier(state, i);
                var slotDamage = 0;
                for (var j = 0; j < slot.Cards.Count; j++)
                {
                    slotDamage += GetCardFinalValue(slot.Cards[j], state, i);
                }
                total += slotDamage * slotMul;
            }
            return Mathf.FloorToInt(total * state.ExtraMultiplier);
        }

        /// <summary>单张矿当前产出（不含 extraMultiplier 的 floor，用于 UI 预览）。</summary>
        public static int CalculateCardOutput(CardInstance card, CombatState state, int slotIndex)
            => GetCardFinalValue(card, state, slotIndex) * GetSlotEffectiveMultiplier(state, slotIndex);
    }
}
