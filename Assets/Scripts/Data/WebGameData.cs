using System.Collections.Generic;

namespace NineGrid.Data
{
    /// <summary>
    /// 船体改造（遗物）效果类型 —— 对应 web relic.effect.type。
    /// </summary>
    public enum RelicEffectType
    {
        None,
        SlotCardBonus,           // 投入指定铸造台的矿石强度+bonus (slotIndex, bonus)
        SlotBonus,               // 指定铸造台倍率+bonus (slotIndex, bonus)
        CrowdSlotBonusSingle,    // 指定铸造台矿石数>threshold时倍率+bonus (slotIndex, threshold, bonus)
        NoStrategySlotBonus,     // 所有铸造台倍率+bonus（常驻）
        FirstCardPerTurnBonus,   // 每回合第一块矿石强度+bonus
        FirstCardPerBattleBonus, // 每场第一块矿石强度+bonus
        FirstRetainBonus,        // 第一块余烬矿强度+bonus
        TurnMonsterDamage,       // 回合开始敌舰扣damage血
        ExtraDraw,               // 每回合多抽bonus块
        LuckDraw,                // chance%概率抽1块
        FirstCardRemain,         // 每场第一块矿石获驻台
        ThirdTurnDraw,           // 第turn回合抽1块
        SlotGrow,                // 投入指定台永久+1，每growPer累计该台倍率+slotBonus
        GrowDouble,              // 淬火多触发一次
        BattleStartGold,         // 战斗开始+gold银元
        FastKillGold,            // turns回合内击杀+gold银元
        FirstRefreshFree,        // 精炼厂/船坞首次刷新免费
        HandGoldPerTurn,         // 回合结束精炼盘每矿+gold银元
        PrintCheatCard,          // 战斗开始加入一块私货(50点)到精炼盘
        DedicateOneFiveX,        // 预热提供1.5倍强度
        DedicatePermanent,       // 预热加成变永久
        DedicateChain,           // 所有预热矿获共生
        ExtraDrawFirstTurn,      // 每回合+bonus抽/首回合额外+bonus
        FreeCardPurchase,        // 买矿不要钱
        RemoveFourCards,         // 选4块矿删除
        CopyCardFour,            // 选1块矿复制4块
    }

    /// <summary>遗物/船体改造定义 —— 对应 web RELIC_DEFS 条目。</summary>
    public sealed class RelicDef
    {
        public string Id;
        public string DisplayName;
        public string Desc;
        public string Rarity;              // common / rare / epic / boss
        public RelicEffectType EffectType;
        public int SlotIndex = -1;
        public int Bonus;
        public int Threshold;
        public int Damage;
        public int Gold;
        public int Turns;
        public int Chance;
        public int Turn;
        public int GrowPer;
        public int SlotBonus;
        public int Price;

        public bool HasEffect => EffectType != RelicEffectType.None;
    }

    /// <summary>敌舰定义 —— 对应 web MONSTER_DEFS 条目。</summary>
    public sealed class EnemyDef
    {
        public string Id;
        public string DisplayName;
        public int Hp;
        public string Description;
        public string[] Keywords;   // 技能关键词
        public string KeywordDesc;  // 技能文案
        public string Theme;
        public string Type;         // normal / elite / boss
    }

    /// <summary>节点配置 —— 对应 web STAGE_CONFIG 条目。</summary>
    public sealed class StageConfig
    {
        public string NodeKey;       // "1-1" 等
        public string Type;          // normal / elite / boss
        public string[] MonsterPool; // 敌人id池
        public string PostBattle;    // shop_high_event / shop_low_event / blacksmith_mid_event / boss_relic_event
        public int GoldReward;
    }

    /// <summary>事件定义 —— 对应 web EVENT_POOLS 条目。</summary>
    public sealed class EventDef
    {
        public string Name;
        public string Desc;
        public string Effect;        // 效果类型字符串
        public int ParamInt;         // 整数参数
        public string ParamStr;      // 字符串参数
        public string Tier;          // common / rare / legendary
    }

