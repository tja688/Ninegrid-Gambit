using System.Collections.Generic;
using NineGrid.Data;

namespace NineGrid.Battle.Combat
{
    /// <summary>
    /// 铸造台运行时状态。对应 web 端 state.slots[i]。
    /// </summary>
    public sealed class SlotState
    {
        public int Index;
        public readonly List<CardInstance> Cards = new();
        public int Multiplier = 1;        // 基础倍率（来自 slotUpgrades + 冲击龙骨遗物，默认 1）
        public int RoundMultiplierBonus;   // 本回合倍率加成，回合结束清零

        public SlotState(int index) => Index = index;

        public void Clear()
        {
            Cards.Clear();
            RoundMultiplierBonus = 0;
        }
    }

    /// <summary>
    /// 战斗全局状态。对应 web 端 battle state 对象。
    /// 矿舱=deck（抽牌堆），精炼盘=hand（桌面未上砧矿石），铸造台=slots[3]，矿渣堆=discard。
    /// </summary>
    public sealed class CombatState
    {
        public readonly List<CardInstance> Deck = new();      // 矿舱（抽牌堆）
        public readonly List<CardInstance> Hand = new();      // 精炼盘（桌面未上砧）
        public readonly List<CardInstance> Discard = new();   // 矿渣堆
        public readonly SlotState[] Slots = { new(0), new(1), new(2) };

        public int PlayerHp;       // 备用锚数量
        public int PlayerMaxHp;
        public int EnemyHp;        // 敌舰装甲值
        public int EnemyMaxHp;
        public int Turn = 1;
        public float ExtraMultiplier = 1f;  // 全局伤害倍率（船体改造加成），默认 1
        public bool IsEnded;
        public bool PlayerWon;

        /// <summary>锻造提交时计算、撞击时应用的伤害。对应 web turnDamage 的拆分。</summary>
        public int PendingRamDamage;

        public bool IsEnemyDead => EnemyHp <= 0;
        public bool IsPlayerDead => PlayerHp <= 0;

        // ===== 敌舰技能（由 CombatModel.InitBattle 从 EnemyDef.Keywords 设置）=====

        /// <summary>敌舰技能关键词列表（如 "center_grow_1", "less_draw_1"）。</summary>
        public readonly List<string> EnemySkills = new();

        /// <summary>被劫掠号禁用的遗物名（displayName），null 表示无禁用。</summary>
        public string DisabledRelicKey;

        // ===== 遗物（由 CombatModel.InitBattle 从 RunData 设置）=====

        /// <summary>本场战斗生效的遗物定义列表。</summary>
        public readonly List<RelicDef> ActiveRelics = new();

        // ===== 首矿追踪（用于 first_card_per_turn/battle、首矿弃置等技能）=====

        /// <summary>本回合是否已投过矿。</summary>
        public bool FirstCardPlayedThisTurn;

        /// <summary>本场是否已投过矿。</summary>
        public bool FirstCardPlayedThisBattle;

        /// <summary>本回合投入的矿石数（用于孢雾号 play_diffusion_every_3）。</summary>
        public int CardsPlayedThisTurn;

        /// <summary>本回合第一块投入的矿石 uuid（用于狂浪号 not_first_slot_penalty 等）。</summary>
        public string FirstCardUuidThisTurn;

        /// <summary>本回合第一块投入的矿石所在铸造台（用于狂浪号）。</summary>
        public int FirstCardSlotThisTurn = -1;

        /// <summary>首块余烬矿 uuid（用于余烬准心遗物 first_retain_bonus）。</summary>
        public string FirstRetainCardUuid;

        // ===== 抽卡修正 =====

        /// <summary>本回合少抽的矿石数（less_draw_1 等）。</summary>
        public int DrawReduction;

        /// <summary>本回合多抽的矿石数（extra_draw 等）。</summary>
        public int DrawBonus;

        // ===== 敌舰技能查询辅助 =====

        public bool HasEnemySkill(string keyword)
        {
            for (var i = 0; i < EnemySkills.Count; i++)
                if (EnemySkills[i] == keyword) return true;
            return false;
        }

        /// <summary>从技能关键词解析尾部数字（如 "heal_20" → 20）。解析失败返回 defaultValue。</summary>
        public static int ParseSkillParam(string keyword, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(keyword)) return defaultValue;
            var lastUnderscore = keyword.LastIndexOf('_');
            if (lastUnderscore < 0 || lastUnderscore >= keyword.Length - 1) return defaultValue;
            if (int.TryParse(keyword.Substring(lastUnderscore + 1), out var val)) return val;
            return defaultValue;
        }

        // ===== 遗物查询辅助 =====

        public RelicDef GetRelicEffect(RelicEffectType effectType)
        {
            for (var i = 0; i < ActiveRelics.Count; i++)
            {
                var r = ActiveRelics[i];
                if (r.EffectType == effectType)
                {
                    if (!string.IsNullOrEmpty(DisabledRelicKey) && r.DisplayName == DisabledRelicKey) continue;
                    return r;
                }
            }
            return null;
        }

        public bool HasRelicEffect(RelicEffectType effectType) => GetRelicEffect(effectType) != null;
    }
}
