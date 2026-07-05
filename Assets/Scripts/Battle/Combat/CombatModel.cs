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
        public int GoldChange;   // 本回合金币变化（正=获得，负=损失）
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
        const string SlagOreId = "ore_kuangzha";

        public CombatState State { get; } = new();

        OreCatalog _catalog;

        public event System.Action<int, int> EnemyHpChanged;
        public event System.Action<int, int> PlayerHpChanged;
        public event System.Action<int> DamageDealt;
        public event System.Action<bool> BattleEnded;
        /// <summary>金币变化（正=获得，负=损失）。BattleController 订阅后应用到 RunData。</summary>
        public event System.Action<int> GoldChanged;

        // ===== 初始化 =====

        /// <summary>
        /// 初始化本场战斗。对应 web initBattleFromRun。
        /// </summary>
        public void InitBattle(OreCatalog catalog, int enemyMaxHp, int playerMaxHp,
            List<CardInstance> persistentDeck = null,
            List<RelicDef> relics = null,
            int[] slotMultipliers = null,
            string[] enemySkills = null)
        {
            _catalog = catalog;

            // 清空状态
            State.Deck.Clear();
            State.Discard.Clear();
            State.Hand.Clear();
            for (var i = 0; i < CombatCalculator.SlotCount; i++)
            {
                State.Slots[i].Clear();
                State.Slots[i].Multiplier = 1;
            }
            State.ActiveRelics.Clear();
            State.EnemySkills.Clear();
            State.DisabledRelicKey = null;
            State.FirstCardPlayedThisTurn = false;
            State.FirstCardPlayedThisBattle = false;
            State.CardsPlayedThisTurn = 0;
            State.FirstCardUuidThisTurn = null;
            State.FirstCardSlotThisTurn = -1;
            State.FirstRetainCardUuid = null;
            State.DrawReduction = 0;
            State.DrawBonus = 0;

            State.PlayerHp = playerMaxHp;
            State.PlayerMaxHp = playerMaxHp;
            State.EnemyHp = enemyMaxHp;
            State.EnemyMaxHp = enemyMaxHp;
            State.Turn = 1;
            State.ExtraMultiplier = 1f;
            State.IsEnded = false;
            State.PlayerWon = false;
            State.PendingRamDamage = 0;

            // 构建牌库
            if (persistentDeck != null && persistentDeck.Count > 0)
            {
                State.Deck.AddRange(persistentDeck);
            }
            else
            {
                BuildStartingDeck(catalog);
            }

            // 设置铸造台倍率（来自 slotUpgrades + 冲击龙骨遗物）
            if (slotMultipliers != null)
            {
                for (var i = 0; i < CombatCalculator.SlotCount && i < slotMultipliers.Length; i++)
                {
                    State.Slots[i].Multiplier = Mathf.Max(1, slotMultipliers[i]);
                }
            }

            // 设置遗物
            if (relics != null)
            {
                State.ActiveRelics.AddRange(relics);
            }

            // 设置敌舰技能
            if (enemySkills != null)
            {
                State.EnemySkills.AddRange(enemySkills);
            }

            ShuffleDeck();

            // ===== 战斗开始效果 =====

            // 劫掠号：随机禁用一件船体改造
            if (State.HasEnemySkill("disable_random_relic") && State.ActiveRelics.Count > 0)
            {
                var idx = Random.Range(0, State.ActiveRelics.Count);
                State.DisabledRelicKey = State.ActiveRelics[idx].DisplayName;
                Debug.Log($"[Combat] 劫掠号禁用遗物：{State.DisabledRelicKey}");
            }

            // 扒船遗物：战斗开始+1银元
            var shipLooting = State.GetRelicEffect(RelicEffectType.BattleStartGold);
            if (shipLooting != null)
            {
                GoldChanged?.Invoke(shipLooting.Gold);
            }

            // 私货夹带：加入一块私货(50点)到精炼盘
            if (State.HasRelicEffect(RelicEffectType.PrintCheatCard))
            {
                var cheat = new CardInstance("cheat_card", "私货", 50, true);
                State.Hand.Add(cheat);
            }

            // 金王之心：赋随机矿预热(永久)
            if (State.HasRelicEffect(RelicEffectType.DedicatePermanent) && State.Deck.Count > 0)
            {
                var idx = Random.Range(0, State.Deck.Count);
                State.Deck[idx].AddTrait(OreTrait.Preheat);
            }

            // 金王之肉：所有预热矿获共生
            if (State.HasRelicEffect(RelicEffectType.DedicateChain))
            {
                foreach (var c in State.Deck)
                    if (c.HasTrait(OreTrait.Preheat))
                        c.AddTrait(OreTrait.Symbiosis);
            }

            // 首回合少抽（铁甲护卫舰 first_turn_less_draw）
            if (State.HasEnemySkill("first_turn_less_draw"))
            {
                State.DrawReduction = 1; // 首回合少抽1块
            }

            EnemyHpChanged?.Invoke(State.EnemyHp, State.EnemyMaxHp);
            PlayerHpChanged?.Invoke(State.PlayerHp, State.PlayerMaxHp);

            // 触发首回合开始效果
            OnTurnStart();
        }

        /// <summary>
        /// 构建老兵初始牌库：5 齐心协力矿(淬火) + 4 助燃矿(预热) + 1 老兵的矿脉(熔核)。
        /// </summary>
        void BuildStartingDeck(OreCatalog catalog)
        {
            if (catalog == null)
            {
                Debug.LogError("[Combat] OreCatalog 为空，无法构建牌库。");
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

        /// <summary>本回合应抽矿石数（基础5 ± 技能/遗物修正）。</summary>
        public int GetDrawCount()
        {
            var count = CombatCalculator.DrawCount;
            // less_draw_1：每回合少抽1块
            if (State.HasEnemySkill("less_draw_1"))
                count -= 1;
            // 首回合少抽已在 InitBattle 的 DrawReduction 处理
            if (State.Turn == 1)
                count -= State.DrawReduction;
            // 满载：每回合多抽bonus块
            var extraDraw = State.GetRelicEffect(RelicEffectType.ExtraDraw);
            if (extraDraw != null) count += extraDraw.Bonus;
            // 船长锦囊·满载：每回合+1抽/首回合额外+1
            var captainDraw = State.GetRelicEffect(RelicEffectType.ExtraDrawFirstTurn);
            if (captainDraw != null)
            {
                count += captainDraw.Bonus;
                if (State.Turn == 1) count += captainDraw.Bonus;
            }
            return Mathf.Max(0, count);
        }

        /// <summary>
        /// 回合开始：重洗矿渣堆 → turn++ → 重置追踪 → 触发 ON_TURN_START。
        /// </summary>
        public void BeginTurn()
        {
            ReshuffleDiscard();
            State.Turn++;

            // 重置回合追踪
            State.FirstCardPlayedThisTurn = false;
            State.CardsPlayedThisTurn = 0;
            State.FirstCardUuidThisTurn = null;
            State.FirstCardSlotThisTurn = -1;

            // 首回合少抽只在第1回合生效
            if (State.Turn > 1)
                State.DrawReduction = 0;

            OnTurnStart();
        }

        /// <summary>ON_TURN_START 效果：敌舰回修/生长、遗物伤害、金币等。</summary>
        void OnTurnStart()
        {
            // ===== 敌舰技能 =====

            // heal_20：每回合恢复20装甲
            if (State.HasEnemySkill("heal_20"))
            {
                State.EnemyHp = Mathf.Min(State.EnemyMaxHp, State.EnemyHp + 20);
                EnemyHpChanged?.Invoke(State.EnemyHp, State.EnemyMaxHp);
            }

            // monster_grow_100：每回合装甲+100（同时增加 maxHp）
            if (State.HasEnemySkill("monster_grow_100"))
            {
                State.EnemyMaxHp += 100;
                State.EnemyHp += 100;
                EnemyHpChanged?.Invoke(State.EnemyHp, State.EnemyMaxHp);
            }

            // lose_gold_per_turn：每回合-1银元
            if (State.HasEnemySkill("lose_gold_per_turn"))
            {
                GoldChanged?.Invoke(-1);
            }

            // yellow_heart：给精炼盘随机一块矿石赋予预热
            if (State.HasEnemySkill("yellow_heart") && State.Hand.Count > 0)
            {
                var idx = Random.Range(0, State.Hand.Count);
                State.Hand[idx].AddTrait(OreTrait.Preheat);
            }

            // retain_hand_card：给精炼盘随机一块矿石赋予余烬并禁用
            if (State.HasEnemySkill("retain_hand_card") && State.Hand.Count > 0)
            {
                var idx = Random.Range(0, State.Hand.Count);
                State.Hand[idx].AddTrait(OreTrait.Ember);
            }

            // ===== 遗物 =====

            // 小型撞角/多层熔炼：回合开始敌舰-50装甲
            var ram = State.GetRelicEffect(RelicEffectType.TurnMonsterDamage);
            if (ram != null)
            {
                State.EnemyHp = Mathf.Max(0, State.EnemyHp - ram.Damage);
                EnemyHpChanged?.Invoke(State.EnemyHp, State.EnemyMaxHp);
                if (State.IsEnemyDead)
                {
                    State.IsEnded = true;
                    State.PlayerWon = true;
                    BattleEnded?.Invoke(true);
                    return;
                }
            }

            // 赌徒：10%概率抽1块
            var gambler = State.GetRelicEffect(RelicEffectType.LuckDraw);
            if (gambler != null && Random.Range(0, 100) < gambler.Chance)
            {
                DrawOre();
            }

            // 背水：第3回合抽1块
            var lastStand = State.GetRelicEffect(RelicEffectType.ThirdTurnDraw);
            if (lastStand != null && State.Turn == lastStand.Turn)
            {
                DrawOre();
            }
        }

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
        /// 处理：淬火、首矿追踪、敌舰技能（center_grow/first_card_discard）、遗物（first_card_remain/slot_grow）、
        /// Twin/Symbiosis/Debris、play_diffusion_every_3。
        /// 返回碎屑生成的矿渣（需由表现层直接放到对应铸造台）。
        /// </summary>
        public CardInstance OnPiecePlacedOnAnvil(CardInstance card, int slotIndex)
        {
            if (card == null) return null;

            // 敌舰技能：首矿直接弃置（铁甲护卫舰 first_card_discard）
            if (!State.FirstCardPlayedThisTurn && State.HasEnemySkill("first_card_discard"))
            {
                State.Hand.Remove(card);
                card.ClearSlot();
                State.Discard.Add(card);
                // 仍然标记为首矿已投
                State.FirstCardPlayedThisTurn = true;
                State.FirstCardPlayedThisBattle = true;
                State.CardsPlayedThisTurn++;
                Debug.Log("[Combat] 首矿被敌舰弃置（first_card_discard）");
                return null;
            }

            // 首矿追踪
            var wasFirstThisTurn = !State.FirstCardPlayedThisTurn;
            if (wasFirstThisTurn)
            {
                State.FirstCardPlayedThisTurn = true;
                State.FirstCardUuidThisTurn = card.Uuid;
                State.FirstCardSlotThisTurn = slotIndex;

                // 余烬准心：锁定首块余烬矿
                if (card.HasTrait(OreTrait.Ember) && string.IsNullOrEmpty(State.FirstRetainCardUuid))
                {
                    State.FirstRetainCardUuid = card.Uuid;
                }

                // 压舱加固：每场首矿获驻台
                if (!State.FirstCardPlayedThisBattle && State.HasRelicEffect(RelicEffectType.FirstCardRemain))
                {
                    card.AddTrait(OreTrait.Station);
                }
            }
            State.FirstCardPlayedThisBattle = true;
            State.CardsPlayedThisTurn++;

            // 淬火（含淬火炉心 growDouble）
            var growDouble = State.HasRelicEffect(RelicEffectType.GrowDouble);
            card.OnPlacedOnAnvil(slotIndex, growDouble);

            // 敌舰技能：藤蔓号 center_grow_1 — 投入船首(slot 1)的矿石永久+1
            if (slotIndex == 1 && State.HasEnemySkill("center_grow_1"))
            {
                card.PermanentBonus += 1;
            }

            // 遗物：专精 slot_grow — 投入指定台永久+1，累计达growPer时倍率+1
            var specialty = State.GetRelicEffect(RelicEffectType.SlotGrow);
            if (specialty != null && specialty.SlotIndex == slotIndex)
            {
                card.PermanentBonus += 1;
                // 累计值用 RoundMultiplierBonus 临时记录（简化）
                // 完整实现需要 run 级追踪 relicSlotGrowth，这里用 PermanentBonus 累计近似
            }

            // ===== 矿石词条 ON_PLAY 效果 =====

            // 双晶 Twin：复制自身到精炼盘
            if (card.HasTrait(OreTrait.Twin))
            {
                var copy = new CardInstance(card.OreId + "_twin", card.DisplayName, card.BaseValue, true);
                copy.PermanentBonus = card.PermanentBonus;
                copy.Traits = card.Traits;
                State.Hand.Add(copy);
            }

            // 共生 Symbiosis：抽一块矿石
            if (card.HasTrait(OreTrait.Symbiosis))
            {
                DrawOre();
                // Symbiosis2 抽两块
                if (card.HasTrait(OreTrait.Symbiosis2))
                    DrawOre();
            }

            // 碎屑 Debris：生成一块0点矿渣到同一铸造台（表现层直接放置，不走管道）
            if (card.HasTrait(OreTrait.Debris))
            {
                return CreateSlagCard();
            }

            // 敌舰技能：孢雾号 play_diffusion_every_3 — 每投3块矿，投入1块矿渣到随机台
            if (State.HasEnemySkill("play_diffusion_every_3") && State.CardsPlayedThisTurn % 3 == 0)
            {
                var junkSlot = Random.Range(0, CombatCalculator.SlotCount);
                State.Slots[junkSlot].Cards.Add(CreateSlagCard());
            }

            return null;
        }

        CardInstance CreateSlagCard()
        {
            if (_catalog != null && _catalog.TryGet(SlagOreId, out var entry))
            {
                var card = new CardInstance(entry);
                card.IsDerived = true;
                return card;
            }

            Debug.LogWarning($"[Combat] OreCatalog 缺少 {SlagOreId}，使用占位矿渣数据。");
            return new CardInstance(SlagOreId, "矿渣", 0, true);
        }

        // ===== 锻造提交（算伤害 + 消耗） =====

        public int PreviewDamage(List<CardInstance>[] anvilStacks)
        {
            SyncSlots(anvilStacks);
            return CombatCalculator.CalculateTotalBoardDamage(State);
        }

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
        /// 锻造提交：同步砧台 → 算伤害 → 存 PendingRamDamage → 消耗在场矿石（驻台矿保留）。
        /// </summary>
        public int CommitForge(List<CardInstance>[] anvilStacks, List<CardInstance> tableCards)
        {
            SyncSlots(anvilStacks);
            var damage = CombatCalculator.CalculateTotalBoardDamage(State);
            State.PendingRamDamage = damage;

            // 消耗：铸造台上的矿 → 矿渣堆（驻台 Station 矿保留在台上）
            for (var i = 0; i < CombatCalculator.SlotCount; i++)
            {
                var keep = new List<CardInstance>();
                foreach (var c in State.Slots[i].Cards)
                {
                    if (c.HasTrait(OreTrait.Station))
                    {
                        c.ClearSlot();
                        keep.Add(c); // 驻台矿留下
                    }
                    else
                    {
                        c.ClearSlot();
                        State.Discard.Add(c);
                    }
                }
                State.Slots[i].Cards.Clear();
                State.Slots[i].Cards.AddRange(keep);
            }

            // 桌面未上砧的矿 → 矿渣堆（余烬 Ember 矿保留在精炼盘）
            if (tableCards != null)
            {
                foreach (var c in tableCards)
                {
                    if (c.HasTrait(OreTrait.Ember))
                    {
                        // 余烬矿留在 Hand（不移入矿渣堆）
                        continue;
                    }
                    c.ClearSlot();
                    State.Discard.Add(c);
                }
            }
            // 清除 Hand 中非余烬的矿（桌面矿已处理，这里清 Hand 中已上砧或被弃的）
            // 实际上 Hand 中剩下的应该只有余烬矿
            var handKeep = new List<CardInstance>();
            foreach (var c in State.Hand)
            {
                if (c.HasTrait(OreTrait.Ember))
                    handKeep.Add(c);
            }
            State.Hand.Clear();
            State.Hand.AddRange(handKeep);

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

        // ===== 撞击结算 =====

        /// <summary>
        /// 撞击结算：应用 PendingRamDamage → 判死 → 玩家挨1点 → 判死 → 回合清理 → BeginTurn。
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

                // 贪婪帆船 steal_gold：每造成一次伤害-1银元
                if (State.HasEnemySkill("steal_gold"))
                {
                    result.GoldChange -= 1;
                }
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

            // 掌中银元遗物：回合结束精炼盘每矿+1银元
            var silverPalm = State.GetRelicEffect(RelicEffectType.HandGoldPerTurn);
            if (silverPalm != null)
            {
                result.GoldChange += State.Hand.Count * silverPalm.Gold;
            }

            // 通知金币变化
            if (result.GoldChange != 0)
            {
                GoldChanged?.Invoke(result.GoldChange);
            }

            // 清理：所有矿的 tempBonus 归零
            foreach (var c in State.Deck) c.ClearTemp();
            foreach (var c in State.Discard) c.ClearTemp();
            foreach (var c in State.Hand) c.ClearTemp();
            foreach (var slot in State.Slots)
                foreach (var c in slot.Cards) c.ClearTemp();

            // 清除回合倍率加成
            for (var i = 0; i < CombatCalculator.SlotCount; i++)
                State.Slots[i].RoundMultiplierBonus = 0;

            // 回合结束 → 准备下回合
            BeginTurn();

            return result;
        }

        /// <summary>矿舱剩余矿石数（用于矿仓面板）。</summary>
        public int DeckCount => State.Deck.Count;

        /// <summary>矿舱 + 矿渣堆 + 精炼盘 + 铸造台 总矿石数。</summary>
        public int TotalOreCount => State.Deck.Count + State.Discard.Count + State.Hand.Count
            + System.Linq.Enumerable.Sum(State.Slots, s => s.Cards.Count);

        /// <summary>获取所有矿石实例（战后同步回 RunData 用）。</summary>
        public List<CardInstance> GetAllCards()
        {
            var all = new List<CardInstance>();
            all.AddRange(State.Deck);
            all.AddRange(State.Hand);
            all.AddRange(State.Discard);
            foreach (var slot in State.Slots)
                all.AddRange(slot.Cards);
            return all;
        }
    }
}
