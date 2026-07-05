using System.Collections.Generic;
using System.Text;
using NineGrid.Battle.Combat;
using NineGrid.Data;

namespace NineGrid.GameFlow
{
    /// <summary>
    /// 矿石战斗机制描述（模板：名称：提供 X 点钻头伤害，词条：效果；…）。
    /// </summary>
    public static class OreMechanicalText
    {
        struct TraitDef
        {
            public OreTrait Flag;
            public string Label;
            public string Effect;
        }

        static readonly TraitDef[] TraitDefs =
        {
            new() { Flag = OreTrait.Ember, Label = "余烬", Effect = "抛锚撞击后仍留精炼盘，不移入矿渣堆也不返矿舱" },
            new() { Flag = OreTrait.Quench2, Label = "淬火2", Effect = "摆下时本矿石永久+2点" },
            new() { Flag = OreTrait.Quench, Label = "淬火", Effect = "摆下时本矿石永久+1点" },
            new() { Flag = OreTrait.Station, Label = "驻台", Effect = "抛锚撞击后本矿石不移入矿渣堆，下回合开始时移除本矿石驻台特性" },
            new() { Flag = OreTrait.Debris, Label = "碎屑", Effect = "摆下时在铸造台生成1块0点矿渣" },
            new() { Flag = OreTrait.Twin, Label = "双晶", Effect = "摆下时在精炼盘复制1块与自身相同的矿石" },
            new() { Flag = OreTrait.Preheat, Label = "预热", Effect = "本铸造台上下一块投入的本矿石获得上方预热矿半数点数（向下取整）" },
            new() { Flag = OreTrait.Symbiosis2, Label = "共生2", Effect = "摆下时从矿舱抽2块矿石入精炼盘" },
            new() { Flag = OreTrait.Symbiosis, Label = "共生", Effect = "摆下时从矿舱抽1块矿石入精炼盘" },
            new() { Flag = OreTrait.Core, Label = "熔核", Effect = "伤害结算时本矿石点数翻倍" },
            new() { Flag = OreTrait.Unity, Label = "齐心", Effect = "本铸造台每多1块其他矿石，本矿石+1点" },
            new() { Flag = OreTrait.Sociable, Label = "合群", Effect = "相邻铸造台每有1块矿石，本矿石+1点" },
        };

        static readonly Dictionary<string, string> SpecialClauses = new()
        {
            ["ore_dianhou"] = "殿后：本回合最后一块投入时额外+10点",
            ["ore_xianshou"] = "先手：本回合首块投入时额外+5点",
            ["ore_duanhen"] = "锻痕：每次投入时本矿石永久+1点",
            ["ore_mengduan"] = "猛锻：淬火时本矿石额外永久+1点",
            ["ore_touzhi"] = "透支：本场战斗后续每次再投入本矿石时永久-10点",
            ["ore_shuliantouzhi"] = "熟练透支：本场战斗后续每次再投入本矿石时永久-15点",
            ["ore_heduan"] = "合锻：摆下时随机将1块精炼盘矿石送回矿舱",
            ["ore_xuetuzhuzao"] = "学徒：摆下时本铸造台倍率+1（本回合）",
            ["ore_dashizhuzao"] = "大师：摆下时本铸造台倍率+2（本回合）",
            ["ore_wanmeijili"] = "借力：伤害结算时加上相邻铸造台所有矿石当前点数之和",
            ["ore_duzuan"] = "独钻：摆下时将矿舱与精炼盘所有矿石当前点数加总到本矿石",
            ["ore_wosi"] = "我思：摆下时按当前铸造台布局自适应转化（效果因局而异）",
            ["ore_hexumoulue"] = "蛮撞：不参与叠牌点数加成，仅按基础点数结算",
            ["ore_choulou"] = "炫耀：本铸造台叠牌≥3层时倍率额外+1",
            ["ore_routizhihui"] = "体智：单块投入即可打出全额点数，不依赖叠牌层数",
            ["ore_jitiicuihuo"] = "集体淬火：摆下时矿舱内所有矿石获得1次淬火",
            ["ore_cuihuochengguo"] = "成果：吸收矿舱已有淬火累积并转化为自身点数",
            ["ore_sihuo"] = "私货：临时加入精炼盘，本场战斗结束后自动移除",
            ["ore_kuangzha"] = "矿渣：占用铸造台槽位，不提供钻头伤害",
        };

        /// <summary>生成完整机制描述（不含轮播截断）。</summary>
        public static string Build(CardInstance card)
        {
            if (card == null)
            {
                return "矿石信息：未知";
            }

            return Build(card.OreId, card.DisplayName, card.GetBaseValue(), card.Traits);
        }

        /// <summary>生成完整机制描述（数据层条目）。</summary>
        public static string Build(OreDataEntry entry)
        {
            if (entry == null)
            {
                return "矿石信息：未知";
            }

            return Build(entry.OreId, entry.DisplayName, entry.BasePoints, entry.Traits);
        }

        public static string Build(string oreId, string displayName, int points, OreTrait traits)
        {
            var sb = new StringBuilder();
            sb.Append(displayName).Append('：').Append("提供 ").Append(points).Append(" 点钻头伤害");

            if (!string.IsNullOrEmpty(oreId) && SpecialClauses.TryGetValue(oreId, out var special))
            {
                sb.Append('，').Append(special);
            }

            var traitLines = CollectTraitLines(traits);
            for (var i = 0; i < traitLines.Count; i++)
            {
                sb.Append('，').Append(traitLines[i].Label).Append('：').Append(traitLines[i].Effect);
            }

            return sb.ToString();
        }

        static List<TraitDef> CollectTraitLines(OreTrait traits)
        {
            var lines = new List<TraitDef>(4);
            for (var i = 0; i < TraitDefs.Length; i++)
            {
                var line = TraitDefs[i];
                if ((traits & line.Flag) == 0)
                {
                    continue;
                }

                if (line.Flag == OreTrait.Quench && (traits & OreTrait.Quench2) != 0)
                {
                    continue;
                }

                if (line.Flag == OreTrait.Symbiosis && (traits & OreTrait.Symbiosis2) != 0)
                {
                    continue;
                }

                lines.Add(line);
            }

            return lines;
        }
    }
}
