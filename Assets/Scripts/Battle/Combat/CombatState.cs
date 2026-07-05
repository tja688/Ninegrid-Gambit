using System.Collections.Generic;

namespace NineGrid.Battle.Combat
{
    /// <summary>
    /// 铸造台运行时状态。对应 web 端 state.slots[i]。
    /// </summary>
    public sealed class SlotState
    {
        public int Index;
        public readonly List<CardInstance> Cards = new();
        public int Multiplier = 1;        // 基础倍率（来自 slotUpgrades，默认 1）
        public int RoundMultiplierBonus;   // 本回合倍率加成（钢钻/灵活调度等），回合结束清零

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
    }
}
