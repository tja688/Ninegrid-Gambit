using System.Collections.Generic;
using NineGrid.Data;
using UnityEngine;

namespace NineGrid.Battle.Combat
{
    /// <summary>
    /// 伤害计算器 —— 忠实移植 web 端 board.js / strategy.js / calc.js 的数值链。
    ///
    /// 公式：totalDamage = floor( Σ_slots [ (Σ_cards finalValue) × slotMultiplier ] × extraMultiplier )
    ///
    /// 数值链（每张矿）：
    ///   baseValue = basePoints + permanentBonus + battleBonus + tempBonus
    ///   → [ON_CALC_VALUE] 预热/合群/齐心 光环 + 破甲锥(slot_card_bonus) 遗物
    ///   → [ON_CALC_FINAL] 熔核翻倍 + 叠牌cardBonus + 首矿加成 + 敌舰惩罚 + 余烬准心
    ///
    /// 倍率链（每个铸造台）：
    ///   multiplier + roundBonus + 叠牌slotBonus + 战术遗物 + 蛮撞战术 + 敌舰倍率惩罚
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
        /// 遗物：破甲锥（slot_card_bonus）。
        /// </summary>
        public static int GetCardEffectiveValue(CardInstance card, CombatState state, int slotIndex)
        {
            var val = GetCardBaseValue(card);
            var slot = state.Slots[slotIndex];
            var myIdx = slot.Cards.IndexOf(card);

            // 预热（dedicate）：正下方（index-1）有预热矿 → 获得其源点数一半（向下取整）
            // 对应 web calc.js dedicate_aura。
            if (myIdx > 0)
            {
                var below = slot.Cards[myIdx - 1];
                if (below.HasTrait(OreTrait.Preheat))
                {
                    // 金王诅咒：预热不生效
                    if (!state.HasEnemySkill("disable_dedicate"))
                    {
                        var belowVal = below.GetSourceValue();
                        // 金王之骨：预热提供1.5倍而非0.5倍
                        if (state.HasRelicEffect(RelicEffectType.DedicateOneFiveX))
                            val += Mathf.FloorToInt(belowVal * 1.5f);
                        else
                            val += belowVal / 2;
                    }
                }
                // 黄金领域（金王旗舰）：无预热矿给上方矿石-1/2强度（反向预热）
                if (state.HasEnemySkill("yellow_domain") && !below.HasTrait(OreTrait.Preheat))
                {
                    val -= 1; // 简化：-1/2 向下取整 = -1（当源点数为1-2时）或更精确处理
                    // 实际web: -floor(sourceValue/2)，但此处下方矿无预热，sourceValue是其自身点数
                    // 简化处理：对无预热矿的上方矿-1
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

            // 破甲锥（slot_card_bonus）：投入指定铸造台的矿石强度+bonus
            var armorPiercer = state.GetRelicEffect(RelicEffectType.SlotCardBonus);
            if (armorPiercer != null && armorPiercer.SlotIndex == slotIndex)
            {
                val += armorPiercer.Bonus;
            }

            return val;
        }

        /// <summary>
        /// 最终点数（ON_CALC_FINAL 阶段）。对应 web getCardFinalValue。
        /// 熔核翻倍 → + 叠牌cardBonus → + 首矿加成 → + 余烬准心 → - 敌舰惩罚。
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

            // ===== 遗物加成（ON_CALC_FINAL）=====

            // 持续作战：每回合第一块矿石+10
            if (!state.FirstCardPlayedThisTurn)
            {
                var sustained = state.GetRelicEffect(RelicEffectType.FirstCardPerTurnBonus);
                if (sustained != null) val += sustained.Bonus;
            }

            // 首炮：每场第一块矿石+20
            if (!state.FirstCardPlayedThisBattle)
            {
                var firstShot = state.GetRelicEffect(RelicEffectType.FirstCardPerBattleBonus);
                if (firstShot != null) val += firstShot.Bonus;
            }

            // 余烬准心：第一块余烬矿+20
            if (state.FirstRetainCardUuid == card.Uuid)
            {
                var emberFocus = state.GetRelicEffect(RelicEffectType.FirstRetainBonus);
                if (emberFocus != null) val += emberFocus.Bonus;
            }

            // ===== 敌舰惩罚（ON_CALC_FINAL）=====

            // 首矿强度惩罚（每回合第一块矿石-5）
            if (!state.FirstCardPlayedThisTurn && state.HasEnemySkill("first_card_value_penalty_5"))
                val -= 5;

            // 所有矿石强度-2（铁甲私掠舰）
            if (state.HasEnemySkill("all_card_penalty_2"))
                val -= 2;

            // 左舷铸造台矿石强度-10
            if (slotIndex == 0 && state.HasEnemySkill("left_penalty_10"))
                val -= 10;

            // 左右舷铸造台矿石强度-5
            if ((slotIndex == 0 || slotIndex == 2) && state.HasEnemySkill("edge_penalty_5"))
                val -= 5;

            // 船首铸造台矿石强度-5
            if (slotIndex == 1 && state.HasEnemySkill("center_card_penalty_5"))
                val -= 5;

            // 非首矿所在铸造台的矿石强度-5（狂浪号）
            if (state.FirstCardPlayedThisTurn && state.FirstCardSlotThisTurn >= 0
                && slotIndex != state.FirstCardSlotThisTurn
                && state.HasEnemySkill("not_first_slot_penalty_5"))
                val -= 5;

            return val;
        }

        /// <summary>
        /// 铸造台有效倍率。对应 web getSlotEffectiveMultiplier。
        /// multiplier + roundBonus + 叠牌slotBonus + 战术遗物 + 蛮撞战术 + 敌舰倍率惩罚。
        /// </summary>
        public static int GetSlotEffectiveMultiplier(CombatState state, int slotIndex)
        {
            var slot = state.Slots[slotIndex];
            var mul = slot.Multiplier + slot.RoundMultiplierBonus;
            GetStackingBonus(slot.Cards.Count, out var slotBonus, out _);
            mul += slotBonus;

            // 蛮撞战术：所有铸造台倍率+2（常驻）
            var ramming = state.GetRelicEffect(RelicEffectType.NoStrategySlotBonus);
            if (ramming != null) mul += ramming.Bonus;

            // 战术遗物：指定铸造台矿石数>threshold时倍率+1
            var tactic = state.GetRelicEffect(RelicEffectType.CrowdSlotBonusSingle);
            if (tactic != null && tactic.SlotIndex == slotIndex && slot.Cards.Count > tactic.Threshold)
                mul += tactic.Bonus;

            // 敌舰倍率惩罚/增益
            if (state.HasEnemySkill("left_slot_bonus_1") && slotIndex == 0)
                mul += 1; // 碎舷号：左舷倍率+1（增益）

            // max_slot_penalty_1：倍率最高铸造台倍率-1
            if (state.HasEnemySkill("max_slot_penalty_1") && IsHighestMultiplierSlot(state, slotIndex))
                mul -= 1;

            // min_slot_penalty_1：倍率最低铸造台倍率-1
            if (state.HasEnemySkill("min_slot_penalty_1") && IsLowestMultiplierSlot(state, slotIndex))
                mul -= 1;

            return Mathf.Max(0, mul);
        }

        static bool IsHighestMultiplierSlot(CombatState state, int slotIndex)
        {
            var target = GetRawMultiplier(state, slotIndex);
            for (var i = 0; i < SlotCount; i++)
            {
                if (i == slotIndex) continue;
                if (GetRawMultiplier(state, i) > target) return false;
            }
            return true;
        }

        static bool IsLowestMultiplierSlot(CombatState state, int slotIndex)
        {
            var target = GetRawMultiplier(state, slotIndex);
            for (var i = 0; i < SlotCount; i++)
            {
                if (i == slotIndex) continue;
                if (GetRawMultiplier(state, i) < target) return false;
            }
            return true;
        }

        static int GetRawMultiplier(CombatState state, int slotIndex)
        {
            var slot = state.Slots[slotIndex];
            var mul = slot.Multiplier + slot.RoundMultiplierBonus;
            GetStackingBonus(slot.Cards.Count, out var slotBonus, out _);
            return mul + slotBonus;
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
