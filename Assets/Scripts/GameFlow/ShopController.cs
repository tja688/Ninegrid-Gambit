using System.Collections.Generic;
using NineGrid.Data;
using UnityEngine;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 商店逻辑控制器 —— 纯 C# 类，由 IslandController 调用。
    /// 精炼厂(Refinery)：买矿/遗物、删矿、强化矿、刷新。
    /// 船坞(Shipyard)：铸造台强化、附魔、刷新。
    /// 对应 web systems/shop.js + data/shop.js。
    /// </summary>
    public sealed class ShopController
    {
        readonly RunData _run;
        readonly OreCatalog _oreCatalog;
        readonly HullModCatalog _hullModCatalog;

        public ShopController(RunData run, OreCatalog oreCatalog, HullModCatalog hullModCatalog)
        {
            _run = run ?? RunData.Ensure();
            _oreCatalog = oreCatalog;
            _hullModCatalog = hullModCatalog;
        }

        // ===== 精炼厂（Refinery）=====

        /// <summary>生成精炼厂库存：5 个矿石（船体改造在船坞面板）。</summary>
        public void GenerateRefineryStock()
        {
            _run.ShopOres.Clear();

            if (_oreCatalog != null)
            {
                var picked = new HashSet<string>();
                for (var i = 0; i < 5; i++)
                {
                    var tierName = WebGameData.PickRandomOreTier();
                    var ore = PickRandomOreByTier(tierName, picked);
                    if (ore != null)
                    {
                        picked.Add(ore.OreId);
                        var price = WebGameData.GetOrePriceByTier(tierName);
                        _run.ShopOres.Add(new ShopOreItem { OreId = ore.OreId, Price = price });
                    }
                }
            }

            _run.ShopFirstRefreshUsed = false;
            RunData.Save();
        }

        /// <summary>生成船坞船体改造库存：3 件可选改造。</summary>
        public void GenerateShipyardRelicStock()
        {
            _run.ShopRelics.Clear();

            var relicPool = new List<RelicDef>();
            foreach (var r in WebGameData.Relics)
            {
                if (r.Rarity == "boss") continue;
                if (_run.Relics.Contains(r.DisplayName)) continue;
                relicPool.Add(r);
            }

            for (var i = relicPool.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (relicPool[i], relicPool[j]) = (relicPool[j], relicPool[i]);
            }

            for (var i = 0; i < Mathf.Min(3, relicPool.Count); i++)
            {
                var r = relicPool[i];
                _run.ShopRelics.Add(new ShopRelicItem
                {
                    DisplayName = r.DisplayName,
                    Price = r.Price > 0 ? r.Price : (r.Rarity == "common" ? 2 : r.Rarity == "rare" ? 4 : 8)
                });
            }

            RunData.Save();
        }

        /// <summary>购买精炼厂矿石。返回是否成功。</summary>
        public bool BuyOre(int index)
        {
            if (index < 0 || index >= _run.ShopOres.Count) return false;
            var item = _run.ShopOres[index];
            if (item.Bought) return false;

            var price = _run.HasFreeCardPurchase ? 0 : item.Price;
            if (_run.Gold < price)
            {
                Debug.Log($"[Shop] 金币不足：{_run.Gold}/{price}");
                return false;
            }

            _run.Gold -= price;
            item.Bought = true;
            _run.AddOre(item.OreId);
            Debug.Log($"[Shop] 购买矿石 {item.OreId}（-{price}银元，剩余{_run.Gold}）");
            RunData.Save();
            return true;
        }

        /// <summary>购买精炼厂船体改造。返回是否成功。</summary>
        public bool BuyRelic(int index)
        {
            if (index < 0 || index >= _run.ShopRelics.Count) return false;
            var item = _run.ShopRelics[index];
            if (item.Bought) return false;

            if (_run.Gold < item.Price)
            {
                Debug.Log($"[Shop] 金币不足：{_run.Gold}/{item.Price}");
                return false;
            }

            _run.Gold -= item.Price;
            item.Bought = true;
            _run.AddRelic(item.DisplayName);
            Debug.Log($"[Shop] 购船体改造 {item.DisplayName}（-{item.Price}银元，剩余{_run.Gold}）");
            RunData.Save();
            return true;
        }

        /// <summary>删除矿石（费用2起，每次翻倍）。</summary>
        public bool RemoveOre(int deckIndex)
        {
            if (deckIndex < 0 || deckIndex >= _run.Deck.Count) return false;
            var cost = _run.ShopRemoveCost;
            if (_run.Gold < cost)
            {
                Debug.Log($"[Shop] 金币不足：{_run.Gold}/{cost}");
                return false;
            }
            _run.Gold -= cost;
            _run.ShopRemoveCost *= 2;
            _run.RemoveOreAt(deckIndex);
            Debug.Log($"[Shop] 删除矿石（-{cost}银元，剩余{_run.Gold}）");
            RunData.Save();
            return true;
        }

        /// <summary>强化矿石+5（费用2起，每块独立计费）。</summary>
        public bool UpgradeOre(int deckIndex)
        {
            if (deckIndex < 0 || deckIndex >= _run.Deck.Count) return false;
            var entry = _run.Deck[deckIndex];
            var costKey = entry.OreId + "_" + deckIndex;
            if (!_run.ShopUpgradeCosts.ContainsKey(costKey))
                _run.ShopUpgradeCosts[costKey] = 2;
            var cost = _run.ShopUpgradeCosts[costKey];
            if (_run.Gold < cost)
            {
                Debug.Log($"[Shop] 金币不足：{_run.Gold}/{cost}");
                return false;
            }
            _run.Gold -= cost;
            _run.ShopUpgradeCosts[costKey] = cost + 2;
            entry.PermanentBonus += 5;
            Debug.Log($"[Shop] 强化矿石 {entry.OreId} +5（-{cost}银元，剩余{_run.Gold}）");
            RunData.Save();
            return true;
        }

        /// <summary>刷新精炼厂库存（费用5起，每次+1）。</summary>
        public bool RefreshRefinery()
        {
            var cost = _run.ShopRefreshCost;
            // 老主顾遗物：首次刷新免费
            if (_run.HasFirstRefreshFree && !_run.ShopFirstRefreshUsed)
            {
                cost = 0;
                _run.ShopFirstRefreshUsed = true;
            }
            if (_run.Gold < cost && cost > 0)
            {
                Debug.Log($"[Shop] 金币不足：{_run.Gold}/{cost}");
                return false;
            }
            if (cost > 0) _run.Gold -= cost;
            _run.ShopRefreshCost += 1;
            GenerateRefineryStock();
            Debug.Log($"[Shop] 刷新精炼厂（-{cost}银元，剩余{_run.Gold}）");
            return true;
        }

        // ===== 船坞（Shipyard）=====

        /// <summary>生成船坞库存：3 件船体改造 + 2 个附魔词条。</summary>
        public void GenerateShipyardStock()
        {
            GenerateShipyardRelicStock();
            _run.EnchantOptions.Clear();
            var traits = new[] { "Station", "Debris", "Quench", "Ember", "Sociable", "Unity", "Preheat", "Symbiosis", "Twin", "Core" };
            var prices = new Dictionary<string, int>
            {
                { "Station", 2 }, { "Debris", 2 }, { "Quench", 2 }, { "Ember", 2 }, { "Sociable", 2 }, { "Unity", 2 },
                { "Preheat", 4 }, { "Symbiosis", 4 }, { "Twin", 4 },
                { "Core", 8 },
            };
            var picked = new HashSet<string>();
            for (var i = 0; i < 2; i++)
            {
                var attempts = 0;
                string trait;
                do
                {
                    trait = traits[Random.Range(0, traits.Length)];
                    attempts++;
                } while (attempts < 20 && (picked.Contains(trait) || _run.BlacksmithEnchantedKeywords.Contains(trait)));
                picked.Add(trait);
                _run.EnchantOptions.Add(new EnchantOption
                {
                    TraitName = trait,
                    Price = prices.GetValueOrDefault(trait, 2)
                });
            }
            _run.BlacksmithFirstEnchantUsed = false;
            RunData.Save();
        }

        /// <summary>铸造台强化：随机一个铸造台倍率+1（4银元，同一船坞限一次）。</summary>
        public bool UpgradeSlot()
        {
            if (_run.BlacksmithSlotUpgraded) return false;
            var cost = 4;
            if (_run.Gold < cost)
            {
                Debug.Log($"[Shipyard] 金币不足：{_run.Gold}/{cost}");
                return false;
            }
            _run.Gold -= cost;
            _run.BlacksmithSlotUpgraded = true;
            var slotIdx = Random.Range(0, 3);
            _run.SlotUpgrades[slotIdx]++;
            Debug.Log($"[Shipyard] 铸造台{slotIdx}倍率+1（-{cost}银元，剩余{_run.Gold}）");
            RunData.Save();
            return true;
        }

        /// <summary>附魔：给指定矿石添加词条。</summary>
        public bool EnchantOre(int deckIndex, int enchantIndex)
        {
            if (deckIndex < 0 || deckIndex >= _run.Deck.Count) return false;
            if (enchantIndex < 0 || enchantIndex >= _run.EnchantOptions.Count) return false;
            var enchant = _run.EnchantOptions[enchantIndex];
            if (enchant.Used) return false;

            var cost = enchant.Price;
            // 首次附魔免费
            if (_run.BlacksmithFirstEnchantFree && !_run.BlacksmithFirstEnchantUsed)
            {
                cost = 0;
                _run.BlacksmithFirstEnchantUsed = true;
            }
            if (_run.Gold < cost && cost > 0)
            {
                Debug.Log($"[Shipyard] 金币不足：{_run.Gold}/{cost}");
                return false;
            }
            if (cost > 0) _run.Gold -= cost;
            enchant.Used = true;
            _run.BlacksmithEnchantedKeywords.Add(enchant.TraitName);

            // 应用词条到矿石（持久化到 DeckEntry.TraitsInt）
            var trait = ParseOreTrait(enchant.TraitName);
            var entry = _run.Deck[deckIndex];
            entry.TraitsInt |= (int)trait;
            Debug.Log($"[Shipyard] 附魔 {entry.OreId} +{enchant.TraitName}（-{cost}银元，剩余{_run.Gold}）");
            RunData.Save();
            return true;
        }

        /// <summary>刷新船坞库存（费用5起）。</summary>
        public bool RefreshShipyard()
        {
            var cost = _run.BlacksmithRefreshCost;
            if (_run.HasFirstRefreshFree && !_run.BlacksmithFirstEnchantUsed)
            {
                cost = 0;
                _run.BlacksmithFirstEnchantUsed = true;
            }
            if (_run.Gold < cost && cost > 0)
            {
                Debug.Log($"[Shipyard] 金币不足：{_run.Gold}/{cost}");
                return false;
            }
            if (cost > 0) _run.Gold -= cost;
            _run.BlacksmithRefreshCost += 1;
            GenerateShipyardStock();
            Debug.Log($"[Shipyard] 刷新船坞（-{cost}银元，剩余{_run.Gold}）");
            return true;
        }

        // ===== 辅助 =====

        OreDataEntry PickRandomOreByTier(string tierName, HashSet<string> exclude)
        {
            if (_oreCatalog == null) return null;
            var candidates = new List<OreDataEntry>();
            foreach (var entry in _oreCatalog.Entries)
            {
                if (exclude.Contains(entry.OreId)) continue;
                if (entry.Tier.ToString() == tierName)
                    candidates.Add(entry);
            }
            if (candidates.Count == 0)
            {
                // 降级：从全部矿石选
                foreach (var entry in _oreCatalog.Entries)
                    if (!exclude.Contains(entry.OreId))
                        candidates.Add(entry);
            }
            if (candidates.Count == 0) return null;
            return candidates[Random.Range(0, candidates.Count)];
        }

        public static OreTrait ParseOreTrait(string name)
        {
            return name switch
            {
                "Ember" => OreTrait.Ember,
                "Quench" => OreTrait.Quench,
                "Quench2" => OreTrait.Quench2,
                "Station" => OreTrait.Station,
                "Debris" => OreTrait.Debris,
                "Twin" => OreTrait.Twin,
                "Preheat" => OreTrait.Preheat,
                "Symbiosis" => OreTrait.Symbiosis,
                "Symbiosis2" => OreTrait.Symbiosis2,
                "Core" => OreTrait.Core,
                "Unity" => OreTrait.Unity,
                "Sociable" => OreTrait.Sociable,
                _ => OreTrait.None,
            };
        }

        public string GetRefineryPanelHint()
            => "精炼厂：悬停商品或服务查看详情，点击购买或办理。";

        public string GetShipyardPanelHint()
            => "船坞：悬停改造查看详情；附魔/铸造台强化可用键盘 S、1、2。";

        public string GetOreHoverText(int index)
        {
            if (index < 0 || index >= _run.ShopOres.Count)
            {
                return string.Empty;
            }

            var item = _run.ShopOres[index];
            var ore = _oreCatalog?.Get(item.OreId);
            var name = ore?.DisplayName ?? item.OreId;
            var price = _run.HasFreeCardPurchase ? 0 : item.Price;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(name);
            sb.AppendLine(item.Bought ? "已购买" : $"价格：{price} 银元");
            if (!string.IsNullOrEmpty(ore?.Description))
            {
                sb.AppendLine(ore.Description);
            }

            if (!item.Bought)
            {
                sb.AppendLine("点击购买并加入矿舱。");
            }

            return sb.ToString().TrimEnd();
        }

        public string GetRelicHoverText(int index)
        {
            if (index < 0 || index >= _run.ShopRelics.Count)
            {
                return string.Empty;
            }

            var item = _run.ShopRelics[index];
            var def = WebGameData.GetRelicByName(item.DisplayName);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(item.DisplayName);
            sb.AppendLine(item.Bought ? "已购买" : $"价格：{item.Price} 银元");
            if (!string.IsNullOrEmpty(def?.Desc))
            {
                sb.AppendLine(def.Desc);
            }

            if (!item.Bought)
            {
                sb.AppendLine("点击购买船体改造。");
            }

            return sb.ToString().TrimEnd();
        }

        public string GetRemoveOreHoverText()
            => $"删除矿石服务\n费用：{_run.ShopRemoveCost} 银元\n从矿舱移除一块矿石。\n点击后打开矿舱选择。";

        public string GetUpgradeOreHoverText(int deckIndex)
        {
            if (deckIndex < 0 || deckIndex >= _run.Deck.Count)
            {
                return "矿物强化 +5\n费用：2 银元起\n点击后打开矿舱选择要强化的矿石。";
            }

            var entry = _run.Deck[deckIndex];
            var costKey = entry.OreId + "_" + deckIndex;
            if (!_run.ShopUpgradeCosts.ContainsKey(costKey))
            {
                _run.ShopUpgradeCosts[costKey] = 2;
            }

            var cost = _run.ShopUpgradeCosts[costKey];
            var ore = _oreCatalog?.Get(entry.OreId);
            var name = ore?.DisplayName ?? entry.OreId;
            return $"矿物强化 +5\n目标：{name}\n费用：{cost} 银元\n永久 +5 强度。";
        }

        public string GetRefreshRefineryHoverText()
        {
            var cost = _run.ShopRefreshCost;
            if (_run.HasFirstRefreshFree && !_run.ShopFirstRefreshUsed)
            {
                cost = 0;
            }

            return cost <= 0
                ? "刷新精炼厂\n老主顾：本次免费\n重新生成商品栏。"
                : $"刷新精炼厂\n费用：{cost} 银元\n重新生成商品栏。";
        }

        public string GetRefreshShipyardHoverText()
        {
            var cost = _run.BlacksmithRefreshCost;
            return $"刷新船坞\n费用：{cost} 银元\n重新生成改造与附魔选项。";
        }

        public string GetUpgradeSlotHoverText()
            => _run.BlacksmithSlotUpgraded
                ? "铸造台强化\n本船坞已使用过。"
                : "铸造台强化\n费用：4 银元\n随机一座铸造台倍率 +1。\n键盘 S 确认。";

        public string GetEnchantHoverText(int index)
        {
            if (index < 0 || index >= _run.EnchantOptions.Count)
            {
                return string.Empty;
            }

            var opt = _run.EnchantOptions[index];
            var cost = opt.Price;
            if (_run.BlacksmithFirstEnchantFree && !_run.BlacksmithFirstEnchantUsed)
            {
                cost = 0;
            }

            return opt.Used
                ? $"{opt.TraitName}\n已使用。"
                : $"{opt.TraitName}\n费用：{cost} 银元\n为矿舱首块矿石附加词条。\n键盘 {index + 1} 确认。";
        }
    }
}
