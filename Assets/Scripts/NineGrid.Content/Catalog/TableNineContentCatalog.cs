using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;

namespace NineGrid.Content
{
    public static class TableNineContentCatalog
    {
        public static GameContentCatalog CreateDefault()
        {
            var catalog = new GameContentCatalog();
            AddEffects(catalog);
            AddHelpCards(catalog);
            AddRelics(catalog);
            AddPlayerSkills(catalog);
            AddMonsterSkills(catalog);
            AddMonsterCards(catalog);
            AddRewardsAndRooms(catalog);
            return catalog;
        }

        private static void AddEffects(GameContentCatalog c)
        {
            c.AddEffect(Impl("help.healing_potion.use", EffectContainerType.HelpCard,
                Triggered("help.healing_potion.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Heal\",\"amount\":10,\"actor\":\"Player\"}"),
                "[使用时] 恢复10点血量"));

            c.AddEffect(Impl("help.throwing_knife.use", EffectContainerType.HelpCard,
                Triggered("help.throwing_knife.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"RandomMonster\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":6,\"actor\":\"Player\"}"),
                "[使用时] 对目标怪物卡造成6点伤害"));

            c.AddEffect(Impl("help.bomb.use", EffectContainerType.HelpCard,
                Triggered("help.bomb.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"AllMonsters\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":4,\"actor\":\"Player\"}"),
                "[使用时] 对所有怪物卡造成4点伤害"));

            c.AddEffect(Impl("help.sturdy_shield.use", EffectContainerType.HelpCard,
                Triggered("help.sturdy_shield.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"GainArmor\",\"amount\":5}"),
                "[使用时] 玩家获得5点护甲"));

            c.AddEffect(Impl("help.gold_card.use", EffectContainerType.HelpCard,
                Triggered("help.gold_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"ModifyGold\",\"delta\":50,\"reason\":\"goldCard\"}"),
                "[使用时] 为玩家提供50金币"));

            c.AddEffect(Impl("help.flame.use", EffectContainerType.HelpCard,
                Triggered("help.flame.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":4,\"actor\":\"Self\"}"),
                "[使用时] 对玩家造成4点伤害并永久移除本卡"));

            c.AddEffect(Impl("relic.junk_recycler.use", EffectContainerType.Relic,
                Triggered("relic.junk_recycler.use", "Relic",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Heal\",\"amount\":2,\"actor\":\"Player\"}"),
                "[使用帮助卡时] 恢复2点血量"));

            c.AddEffect(Impl("relic.junk_launcher.use", EffectContainerType.Relic,
                Triggered("relic.junk_launcher.use", "Relic",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"RandomMonster\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Player\"}"),
                "[使用帮助卡时] 对随机一张怪物卡造成2点伤害"));

            c.AddEffect(Impl("relic.junk_coating.use", EffectContainerType.Relic,
                Triggered("relic.junk_coating.use", "Relic",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"GainArmor\",\"amount\":1}"),
                "[使用帮助卡时] 获得1点当前护甲"));

            c.AddEffect(Impl("relic.sling.kill", EffectContainerType.Relic,
                Triggered("relic.sling.kill", "Relic",
                    "{\"atom\":\"OnKill\"}",
                    "{\"atom\":\"RandomMonster\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":4,\"actor\":\"Player\"}"),
                "[击杀怪物时] 对随机一张怪物卡造成4点伤害"));

            c.AddEffect(Impl("relic.shield_knife.kill", EffectContainerType.Relic,
                Triggered("relic.shield_knife.kill", "Relic",
                    "{\"atom\":\"OnKill\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"GainArmor\",\"amount\":1}"),
                "[击杀怪物时] 获得1点当前护甲"));

            c.AddEffect(Impl("relic.gold_knife.kill", EffectContainerType.Relic,
                Triggered("relic.gold_knife.kill", "Relic",
                    "{\"atom\":\"OnKill\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"ModifyGold\",\"delta\":2,\"reason\":\"goldKnife\"}"),
                "[击杀怪物时] 获得2金币"));

            c.AddEffect(Impl("relic.vitality_amulet.node_end", EffectContainerType.Relic,
                Triggered("relic.vitality_amulet.node_end", "Relic",
                    "{\"atom\":\"OnNodeEnd\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Heal\",\"amount\":6,\"actor\":\"Player\"}"),
                "[每关卡结束时] 恢复6点血量"));

            c.AddEffect(Impl("relic.dragon_scale_armor.rule", EffectContainerType.Relic,
                Rule("relic.dragon_scale_armor.rule", "Relic",
                    "{\"rule\":\"EnemyAttackDelta\",\"op\":\"Add\",\"value\":-1,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "所有怪物卡的攻击-1"));

            c.AddEffect(Impl("relic.craving.rule", EffectContainerType.Relic,
                Rule("relic.craving.rule", "Relic",
                    "{\"rule\":\"RecoveryMultiplier\",\"op\":\"Multiply\",\"value\":2,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "所有恢复血量效果翻倍"));

            c.AddEffect(Impl("relic.phoenix_feather.fatal", EffectContainerType.Relic,
                Triggered("relic.phoenix_feather.fatal", "Relic",
                    "{\"atom\":\"OnFatalDamage\",\"target\":\"Player\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Sequence\",\"actions\":[{\"atom\":\"Heal\",\"amount\":15,\"actor\":\"Player\"},{\"atom\":\"DeactivateSelfEffect\"}]}"),
                "[受到致命伤害时] 恢复50%血量并永久移除本遗物"));

            c.AddEffect(Impl("relic.wood_shield.base", EffectContainerType.Relic,
                Modifier("relic.wood_shield.base", "Relic", "{\"atom\":\"Player\"}", null,
                    "{\"stat\":\"Armor\",\"op\":\"Add\",\"value\":1,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "基础护甲+1"));

            c.AddEffect(Impl("relic.wood_sword.base", EffectContainerType.Relic,
                Modifier("relic.wood_sword.base", "Relic", "{\"atom\":\"Player\"}", null,
                    "{\"stat\":\"Attack\",\"op\":\"Add\",\"value\":1,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "攻击+1"));

            c.AddEffect(Impl("relic.wood_armor.base", EffectContainerType.Relic,
                Modifier("relic.wood_armor.base", "Relic", "{\"atom\":\"Player\"}", null,
                    "{\"stat\":\"MaxHp\",\"op\":\"Add\",\"value\":2,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "血量上限+2"));

            var woodSetCondition = "[{\"atom\":\"OwnsRelicSet\",\"defIds\":[\"relic.wood_shield\",\"relic.wood_sword\",\"relic.wood_armor\"]}]";
            c.AddEffect(Impl("relic.wood_shield.set", EffectContainerType.Relic,
                Modifier("relic.wood_shield.set", "Relic", "{\"atom\":\"Player\"}", woodSetCondition,
                    "{\"stat\":\"Armor\",\"op\":\"Add\",\"value\":2,\"layer\":\"Conditional\",\"scope\":\"Permanent\"}"),
                "木盾套装基础护甲额外+2"));
            c.AddEffect(Impl("relic.wood_sword.set", EffectContainerType.Relic,
                Modifier("relic.wood_sword.set", "Relic", "{\"atom\":\"Player\"}", woodSetCondition,
                    "{\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Conditional\",\"scope\":\"Permanent\"}"),
                "木剑套装攻击额外+2"));
            c.AddEffect(Impl("relic.wood_armor.set", EffectContainerType.Relic,
                Modifier("relic.wood_armor.set", "Relic", "{\"atom\":\"Player\"}", woodSetCondition,
                    "{\"stat\":\"MaxHp\",\"op\":\"Add\",\"value\":8,\"layer\":\"Conditional\",\"scope\":\"Permanent\"}"),
                "木甲套装血量额外+8"));

            c.AddEffect(Impl("skill.hard_skin.max_hp", EffectContainerType.PlayerSkill,
                Modifier("skill.hard_skin.max_hp", "PlayerSkill", "{\"atom\":\"Player\"}", null,
                    "{\"stat\":\"MaxHp\",\"op\":\"Add\",\"value\":10,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "获得本技能时血量上限+10"));
            c.AddEffect(Impl("skill.hard_skin.node_end", EffectContainerType.PlayerSkill,
                Triggered("skill.hard_skin.node_end", "PlayerSkill",
                    "{\"atom\":\"OnNodeEnd\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Heal\",\"amount\":10,\"actor\":\"Player\"}"),
                "[每关卡结束时] 恢复10点血量"));
            c.AddEffect(Impl("skill.battle_hardened.battle", EffectContainerType.PlayerSkill,
                Triggered("skill.battle_hardened.battle", "PlayerSkill",
                    "{\"atom\":\"OnBattle\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Temporary\",\"scope\":\"UntilEnemyChanges\",\"source\":\"skill.battle_hardened\"}"),
                "[战斗时] 攻击+2（仅对当前敌人有效）"));
            c.AddEffect(Impl("skill.arsenal.node_end", EffectContainerType.PlayerSkill,
                Triggered("skill.arsenal.node_end", "PlayerSkill",
                    "{\"atom\":\"OnNodeEnd\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"WeightedRandom\",\"choices\":["
                    + "{\"weight\":1,\"action\":{\"atom\":\"Spawn\",\"defId\":\"help.throwing_knife\",\"kind\":\"HelpCard\",\"zone\":\"PlayerCardPool\"}},"
                    + "{\"weight\":1,\"action\":{\"atom\":\"Spawn\",\"defId\":\"help.bomb\",\"kind\":\"HelpCard\",\"zone\":\"PlayerCardPool\"}},"
                    + "{\"weight\":1,\"action\":{\"atom\":\"Spawn\",\"defId\":\"help.armor_breaking_hammer\",\"kind\":\"HelpCard\",\"zone\":\"PlayerCardPool\"}}]}"),
                "[每关卡结束时] 加入飞刀/爆弹/破击锤之一"));
            c.AddEffect(Impl("skill.tower_child.node_start", EffectContainerType.PlayerSkill,
                Triggered("skill.tower_child.node_start", "PlayerSkill",
                    "{\"atom\":\"OnNodeStart\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Spawn\",\"defId\":\"help.doubling_tower\",\"kind\":\"HelpCard\",\"zone\":\"ItemSlots\"}"),
                "[每关卡开始时] 将一张倍增塔放入道具牌格"));

            c.AddEffect(Impl("skill.beggar_bond.move", EffectContainerType.MonsterSkill,
                Triggered("skill.beggar_bond.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":5}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.beggar\",\"kind\":\"Monster\",\"count\":1,\"top\":false}"),
                "每移动5次，将一张乞丐怪物卡洗入战斗卡组"));
            c.AddEffect(Impl("skill.stray_cub.slot6", EffectContainerType.MonsterSkill,
                Modifier("skill.stray_cub.slot6", "MonsterSkill", "{\"atom\":\"Self\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":6}]",
                    "{\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Conditional\",\"scope\":\"Permanent\"}"),
                "[场上] 处于格6时，本卡攻击力+2"));
            c.AddEffect(Impl("skill.monster_battle_hardened.battle", EffectContainerType.MonsterSkill,
                Triggered("skill.monster_battle_hardened.battle", "MonsterSkill",
                    "{\"atom\":\"OnBattle\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Temporary\",\"scope\":\"UntilBattleEnds\",\"source\":\"skill.monster_battle_hardened\"}"),
                "[战斗时] 本卡攻击+2"));
            c.AddEffect(Impl("skill.survival_wisdom.battle", EffectContainerType.MonsterSkill,
                Triggered("skill.survival_wisdom.battle", "MonsterSkill",
                    "{\"atom\":\"OnBattle\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":[{\"atom\":\"Heal\",\"amount\":1,\"actor\":\"Self\"},{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":1,\"layer\":\"Persistent\",\"scope\":\"Permanent\",\"source\":\"skill.survival_wisdom\"}]}"),
                "[战斗时] 恢复1点血量，本卡攻击+1"));
            c.AddEffect(Impl("skill.devotion.remove", EffectContainerType.MonsterSkill,
                Triggered("skill.devotion.remove", "MonsterSkill",
                    "{\"atom\":\"OnRemove\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ShuffleInto\",\"defId\":\"help.flame\",\"kind\":\"HelpCard\",\"count\":1,\"top\":false}"),
                "[被移除时] 将一张烈焰加入战斗卡组"));
            c.AddEffect(Impl("skill.love_fire.aura", EffectContainerType.MonsterSkill,
                Modifier("skill.love_fire.aura", "MonsterSkill", "{\"atom\":\"Self\"}",
                    "[{\"atom\":\"HasCard\",\"defId\":\"help.flame\"}]",
                    "{\"stat\":\"Attack\",\"op\":\"Add\",\"value\":4,\"layer\":\"Conditional\",\"scope\":\"Permanent\"}"),
                "[场上] 有烈焰时，本卡攻击+4"));
            c.AddEffect(Impl("skill.breathe_fire.move", EffectContainerType.MonsterSkill,
                Triggered("skill.breathe_fire.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":3}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Spawn\",\"defId\":\"help.flame\",\"kind\":\"HelpCard\",\"zone\":\"ItemSlots\"}"),
                "每移动3次，将一张烈焰放入道具牌格"));
            c.AddEffect(Impl("skill.sharp_stone.armor_break", EffectContainerType.MonsterSkill,
                Triggered("skill.sharp_stone.armor_break", "MonsterSkill",
                    "{\"atom\":\"OnArmorBreak\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":1,\"actor\":\"Self\"}"),
                "当护甲归零时，对玩家造成1点伤害"));
            c.AddEffect(Impl("skill.rotate_lover.move", EffectContainerType.MonsterSkill,
                Triggered("skill.rotate_lover.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":3}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Rotate\",\"count\":1}"),
                "每移动3次，旋转一次"));
            c.AddEffect(Impl("skill.give_punch.enter2", EffectContainerType.MonsterSkill,
                Triggered("skill.give_punch.enter2", "MonsterSkill",
                    "{\"atom\":\"OnEnter\"}", "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":2}]"),
                "[登场] 若处于格2，对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.give_punch.enter4", EffectContainerType.MonsterSkill,
                Triggered("skill.give_punch.enter4", "MonsterSkill",
                    "{\"atom\":\"OnEnter\"}", "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":4}]"),
                "[登场] 若处于格4，对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.give_punch.enter6", EffectContainerType.MonsterSkill,
                Triggered("skill.give_punch.enter6", "MonsterSkill",
                    "{\"atom\":\"OnEnter\"}", "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":6}]"),
                "[登场] 若处于格6，对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.give_punch.enter8", EffectContainerType.MonsterSkill,
                Triggered("skill.give_punch.enter8", "MonsterSkill",
                    "{\"atom\":\"OnEnter\"}", "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":8}]"),
                "[登场] 若处于格8，对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.call_followers.move", EffectContainerType.MonsterSkill,
                Triggered("skill.call_followers.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":3}", "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.dragon_follower\",\"kind\":\"Monster\",\"count\":1,\"top\":false}"),
                "每移动3次，将一张龙信徒加入战斗卡组"));
            c.AddEffect(Impl("skill.gift.move", EffectContainerType.MonsterSkill,
                Triggered("skill.gift.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":3}", "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Spawn\",\"defId\":\"help.rotation_wheel\",\"kind\":\"HelpCard\",\"zone\":\"ItemSlots\"}"),
                "每移动3次，将一张旋转轮放入道具牌格"));
            c.AddEffect(Impl("skill.stroll.move", EffectContainerType.MonsterSkill,
                Triggered("skill.stroll.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":1}", "{\"atom\":\"Self\"}",
                    "{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":2,\"layer\":\"Persistent\",\"scope\":\"Permanent\",\"source\":\"skill.stroll\"}"),
                "每移动1次，本卡攻击+2"));
            c.AddEffect(Impl("skill.falling_rocks.cumulative", EffectContainerType.MonsterSkill,
                Triggered("skill.falling_rocks.cumulative", "MonsterSkill",
                    "{\"atom\":\"OnCumulative\",\"metric\":\"armorLost\",\"threshold\":10}", "{\"atom\":\"Player\"}",
                    "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.stone_man\",\"kind\":\"Monster\",\"count\":1,\"top\":false}"),
                "每累计损失满10点护甲，将一张石人军团怪物洗入战斗卡组"));
        }

        private static void AddHelpCards(GameContentCatalog c)
        {
            Help(c, "help.healing_potion", "恢复药水", ContentRarity.White, 30, "恢复").AddEffect("help.healing_potion.use");
            Help(c, "help.ward_magic_card", "庇佑魔法卡", ContentRarity.White, 20, "护甲").AddEffect(Pending(c, "help.ward_magic_card.pending", EffectContainerType.HelpCard, "下一次玩家受到伤害时，该次伤害变为0"));
            Help(c, "help.throwing_knife", "飞刀", ContentRarity.White, 20, "直伤").AddEffect("help.throwing_knife.use");
            Help(c, "help.fireball", "火球术", ContentRarity.White, 30, "直伤").AddEffect(Pending(c, "help.fireball.pending", EffectContainerType.HelpCard, "对目标怪物卡造成等同于玩家攻击的伤害"));
            Help(c, "help.rotation_wheel", "旋转轮", ContentRarity.White, 20, "位移").AddEffect(Pending(c, "help.rotation_wheel.pending", EffectContainerType.HelpCard, "逆时针旋转一次"));
            Help(c, "help.brutality_card", "暴力卡", ContentRarity.White, 30, "攻击").AddEffect(Pending(c, "help.brutality_card.pending", EffectContainerType.HelpCard, "玩家当前总攻击翻倍，战斗一次后复原"));
            Help(c, "help.rolling_stone", "滚石", ContentRarity.White, 50, "直伤").AddEffect(Pending(c, "help.rolling_stone.pending", EffectContainerType.HelpCard, "移动到格3时移除格6普通怪物和本卡"));
            Help(c, "help.bomb", "爆弹", ContentRarity.White, 50, "直伤").AddEffect("help.bomb.use");
            Help(c, "help.swap_card", "交换卡", ContentRarity.White, 50, "位移").AddEffect(Pending(c, "help.swap_card.pending", EffectContainerType.HelpCard, "选择两张非玩家卡互换位置"));
            Help(c, "help.armor_breaking_hammer", "破击锤", ContentRarity.White, 50, "直伤").AddEffect(Pending(c, "help.armor_breaking_hammer.pending", EffectContainerType.HelpCard, "将目标怪物卡护甲降低10点"));
            Help(c, "help.sturdy_shield", "耐用盾牌", ContentRarity.White, 50, "护甲").AddEffect("help.sturdy_shield.use");
            Help(c, "help.bear_trap", "捕熊陷阱", ContentRarity.White, 50, "直伤").AddEffect(Pending(c, "help.bear_trap.pending", EffectContainerType.HelpCard, "正交相邻格补牌为怪物时造成10点伤害后移除本卡"));
            Help(c, "help.teleport_card", "传送卡", ContentRarity.White, 30, "位移").AddEffect(Pending(c, "help.teleport_card.pending", EffectContainerType.HelpCard, "将一张非玩家卡洗回战斗卡组"));
            Help(c, "help.blood_conversion", "血液转换", ContentRarity.White, 50, "特殊").AddEffect(Pending(c, "help.blood_conversion.pending", EffectContainerType.HelpCard, "扣除5点血量上限并随机获得奖励"));
            Help(c, "help.gold_card", "金币卡", ContentRarity.Blue, 30, "经济").AddEffect("help.gold_card.use");
            Help(c, "help.food_card", "食品卡", ContentRarity.Blue, 50, "恢复").AddEffect(Pending(c, "help.food_card.pending", EffectContainerType.HelpCard, "将玩家卡血量回满"));
            Help(c, "help.common_chest_card", "普通宝箱卡", ContentRarity.Blue, 100, "经济").AddEffect(Pending(c, "help.common_chest_card.pending", EffectContainerType.HelpCard, "从三个遗物中选择一个获得"));
            Help(c, "help.healing_spring", "治疗泉", ContentRarity.Blue, 80, "恢复").AddEffect(Pending(c, "help.healing_spring.pending", EffectContainerType.HelpCard, "多区域恢复效果"));
            Help(c, "help.impact_tutorial", "撞击教程", ContentRarity.Blue, 80, "血量").AddEffect(Pending(c, "help.impact_tutorial.pending", EffectContainerType.HelpCard, "造成等同于玩家当前血量的伤害"));
            Help(c, "help.shield_bash_tutorial", "盾击教程", ContentRarity.Blue, 80, "护甲").AddEffect(Pending(c, "help.shield_bash_tutorial.pending", EffectContainerType.HelpCard, "造成等同于玩家当前护甲的伤害"));
            Help(c, "help.kidnapping", "绑票", ContentRarity.Blue, 100, "护甲").AddEffect(Pending(c, "help.kidnapping.pending", EffectContainerType.HelpCard, "移除非精英非层主怪物并获得等同于护甲的护甲"));
            Help(c, "help.blue_chest_card", "蓝色宝箱卡", ContentRarity.Gold, 150, "经济").AddEffect(Pending(c, "help.blue_chest_card.pending", EffectContainerType.HelpCard, "从三个遗物中选择一个获得"));
            Help(c, "help.watchtower", "瞭望塔", ContentRarity.Gold, 150, "直伤").AddEffect(Pending(c, "help.watchtower.pending", EffectContainerType.HelpCard, "场上/道具牌格随机伤害"));
            Help(c, "help.doubling_tower", "倍增塔", ContentRarity.Gold, 150, "特殊").AddEffect(Pending(c, "help.doubling_tower.pending", EffectContainerType.HelpCard, "帮助卡触发两次"));
            Help(c, "help.stat_boost_card", "属性提升卡", ContentRarity.Gold, 100, "特殊").AddEffect(Pending(c, "help.stat_boost_card.pending", EffectContainerType.HelpCard, "选择攻击/护甲/血量提升"));
            Help(c, "help.golden_chest_card", "金色宝箱卡", ContentRarity.Red, 400, "特殊").AddEffect(Pending(c, "help.golden_chest_card.pending", EffectContainerType.HelpCard, "从三个遗物中选择一个获得"));
            Help(c, "help.flame", "烈焰", ContentRarity.Red, 400, "特殊").AddEffect("help.flame.use");
        }

        private static void AddRelics(GameContentCatalog c)
        {
            Relic(c, "relic.junk_recycler", "废物利用机", ContentRarity.White, "使用帮助卡时恢复2点血量").AddEffect("relic.junk_recycler.use");
            Relic(c, "relic.wood_shield", "木盾", ContentRarity.White, "基础护甲+1，木套装额外+2").AddEffect("relic.wood_shield.base").AddEffect("relic.wood_shield.set");
            Relic(c, "relic.wood_sword", "木剑", ContentRarity.White, "攻击+1，木套装额外+2").AddEffect("relic.wood_sword.base").AddEffect("relic.wood_sword.set");
            Relic(c, "relic.wood_armor", "木甲", ContentRarity.White, "血量上限+2，木套装额外+8").AddEffect("relic.wood_armor.base").AddEffect("relic.wood_armor.set");
            Relic(c, "relic.lucky_coin", "幸运硬币", ContentRarity.White, "击杀精英/层主时加入金币卡").AddEffect(Pending(c, "relic.lucky_coin.pending", EffectContainerType.Relic, "精英/Boss 击杀额外金币卡"));
            Relic(c, "relic.throwing_knife_bag", "飞刀袋", ContentRarity.White, "每关卡开始加入两张飞刀").AddEffect(Pending(c, "relic.throwing_knife_bag.pending", EffectContainerType.Relic, "每关卡开始加入两张飞刀"));
            Relic(c, "relic.potion_bag", "药水袋", ContentRarity.White, "每关卡开始加入两张恢复药水").AddEffect(Pending(c, "relic.potion_bag.pending", EffectContainerType.Relic, "每关卡开始加入两张恢复药水"));
            Relic(c, "relic.junk_launcher", "废物发射器", ContentRarity.White, "使用帮助卡时随机伤害").AddEffect("relic.junk_launcher.use");
            Relic(c, "relic.junk_coating", "废物涂层", ContentRarity.White, "使用帮助卡时获得护甲").AddEffect("relic.junk_coating.use");
            Relic(c, "relic.sling", "弹弓", ContentRarity.White, "击杀怪物时随机伤害").AddEffect("relic.sling.kill");
            Relic(c, "relic.shield_knife", "打盾刀", ContentRarity.White, "击杀怪物时获得护甲").AddEffect("relic.shield_knife.kill");
            Relic(c, "relic.gold_knife", "打金刀", ContentRarity.White, "击杀怪物时获得2金币").AddEffect("relic.gold_knife.kill");
            Relic(c, "relic.heavy_armor", "重盔甲", ContentRarity.White, "基础护甲+1，按基础护甲补当前护甲").AddEffect(Pending(c, "relic.heavy_armor.pending", EffectContainerType.Relic, "关卡开始按基础护甲获得护甲"));
            Relic(c, "relic.gold_armor", "金币盔甲", ContentRarity.White, "金币抵消护甲伤害").AddEffect(Pending(c, "relic.gold_armor.pending", EffectContainerType.Relic, "伤害公式金币抵消"));
            Relic(c, "relic.vitality_amulet", "活力护符", ContentRarity.Blue, "血量上限+6，关卡结束恢复6").AddEffect(Pending(c, "relic.vitality_amulet.max_hp", EffectContainerType.Relic, "血量上限+6")).AddEffect("relic.vitality_amulet.node_end");
            Relic(c, "relic.dragon_scale_armor", "龙鳞甲", ContentRarity.Gold, "所有怪物攻击-1").AddEffect("relic.dragon_scale_armor.rule");
            Relic(c, "relic.phoenix_feather", "凤凰羽毛", ContentRarity.Gold, "致命伤害免死").AddEffect("relic.phoenix_feather.fatal");
            Relic(c, "relic.craving", "渴望", ContentRarity.Gold, "所有恢复血量效果翻倍").AddEffect("relic.craving.rule");
            Relic(c, "relic.junk_slot_machine", "废物老虎机", ContentRarity.Gold, "使用帮助卡时随机触发九选一").AddEffect(Pending(c, "relic.junk_slot_machine.pending", EffectContainerType.Relic, "九选一复合随机"));
        }

        private static void AddPlayerSkills(GameContentCatalog c)
        {
            Skill(c, "skill.thorn_skin", "刺皮", EffectContainerType.PlayerSkill, "战斗时对怪物造成等同其攻击的伤害")
                .AddEffect(Pending(c, "skill.thorn_skin.pending", EffectContainerType.PlayerSkill, "动态取目标攻击值"));
            Skill(c, "skill.hard_skin", "硬皮", EffectContainerType.PlayerSkill, "血量上限+10，关卡结束恢复10")
                .AddEffect("skill.hard_skin.max_hp").AddEffect("skill.hard_skin.node_end");
            Skill(c, "skill.battle_hardened", "历战", EffectContainerType.PlayerSkill, "战斗时攻击+2，换敌复原")
                .AddEffect("skill.battle_hardened.battle");
            Skill(c, "skill.arsenal", "军械库", EffectContainerType.PlayerSkill, "关卡结束加入飞刀/爆弹/破击锤之一")
                .AddEffect("skill.arsenal.node_end");
            Skill(c, "skill.even_hatred", "偶数仇恨", EffectContainerType.PlayerSkill, "对偶数等级怪物造成双倍伤害")
                .AddEffect(Pending(c, "skill.even_hatred.pending", EffectContainerType.PlayerSkill, "等级奇偶条件伤害倍率"));
            Skill(c, "skill.tower_child", "塔之子", EffectContainerType.PlayerSkill, "关卡开始放入倍增塔")
                .AddEffect("skill.tower_child.node_start");
            Skill(c, "skill.easy_road", "轻车熟路", EffectContainerType.PlayerSkill, "关卡结束白色帮助卡三选一")
                .AddEffect(Pending(c, "skill.easy_road.pending", EffectContainerType.PlayerSkill, "白色帮助卡三选一奖励"));
        }

        private static void AddMonsterSkills(GameContentCatalog c)
        {
            Skill(c, "skill.beggar_bond", "丐帮同心", EffectContainerType.MonsterSkill, "每移动5次洗入乞丐").AddEffect("skill.beggar_bond.move");
            Skill(c, "skill.stray_cub", "流浪幼崽", EffectContainerType.MonsterSkill, "格6攻击+2并获得先攻").AddEffect("skill.stray_cub.slot6").AddEffect(Pending(c, "skill.stray_cub.first_strike", EffectContainerType.MonsterSkill, "先攻技能"));
            Skill(c, "skill.thief_claims", "东西归我了！", EffectContainerType.MonsterSkill, "每移动3次移除相邻帮助卡").AddEffect(Pending(c, "skill.thief_claims.pending", EffectContainerType.MonsterSkill, "相邻帮助卡目标筛选"));
            Skill(c, "skill.monster_battle_hardened", "历战怪物", EffectContainerType.MonsterSkill, "战斗时本卡攻击+2").AddEffect("skill.monster_battle_hardened.battle");
            Skill(c, "skill.learning_growth", "学习成长", EffectContainerType.MonsterSkill, "其他怪物获得攻击时本卡攻击+1").AddEffect(Pending(c, "skill.learning_growth.pending", EffectContainerType.MonsterSkill, "监听其他怪物攻击增加"));
            Skill(c, "skill.survival_wisdom", "生存智慧", EffectContainerType.MonsterSkill, "战斗时恢复1且攻击+1").AddEffect("skill.survival_wisdom.battle");
            Skill(c, "skill.devotion", "献身", EffectContainerType.MonsterSkill, "被移除时加入烈焰").AddEffect("skill.devotion.remove");
            Skill(c, "skill.love_fire", "恋火", EffectContainerType.MonsterSkill, "有烈焰时攻击+4").AddEffect("skill.love_fire.aura");
            Skill(c, "skill.breathe_fire", "喷火", EffectContainerType.MonsterSkill, "每移动3次放烈焰").AddEffect("skill.breathe_fire.move");
            Skill(c, "skill.sharp_stone", "尖石", EffectContainerType.MonsterSkill, "护甲归零时伤害玩家").AddEffect("skill.sharp_stone.armor_break");
            Skill(c, "skill.hard", "坚硬", EffectContainerType.MonsterSkill, "左列战斗时按损失护甲伤害玩家").AddEffect(Pending(c, "skill.hard.pending", EffectContainerType.MonsterSkill, "战斗损失护甲动态伤害"));
            Skill(c, "skill.swallow_stone", "吞石", EffectContainerType.MonsterSkill, "每移动2次吸相邻怪物护甲").AddEffect(Pending(c, "skill.swallow_stone.pending", EffectContainerType.MonsterSkill, "护甲转移"));
            Skill(c, "skill.taunt", "嘲讽", EffectContainerType.MonsterSkill, "相邻时只能与本卡战斗").AddEffect(Pending(c, "skill.taunt.pending", EffectContainerType.MonsterSkill, "交互合法性规则改写"));
            Skill(c, "skill.recombine_head", "重新组合头", EffectContainerType.MonsterSkill, "相邻骷髅头组合").AddEffect(Pending(c, "skill.recombine_head.pending", EffectContainerType.MonsterSkill, "按相邻指定 def 组合移除"));
            Skill(c, "skill.recombine_body", "重新组合身", EffectContainerType.MonsterSkill, "相邻无头骷髅组合").AddEffect(Pending(c, "skill.recombine_body.pending", EffectContainerType.MonsterSkill, "按相邻指定 def 组合移除"));
            Skill(c, "skill.unstable", "不稳定", EffectContainerType.MonsterSkill, "每移动3次随机交换怪物").AddEffect(Pending(c, "skill.unstable.pending", EffectContainerType.MonsterSkill, "随机第二目标交换"));
            Skill(c, "skill.rotate_lover", "爱好旋转", EffectContainerType.MonsterSkill, "每移动3次旋转").AddEffect("skill.rotate_lover.move");
            Skill(c, "skill.random_walk", "乱步", EffectContainerType.MonsterSkill, "每移动3次与随机帮助卡交换").AddEffect(Pending(c, "skill.random_walk.pending", EffectContainerType.MonsterSkill, "随机帮助卡目标"));
            Skill(c, "skill.give_punch", "给你一拳", EffectContainerType.MonsterSkill, "登场在偶数边位时伤害玩家").AddEffect("skill.give_punch.enter2").AddEffect("skill.give_punch.enter4").AddEffect("skill.give_punch.enter6").AddEffect("skill.give_punch.enter8");
            Skill(c, "skill.call_followers", "呼唤信徒", EffectContainerType.MonsterSkill, "每移动3次加入龙信徒").AddEffect("skill.call_followers.move");
            Skill(c, "skill.gift", "礼物", EffectContainerType.MonsterSkill, "每移动3次放旋转轮").AddEffect("skill.gift.move");
            Skill(c, "skill.stroll", "漫步", EffectContainerType.MonsterSkill, "每移动1次攻击+2").AddEffect("skill.stroll.move");
            Skill(c, "skill.falling_rocks", "落石", EffectContainerType.MonsterSkill, "累计损失10护甲洗入石人").AddEffect("skill.falling_rocks.cumulative");

            PendingSkill(c, "skill.hoodlum", "混的人");
            PendingSkill(c, "skill.rascality", "痞气");
            PendingSkill(c, "skill.bloodthirst", "嗜血");
            PendingSkill(c, "skill.smart", "大聪明");
            PendingSkill(c, "skill.gear_delivery", "发装备了！");
            PendingSkill(c, "skill.fire_power", "火之力");
            PendingSkill(c, "skill.sacrifice", "献祭");
            PendingSkill(c, "skill.rolling_crush", "滚动碾压");
            PendingSkill(c, "skill.stone_lover", "石头爱好者");
            PendingSkill(c, "skill.throw_stone", "丢石头");
            PendingSkill(c, "skill.fall_apart", "散架");
            PendingSkill(c, "skill.strong_combo", "强力组合");
            PendingSkill(c, "skill.delivery", "快递");
            PendingSkill(c, "skill.turn_world", "转动");
            PendingSkill(c, "skill.hot_observation", "灼热观察");
            PendingSkill(c, "skill.air_strike", "空中打击");
            PendingSkill(c, "skill.relentless_chase", "不休追击");
            PendingSkill(c, "skill.stocking", "进货");
            PendingSkill(c, "skill.orc_tactics", "兽人战术");
            PendingSkill(c, "skill.fight_me", "和我打！");
            PendingSkill(c, "skill.intense_burning", "剧烈燃烧");
            PendingSkill(c, "skill.stone_growth", "石增长");
            PendingSkill(c, "skill.stone_shelter", "石庇护");
            PendingSkill(c, "skill.fracture_fall_apart", "折损散架");
            PendingSkill(c, "skill.range_expand", "范围扩大");
            PendingSkill(c, "skill.flame_breath", "烈焰吐息");
            PendingSkill(c, "skill.flame_boiling", "烈焰沸腾");
            PendingSkill(c, "skill.space_mastery", "空间掌握");
            PendingSkill(c, "skill.otherworld_help", "异界帮助");
            PendingSkill(c, "skill.absorb_stone", "吸石");
            PendingSkill(c, "skill.guide", "引路");
            PendingSkill(c, "skill.find_weakness", "发现弱点");
            PendingSkill(c, "skill.violence_maniac", "暴力狂");
            PendingSkill(c, "skill.violence_nutrition", "暴力即养分");
            PendingSkill(c, "skill.absorb_bone", "吸骨");
            PendingSkill(c, "skill.mixed_bones", "混合骨头");
            PendingSkill(c, "skill.first_strike", "先攻");
            PendingSkill(c, "skill.blessing", "庇佑");
        }

        private static void AddMonsterCards(GameContentCatalog c)
        {
            var wandering = Deck(c, "deck.wandering_legion", "流浪军团牌组", MonsterDeckKind.WeakElite);
            Monster(c, wandering, "monster.beggar", "乞丐", 1, 4, 2, 0, "skill.beggar_bond");
            Monster(c, wandering, "monster.wandering_child", "流浪孩童", 1, 1, 1, 3, "skill.stray_cub");
            Monster(c, wandering, "monster.pickpocket", "扒手", 1, 4, 1, 0, "skill.thief_claims");
            Monster(c, wandering, "monster.vagrant", "流浪汉", 1, 5, 2, 0);
            Monster(c, wandering, "monster.hoodlum", "混混", 2, 6, 2, 0, "skill.hoodlum");
            Monster(c, wandering, "monster.rogue", "流氓", 2, 2, 2, 6, "skill.rascality");
            Monster(c, wandering, "monster.thug", "打手", 2, 6, 2, 0, "skill.give_punch");
            Monster(c, wandering, "monster.killer", "杀手", 3, 6, 3, 4, "skill.relentless_chase");
            Monster(c, wandering, "monster.smuggler", "走私者", 3, 4, 2, 5, "skill.stocking");
            Monster(c, wandering, "monster.ringleader", "领头人", 0, 14, 3, 4, "skill.guide", "skill.find_weakness").AsElite();

            var stone = Deck(c, "deck.stone_legion", "石人军团牌组", MonsterDeckKind.WeakElite);
            Monster(c, stone, "monster.sharp_stone", "尖石", 1, 3, 1, 1, "skill.sharp_stone");
            Monster(c, stone, "monster.big_stone", "大石头", 1, 1, 0, 5, "skill.hard");
            Monster(c, stone, "monster.stone_swallower", "吞石者", 1, 1, 2, 3, "skill.swallow_stone");
            Monster(c, stone, "monster.stone_man", "石头人", 1, 1, 2, 4);
            Monster(c, stone, "monster.rolling_stone_man", "滚石人", 2, 4, 2, 3, "skill.rolling_crush");
            Monster(c, stone, "monster.stone_shrimp", "石虾", 2, 2, 2, 6, "skill.stone_lover");
            Monster(c, stone, "monster.stone_thrower", "丢石人", 2, 2, 1, 6, "skill.throw_stone");
            Monster(c, stone, "monster.growing_stone", "增生石块", 3, 10, 3, 0, "skill.stone_growth");
            Monster(c, stone, "monster.shelter_stone", "庇护石", 3, 4, 2, 6, "skill.stone_shelter");
            Monster(c, stone, "monster.megalith", "巨石人", 0, 8, 2, 12, "skill.absorb_stone", "skill.falling_rocks").AsElite();

            var orc = Deck(c, "deck.orc_legion", "兽人军团牌组", MonsterDeckKind.StrongElite);
            Monster(c, orc, "monster.old_orc", "年迈兽人", 1, 9, 1, 0, "skill.survival_wisdom");
            Monster(c, orc, "monster.young_orc", "年轻兽人", 1, 6, 2, 4, "skill.learning_growth");
            Monster(c, orc, "monster.brainless_orc", "无脑兽人", 1, 9, 1, 0, "skill.monster_battle_hardened");
            Monster(c, orc, "monster.veteran_orc", "历战兽人", 1, 10, 2, 0);
            Monster(c, orc, "monster.orc_warrior", "兽人战士", 2, 10, 2, 0, "skill.bloodthirst");
            Monster(c, orc, "monster.smart_orc", "聪明兽人", 2, 9, 2, 0, "skill.smart");
            Monster(c, orc, "monster.orc_quartermaster", "兽人军需官", 2, 8, 2, 3, "skill.gear_delivery");
            Monster(c, orc, "monster.big_orc", "兽人大只佬", 3, 14, 3, 0, "skill.fight_me");
            Monster(c, orc, "monster.orc_commander", "兽人指挥官", 3, 10, 2, 2, "skill.orc_tactics");
            Monster(c, orc, "monster.orc_boss", "兽人老大", 0, 22, 4, 4, "skill.violence_maniac", "skill.violence_nutrition").AsElite();

            var skeleton = Deck(c, "deck.skeleton_legion", "骷髅军团牌组", MonsterDeckKind.StrongElite);
            Monster(c, skeleton, "monster.headless_skeleton", "无头骷髅", 1, 6, 2, 0, "skill.recombine_head");
            Monster(c, skeleton, "monster.skull_head", "骷髅头", 1, 3, 2, 3, "skill.recombine_body");
            Monster(c, skeleton, "monster.skeleton_taunter", "骷髅嘲讽子", 1, 7, 2, 1, "skill.taunt");
            Monster(c, skeleton, "monster.bone_club_skeleton", "骨棒骷髅", 1, 8, 2, 1);
            Monster(c, skeleton, "monster.big_skeleton", "大骷髅", 2, 10, 2, 0, "skill.fall_apart");
            Monster(c, skeleton, "monster.multi_bone_worm", "多骨虫", 2, 3, 2, 6, "skill.strong_combo");
            Monster(c, skeleton, "monster.bone_courier", "骨头快递员", 2, 7, 2, 1, "skill.delivery");
            Monster(c, skeleton, "monster.giant_skeleton", "巨大骷髅", 3, 12, 3, 0, "skill.fracture_fall_apart");
            Monster(c, skeleton, "monster.skeleton_mage", "骷髅法师", 3, 6, 3, 5, "skill.range_expand");
            Monster(c, skeleton, "monster.skeleton_king", "骷髅王", 0, 12, 3, 10, "skill.absorb_bone", "skill.mixed_bones").AsElite();

            var dragon = Deck(c, "deck.dragon", "巨龙牌组", MonsterDeckKind.Boss);
            Monster(c, dragon, "monster.dragon_follower", "龙信徒", 1, 6, 2, 0, "skill.devotion");
            Monster(c, dragon, "monster.fire_bather", "浴火者", 1, 10, 2, 0, "skill.love_fire");
            Monster(c, dragon, "monster.salamander", "火蜥蜴", 1, 9, 3, 0, "skill.breathe_fire");
            Monster(c, dragon, "monster.stone_golem", "石傀儡", 1, 8, 2, 6);
            Monster(c, dragon, "monster.fire_priest", "火焰祭司", 2, 12, 0, 8, "skill.fire_power");
            Monster(c, dragon, "monster.fire_swallower", "吞火者", 2, 8, 2, 8, "skill.hard");
            Monster(c, dragon, "monster.executioner", "刽子手", 2, 14, 3, 0, "skill.sacrifice");
            Monster(c, dragon, "monster.dragon_cult_leader", "龙教主", 3, 18, 3, 3, "skill.call_followers");
            Monster(c, dragon, "monster.fire_cult_leader", "火教主", 3, 18, 3, 3, "skill.intense_burning");
            Monster(c, dragon, "monster.fire_dragon", "火龙", 0, 50, 5, 5, "skill.flame_breath", "skill.flame_boiling").AsBoss();

            var voidDeck = Deck(c, "deck.void", "虚空牌组", MonsterDeckKind.Boss);
            Monster(c, voidDeck, "monster.void_cub", "虚空幼崽", 1, 10, 2, 0, "skill.unstable");
            Monster(c, voidDeck, "monster.rotating_cub", "旋转幼崽", 1, 10, 2, 0, "skill.rotate_lover");
            Monster(c, voidDeck, "monster.stepwalker", "踏步行者", 1, 9, 2, 0, "skill.random_walk");
            Monster(c, voidDeck, "monster.void_lost", "误入虚空者", 1, 14, 2, 0);
            Monster(c, voidDeck, "monster.sky_eye", "空中巨眼", 2, 14, 2, 2, "skill.air_strike");
            Monster(c, voidDeck, "monster.observer", "观察者", 2, 8, 2, 3, "skill.hot_observation");
            Monster(c, voidDeck, "monster.world_turning_hand", "转动世界的手", 2, 12, 3, 0, "skill.turn_world");
            Monster(c, voidDeck, "monster.mist", "迷雾", 3, 10, 1, 0, "skill.stroll");
            Monster(c, voidDeck, "monster.friendly_ancient", "友好的远古生物", 3, 18, 2, 5, "skill.gift");
            Monster(c, voidDeck, "monster.space_master", "空间大师", 0, 42, 4, 10, "skill.space_mastery", "skill.otherworld_help").AsBoss();
        }

        private static void AddRewardsAndRooms(GameContentCatalog c)
        {
            c.Rewards
                .AddPool(new RewardPoolDefinition("kill.elite", 3)
                    .Add("help.blue_chest_card", CardKind.HelpCard, 1)
                    .Add("help.gold_card", CardKind.HelpCard, 1)
                    .Add("help.stat_boost_card", CardKind.HelpCard, 1))
                .AddPool(new RewardPoolDefinition("kill.boss", 3)
                    .Add("help.golden_chest_card", CardKind.HelpCard, 1)
                    .Add("help.gold_card", CardKind.HelpCard, 1, 2)
                    .Add("help.stat_boost_card", CardKind.HelpCard, 1))
                .AddPool(new RewardPoolDefinition("help.choice", 3)
                    .Add("help.healing_potion", CardKind.HelpCard, 20)
                    .Add("help.throwing_knife", CardKind.HelpCard, 20)
                    .Add("help.sturdy_shield", CardKind.HelpCard, 20)
                    .Add("help.bomb", CardKind.HelpCard, 8)
                    .Add("help.gold_card", CardKind.HelpCard, 5))
                .AddPool(new RewardPoolDefinition("relic.common_chest", 3)
                    .Add("relic.wood_shield", CardKind.Relic, 65)
                    .Add("relic.vitality_amulet", CardKind.Relic, 30)
                    .Add("relic.dragon_scale_armor", CardKind.Relic, 5))
                .AddRoom(new RoomDefinition(RoomKind.Shop, "商店") { Weight = 25, ShopOfferCount = 6 })
                .AddRoom(new RoomDefinition(RoomKind.Gold, "金币房") { Weight = 20, GoldDelta = 50 })
                .AddRoom(new RoomDefinition(RoomKind.Treasure, "宝箱房") { Weight = 20, RewardPoolId = "relic.common_chest" })
                .AddRoom(new RoomDefinition(RoomKind.Fountain, "温泉房") { Weight = 20, MaxHpDelta = 4, HealToFull = true })
                .AddRoom(new RoomDefinition(RoomKind.Tavern, "酒馆") { Weight = 15 });

            AddNodeRule(c, 1, 10, 7, 8, 2, 3, 0, 0, 0, 0, MonsterDeckKind.WeakElite);
            AddNodeRule(c, 2, 11, 5, 6, 3, 4, 1, 2, 0, 0, MonsterDeckKind.WeakElite);
            AddNodeRule(c, 3, 12, 3, 4, 5, 6, 2, 4, 1, 0, MonsterDeckKind.WeakElite);
            AddNodeRule(c, 4, 12, 8, 9, 3, 4, 0, 0, 0, 0, MonsterDeckKind.StrongElite);
            AddNodeRule(c, 5, 13, 6, 7, 4, 5, 1, 3, 0, 0, MonsterDeckKind.StrongElite);
            AddNodeRule(c, 6, 14, 4, 5, 5, 6, 3, 5, 1, 0, MonsterDeckKind.StrongElite);
            AddNodeRule(c, 7, 14, 8, 9, 5, 6, 0, 0, 0, 0, MonsterDeckKind.Boss);
            AddNodeRule(c, 8, 15, 6, 7, 6, 7, 1, 3, 0, 0, MonsterDeckKind.Boss);
            AddNodeRule(c, 9, 16, 4, 5, 7, 8, 3, 5, 0, 1, MonsterDeckKind.Boss);
        }

        private static CardContentDefinition Help(GameContentCatalog c, string id, string name, ContentRarity rarity, int price, string tag)
        {
            var card = new CardContentDefinition(id, name, CardKind.HelpCard)
                .WithRarity(rarity)
                .WithPrice(price)
                .AddTag(tag);
            c.AddCard(card);
            return card;
        }

        private static RelicContentDefinition Relic(GameContentCatalog c, string id, string name, ContentRarity rarity, string text)
        {
            var relic = new RelicContentDefinition(id, name, rarity, text);
            c.AddRelic(relic);
            return relic;
        }

        private static SkillContentDefinition Skill(GameContentCatalog c, string id, string name, EffectContainerType type, string text)
        {
            var skill = new SkillContentDefinition(id, name, type, text);
            c.AddSkill(skill);
            return skill;
        }

        private static void PendingSkill(GameContentCatalog c, string id, string name)
        {
            Skill(c, id, name, EffectContainerType.MonsterSkill, name)
                .AddEffect(Pending(c, id + ".pending", EffectContainerType.MonsterSkill, name));
        }

        private static MonsterDeckDefinition Deck(GameContentCatalog c, string id, string name, MonsterDeckKind kind)
        {
            var deck = new MonsterDeckDefinition(id, name, kind);
            c.AddMonsterDeck(deck);
            return deck;
        }

        private static CardContentDefinition Monster(
            GameContentCatalog c,
            MonsterDeckDefinition deck,
            string id,
            string name,
            int level,
            int hp,
            int attack,
            int armor,
            params string[] skills)
        {
            var card = new CardContentDefinition(id, name, CardKind.Monster)
                .WithStats(hp, attack, armor)
                .WithLevel(level)
                .InDeck(deck.Id);
            for (var i = 0; i < skills.Length; i++)
            {
                card.AddSkill(skills[i]);
            }

            c.AddCard(card);
            deck.AddMonster(id);
            return card;
        }

        private static void AddNodeRule(
            GameContentCatalog c,
            int node,
            int total,
            int l1Min,
            int l1Max,
            int l2Min,
            int l2Max,
            int l3Min,
            int l3Max,
            int elite,
            int boss,
            MonsterDeckKind kind)
        {
            c.Rewards.AddNodeRule(new NodeDeckRule
            {
                NodeIndex = node,
                TotalMonsterCount = total,
                Level1Min = l1Min,
                Level1Max = l1Max,
                Level2Min = l2Min,
                Level2Max = l2Max,
                Level3Min = l3Min,
                Level3Max = l3Max,
                EliteCount = elite,
                BossCount = boss,
                DeckKind = kind
            });
        }

        private static ContentEffectDefinition Impl(string id, EffectContainerType type, string json, string text)
        {
            return new ContentEffectDefinition(id, type, json, ContentImplementationState.Implemented, text);
        }

        private static string Pending(GameContentCatalog c, string id, EffectContainerType type, string text)
        {
            c.AddEffect(new ContentEffectDefinition(id, type, string.Empty, ContentImplementationState.PendingAtom, text));
            return id;
        }

        private static string Triggered(string id, string container, string trigger, string target, string action)
        {
            return Triggered(id, container, trigger, target, action, null);
        }

        private static string Triggered(string id, string container, string trigger, string target, string action, string conditions)
        {
            return "{"
                + "\"id\":\"" + id + "\","
                + "\"typeTag\":\"" + TypeTag(container) + "\","
                + "\"containerType\":\"" + container + "\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":" + trigger + ","
                + (string.IsNullOrEmpty(conditions) ? string.Empty : "\"conditions\":" + conditions + ",")
                + "\"target\":" + target + ","
                + "\"action\":" + action
                + "}";
        }

        private static string Modifier(string id, string container, string target, string conditions, string modifier)
        {
            return "{"
                + "\"id\":\"" + id + "\","
                + "\"typeTag\":\"" + TypeTag(container) + "\","
                + "\"containerType\":\"" + container + "\","
                + "\"kind\":\"Modifier\","
                + "\"target\":" + target + ","
                + (string.IsNullOrEmpty(conditions) ? string.Empty : "\"conditions\":" + conditions + ",")
                + "\"modifier\":" + modifier
                + "}";
        }

        private static string Rule(string id, string container, string rule)
        {
            return "{"
                + "\"id\":\"" + id + "\","
                + "\"typeTag\":\"" + TypeTag(container) + "\","
                + "\"containerType\":\"" + container + "\","
                + "\"kind\":\"RuleModifier\","
                + "\"ruleModifier\":" + rule
                + "}";
        }

        private static string TypeTag(string container)
        {
            if (container == "Relic")
            {
                return "【类型遗物】";
            }

            if (container == "MonsterSkill")
            {
                return "【类型怪物技能】";
            }

            if (container == "PlayerSkill")
            {
                return "【类型玩家技能】";
            }

            return "【类型帮助卡】";
        }
    }
}
