using System.Collections.Generic;
using NineGrid.Data;
using UnityEngine;

namespace NineGrid.Battle.Combat
{
    /// <summary>
    /// 撞击结算结果。
    /// </summary>
    public struct RamResult
    {
        public int Damage;       // 本次撞击造成的伤害
        public bool EnemyDead;   // 敌舰是否被击沉
        public bool PlayerDead;  // 玩家是否阵亡
    }

    /// <summary>
    /// 战斗模型编排器 —— 纯 C# 类，由 BattleController 持有并驱动。
    /// 对应 web 端 systems/battle.js + systems/board.js 的逻辑层。
    ///
    /// 生命周期：
    ///   InitBattle → BeginTurn(抽5) → 玩家拖拽排布(OnPlacedOnAnvil) → CommitForge(算伤害+消耗) → ApplyRam(扣敌血+玩家挨打) → 下一回合
    ///
    /// 卡牌流转：
    ///   矿舱(deck) ──抽卡──> 精炼盘(hand/桌面) ──投矿──> 铸造台(slots/砧台)
    ///        ↑                                              │
    ///        └── 重洗(discard→deck) ←── 消耗( CommitForge ) ←┘
    /// </summary>
    public sealed class CombatModel
    {
        public CombatState State { get; } = new();

        /// <summary>敌舰血量变化（current, max）。供未来血条 UI 订阅。</summary>
        public event System.Action<int, int> EnemyHpChanged;

        /// <summary>玩家血量（备用锚）变化（current, max）。</summary>
        public event System.Action<int, int> PlayerHpChanged;

        /// <summary>造成伤害时触发（damage）。</summary>
        public event System.Action<int> DamageDealt;

        /// <summary>战斗结束（playerWon）。</summary>
        public event System.Action<bool> BattleEnded;

        // ===== 初始化 =====

        /// <summary>
        /// 初始化本场战斗：构建老兵初始牌库、设定敌我血量、洗牌。
        /// 对应 web initBattleFromRun。
        /// </summary>
        public void InitBattle(OreCatalog catalog, int enemyMaxHp, int playerMaxHp)
        {
            State.Deck.Clear();
            State.Discard.Clear();
            State.Hand.Clear();
            for (var i = 0; i < CombatCalculator.SlotCount; i++)
            {
                State.Slots[i].Clear();
                State.Slots[i].Multiplier = 1;
            }

            State.PlayerHp = playerMaxHp;
            State.PlayerMaxHp = playerMaxHp;
            State.EnemyHp = enemyMaxHp;
            State.EnemyMaxHp = enemyMaxHp;
            State.Turn = 1;
            State.ExtraMultiplier = 1f;
            State.IsEnded = false;
            State.PlayerWon = false;
            State.PendingRamDamage = 0;

            BuildStartingDeck(catalog);
            ShuffleDeck();

            EnemyHpChanged?.Invoke(State.EnemyHp, State.EnemyMaxHp);
            PlayerHpChanged?.Invoke(State.PlayerHp, State.PlayerMaxHp);
        }

        /// <summary>
        /// 构建老兵初始牌库：5 齐心协力矿(淬火) + 4 助燃矿(预热) + 1 老兵的矿脉(熔核)。
        /// 对应 web CLASS_DEFS.veteran.startingDeck。
        /// </summary>
        void BuildStartingDeck(OreCatalog catalog)
        {
            if (catalog == null)
            {
                Debug.LogError("[Combat] OreCatalog 为空，无法构建牌库。请在 BattleController Inspector 指定。");
                return;
            }

            AddCopies(catalog, "ore_qixinxieli", 5);
            AddCopies(catalog, "ore_zhuran", 4);
            AddCopies(catalog, "ore_laobing", 1);
        }

        void AddCopies(OreCatalog catalog, string oreId, int count)
        {
            if (!catalog.TryGet(oreId, out var entry))
            {
                Debug.LogWarning($"[Combat] OreCatalog 缺少 {oreId}，跳过 {count} 张。");
                return;
            }
            for (var i = 0; i < count; i++)
            {
                State.Deck.Add(new CardInstance(entry));
            }
        }

        // ===== 抽卡 =====

        /// <summary>
        /// 从矿舱抽一块矿石到精炼盘。矿舱空时先重洗矿渣堆。
        /// 对应 web drawCards（单张）。
        /// </summary>
        public CardInstance DrawOre()
        {
            if (State.Deck.Count == 0)
            {
                ReshuffleDiscard();
                if (State.Deck.Count == 0) return null;
            }

            var card = State.Deck[State.Deck.Count - 1];
            State.Deck.RemoveAt(State.Deck.Count - 1);
            State.Hand.Add(card);
            return card;
        }

        /// <summary>
        /// 回合开始：重洗矿渣堆（如有）→ 精炼盘已清空（锻造消耗时清的）→ 不在此抽卡。
        /// 抽卡由 HydraulicMaterialLane.AutoDeliver 在进入锻造时执行。
        /// 对应 web endTurn 末尾的 shuffleDiscardToDeck + drawCards。
        /// </summary>
        public void BeginTurn()
        {
            ReshuffleDiscard();
            State.Turn++;
        }

        /// <summary>重洗矿渣堆入矿舱（Fisher-Yates）。对应 web shuffleDiscardToDeck。</summary>
        void ReshuffleDiscard()
        {
            if (State.Discard.Count == 0) return;

            for (var i = State.Discard.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (State.Discard[i], State.Discard[j]) = (State.Discard[j], State.Discard[i]);
            }

            State.Deck.AddRange(State.Discard);
            State.Discard.Clear();
        }

