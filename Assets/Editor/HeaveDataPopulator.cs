#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using NineGrid.Data;

namespace NineGrid.Editor
{
    /// <summary>
    /// 一次性工具：生成全部矿石 & 船体改造 SO 资产并配置数据与图片引用。
    /// 菜单路径：NineGrid/Tools/Populate Game Data
    /// </summary>
    public static class HeaveDataPopulator
    {
        const string OreDir      = "Assets/ScriptableObjects/Data/Ores";
        const string HullModDir  = "Assets/ScriptableObjects/Data/HullMods";
        const string CatalogDir  = "Assets/ScriptableObjects/Data";

        const string OreSpritePrefix = "Assets/Arts/Images/Png/separate_2x/";
        const string IconPrefix      = "Assets/Arts/Images/Png/Icons/";

        [MenuItem("NineGrid/Tools/Populate Game Data")]
        public static void Populate()
        {
            EnsureFolder(OreDir);
            EnsureFolder(HullModDir);

            // ========== 矿石 47 ==========
            var oreSOs = new List<OreDataSO>();
            oreSOs.Add(MakeOre("ore_chutie",        "粗铁",       OreTier.Crude,   OreVein.Universal, 15, 2, OreTrait.None,      "从锚岛表层矿坑随手刨出来的粗铁块，杂质多、形状歪，但胜在沉。",                          "001_iron_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_duyin",          "镀银",       OreTier.Crude,   OreVein.Universal, 10, 2, OreTrait.None,      "工匠在粗铁外层薄薄镀了一层银。卖相好，撞击时还能溅出漂亮的火花——可惜火花不伤人。",       "005_silver_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_dianhou",        "殿后矿",     OreTier.Crude,   OreVein.Universal, 10, 2, OreTrait.None,      "船舱最深处压着的压舱矿料。平时没人动它，等主钻头都铸完了，这块才被翻出来补最后一层。",    "012_weathered_lead_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_yachangshi",     "压舱石",     OreTier.Crude,   OreVein.Universal,  8, 2, OreTrait.None,      "本来只是压舱用的石头。紧急时顺手抄起往铸造台一塞——反正也是矿石嘛，别挑。",               "011_lead_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_chuanmao",       "船锚徽记",   OreTier.Crude,   OreVein.Universal,  5, 2, OreTrait.None,      "一枚小小的船锚形状徽章。工程师说把它焊在钻头上能鼓舞士气——点数不高，但象征意义大。",      "017_bismuth_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_xianshou",       "先手矿",     OreTier.Crude,   OreVein.Universal, 10, 2, OreTrait.None,      "精炼最快的矿石。抛锚前头一块摆上，能让后续矿料顺着已经热起来的槽道滑进去。",              "013_nickel_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_tongzhu",        "同铸",       OreTier.Crude,   OreVein.Universal, 10, 2, OreTrait.Unity,     "两块必须一起下炉的矿石。工匠说它们\u300C认彼此\u300D，分开了反而不出活。",                         "022_bronze_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_youhao",         "友好矿脉",   OreTier.Crude,   OreVein.Universal, 10, 2, OreTrait.Sociable,  "产自共生矿脉中段的温和矿石，和谁都能搭。工程师把它塞到哪一层都不嫌挤。",                   "024_brass_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_wuxin",          "钨芯",       OreTier.Crude,   OreVein.Universal, 10, 2, OreTrait.Station,   "含钨量极高的耐热芯块。哪怕抛锚撞击的高温也熔不化它——下一次抛锚还能继续用。",              "009_zinc_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_yujin",          "余烬矿",     OreTier.Crude,   OreVein.Universal, 10, 2, OreTrait.Ember,     "始终温热的矿石。摆在精炼盘里不熄灭，下回合捞出来时正好是最佳温度。",                       "028_mercury_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_tankuang",       "探矿",       OreTier.Crude,   OreVein.Universal,  8, 2, OreTrait.Symbiosis, "工程师把这块矿石当作\u300C探针\u300D：摆下去时能感知矿舱里还有什么好料，顺手抽上来一块。",           "008_tin_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_pateng",         "爬藤矿",     OreTier.Crude,   OreVein.Universal, 10, 2, OreTrait.Debris,    "表面布满藤状纹路的矿石。一砸到铸造台上，碎屑四溅，顺手还给你手里添一块矿渣。",            "003_copper_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_zhuran",         "助燃矿",     OreTier.Crude,   OreVein.Universal,  8, 2, OreTrait.Preheat,   "低熔点的助燃矿石。摆下去就烧自己，把热量全灌给下一块入炉的矿石。",                        "038_spelter_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_chuanling",      "传令矿",     OreTier.Refined, OreVein.Universal, 10, 4, OreTrait.None,      "精炼过的矿石，内部含有传令用的金属薄片。价格不高，但调度起来特别顺手。",                   "006_tarnished_silver_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_linghuodiaodu",  "灵活调度矿", OreTier.Refined, OreVein.Universal, 10, 4, OreTrait.None,      "被工匠切成规整小块的精炼矿。可以迅速塞到任何一个铸造台救急。",                           "014_tarnished_nickel_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_dakuaicutie",    "大块粗铁",   OreTier.Refined, OreVein.Universal, 30, 4, OreTrait.None,      "从矿坑深处刨出来的一大块粗铁。沉，纯度高，光靠吨位就能压出可观的伤害。",                  "002_rusty_iron_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_shuangjing",     "双晶矿",     OreTier.Refined, OreVein.Universal, 10, 4, OreTrait.Twin | OreTrait.Preheat, "天然双晶结构。摆下去的瞬间会分裂出一块复制品到精炼盘；自身还能给后摆上的矿石预热。", "018_iridescent_bismuth_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_jixiantankuang", "极限探矿",   OreTier.Refined, OreVein.Universal,  5, 4, OreTrait.Twin | OreTrait.Symbiosis, "探针的极限版本。摆下时既分裂出复制品又顺带抽一块，代价是本身点数被压缩得可怜。",   "040_melchior_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_liqing",         "理清矿脉",   OreTier.Refined, OreVein.Universal, 10, 4, OreTrait.Symbiosis2, "产自一条特别整齐的矿脉。摆下去时工程师能顺藤摸瓜连抽两块矿石。",                     "029_billon_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_xuantie",        "玄铁锭",     OreTier.PureGold,OreVein.Universal, 60, 8, OreTrait.None,      "锚岛深处才挖得到的玄铁，精炼后凝成一枚高密度的锭。光是一块就够铸出一枚主钻头。",          "033_steel_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_jinggang",       "精钢钻头",   OreTier.PureGold,OreVein.Universal, 20, 8, OreTrait.None,      "一枚工匠预先铸好的精钢钻头，拿来即用。点数看似不高，但它的存在本身就是种奢侈。",           "034_damascus_steel_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_wosi",           "我思故我在", OreTier.PureGold,OreVein.Universal,  0, 8, OreTrait.None,      "一块看不出是什么材质的矿石。摆下去时它会\u300C思考\u300D，按你当前的铸造台布局变成你需要的东西——具体是什么，只有工程师知道。", "042_mokume_ingot_1x.png"));

            // ---- 淬火矿脉 12 ----
            oreSOs.Add(MakeOre("ore_duanhen",        "锻痕",       OreTier.Crude,   OreVein.Quench,  0, 2, OreTrait.None,    "一块布满锤痕的垫底矿石。它本身不造成伤害，但每次锤打都会在上面留下永久痕迹——越用越顺手。",     "002_rusty_iron_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_mengduan",       "猛锻",       OreTier.Crude,   OreVein.Quench,  5, 2, OreTrait.None,    "工匠拿它反复猛锻，淬火值涨得比常规矿石更快。是淬火流的中坚打工矿。",                       "016_cobalt_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_zhuduan",        "助锻矿",     OreTier.Crude,   OreVein.Quench, 10, 2, OreTrait.None,    "专门给其他矿石打辅助的伴锻矿。自己不算锋利，但能让旁边的矿石更快进入状态。",                 "019_platinumum_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_zhanshicuihuo",  "战时淬火",   OreTier.Crude,   OreVein.Quench,  8, 2, OreTrait.Quench2, "战时应急用的淬火工艺，一次摆下就能完成两轮淬火的硬度提升。",                               "032_corroded_pig_iron_1x.png"));
            oreSOs.Add(MakeOre("ore_duanban",        "锻伴",       OreTier.Crude,   OreVein.Quench,  5, 2, OreTrait.Quench2, "一对伴生矿石，必须两个工程师一起锻。摆一次等于淬两次火。",                                 "016_patinated_cobalt_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_duanji",         "锻集",       OreTier.Crude,   OreVein.Quench,  0, 2, OreTrait.Quench2, "一组矿石样板，本身无伤害，但每次被拿出来比对都能让同类矿石淬火两次。",                       "039_chalky_spelter_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_sanshicuihuo",   "三十次淬火", OreTier.Refined, OreVein.Quench, 10, 4, OreTrait.Quench,  "工匠花了三十次反复淬火才铸成的精炼矿。点数一般，但每摆一次就更锋利。",                       "0031_pig_iron_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_jisucuihuo",     "激素淬火",   OreTier.Refined, OreVein.Quench,  0, 4, OreTrait.Quench2, "掺了激素的淬火液。本身没硬度，但淬火次数直接翻倍——典型的\u300C赌后期\u300D矿。",                   "030_tarnished_billon_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_guilvcuihuo",    "规律淬火",   OreTier.Refined, OreVein.Quench, 10, 4, OreTrait.Quench,  "按严格节律反复加热冷却的精炼矿。每次摆下，硬度都稳步上升。",                               "036_pewter_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_cuihuochengguo", "淬火成果",   OreTier.PureGold,OreVein.Quench,  0, 8, OreTrait.Quench,  "工匠毕生淬火的结晶。初始点数零，但它的淬火效果能让整舱矿石的锋芒都堆在它身上。",             "034_damascus_steel_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_jitiicuihuo",    "集体淬火",   OreTier.PureGold,OreVein.Quench, 20, 8, OreTrait.None,    "一整批矿石同时淬火的产物。本身就有纯金矿的硬度，还能给全舱矿石加淬火值。",                   "035_damascus_copper_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_qixinxieli",     "齐心协力矿", OreTier.Crude,   OreVein.Quench, 10, 2, OreTrait.Quench,  "矿工们齐心协力挖出的一块矿石。每摆一次就永久变强一点。",                                   "042_mokume_ingot_1x.png"));

            // ---- 重铸矿脉 11 ----
            oreSOs.Add(MakeOre("ore_touzhi",         "透支矿",     OreTier.Crude,   OreVein.Reforging, 30, 2, OreTrait.None,   "从明天的份额里提前支取的矿石。摆下去时硬得惊人，但本场战斗后续每次摆它都会少掉10点数——借来的总要还。",    "0031_pig_iron_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_xuetuzhuzao",    "学徒铸造",   OreTier.Crude,   OreVein.Reforging,  5, 2, OreTrait.None,   "学徒工第一次铸出的钻头。点数可怜，但摆下时能临时提升铸造台强度，为后续大块头铺路。",                     "004_patinated_copper_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_hexumoulue",     "何需谋略？", OreTier.Crude,   OreVein.Reforging, 30, 2, OreTrait.None,   "一块蛮横到不需要叠牌的矿石。船长拍在台上吼：\u300C给我撞就完了！\u300D",                                    "032_corroded_pig_iron_1x.png"));
            oreSOs.Add(MakeOre("ore_heduan",         "合锻",       OreTier.Crude,   OreVein.Reforging, 30, 2, OreTrait.None,   "必须牺牲一块精炼盘里的矿石才能开炉。摆下时随机把一块手牌塞回矿舱，换来自身30点硬度的暴力。",             "023_weathered_bronze_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_choulou",        "丑陋炫耀",   OreTier.Crude,   OreVein.Reforging, 10, 2, OreTrait.None,   "奇形怪状的矿石，船长偏偏爱摆在最显眼的位置。点数不高，但它的存在能触发某种虚荣加成。",                 "010_chalky_zinc_1x.png"));
            oreSOs.Add(MakeOre("ore_routizhihui",    "肉体智慧！", OreTier.Crude,   OreVein.Reforging, 10, 2, OreTrait.None,   "工匠说它\u300C有肌肉记忆\u300D。不需要复杂的叠牌策略，摆下去就知道往哪撞最疼。",                          "025_tarnished_brass_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_shuliantouzhi",  "熟练透支",   OreTier.Refined, OreVein.Reforging, 40, 4, OreTrait.None,   "老练矿工提前支取的份额。透支矿的升级版，本场战斗的代价已经算得很清楚。",                             "007_gold_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_dashizhuzao",    "大师铸造",   OreTier.Refined, OreVein.Reforging, 10, 4, OreTrait.None,   "大师级工匠的得意之作。摆下时能大幅强化当前铸造台。",                                             "026_nismuth_bronze_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_wanmeijili",     "完美借力",   OreTier.Refined, OreVein.Reforging,  0, 4, OreTrait.None,   "一块自己几乎没硬度的矿石。但它能完美借用周围所有钻头的硬度——摆对位置的话，伤害爆表。",               "20_electrum_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_duzuan",         "独钻",       OreTier.PureGold,OreVein.Reforging,  0, 8, OreTrait.Ember,  "一枚传说中的独钻头。始终留在精炼盘里不熄灭，摆下时把全舱所有矿石的点数加总到自己身上——一发入魂。",    "007_gold_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_laobing",        "老兵的矿脉", OreTier.Crude,   OreVein.Reforging,  5, 2, OreTrait.Core,   "老船长压箱底的一块矿石。点数看似可怜，但触发熔核时直接翻倍——属于老海盗最后的骄傲。",               "018_iridescent_bismuth_ingot_1x.png"));

            // ---- 衍生物 2 ----
            oreSOs.Add(MakeOre("ore_sihuo",          "私货",       OreTier.PureGold,OreVein.Derivative, 50, 0, OreTrait.None, "船长从黑市搞来的私货。不在正规矿舱流通，但50点的硬度不是假的。战后必须清理掉，免得惹官非。",       "007_gold_ingot_1x.png"));
            oreSOs.Add(MakeOre("ore_kuangzha",       "矿渣",       OreTier.Crude,   OreVein.Derivative,  0, 0, OreTrait.None, "碎屑特性产出的矿渣。没什么硬度，但塞在铸造台上会挤掉真正的好矿石——敌方蘑菇号也爱往你台上扔这个。", "010_chalky_zinc_1x.png"));

            // ========== 船体改造 33 ==========
            var hmSOs = new List<HullModDataSO>();

            // ---- 低阶 26 (white_node) ----
            hmSOs.Add(MakeHM("hm_shoupao",         "首炮",           HullModRarity.Low, HullModType.OrePoints,      2, "每场战斗摆下的第一块矿石点数 +20",                                                    "white_node", "star.png"));
            hmSOs.Add(MakeHM("hm_chixuzuozhan",    "持续作战",       HullModRarity.Low, HullModType.OrePoints,      3, "每回合摆下的第一块矿石点数 +10",                                                      "white_node", "flag_piece.png"));
            hmSOs.Add(MakeHM("hm_zuoxianpoijia",   "左舷破甲锥",     HullModRarity.Low, HullModType.OrePoints,      4, "伸到左舷撞击位的钻头，其矿石点数 +5",                                                 "white_node", "diamond.png"));
            hmSOs.Add(MakeHM("hm_chuanshoupojia", "船首破甲锥",     HullModRarity.Low, HullModType.OrePoints,      4, "伸到船首撞击位的钻头，其矿石点数 +5",                                                 "white_node", "spade.png"));
            hmSOs.Add(MakeHM("hm_youxianpojia",    "右舷破甲锥",     HullModRarity.Low, HullModType.OrePoints,      4, "伸到右舷撞击位的钻头，其矿石点数 +5",                                                 "white_node", "club.png"));
            hmSOs.Add(MakeHM("hm_yujinzhunxin",    "余烬准心",       HullModRarity.Low, HullModType.OrePoints,      2, "每场战斗摆下的第一块带余烬特性的矿石点数 +20",                                         "white_node", "heart.png"));
            hmSOs.Add(MakeHM("hm_chuanshouchongji", "船首冲击龙骨",   HullModRarity.Low, HullModType.ImpactBonus,    4, "船首撞击位的冲击力 +1（船首钻头撞击敌舰时的倍率 +1）",                                 "white_node", "rook.png"));
            hmSOs.Add(MakeHM("hm_zuoxianchongji",   "左舷冲击龙骨",   HullModRarity.Low, HullModType.ImpactBonus,    4, "左舷撞击位的冲击力 +1",                                                              "white_node", "knight.png"));
            hmSOs.Add(MakeHM("hm_youxianchongji",   "右舷冲击龙骨",   HullModRarity.Low, HullModType.ImpactBonus,    4, "右舷撞击位的冲击力 +1",                                                              "white_node", "bishop.png"));
            hmSOs.Add(MakeHM("hm_manzhuang",        "蛮撞战术",       HullModRarity.Low, HullModType.ImpactBonus,    4, "如果三个铸造台都有矿石（不空台），则所有撞击位冲击力 +2",                               "white_node", "bowling_pins.png"));
            hmSOs.Add(MakeHM("hm_zuoxianzhanshu",   "左舷战术",       HullModRarity.Low, HullModType.ImpactBonus,    2, "如果左舷撞击位熔炼层数 > 2，左舷冲击力 +1",                                           "white_node", "diamond_outline.png"));
            hmSOs.Add(MakeHM("hm_chuanshouzhanshu", "船首战术",       HullModRarity.Low, HullModType.ImpactBonus,    2, "如果船首撞击位熔炼层数 > 2，船首冲击力 +1",                                           "white_node", "spade_outline.png"));
            hmSOs.Add(MakeHM("hm_youxianzhanshu",   "右舷战术",       HullModRarity.Low, HullModType.ImpactBonus,    2, "如果右舷撞击位熔炼层数 > 2，右舷冲击力 +1",                                           "white_node", "club_outline.png"));
            hmSOs.Add(MakeHM("hm_shangjinlieren",   "赏金猎人",       HullModRarity.Low, HullModType.Economy,        4, "两回合内击沉敌舰，获得 2 银元",                                                      "white_node", "coins.png"));
            hmSOs.Add(MakeHM("hm_zhangzhongyinyuan","掌中银元",       HullModRarity.Low, HullModType.Economy,        4, "回合结束时，精炼盘每有一块矿石获得 1 银元",                                           "white_node", "dollar.png"));
            hmSOs.Add(MakeHM("hm_laozhugu",         "老主顾",         HullModRarity.Low, HullModType.Economy,        4, "每次进入精炼厂或船坞的第一次刷新免费",                                                 "white_node", "house.png"));
            hmSOs.Add(MakeHM("hm_bachuan",          "扒船",           HullModRarity.Low, HullModType.Economy,        2, "每场战斗开始获得 1 银元",                                                            "white_node", "yen.png"));
            hmSOs.Add(MakeHM("hm_beishui",          "背水",           HullModRarity.Low, HullModType.Operation,      4, "每场战斗第三回合开始，多抽一块矿石",                                                   "white_node", "pawn.png"));
            hmSOs.Add(MakeHM("hm_yachangjiagu",     "压舱加固",       HullModRarity.Low, HullModType.Operation,      4, "每场战斗摆下的第一块矿石获得驻台特性",                                                 "white_node", "hash.png"));
            hmSOs.Add(MakeHM("hm_dutu",             "赌徒",           HullModRarity.Low, HullModType.Operation,      4, "每回合开始，10% 概率抽一块矿石",                                                      "white_node", "dice.png"));
            hmSOs.Add(MakeHM("hm_zuoxianzhuanjing", "左舷专精",       HullModRarity.Low, HullModType.LongTermQuench, 4, "伸到左舷撞击位的矿石永久 +1 点数；依靠此效果每获得 50 点数后，左舷冲击力 +1",           "white_node", "magic_wand.png"));
            hmSOs.Add(MakeHM("hm_cuihuoluxin",      "淬火炉心",       HullModRarity.Low, HullModType.LongTermQuench, 4, "淬火效果多触发一次",                                                                 "white_node", "spinner.png"));
            hmSOs.Add(MakeHM("hm_youxianzhuanjing", "右舷专精",       HullModRarity.Low, HullModType.LongTermQuench, 4, "伸到右舷撞击位的矿石永久 +1 点数；依靠此效果每获得 50 点数后，右舷冲击力 +1",           "white_node", "star_outline.png"));
            hmSOs.Add(MakeHM("hm_chuanshouzhuanjing","船首专精",      HullModRarity.Low, HullModType.LongTermQuench, 4, "伸到船首撞击位的矿石永久 +1 点数；依靠此效果每获得 50 点数后，船首冲击力 +1",           "white_node", "heart_outline.png"));
            hmSOs.Add(MakeHM("hm_xiaoxingzhuangjiao","小型撞角",      HullModRarity.Low, HullModType.Special,        2, "每回合减少敌舰装甲值 50",                                                            "white_node", "billiard.png"));
            hmSOs.Add(MakeHM("hm_laobinghanglu",    "老兵航路",       HullModRarity.Low, HullModType.Operation,      0, "每场战斗首回合多抽一块矿石（老兵船长初始改造）",                                       "white_node", "flag_piece.png"));

            // ---- 中阶 2 (green_control) ----
            hmSOs.Add(MakeHM("hm_sihuojiadai",      "私货夹带",       HullModRarity.Mid, HullModType.OrePoints,      4, "每场战斗开始时，将一块\u300C私货\u300D加入精炼盘（临时，战后移除）",                          "green_control", "cards.png"));
            hmSOs.Add(MakeHM("hm_duocengronglian",  "多层熔炼",       HullModRarity.Mid, HullModType.LongTermQuench, 8, "熔炼在本铸造台的下一块矿石额外触发一次效果（与小型撞角同效果，可叠加）",                "green_control", "checkerboard.png"));

            // ---- 高阶 1 (purple_animation) ----
            hmSOs.Add(MakeHM("hm_manzai",           "满载",           HullModRarity.High, HullModType.Operation,    10, "每回合多抽一块矿石",                                                                 "purple_animation", "deck.png"));

            // ---- 私掠舰专属 4 (red_3d) ----
            hmSOs.Add(MakeHM("hm_chuanzhajinnang",  "船长锦囊",       HullModRarity.Privateer, HullModType.Privateer, 0, "包含四个选项：满载（每回合多抽一块，首回合额外多抽）/ 收缴证（购矿石不需银元）/ 我爱玩小矿舱（删除4块矿石）/ 我要玩大矿舱（复制1块矿石4份）", "red_3d", "jewel.png"));
            hmSOs.Add(MakeHM("hm_jinwangzhigu",     "金王之骨",       HullModRarity.Privateer, HullModType.Privateer, 0, "预热特性现在提供 1.5 倍自身点数给下一块矿石",                                        "red_3d", "king.png"));
            hmSOs.Add(MakeHM("hm_jinwangzhixin",    "金王之心",       HullModRarity.Privateer, HullModType.Privateer, 0, "每场战斗开始赋予矿舱内随机一块矿石预热特性，且预热提供的点数变为永久保留",              "red_3d", "queen.png"));
            hmSOs.Add(MakeHM("hm_jinwangzhirou",    "金王之肉",       HullModRarity.Privateer, HullModType.Privateer, 0, "矿舱内所有带预热特性的矿石获得共生特性",                                             "red_3d", "joker.png"));

            // ========== 目录汇总 ==========
            BuildOreCatalog(oreSOs);
            BuildHullModCatalog(hmSOs);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[HeaveDataPopulator] 完成：{oreSOs.Count} 矿石 + {hmSOs.Count} 船体改造");
        }