    /// <summary>
    /// web 端全部平衡数据的 C# 翻译。作为逻辑层 source of truth。
    /// Unity SO（OreCatalog/EnemyShipCatalog/HullModCatalog/EventCatalog）提供表现层（图标/视觉/文案）。
    /// 两者通过 displayName（中文名）交叉引用。
    /// </summary>
    public static class WebGameData
    {
        // ===== 敌舰定义（3航段x8节点=24+艘）=====

        public static readonly EnemyDef[] Enemies =
        {
            // 第一航段
            NewEnemy("vine_ship", "藤蔓号", 200, "被海藻缠绕的幽灵船", new[]{"center_grow_1","less_draw_1"}, "缠海藻：投入船首的矿石淬火1；缠索：每回合少抽1块", "flower", "normal"),
            NewEnemy("shattered_hull", "碎舷号", 500, "船舷破碎的战舰残骸", new[]{"left_slot_bonus_1","center_card_penalty_5"}, "左舷薄弱：左舷铸造台倍率+1；船首重甲：船首矿石强度-5", "normal", "normal"),
            NewEnemy("wild_wave", "狂浪号", 500, "在狂浪中颠簸的战舰", new[]{"first_card_random_slot_remain","not_first_slot_penalty_5"}, "颠浪：每回合首矿随机投入并驻台；战舞：非首矿所在台的矿石-5", "elite", "normal"),
            NewEnemy("rat_swarm", "鼠群舰", 500, "被鼠群占据的废弃船只", new[]{"left_penalty_10"}, "左舷铸造台矿石强度-10", "normal", "normal"),
            NewEnemy("wall_guard", "礁石守卫", 500, "由礁石构成的防御舰艇", new[]{"left_penalty_10"}, "左舷铸造台矿石强度-10", "stone", "normal"),
            NewEnemy("mist_blade", "雾刃舰", 500, "在迷雾中游荡的刃装战舰", System.Array.Empty<string>(), "", "normal", "normal"),
            NewEnemy("ironclad_privateer", "铁甲私掠舰", 600, "重甲私掠舰", new[]{"all_card_penalty_2"}, "铁甲：所有矿石强度-2", "elite", "elite"),
            NewEnemy("fog_ship", "迷雾号", 800, "迷雾中的幽灵船", new[]{"retain_hand_card"}, "迷魂雾：回合开始给一块精炼盘矿石赋予余烬并禁用", "bat", "normal"),
            NewEnemy("spore_fog", "孢雾号", 1200, "释放有毒孢雾的生化战舰", new[]{"play_diffusion_every_3","monster_grow_100"}, "烟幕弹：每投3块矿投1块矿渣到随机台；回修：每回合装甲+100", "slime", "normal"),
            NewEnemy("plunder_ship", "劫掠号", 1600, "劫掠型敌舰", new[]{"disable_random_relic","lose_gold_per_turn"}, "破坏缆绳：随机船体改造失效；扒船反击：每回合-1银元", "normal", "normal"),
            NewEnemy("gold_king_flagship", "金王旗舰", 2000, "深海金王旗舰", new[]{"disable_dedicate","yellow_domain","yellow_heart"}, "金王诅咒：预热不生效；黄金领域：无预热矿反向预热(-1/2)；金王之心：回合开始赋随机矿预热", "boss", "boss"),
            // 第二航段
            NewEnemy("patrol_corvette", "巡逻舰", 2000, "第二航段巡逻敌舰", new[]{"heal_20"}, "每回合恢复20装甲", "normal", "normal"),
            NewEnemy("night_raid", "夜袭艇", 3000, "夜间突袭轻型战舰", new[]{"edge_penalty_5"}, "左右舷铸造台矿石强度-5", "bat", "normal"),
            NewEnemy("acid_hull", "酸蚀舰", 4000, "装备酸液喷射的敌舰", new[]{"left_penalty_10"}, "左舷铸造台矿石强度-10", "slime", "normal"),
            NewEnemy("ironclad_frigate", "铁甲护卫舰", 5000, "重装甲精英护卫舰", new[]{"first_card_discard","first_turn_less_draw"}, "每回合首矿直接进矿渣堆；首回合少抽1块", "elite", "elite"),
            NewEnemy("magma_walker", "熔岩行者", 7500, "熔岩海域战舰", new[]{"max_slot_penalty_1"}, "倍率最高铸造台倍率-1", "stone", "normal"),
            NewEnemy("shadow_hunter", "暗影猎手", 10000, "暗影海域猎舰", new[]{"steady_penalty"}, "叠牌稳重推进时所有铸造台倍率-1", "bat", "normal"),
            NewEnemy("greedy_galleon", "贪婪帆船", 11000, "贪婪大型帆船", new[]{"steal_gold"}, "每造成一次伤害减少玩家1银元", "normal", "normal"),
            NewEnemy("magma_leviathan", "熔岩巨舰", 16500, "熔岩巨型战舰", new[]{"min_slot_penalty_1","first_card_value_penalty_5"}, "倍率最低铸造台倍率-1；每回合首矿强度-5", "boss", "boss"),
            // 第三航段
            NewEnemy("hell_patrol", "地狱巡逻舰", 22000, "深海猎犬级巡逻舰", new[]{"heal_20"}, "每回合恢复20装甲", "normal", "normal"),
            NewEnemy("nightmare_raid", "噩梦突袭", 24000, "噩梦级突袭编队", new[]{"edge_penalty_5"}, "左右舷铸造台矿石强度-5", "bat", "normal"),
            NewEnemy("chaos_hull", "混沌舰体", 30000, "混沌之力化身", new[]{"left_penalty_10"}, "左舷铸造台矿石强度-10", "slime", "normal"),
            NewEnemy("fallen_frigate", "陨落舰艇", 40000, "堕落精英舰艇", new[]{"first_card_discard","first_turn_less_draw"}, "每回合首矿直接进矿渣堆；首回合少抽1块", "elite", "elite"),
            NewEnemy("void_walker", "虚空行者", 40000, "虚空幽灵舰", new[]{"max_slot_penalty_1"}, "倍率最高铸造台倍率-1", "bat", "normal"),
            NewEnemy("soul_reaper", "灵魂收割者", 50000, "收割灵魂死神舰", new[]{"steady_penalty"}, "叠牌稳重推进时所有铸造台倍率-1", "stone", "normal"),
            NewEnemy("greed_dreadnought", "贪婪无畏舰", 70000, "极度贪婪无畏舰", new[]{"steal_gold"}, "每造成一次伤害减少玩家1银元", "elite", "normal"),
            NewEnemy("chaos_lord", "混沌旗舰", 100000, "深海终极旗舰", new[]{"min_slot_penalty_1","first_card_value_penalty_5"}, "倍率最低铸造台倍率-1；每回合首矿强度-5", "boss", "boss"),
        };