        void ShuffleDeck()
        {
            for (var i = State.Deck.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (State.Deck[i], State.Deck[j]) = (State.Deck[j], State.Deck[i]);
            }
        }

        // ===== 投矿 / 放置 =====

        /// <summary>
        /// 矿石放置到铸造台时触发（对应 web ON_PLAY）。
        /// 当前实现：淬火（grow）→ PermanentBonus += GrowAmount。
        /// </summary>
        public void OnPiecePlacedOnAnvil(CardInstance card, int slotIndex)
        {
            if (card == null) return;
            card.OnPlacedOnAnvil(slotIndex);
        }

        // ===== 锻造提交（算伤害 + 消耗） =====

        /// <summary>
        /// 从当前砧台快照计算全场伤害（不消耗，用于 UI 预览）。
        /// 对应 web getPlacementPreview 的伤害部分。
        /// </summary>
        public int PreviewDamage(List<CardInstance>[] anvilStacks)
        {
            SyncSlots(anvilStacks);
            return CombatCalculator.CalculateTotalBoardDamage(State);
        }

        /// <summary>
        /// 单个铸造台的伤害贡献（需先调 PreviewDamage 同步砧台到 state）。
        /// = (Σ 矿石最终点数) × 铸造台倍率。
        /// </summary>
        public int GetSlotDamage(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= CombatCalculator.SlotCount) return 0;
            var slot = State.Slots[slotIndex];
            var slotDamage = 0;
            for (var j = 0; j < slot.Cards.Count; j++)
                slotDamage += CombatCalculator.GetCardFinalValue(slot.Cards[j], State, slotIndex);
            return slotDamage * CombatCalculator.GetSlotEffectiveMultiplier(State, slotIndex);
        }

        /// <summary>
        /// 锻造提交：同步砧台 → 算伤害 → 存为 PendingRamDamage → 消耗全部在场矿石到矿渣堆。
        /// 对应 web endTurn 的伤害计算 + 清理（拆分到撞击时才扣血）。
        /// </summary>
        public int CommitForge(List<CardInstance>[] anvilStacks, List<CardInstance> tableCards)
        {
            SyncSlots(anvilStacks);
            var damage = CombatCalculator.CalculateTotalBoardDamage(State);
            State.PendingRamDamage = damage;

            // 消耗：铸造台上的矿 + 桌面未上砧的矿 → 全部入矿渣堆
            for (var i = 0; i < CombatCalculator.SlotCount; i++)
            {
                foreach (var c in State.Slots[i].Cards)
                {
                    c.ClearSlot();
                    State.Discard.Add(c);
                }
                State.Slots[i].Clear();
            }

            if (tableCards != null)
            {
                foreach (var c in tableCards)
                {
                    c.ClearSlot();
                    State.Discard.Add(c);
                }
            }
            State.Hand.Clear();

            return damage;
        }

        void SyncSlots(List<CardInstance>[] anvilStacks)
        {
            for (var i = 0; i < CombatCalculator.SlotCount; i++)
            {
                State.Slots[i].Cards.Clear();
                if (anvilStacks != null && i < anvilStacks.Length && anvilStacks[i] != null)
                {
                    State.Slots[i].Cards.AddRange(anvilStacks[i]);
                }
            }
        }

        // ===== 撞击结算（扣敌血 + 玩家挨打 + 回合清理） =====

        /// <summary>
        /// 撞击结算：应用 PendingRamDamage 到敌舰 → 判死 → 玩家挨 1 点 → 判死 → 清理 tempBonus → 下回合。
        /// 对应 web endTurn 的扣血 + 判定 + cleanup 部分。
        /// </summary>
        public RamResult ApplyRam()
        {
            var result = new RamResult { Damage = State.PendingRamDamage };
            State.PendingRamDamage = 0;

            // 扣敌舰血
            if (result.Damage > 0)
            {
                State.EnemyHp = Mathf.Max(0, State.EnemyHp - result.Damage);
                EnemyHpChanged?.Invoke(State.EnemyHp, State.EnemyMaxHp);
                DamageDealt?.Invoke(result.Damage);
            }

            // 敌舰击沉 → 胜利
            if (State.IsEnemyDead)
            {
                State.IsEnded = true;
                State.PlayerWon = true;
                result.EnemyDead = true;
                BattleEnded?.Invoke(true);
                return result;
            }

            // 玩家挨 1 点（敌舰反击）
            State.PlayerHp -= 1;
            PlayerHpChanged?.Invoke(State.PlayerHp, State.PlayerMaxHp);

            if (State.IsPlayerDead)
            {
                State.IsEnded = true;
                State.PlayerWon = false;
                result.PlayerDead = true;
                BattleEnded?.Invoke(false);
                return result;
            }

            // 清理：所有矿的 tempBonus 归零
            foreach (var c in State.Deck) c.ClearTemp();
            foreach (var c in State.Discard) c.ClearTemp();

            // 回合结束 → 准备下回合（重洗 + turn++）
            BeginTurn();

            return result;
        }

        /// <summary>矿舱剩余矿石数（用于矿仓面板）。</summary>
        public int DeckCount => State.Deck.Count;

        /// <summary>矿舱 + 矿渣堆 总矿石数（玩家持有总量）。</summary>
        public int TotalOreCount => State.Deck.Count + State.Discard.Count + State.Hand.Count
            + System.Linq.Enumerable.Sum(State.Slots, s => s.Cards.Count);
    }
}