        [MenuItem("NineGrid/Tools/Rebuild Catalogs")]
        public static void RebuildCatalogs()
        {
            var oreSOs = LoadAllInFolder<OreDataSO>(OreDir);
            var hmSOs  = LoadAllInFolder<HullModDataSO>(HullModDir);
            BuildOreCatalog(oreSOs);
            BuildHullModCatalog(hmSOs);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[HeaveDataPopulator] 汇总完成：{oreSOs.Count} 矿石 + {hmSOs.Count} 船体改造");
        }

        static List<T> LoadAllInFolder<T>(string folder) where T : ScriptableObject
        {
            var result = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset != null) result.Add(asset);
            }
            result.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            return result;
        }

        // ---------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------

        static OreDataSO MakeOre(string id, string name, OreTier tier, OreVein vein,
            int pts, int price, OreTrait traits, string desc, string spriteFile)
        {
            var so = CreateOrLoad<OreDataSO>(OreDir + "/" + id);
            SetField(so, "oreId",       id);
            SetField(so, "displayName", name);
            SetField(so, "tier",        tier);
            SetField(so, "vein",        vein);
            SetField(so, "basePoints",  pts);
            SetField(so, "price",       price);
            SetField(so, "traits",      traits);
            SetField(so, "description", desc);
            SetField(so, "icon",        LoadSprite(OreSpritePrefix + spriteFile));
            EditorUtility.SetDirty(so);
            return so;
        }

        static HullModDataSO MakeHM(string id, string name, HullModRarity rarity, HullModType type,
            int price, string effect, string colorSubDir, string iconFile)
        {
            var so = CreateOrLoad<HullModDataSO>(HullModDir + "/" + id);
            SetField(so, "modId",             id);
            SetField(so, "displayName",       name);
            SetField(so, "rarity",            rarity);
            SetField(so, "modType",           type);
            SetField(so, "price",             price);
            SetField(so, "effectDescription", effect);
            SetField(so, "icon",              LoadSprite(IconPrefix + colorSubDir + "/" + iconFile));
            EditorUtility.SetDirty(so);
            return so;
        }

        static T CreateOrLoad<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path + ".asset");
            if (existing != null) return existing;

            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path + ".asset");
            return so;
        }

        static void SetField(Object target, string fieldName, object value)
        {
            var type = target.GetType();
            var fi = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (fi != null) { fi.SetValue(target, value); return; }
            Debug.LogWarning($"[HeaveDataPopulator] 字段未找到: {type.Name}.{fieldName}");
        }

        static Sprite LoadSprite(string path)
        {
            var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp == null) Debug.LogWarning($"[HeaveDataPopulator] 图片未找到: {path}");
            return sp;
        }

        static void BuildOreCatalog(List<OreDataSO> oreSOs)
        {
            var catalogPath = CatalogDir + "/OreCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<OreCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<OreCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            var list = new List<OreDataEntry>();
            foreach (var so in oreSOs)
            {
                var entry = new OreDataEntry();
                CopyOreFields(so, entry);
                list.Add(entry);
            }

            typeof(OreCatalog)
                .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.SetValue(catalog, list);
            EditorUtility.SetDirty(catalog);
        }

        static void CopyOreFields(OreDataSO so, OreDataEntry entry)
        {
            var srcType = typeof(OreDataSO);
            var dstType = typeof(OreDataEntry);
            string[] fields = { "oreId", "displayName", "tier", "vein", "basePoints", "price", "traits", "description", "icon" };
            foreach (var f in fields)
            {
                var src = srcType.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var dst = dstType.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (src != null && dst != null) dst.SetValue(entry, src.GetValue(so));
            }
        }

        static void BuildHullModCatalog(List<HullModDataSO> hmSOs)
        {
            var catalogPath = CatalogDir + "/HullModCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<HullModCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<HullModCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            var list = new List<HullModDataEntry>();
            foreach (var so in hmSOs)
            {
                var entry = new HullModDataEntry();
                CopyHMFields(so, entry);
                list.Add(entry);
            }

            typeof(HullModCatalog)
                .GetField("entries", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?.SetValue(catalog, list);
            EditorUtility.SetDirty(catalog);
        }

        static void CopyHMFields(HullModDataSO so, HullModDataEntry entry)
        {
            var srcType = typeof(HullModDataSO);
            var dstType = typeof(HullModDataEntry);
            string[] fields = { "modId", "displayName", "rarity", "modType", "price", "effectDescription", "icon" };
            foreach (var f in fields)
            {
                var src = srcType.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var dst = dstType.GetField(f, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (src != null && dst != null) dst.SetValue(entry, src.GetValue(so));
            }
        }

        static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
                var folderName = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folderName);
            }
        }
    }
}
#endif
