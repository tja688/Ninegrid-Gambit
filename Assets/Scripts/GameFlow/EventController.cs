using System;
using System.Collections.Generic;
using NineGrid.Data;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 事件效果逻辑（无 UI）。场景交互由 <see cref="RouteController"/> + <see cref="OreInventoryPanel"/> 驱动。
    /// 对应 web systems/post-battle.js + input/index.js handleEventClick。
    /// </summary>
    public static class EventController
    {
        /// <summary>生成并缓存当前节点的事件选项。</summary>
        public static List<EventDef> PrepareOptions(GameFlowState state)
        {
            var poolType = RunData.GetEventPoolType(state);
            var run = RunData.Ensure();

            if (run.PendingEventNames != null && run.PendingEventNames.Count > 0)
            {
                var options = new List<EventDef>();
                foreach (var name in run.PendingEventNames)
                {
                    var ev = FindEventByName(name);
                    if (ev != null)
                    {
                        options.Add(ev);
                    }
                }

                if (options.Count > 0)
                {
                    return options;
                }
            }

            var generated = WebGameData.GeneratePostBattleEvents(poolType);
            run.PendingEventNames.Clear();
            foreach (var e in generated)
            {
                run.PendingEventNames.Add(e.Name);
            }

            RunData.Save();
            return generated;
        }

        public static bool NeedsOreSelection(string effect)
        {
            return effect == "buff_card" || effect == "enchant_spread" || effect == "enchant_mighty"
                || effect == "duplicate_card" || effect == "transform_card" || effect == "free_remove_card";
        }

        /// <summary>悬停浮标时显示的完整说明（名称 / 描述 / 效果后果）。</summary>
        public static string BuildHoverText(EventDef ev)
        {
            if (ev == null)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine(ev.Name);
            sb.AppendLine(ev.Desc);
            var outcome = DescribeEffectOutcome(ev);
            if (!string.IsNullOrEmpty(outcome))
            {
                sb.AppendLine(outcome);
            }

            sb.Append("点击选择");
            return sb.ToString();
        }

        static string DescribeEffectOutcome(EventDef ev)
        {
            switch (ev.Effect)
            {
                case "gain_gold":
                    return $"→ 获得 {ev.ParamInt} 银元";
                case "random_strategy_level":
                    return "→ 获得 1 银元";
                case "buff_card":
                    return $"→ 选 1 块矿，强度 +{ev.ParamInt}";
                case "self_blacksmith":
                    return $"→ 消耗 {ev.ParamInt} 银元，随机砧台倍率 +1";
                case "card_pick_three":
                    return "→ 三选一矿石（随机获得其一）";
                case "free_remove_card":
                    return "→ 选 1 块矿免费移除";
                case "buy_random_relic":
                    return $"→ 消耗 {ev.ParamInt} 银元，随机获得船体改造";
                case "pick_rare_relic":
                    return "→ 随机获得 1 件中阶船体改造";
                case "gain_specific_card":
                    return $"→ 获得矿石 {ResolveOreName(!string.IsNullOrEmpty(ev.ParamStr) ? ev.ParamStr : ev.Name)}";
                case "next_monster_hp_down":
                    return $"→ 下一场敌舰装甲 -{ev.ParamInt}";
                case "next_battle_hearts_plus":
                    return $"→ 下一场备用锚 +{ev.ParamInt}";
                case "max_hearts_plus":
                    return "→ 本局备用锚上限 +1";
                case "enchant_mighty":
                    return "→ 选 1 块矿获得熔核";
                case "enchant_spread":
                    return "→ 选 1 块矿获得碎屑";
                case "duplicate_card":
                    return "→ 选 1 块矿复制入矿舱";
                case "transform_card":
                    return "→ 选 1 块矿随机转化";
                case "grow_cards_twice":
                    return "→ 矿舱所有矿石强度 +2";
                case "fight_monster":
                case "fight_elite":
                    return "→ 遭遇战（暂未接入，直接继续）";
                case "next_shop_system":
                    return "→ 下次精炼厂矿脉锁定（占位）";
                default:
                    return string.Empty;
            }
        }

        /// <summary>应用事件效果，返回结果提示文案。</summary>
        public static string ApplyEffect(EventDef ev, int oreIndex = -1)
        {
            var run = RunData.Ensure();

            if (NeedsOreSelection(ev.Effect) && run.Deck.Count == 0)
            {
                return $"{ev.Name}：矿舱为空，无法生效。";
            }

            switch (ev.Effect)
            {
                case "gain_gold":
                    run.Gold += ev.ParamInt;
                    RunData.Save();
                    return $"获得 {ev.ParamInt} 银元（当前 {run.Gold}）。";

                case "random_strategy_level":
                    run.Gold += 1;
                    RunData.Save();
                    return "获得 1 银元。";

                case "buff_card":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        run.Deck[oreIndex].PermanentBonus += ev.ParamInt;
                        RunData.Save();
                        return $"选中矿石强度 +{ev.ParamInt}。";
                    }
                    break;

                case "self_blacksmith":
                    if (run.Gold >= ev.ParamInt)
                    {
                        run.Gold -= ev.ParamInt;
                        var slot = UnityEngine.Random.Range(0, 3);
                        run.SlotUpgrades[slot]++;
                        RunData.Save();
                        return $"消耗 {ev.ParamInt} 银元，铸造台 {slot + 1} 倍率 +1。";
                    }

                    return "银元不足，效果未生效。";

                case "card_pick_three":
                    return ApplyCardPickThree(run);

                case "free_remove_card":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        var removed = run.Deck[oreIndex].OreId;
                        run.RemoveOreAt(oreIndex);
                        return $"已移除矿石 {removed}。";
                    }
                    break;

                case "buy_random_relic":
                    if (run.Gold >= ev.ParamInt)
                    {
                        run.Gold -= ev.ParamInt;
                        var pool = new List<RelicDef>();
                        foreach (var r in WebGameData.Relics)
                        {
                            if (r.Rarity != "boss" && !run.Relics.Contains(r.DisplayName))
                            {
                                pool.Add(r);
                            }
                        }

                        if (pool.Count > 0)
                        {
                            var picked = pool[UnityEngine.Random.Range(0, pool.Count)];
                            run.AddRelic(picked.DisplayName);
                            RunData.Save();
                            return $"获得船体改造：{picked.DisplayName}。";
                        }

                        RunData.Save();
                        return "没有可获得的新改造。";
                    }

                    return "银元不足，效果未生效。";

                case "pick_rare_relic":
                    return ApplyPickRareRelic(run);

                case "gain_relic":
                    if (!string.IsNullOrEmpty(ev.ParamStr))
                    {
                        run.AddRelic(ev.ParamStr);
                        return $"获得船体改造：{ev.ParamStr}。";
                    }
                    break;

                case "gain_specific_card":
                    var oreId = !string.IsNullOrEmpty(ev.ParamStr) ? ev.ParamStr : ev.Name;
                    run.AddOre(oreId);
                    return $"获得矿石：{ResolveOreName(oreId)}。";

                case "next_monster_hp_down":
                    run.NextMonsterHpPenalty += ev.ParamInt;
                    RunData.Save();
                    return $"下一场敌舰装甲 -{ev.ParamInt}。";

                case "next_battle_hearts_plus":
                    run.NextBattleHeartBonus += ev.ParamInt;
                    RunData.Save();
                    return $"下一场备用锚 +{ev.ParamInt}。";

                case "max_hearts_plus":
                    run.MaxHearts += 1;
                    RunData.Save();
                    return $"备用锚上限 +1（当前 {run.MaxHearts}）。";

                case "enchant_mighty":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        run.Deck[oreIndex].TraitsInt |= (int)OreTrait.Core;
                        RunData.Save();
                        return "选中矿石获得熔核特性。";
                    }
                    break;

                case "enchant_spread":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        run.Deck[oreIndex].TraitsInt |= (int)OreTrait.Debris;
                        RunData.Save();
                        return "选中矿石获得碎屑特性。";
                    }
                    break;

                case "duplicate_card":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        var src = run.Deck[oreIndex];
                        run.AddOre(src.OreId, src.PermanentBonus);
                        return "已复制选中矿石。";
                    }
                    break;

                case "transform_card":
                    if (oreIndex >= 0 && oreIndex < run.Deck.Count)
                    {
                        var newOre = PickRandomOreId();
                        run.Deck[oreIndex].OreId = newOre;
                        RunData.Save();
                        return $"矿石变为 {ResolveOreName(newOre)}。";
                    }
                    break;

                case "grow_cards_twice":
                    foreach (var entry in run.Deck)
                    {
                        entry.PermanentBonus += 2;
                    }
                    RunData.Save();
                    return "矿舱内所有矿石强度 +2。";

                case "fight_monster":
                case "fight_elite":
                    return $"{ev.Name}：遭遇战暂未接入，直接继续航行。";

                case "next_shop_system":
                    return "下次精炼厂矿脉已锁定（占位）。";

                default:
                    return $"{ev.Name}：效果 {ev.Effect} 暂未实现。";
            }

            return $"{ev.Name}：未能生效。";
        }

        /// <summary>BOSS 改造：取第一项（赶时间简化；后续可改为场景三选一）。</summary>
        public static string ApplyBossRelicChoice(int index)
        {
            var run = RunData.Ensure();
            if (run.BossRelicOptions == null || run.BossRelicOptions.Count == 0)
            {
                var relics = WebGameData.PickBossRelics("gold_king_flagship");
                run.BossRelicOptions.Clear();
                foreach (var r in relics)
                {
                    run.BossRelicOptions.Add(r.DisplayName);
                }
            }

            if (index < 0 || index >= run.BossRelicOptions.Count)
            {
                index = 0;
            }

            var name = run.BossRelicOptions[index];
            run.AddRelic(name);
            run.BossRelicOptions.Clear();
            RunData.Save();
            return $"获得旗舰改造：{name}。";
        }

        static string ApplyCardPickThree(RunData run)
        {
            var options = new List<string>();
            for (var i = 0; i < 3; i++)
            {
                options.Add(PickRandomOreId());
            }

            var picked = options[UnityEngine.Random.Range(0, options.Count)];
            run.AddOre(picked);
            return $"三选一（自动）：获得 {ResolveOreName(picked)}。";
        }

        static string ApplyPickRareRelic(RunData run)
        {
            var relicPool = new List<RelicDef>();
            foreach (var r in WebGameData.Relics)
            {
                if (r.Rarity == "rare" && !run.Relics.Contains(r.DisplayName))
                {
                    relicPool.Add(r);
                }
            }

            if (relicPool.Count == 0)
            {
                return "没有可获得的中阶改造。";
            }

            var picked = relicPool[UnityEngine.Random.Range(0, relicPool.Count)];
            run.AddRelic(picked.DisplayName);
            return $"沉船宝藏：获得 {picked.DisplayName}。";
        }

        public static EventDef FindEventByName(string name)
        {
            foreach (var e in WebGameData.CommonEvents)
            {
                if (e.Name == name)
                {
                    return e;
                }
            }

            foreach (var e in WebGameData.RareEvents)
            {
                if (e.Name == name)
                {
                    return e;
                }
            }

            foreach (var e in WebGameData.LegendaryEvents)
            {
                if (e.Name == name)
                {
                    return e;
                }
            }

            return null;
        }

        static string PickRandomOreId()
        {
            var catalog = LoadOreCatalog();
            if (catalog == null || catalog.Entries.Count == 0)
            {
                return "ore_chutie";
            }

            return catalog.Entries[UnityEngine.Random.Range(0, catalog.Entries.Count)].OreId;
        }

        static string ResolveOreName(string oreId)
        {
            var catalog = LoadOreCatalog();
            if (catalog != null && catalog.TryGet(oreId, out var ore))
            {
                return ore.DisplayName;
            }

            return oreId;
        }

        static OreCatalog _cachedCatalog;
        static OreCatalog LoadOreCatalog()
        {
            if (_cachedCatalog != null)
            {
                return _cachedCatalog;
            }

#if UNITY_EDITOR
            _cachedCatalog = UnityEditor.AssetDatabase.LoadAssetAtPath<OreCatalog>(
                "Assets/ScriptableObjects/Data/OreCatalog.asset");
#endif
            return _cachedCatalog;
        }
    }
}