        // ===== 船体改造/遗物定义（33条）=====

        public static readonly RelicDef[] Relics =
        {
            // 低级 - 矿石强度类
            NewRelic("relic_prow_armor_piercer", "船首破甲锥", "投入船首铸造台的矿石强度+5", "common", RelicEffectType.SlotCardBonus, slotIndex:1, bonus:5, price:4),
            NewRelic("relic_right_armor_piercer", "右舷破甲锥", "投入右舷铸造台的矿石强度+5", "common", RelicEffectType.SlotCardBonus, slotIndex:2, bonus:5, price:4),
            NewRelic("relic_left_armor_piercer", "左舷破甲锥", "投入左舷铸造台的矿石强度+5", "common", RelicEffectType.SlotCardBonus, slotIndex:0, bonus:5, price:4),
            NewRelic("relic_ember_focus", "余烬准心", "每场首块余烬矿强度+20", "common", RelicEffectType.FirstRetainBonus, bonus:20, price:2),
            NewRelic("relic_sustained_combat", "持续作战", "每回合首矿强度+10", "common", RelicEffectType.FirstCardPerTurnBonus, bonus:10, price:3),
            NewRelic("relic_first_shot", "首炮", "每场首矿强度+20", "common", RelicEffectType.FirstCardPerBattleBonus, bonus:20, price:2),
            // 低级 - 铸造台倍率类
            NewRelic("relic_prow_tactic", "船首战术", "船首台>2矿时倍率+1", "common", RelicEffectType.CrowdSlotBonusSingle, slotIndex:1, threshold:2, bonus:1, price:2),
            NewRelic("relic_right_tactic", "右舷战术", "右舷台>2矿时倍率+1", "common", RelicEffectType.CrowdSlotBonusSingle, slotIndex:2, threshold:2, bonus:1, price:2),
            NewRelic("relic_left_tactic", "左舷战术", "左舷台>2矿时倍率+1", "common", RelicEffectType.CrowdSlotBonusSingle, slotIndex:0, threshold:2, bonus:1, price:2),
            NewRelic("relic_ramming_tactic", "蛮撞战术", "所有铸造台倍率+2", "common", RelicEffectType.NoStrategySlotBonus, bonus:2, price:4),
            NewRelic("relic_prow_keel", "船首冲击龙骨", "左舷铸造台倍率+1", "common", RelicEffectType.SlotBonus, slotIndex:0, bonus:1, price:4),
            NewRelic("relic_right_keel", "右舷冲击龙骨", "右舷铸造台倍率+1", "common", RelicEffectType.SlotBonus, slotIndex:2, bonus:1, price:4),
            NewRelic("relic_left_keel", "左舷冲击龙骨", "船首铸造台倍率+1", "common", RelicEffectType.SlotBonus, slotIndex:1, bonus:1, price:4),
            // 低级 - 经济类
            NewRelic("relic_bounty_hunter", "赏金猎人", "2回合内击沉+2银元", "common", RelicEffectType.FastKillGold, turns:2, gold:2, price:4),
            NewRelic("relic_ship_looting", "扒船", "每场战斗开始+1银元", "common", RelicEffectType.BattleStartGold, gold:1, price:2),
            NewRelic("relic_regular_customer", "老主顾", "精炼厂/船坞首次刷新免费", "common", RelicEffectType.FirstRefreshFree, price:4),
            NewRelic("relic_silver_palm", "掌中银元", "回合结束精炼盘每矿+1银元", "common", RelicEffectType.HandGoldPerTurn, gold:1, price:4),
            // 低级 - 运转类
            NewRelic("relic_gambler", "赌徒", "10%概率抽1块", "common", RelicEffectType.LuckDraw, chance:10, price:4),
            NewRelic("relic_ballast_reinforce", "压舱加固", "每场首矿获驻台", "common", RelicEffectType.FirstCardRemain, price:4),
            NewRelic("relic_last_stand", "背水", "第3回合抽1块", "common", RelicEffectType.ThirdTurnDraw, turn:3, price:4),
            // 低级 - 长线淬火类
            NewRelic("relic_prow_specialty", "船首专精", "投入右舷台永久+1，每50累计右舷倍率+1", "common", RelicEffectType.SlotGrow, slotIndex:2, growPer:50, slotBonus:1, price:4),
            NewRelic("relic_right_specialty", "右舷专精", "投入船首台永久+1，每50累计船首倍率+1", "common", RelicEffectType.SlotGrow, slotIndex:1, growPer:50, slotBonus:1, price:4),
            NewRelic("relic_left_specialty", "左舷专精", "投入左舷台永久+1，每50累计左舷倍率+1", "common", RelicEffectType.SlotGrow, slotIndex:0, growPer:50, slotBonus:1, price:4),
            NewRelic("relic_quench_core", "淬火炉心", "淬火多触发一次", "common", RelicEffectType.GrowDouble, price:4),
            // 低级 - 特殊
            NewRelic("relic_small_ram", "小型撞角", "每回合敌舰-50装甲", "common", RelicEffectType.TurnMonsterDamage, damage:50, price:2),
            // 中级
            NewRelic("relic_contraband_run", "私货夹带", "战斗开始加入一块私货(50点)", "rare", RelicEffectType.PrintCheatCard, price:4),
            NewRelic("relic_multi_smelt", "多层熔炼", "每回合敌舰-50装甲", "rare", RelicEffectType.TurnMonsterDamage, damage:50, price:8),
            // 高级
            NewRelic("relic_full_load", "满载", "每回合多抽1块", "epic", RelicEffectType.ExtraDraw, bonus:1, price:10),
            // BOSS改造
            NewRelic("relic_gold_king_bone", "金王之骨", "预热提供1.5倍强度", "boss", RelicEffectType.DedicateOneFiveX),
            NewRelic("relic_gold_king_heart", "金王之心", "战斗开始赋随机矿预热(永久)", "boss", RelicEffectType.DedicatePermanent),
            NewRelic("relic_gold_king_flesh", "金王之肉", "所有预热矿获共生", "boss", RelicEffectType.DedicateChain),
            NewRelic("relic_captain_extra_draw", "船长锦囊·满载", "每回合+1抽/首回合额外+1", "boss", RelicEffectType.ExtraDrawFirstTurn, bonus:1),
            NewRelic("relic_captain_free_buy", "船长锦囊·免税", "买矿不要钱", "boss", RelicEffectType.FreeCardPurchase),
            NewRelic("relic_captain_small_hold", "船长锦囊·精简", "选4块矿删除", "boss", RelicEffectType.RemoveFourCards),
            NewRelic("relic_captain_big_hold", "船长锦囊·扩容", "选1块矿复制4块", "boss", RelicEffectType.CopyCardFour),
        };

