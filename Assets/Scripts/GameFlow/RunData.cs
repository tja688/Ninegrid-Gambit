using System;
using System.Collections.Generic;
using NineGrid.Data;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>矿舱持久化条目：矿石id + 淬火永久加成 + 附魔词条。</summary>
    [Serializable]
    public sealed class DeckEntry
    {
        public string OreId;
        public int PermanentBonus;
        /// <summary>附魔词条（OreTrait 的 int 值，用 | 组合）。0=无附魔。</summary>
        public int TraitsInt;

        public DeckEntry() { }
        public DeckEntry(string oreId, int permanentBonus = 0, int traitsInt = 0)
        {
            OreId = oreId;
            PermanentBonus = permanentBonus;
            TraitsInt = traitsInt;
        }
    }

    /// <summary>精炼厂库存条目。</summary>
    [Serializable]
    public sealed class ShopOreItem
    {
        public string OreId;
        public int Price;
        public bool Bought;
    }

    /// <summary>精炼厂遗物条目。</summary>
    [Serializable]
    public sealed class ShopRelicItem
    {
        public string DisplayName;  // 对应 HullModDataSO.displayName
        public int Price;
        public bool Bought;
    }

    /// <summary>船坞附魔词条条目。</summary>
    [Serializable]
    public sealed class EnchantOption
    {
        public string TraitName;   // OreTrait 名称
        public int Price;
        public bool Used;
    }

    /// <summary>
    /// 一次 run 的全部持久化状态。跨战斗保留金币、矿舱、船体改造、铸造台升级。
    /// 存档用 ES3（已安装）；找不到 ES3 时回退 JsonUtility+PlayerPrefs。
    /// </summary>
    [Serializable]
    public sealed class RunData
    {
        public static RunData Current { get; private set; }

        [Header("进度")]
        public int Act = 1;
        public int StageIndex = 0;
        public bool HasCompletedPrologue;

        [Header("经济")]
        public int Gold;

        [Header("生命")]
        public int MaxHearts = 3;

        [Header("矿舱（跨战斗持久）")]
        public List<DeckEntry> Deck = new();

        [Header("船体改造（displayName 列表）")]
        public List<string> Relics = new();

        [Header("铸造台永久倍率提升")]
        public int[] SlotUpgrades = { 0, 0, 0 };

        [Header("待处理的战后流程")]
        public string PendingPostBattle;           // shop_high_event 等
        public string PendingEventTier;            // high/mid/low/boss
        public int PendingEventCount;
        public List<string> PendingEventNames = new(); // 已生成的事件选项名
        public List<string> BossRelicOptions = new();  // BOSS改造三选一 displayName

        [Header("临时修正")]
        public int NextMonsterHpPenalty;
        public int NextBattleHeartBonus;

        [Header("精炼厂状态")]
        public List<ShopOreItem> ShopOres = new();
        public List<ShopRelicItem> ShopRelics = new();
        public int ShopRefreshCost = 5;
        public int ShopRemoveCost = 2;
        public bool ShopFirstRefreshUsed;
        public Dictionary<string, int> ShopUpgradeCosts = new();

        [Header("船坞状态")]
        public List<EnchantOption> EnchantOptions = new();
        public int BlacksmithRefreshCost = 5;
        public bool BlacksmithFirstEnchantFree = true;
        public bool BlacksmithFirstEnchantUsed;
        public bool BlacksmithSlotUpgraded;
        public List<string> BlacksmithEnchantedKeywords = new();

        // ===== 生命周期 =====

        public static RunData Ensure()
        {
            if (Current == null)
            {
                Current = Load();
            }
            return Current;
        }

        public static void StartNewRun()
        {
            Current = new RunData();
            Current.InitStartingDeck();
            Current.HasCompletedPrologue = GameFlowProgress.HasCompletedPrologue;
            Save();
            Debug.Log($"[RunData] 新 run 开始：金币={Current.Gold} 矿舱={Current.Deck.Count}块 改造={Current.Relics.Count}件");
        }

        void InitStartingDeck()
        {
            Deck.Clear();
            // 老兵初始牌库：5齐心 + 4助燃 + 1老兵
            for (var i = 0; i < 5; i++) Deck.Add(new DeckEntry("ore_qixinxieli"));
            for (var i = 0; i < 4; i++) Deck.Add(new DeckEntry("ore_zhuran"));
            Deck.Add(new DeckEntry("ore_laobing"));
        }

        // ===== 存档 =====

        const string SaveKey = "NinegridRunData";

        public static void Save()
        {
            if (Current == null) return;
            try
            {
                var json = JsonUtility.ToJson(Current);
                PlayerPrefs.SetString(SaveKey, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunData] 存档失败：{e.Message}");
            }
        }

        public static RunData Load()
        {
            try
            {
                if (PlayerPrefs.HasKey(SaveKey))
                {
                    var json = PlayerPrefs.GetString(SaveKey);
                    var data = JsonUtility.FromJson<RunData>(json);
                    if (data != null)
                    {
                        // 字典不被 JsonUtility 序列化，重建
                        if (data.ShopUpgradeCosts == null) data.ShopUpgradeCosts = new();
                        Debug.Log($"[RunData] 读档成功：金币={data.Gold} 矿舱={data.Deck?.Count ?? 0}块 改造={data.Relics?.Count ?? 0}件");
                        return data;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RunData] 读档失败：{e.Message}");
            }
            return new RunData();
        }

        public static void ClearSave()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            Current = null;
            Debug.Log("[RunData] 存档已清除");
        }

        // ===== GameFlowState → 节点映射 =====

        /// <summary>把 Unity 的 GameFlowState 映射到 web 的节点 key（如 "1-1"）。</summary>
        public static string GetStageKey(GameFlowState state)
        {
            return state switch
            {
                GameFlowState.Prologue => "1-1",
                GameFlowState.Battle0 => "1-1",
                GameFlowState.Battle1 => "1-2",
                GameFlowState.Battle2 => "1-3",
                GameFlowState.Battle3Elite => "1-4",
                GameFlowState.Battle4 => "1-5",
                GameFlowState.Battle5 => "1-6",
                GameFlowState.Battle6 => "1-7",
                GameFlowState.BossBattle => "1-8",
                _ => "1-1",
            };
        }

        /// <summary>当前 GameFlowState 对应的节点配置。</summary>
        public static StageConfig GetStageConfig(GameFlowState state)
        {
            var key = GetStageKey(state);
            return WebGameData.GetStage(key);
        }

        /// <summary>岛屿状态 → 是精炼厂还是船坞。</summary>
        public static bool IsRefinery(GameFlowState state)
        {
            return state switch
            {
                GameFlowState.Island1 or GameFlowState.Island2 or GameFlowState.Island3
                or GameFlowState.Island4 or GameFlowState.Island5 or GameFlowState.Island6 => true,
                GameFlowState.IslandEliteReward => false,
                _ => true,
            };
        }

        /// <summary>事件状态 → 对应的战后事件池类型。</summary>
        public static string GetEventPoolType(GameFlowState state)
        {
            return state switch
            {
                GameFlowState.Event1 or GameFlowState.Event2 or GameFlowState.Event3 => "high",
                GameFlowState.Event4 => "mid",
                GameFlowState.Event5 or GameFlowState.Event6 => "low",
                GameFlowState.VictorySettlement => "boss",
                _ => "high",
            };
        }

        /// <summary>事件状态 → 前置战斗的 stage key（用于查 goldReward 等）。</summary>
        public static string GetPrecedingBattleStageKey(GameFlowState state)
        {
            return state switch
            {
                GameFlowState.Event1 => "1-1",
                GameFlowState.Event2 => "1-2",
                GameFlowState.Event3 => "1-3",
                GameFlowState.Event4 => "1-4",
                GameFlowState.Event5 => "1-5",
                GameFlowState.Event6 => "1-6",
                GameFlowState.VictorySettlement => "1-8",
                _ => "1-1",
            };
        }

        // ===== 战斗结算 =====

        /// <summary>战斗胜利后结算：加金币、保存牌库、设置战后流程。</summary>
        public void ResolveBattleWin(GameFlowState battleState, int turnsUsed, bool firstTurnKill)
        {
            var config = GetStageConfig(battleState);
            var goldGain = config?.GoldReward ?? 6;

            if (firstTurnKill) goldGain += 2;

            // 赏金猎人遗物
            var fastKill = GetRelicEffect(RelicEffectType.FastKillGold);
            if (fastKill != null && turnsUsed <= (fastKill.Turns > 0 ? fastKill.Turns : 2))
                goldGain += fastKill.Gold > 0 ? fastKill.Gold : 2;

            // 扒船遗物：战斗开始+1（在 InitBattle 时加，这里不重复）

            Gold += goldGain;
            Debug.Log($"[RunData] 战斗胜利：+{goldGain}银元（当前{Gold}），{turnsUsed}回合");

            // 设置战后流程
            PendingPostBattle = config?.PostBattle ?? "shop_high_event";
            var (tier, count) = WebGameData.GetEventPool(PendingPostBattle);
            PendingEventTier = tier;
            PendingEventCount = count;
            PendingEventNames.Clear();

            // BOSS改造三选一
            if (PendingPostBattle == "boss_relic_event")
            {
                var stageKey = GetStageKey(battleState);
                var stageConfig = WebGameData.GetStage(stageKey);
                var monsterId = stageConfig != null && stageConfig.MonsterPool.Length > 0
                    ? stageConfig.MonsterPool[0] : "gold_king_flagship";
                var bossRelics = WebGameData.PickBossRelics(monsterId);
                BossRelicOptions.Clear();
                foreach (var r in bossRelics) BossRelicOptions.Add(r.DisplayName);
            }

            Save();
        }

        /// <summary>战斗失败：清除 run 存档，回主菜单。</summary>
        public static void ResolveDefeat()
        {
            ClearSave();
        }

        // ===== 遗物查询 =====

        /// <summary>检查玩家是否拥有指定效果类型的遗物（返回第一个匹配的 RelicDef）。</summary>
        public RelicDef GetRelicEffect(RelicEffectType effectType)
        {
            foreach (var name in Relics)
            {
                var def = WebGameData.GetRelicByName(name);
                if (def != null && def.EffectType == effectType) return def;
            }
            return null;
        }

        /// <summary>玩家是否拥有指定效果类型的遗物。</summary>
        public bool HasRelicEffect(RelicEffectType effectType) => GetRelicEffect(effectType) != null;

        /// <summary>获取所有未禁用的遗物定义。</summary>
        public List<RelicDef> GetActiveRelics(string disabledRelicKey = null)
        {
            var result = new List<RelicDef>();
            foreach (var name in Relics)
            {
                if (!string.IsNullOrEmpty(disabledRelicKey) && name == disabledRelicKey) continue;
                var def = WebGameData.GetRelicByName(name);
                if (def != null) result.Add(def);
            }
            return result;
        }

        // ===== 牌库同步 =====

        /// <summary>把战斗后的 CardInstance 列表同步回持久牌库（保留淬火永久加成）。</summary>
        public void SyncDeckFromCombat(List<NineGrid.Battle.Combat.CardInstance> cards)
        {
            Deck.Clear();
            foreach (var c in cards)
            {
                if (c == null || c.IsDerived) continue;
                Deck.Add(new DeckEntry(c.OreId, c.PermanentBonus, (int)c.Traits));
            }
            Save();
        }

        /// <summary>把持久牌库转为 CardInstance 列表（战斗初始化时用）。</summary>
        public List<NineGrid.Battle.Combat.CardInstance> BuildCombatDeck(OreCatalog catalog)
        {
            var list = new List<NineGrid.Battle.Combat.CardInstance>();
            if (catalog == null) return list;
            foreach (var entry in Deck)
            {
                if (catalog.TryGet(entry.OreId, out var oreEntry))
                {
                    var card = new NineGrid.Battle.Combat.CardInstance(oreEntry);
                    card.PermanentBonus = entry.PermanentBonus; // 保留淬火
                    // 应用附魔词条（叠加到原有词条上）
                    if (entry.TraitsInt != 0)
                        card.Traits |= (NineGrid.Data.OreTrait)entry.TraitsInt;
                    list.Add(card);
                }
                else
                {
                    Debug.LogWarning($"[RunData] 牌库矿石 {entry.OreId} 在 OreCatalog 中找不到，跳过。");
                }
            }
            return list;
        }

        /// <summary>向矿舱加入一块矿石。</summary>
        public void AddOre(string oreId, int permanentBonus = 0)
        {
            Deck.Add(new DeckEntry(oreId, permanentBonus));
            Save();
        }

        /// <summary>从矿舱移除指定索引的矿石。</summary>
        public void RemoveOreAt(int index)
        {
            if (index >= 0 && index < Deck.Count)
            {
                Deck.RemoveAt(index);
                Save();
            }
        }

        /// <summary>添加一件船体改造。</summary>
        public void AddRelic(string displayName)
        {
            if (!Relics.Contains(displayName))
            {
                Relics.Add(displayName);
                Save();
                Debug.Log($"[RunData] 获得船体改造：{displayName}（共{Relics.Count}件）");
            }
        }

        /// <summary>获得全部铸台永久倍率提升（含改造和船坞强化）。</summary>
        public int[] GetEffectiveSlotMultipliers()
        {
            var result = new int[3];
            for (var i = 0; i < 3 && i < SlotUpgrades.Length; i++)
                result[i] = SlotUpgrades[i];

            // 冲击龙骨类：指定铸造台倍率+1
            foreach (var name in Relics)
            {
                var def = WebGameData.GetRelicByName(name);
                if (def == null) continue;
                if (def.EffectType == RelicEffectType.SlotBonus && def.SlotIndex >= 0 && def.SlotIndex < 3)
                    result[def.SlotIndex] += def.Bonus;
            }
            return result;
        }

        /// <summary>是否有老主顾遗物（首次刷新免费）。</summary>
        public bool HasFirstRefreshFree => HasRelicEffect(RelicEffectType.FirstRefreshFree);

        /// <summary>是否有免税遗物（买矿不要钱）。</summary>
        public bool HasFreeCardPurchase => HasRelicEffect(RelicEffectType.FreeCardPurchase);
    }
}
