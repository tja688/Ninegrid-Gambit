using NineGrid.Data;
using UnityEngine;

namespace NineGrid.Battle.Combat
{
    /// <summary>
    /// 运行时矿石实例 —— 对应 web 端 createCardInstance 返回的运行时对象。
    /// 持有可变加成（永久/本场/临时），由 CombatModel 管理生命周期。
    /// 字段映射：BaseValue=baseValue, PermanentBonus=permanentBonus, BattleBonus=battleBonus, TempBonus=tempBonus。
    /// </summary>
    public sealed class CardInstance
    {
        static int s_nextId;

        public string Uuid { get; }
        public string OreId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public Sprite Icon { get; }

        /// <summary>基础点数（来自 SO basePoints），可被透支等修改。</summary>
        public int BaseValue { get; set; }

        /// <summary>矿石特性（Flags），运行时可追加（如先手矿临时获得驻台）。</summary>
        public OreTrait Traits { get; set; }

        /// <summary>淬火永久累积，跨回合持久。对应 web permanentBonus。</summary>
        public int PermanentBonus { get; set; }

        /// <summary>本场永久修改（学徒/大师铸造、透支）。对应 web battleBonus。</summary>
        public int BattleBonus { get; set; }

        /// <summary>本回合临时加成（先手+5、殿后+10）。回合结束清零。对应 web tempBonus。</summary>
        public int TempBonus { get; set; }

        /// <summary>淬火增量，默认 1；淬火2 为 2。对应 web growAmount。</summary>
        public int GrowAmount { get; set; } = 1;

        /// <summary>衍生物标记（双晶复制、碎屑矿渣）。</summary>
        public bool IsDerived { get; set; }

        int _slotIndex = -1;

        /// <summary>当前所在铸造台（-1=不在台上）。</summary>
        public int SlotIndex => _slotIndex;

        public CardInstance(OreDataEntry entry)
        {
            Uuid = "c_" + (++s_nextId);
            OreId = entry.OreId;
            DisplayName = entry.DisplayName;
            Description = entry.Description;
            Icon = entry.Icon;
            BaseValue = entry.BasePoints;
            Traits = entry.Traits;
            GrowAmount = (Traits & OreTrait.Quench2) != 0 ? 2 : 1;
        }

        public CardInstance(OreDataSO so)
        {
            Uuid = "c_" + (++s_nextId);
            OreId = so.OreId;
            DisplayName = so.DisplayName;
            Description = so.Description;
            Icon = so.Icon;
            BaseValue = so.BasePoints;
            Traits = so.Traits;
            GrowAmount = (Traits & OreTrait.Quench2) != 0 ? 2 : 1;
        }

        /// <summary>构造衍生物（碎屑矿渣等）。baseValue=0, IsDerived=true。</summary>
        public CardInstance(string oreId, string displayName, int baseValue, bool isDerived)
        {
            Uuid = "c_" + (++s_nextId);
            OreId = oreId;
            DisplayName = displayName;
            Description = "";
            BaseValue = baseValue;
            IsDerived = isDerived;
        }

        public bool HasTrait(OreTrait t) => (Traits & t) != 0;

        /// <summary>添加一条特性（用于事件附魔、敌舰赋词条等）。</summary>
        public void AddTrait(OreTrait t) => Traits |= t;

        /// <summary>移除一条特性。</summary>
        public void RemoveTrait(OreTrait t) => Traits &= ~t;

        /// <summary>基础点数 = baseValue + permanentBonus + battleBonus + tempBonus。对应 web getCardBaseValue。</summary>
        public int GetBaseValue() => BaseValue + PermanentBonus + BattleBonus + TempBonus;

        /// <summary>淬火源点数 = baseValue + permanentBonus + battleBonus（不含 tempBonus）。用于预热光环。</summary>
        public int GetSourceValue() => BaseValue + PermanentBonus + BattleBonus;

        /// <summary>
        /// 投矿到铸造台时触发（对应 web ON_PLAY）。
        /// 只处理矿石自身的淬火；Twin/Symbiosis/Debris 等需要操作牌库的效果由 CombatModel.OnPiecePlacedOnAnvil 处理。
        /// </summary>
        public void OnPlacedOnAnvil(int slotIndex)
        {
            _slotIndex = slotIndex;
            if (HasTrait(OreTrait.Quench) || HasTrait(OreTrait.Quench2))
            {
                var grow = GrowAmount;
                // 淬火炉心：淬火多触发一次
                // 由 CombatModel 调用时传入额外标记，这里不直接访问 state
                PermanentBonus += grow;
            }
        }

        /// <summary>投矿到铸造台时触发（含淬火炉心判定）。</summary>
        public void OnPlacedOnAnvil(int slotIndex, bool growDouble)
        {
            _slotIndex = slotIndex;
            if (HasTrait(OreTrait.Quench) || HasTrait(OreTrait.Quench2))
            {
                PermanentBonus += GrowAmount;
                if (growDouble)
                    PermanentBonus += GrowAmount;
            }
        }

        public void ClearSlot() => _slotIndex = -1;
        public void ClearTemp() => TempBonus = 0;
    }
}