        // ===== 节点配置（3航段x8节点=24节点）=====

        public static readonly StageConfig[] Stages =
        {
            // 第一航段
            NewStage("1-1", "normal", new[]{"vine_ship"}, "shop_high_event", 6),
            NewStage("1-2", "normal", new[]{"shattered_hull"}, "shop_high_event", 6),
            NewStage("1-3", "normal", new[]{"wild_wave"}, "shop_high_event", 6),
            NewStage("1-4", "elite", new[]{"ironclad_privateer"}, "blacksmith_mid_event", 8),
            NewStage("1-5", "normal", new[]{"fog_ship"}, "shop_low_event", 6),
            NewStage("1-6", "normal", new[]{"spore_fog"}, "shop_low_event", 6),
            NewStage("1-7", "normal", new[]{"plunder_ship"}, "shop_low_event", 6),
            NewStage("1-8", "boss", new[]{"gold_king_flagship"}, "boss_relic_event", 10),
            // 第二航段
            NewStage("2-1", "normal", new[]{"patrol_corvette","night_raid","acid_hull"}, "shop_high_event", 6),
            NewStage("2-2", "normal", new[]{"patrol_corvette","night_raid","acid_hull"}, "shop_high_event", 6),
            NewStage("2-3", "normal", new[]{"patrol_corvette","night_raid","acid_hull"}, "shop_high_event", 6),
            NewStage("2-4", "elite", new[]{"ironclad_frigate"}, "blacksmith_mid_event", 8),
            NewStage("2-5", "normal", new[]{"magma_walker","shadow_hunter","greedy_galleon"}, "shop_low_event", 6),
            NewStage("2-6", "normal", new[]{"magma_walker","shadow_hunter","greedy_galleon"}, "shop_low_event", 6),
            NewStage("2-7", "normal", new[]{"magma_walker","shadow_hunter","greedy_galleon"}, "shop_low_event", 6),
            NewStage("2-8", "boss", new[]{"magma_leviathan"}, "boss_relic_event", 10),
            // 第三航段
            NewStage("3-1", "normal", new[]{"hell_patrol","nightmare_raid","chaos_hull"}, "shop_high_event", 6),
            NewStage("3-2", "normal", new[]{"hell_patrol","nightmare_raid","chaos_hull"}, "shop_high_event", 6),
            NewStage("3-3", "normal", new[]{"hell_patrol","nightmare_raid","chaos_hull"}, "shop_high_event", 6),
            NewStage("3-4", "elite", new[]{"fallen_frigate"}, "blacksmith_mid_event", 8),
            NewStage("3-5", "normal", new[]{"void_walker","soul_reaper","greed_dreadnought"}, "shop_low_event", 6),
            NewStage("3-6", "normal", new[]{"void_walker","soul_reaper","greed_dreadnought"}, "shop_low_event", 6),
            NewStage("3-7", "normal", new[]{"void_walker","soul_reaper","greed_dreadnought"}, "shop_low_event", 6),
            NewStage("3-8", "boss", new[]{"chaos_lord"}, "boss_relic_event", 10),
        };

        // ===== 事件池定义 =====

        public static readonly EventDef[] CommonEvents =
        {
            new EventDef{ Tier="common", Name="漂流银箱", Desc="海面上漂来一个银箱，获得4银元", Effect="gain_gold", ParamInt=4 },
            new EventDef{ Tier="common", Name="海战图残卷", Desc="获得一次随机叠牌等级强化", Effect="random_strategy_level" },
            new EventDef{ Tier="common", Name="船首像画家", Desc="从矿舱中选择任意一块矿石+5强度", Effect="buff_card", ParamInt=5 },
            new EventDef{ Tier="common", Name="自助船坞", Desc="玩家-2银元，任意铸造台倍率+1", Effect="self_blacksmith", ParamInt=2 },
            new EventDef{ Tier="common", Name="及时的帮助", Desc="获得一次三选一矿石的机会", Effect="card_pick_three" },
            new EventDef{ Tier="common", Name="求矿乞丐", Desc="获得一次免费删矿机会", Effect="free_remove_card" },
            new EventDef{ Tier="common", Name="黑市掮客", Desc="玩家-4银元，随机获得一件船体改造", Effect="buy_random_relic", ParamInt=4 },
            new EventDef{ Tier="common", Name="走私大副", Desc="获得一块何需谋略", Effect="gain_specific_card", ParamStr="no_strategy" },
            new EventDef{ Tier="common", Name="涂鸦水手", Desc="选择矿舱内任意一块矿石随机转换", Effect="transform_card" },
            new EventDef{ Tier="common", Name="催熟炉", Desc="矿舱内的淬火矿立刻淬火2次", Effect="grow_cards_twice" },
            new EventDef{ Tier="common", Name="暗礁馈赠", Desc="选择矿舱内任意一块矿石获得碎屑特性", Effect="enchant_spread" },
            new EventDef{ Tier="common", Name="顺风波及", Desc="下一场海战，敌舰装甲值减少200点", Effect="next_monster_hp_down", ParamInt=200 },
        };

        public static readonly EventDef[] RareEvents =
        {
            new EventDef{ Tier="rare", Name="遭遇巡逻舰", Desc="立刻与当前航段任意敌舰进行海战", Effect="fight_monster" },
            new EventDef{ Tier="rare", Name="沉船宝藏", Desc="获得一次三选一中级船体改造的机会", Effect="pick_rare_relic" },
            new EventDef{ Tier="rare", Name="残破克隆镜", Desc="下一场海战，备用锚+1", Effect="next_battle_hearts_plus", ParamInt=1 },
            new EventDef{ Tier="rare", Name="走私掮客", Desc="下一次精炼厂刷新矿石均为指定矿脉矿", Effect="next_shop_system" },
            new EventDef{ Tier="rare", Name="赝品工匠", Desc="选择矿舱内任意一块矿石复制加入矿舱", Effect="duplicate_card" },
            new EventDef{ Tier="rare", Name="海盗赃款", Desc="玩家获得8银元", Effect="gain_gold", ParamInt=8 },
        };

        public static readonly EventDef[] LegendaryEvents =
        {
            new EventDef{ Tier="legendary", Name="海神遗物", Desc="玩家在本局中备用锚+1", Effect="max_hearts_plus" },
            new EventDef{ Tier="legendary", Name="古锚祝福", Desc="选择任意一块矿石获得熔核特性", Effect="enchant_mighty" },
            new EventDef{ Tier="legendary", Name="悬赏私掠舰", Desc="立刻与该航段任意私掠舰进行海战", Effect="fight_elite" },
        };

        // ===== 查询方法 =====

        static EnemyDef NewEnemy(string id, string name, int hp, string desc, string[] kw, string kwDesc, string theme, string type)
            => new EnemyDef { Id = id, DisplayName = name, Hp = hp, Description = desc, Keywords = kw, KeywordDesc = kwDesc, Theme = theme, Type = type };

        static RelicDef NewRelic(string id, string name, string desc, string rarity, RelicEffectType effect,
            int slotIndex = -1, int bonus = 0, int threshold = 0, int damage = 0, int gold = 0,
            int turns = 0, int chance = 0, int turn = 0, int growPer = 0, int slotBonus = 0, int price = 0)
            => new RelicDef
            {
                Id = id, DisplayName = name, Desc = desc, Rarity = rarity, EffectType = effect,
                SlotIndex = slotIndex, Bonus = bonus, Threshold = threshold, Damage = damage,
                Gold = gold, Turns = turns, Chance = chance, Turn = turn,
                GrowPer = growPer, SlotBonus = slotBonus, Price = price
            };

        static StageConfig NewStage(string key, string type, string[] pool, string post, int goldReward)
            => new StageConfig { NodeKey = key, Type = type, MonsterPool = pool, PostBattle = post, GoldReward = goldReward };

        /// <summary>按中文名查敌舰定义。Unity SO 的 displayName 与此匹配。</summary>
        public static EnemyDef GetEnemyByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;
            for (var i = 0; i < Enemies.Length; i++)
                if (Enemies[i].DisplayName == displayName) return Enemies[i];
            return null;
        }

        /// <summary>按 web id 查敌舰。</summary>
        public static EnemyDef GetEnemy(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (var i = 0; i < Enemies.Length; i++)
                if (Enemies[i].Id == id) return Enemies[i];
            return null;
        }

        /// <summary>按中文名查遗物定义。</summary>
        public static RelicDef GetRelicByName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return null;
            for (var i = 0; i < Relics.Length; i++)
                if (Relics[i].DisplayName == displayName) return Relics[i];
            return null;
        }

        /// <summary>按 web id 查遗物。</summary>
        public static RelicDef GetRelic(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (var i = 0; i < Relics.Length; i++)
                if (Relics[i].Id == id) return Relics[i];
            return null;
        }

        /// <summary>按节点 key 查节点配置。</summary>
        public static StageConfig GetStage(string nodeKey)
        {
            if (string.IsNullOrEmpty(nodeKey)) return null;
            for (var i = 0; i < Stages.Length; i++)
                if (Stages[i].NodeKey == nodeKey) return Stages[i];
            return null;
        }

        /// <summary>从敌人池随机选一个（对应 web pickRandom）。</summary>
        public static string PickRandomMonster(string[] pool)
        {
            if (pool == null || pool.Length == 0) return null;
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        // ===== 事件池抽取（对应 web pickEventTier / generatePostBattleEvents）=====

        /// <summary>按战后类型决定事件池 tier。</summary>
        public static string PickEventTier(string poolType)
        {
            var r = UnityEngine.Random.value;
            switch (poolType)
            {
                case "high": return r < 0.9f ? "common" : "rare";
                case "mid":
                    if (r < 0.49f) return "common";
                    return r < 0.99f ? "rare" : "legendary";
                case "low": return r < 0.9f ? "rare" : "legendary";
                case "boss": return "legendary";
                default: return "common";
            }
        }

        /// <summary>从指定 tier 池随机取一个事件。</summary>
        public static EventDef PickEventFromPool(string tier)
        {
            EventDef[] pool = tier switch
            {
                "rare" => RareEvents,
                "legendary" => LegendaryEvents,
                _ => CommonEvents,
            };
            return pool[UnityEngine.Random.Range(0, pool.Length)];
        }

        /// <summary>战后事件池类型 -> tier/count。</summary>
        public static (string tier, int count) GetEventPool(string postBattle)
        {
            switch (postBattle)
            {
                case "shop_high_event": return ("high", 2);
                case "shop_low_event": return ("low", 2);
                case "blacksmith_mid_event": return ("mid", 3);
                case "boss_relic_event": return ("boss", 3);
                default: return ("high", 2);
            }
        }

        /// <summary>生成 N 个不重复的战后事件选项。</summary>
        public static List<EventDef> GeneratePostBattleEvents(string postBattle)
        {
            var (tier, count) = GetEventPool(postBattle);
            var options = new List<EventDef>();
            var maxAttempts = 50;
            for (var i = 0; i < count; i++)
            {
                var attempts = 0;
                EventDef ev;
                do
                {
                    var t = PickEventTier(tier);
                    ev = PickEventFromPool(t);
                    attempts++;
                } while (attempts < maxAttempts && options.Exists(o => o.Name == ev.Name));
                options.Add(ev);
            }
            return options;
        }

        /// <summary>BOSS 遗物三选一（对应 web pickBossRelics）。</summary>
        public static List<RelicDef> PickBossRelics(string monsterId)
        {
            var options = new List<RelicDef>();
            var pool = new List<RelicDef>();
            foreach (var r in Relics)
                if (r.Rarity == "boss") pool.Add(r);

            var fallbackPool = new List<RelicDef>();
            foreach (var r in Relics)
                if (r.Rarity == "epic") fallbackPool.Add(r);

            var bossSpecific = monsterId switch
            {
                "gold_king_flagship" => new[] { "relic_gold_king_bone", "relic_gold_king_heart", "relic_gold_king_flesh" },
                _ => System.Array.Empty<string>()
            };

            var specific = new List<RelicDef>();
            foreach (var id in bossSpecific)
            {
                var r = GetRelic(id);
                if (r != null) specific.Add(r);
            }
            if (specific.Count > 0)
                options.Add(specific[UnityEngine.Random.Range(0, specific.Count)]);

            var used = new HashSet<string>();
            foreach (var r in options) used.Add(r.Id);

            var usable = new List<RelicDef>();
            var source = pool.Count >= 3 ? pool : fallbackPool;
            foreach (var r in source)
                if (!used.Contains(r.Id)) usable.Add(r);

            for (var i = usable.Count - 1; i > 0; i--)
            {
                var j = UnityEngine.Random.Range(0, i + 1);
                (usable[i], usable[j]) = (usable[j], usable[i]);
            }

            for (var i = 0; i < usable.Count && options.Count < 3; i++)
                options.Add(usable[i]);

            return options;
        }

        /// <summary>按稀有度权重随机选一个矿石 tier（white 65% / blue 30% / gold 5%）。</summary>
        public static string PickRandomOreTier()
        {
            var r = UnityEngine.Random.value;
            if (r < 0.65f) return "Crude";
            return r < 0.95f ? "Refined" : "PureGold";
        }

        /// <summary>按稀有度获取矿石价格（对应 web RARITY_PRICE）。</summary>
        public static int GetOrePriceByTier(string tier)
        {
            return tier switch
            {
                "Crude" => 2,
                "Refined" => 4,
                "PureGold" => 8,
                _ => 2,
            };
        }
    }
}
