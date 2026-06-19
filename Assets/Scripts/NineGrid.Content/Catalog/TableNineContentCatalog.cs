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

            c.AddEffect(Impl("help.fireball.use", EffectContainerType.HelpCard,
                Triggered("help.fireball.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"RandomMonster\"}",
                    "{\"atom\":\"DealDamage\",\"value\":{\"source\":\"Player\",\"stat\":\"Attack\"},\"actor\":\"Player\"}"),
                "[使用时] 对目标怪物卡造成等同于玩家攻击的伤害"));

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

            c.AddEffect(Impl("help.food_card.use", EffectContainerType.HelpCard,
                Triggered("help.food_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Heal\",\"value\":{\"op\":\"Subtract\",\"values\":[{\"source\":\"Player\",\"stat\":\"MaxHp\"},{\"source\":\"Player\",\"stat\":\"Hp\"}]},\"actor\":\"Player\"}"),
                "[使用时] 将玩家卡血量回满"));

            c.AddEffect(Impl("help.rotation_wheel.use", EffectContainerType.HelpCard,
                Triggered("help.rotation_wheel.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Rotate\",\"direction\":\"CounterClockwise\"}"),
                "[使用时] 逆时针旋转一次"));

            c.AddEffect(Impl("help.bear_trap.use", EffectContainerType.HelpCard,
                Triggered("help.bear_trap.use", "HelpCard",
                    "{\"atom\":\"OnDeal\"}",
                    "{\"atom\":\"EventCard\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"DealDamage\",\"amount\":10,\"actor\":\"Self\"},"
                    + "{\"atom\":\"RemoveCard\",\"target\":{\"atom\":\"Self\"},\"destination\":\"Removed\",\"reason\":\"bearTrap\"},"
                    + "{\"atom\":\"DeactivateSelfEffect\"}"
                    + "]}",
                    "[{\"atom\":\"ActionSource\",\"action\":\"FillEmptySlots\"},"
                    + "{\"atom\":\"EventFilter\",\"eventType\":\"CardDealt\",\"targetKind\":\"Monster\"},"
                    + "{\"atom\":\"Adjacent\",\"left\":\"Self\",\"right\":\"EventCard\"}]"),
                "[场上] 当正交相邻格补牌为怪物卡时，对其造成10点伤害，之后移除本卡"));

            c.AddEffect(Impl("help.ward_magic_card.use", EffectContainerType.HelpCard,
                Triggered("help.ward_magic_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"AddRuleModifier\",\"rule\":\"DamageMultiplier\",\"op\":\"Override\",\"value\":0,\"layer\":\"Temporary\",\"scope\":\"Once\",\"source\":\"help.ward_magic_card\"}"),
                "[使用时] 下一次玩家受到伤害时，该次伤害变为0"));
            c.AddEffect(Impl("help.doubling_tower.board_monster", EffectContainerType.HelpCard,
                Triggered("help.doubling_tower.board_monster", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\",\"ownerOnly\":false,\"excludeSelf\":true}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ReplayHelpCardEffects\",\"targetKind\":\"Monster\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":1}]"),
                "[场上] 处于格1时，对怪物卡使用的帮助卡触发两次"));
            c.AddEffect(Impl("help.doubling_tower.item_player", EffectContainerType.HelpCard,
                Triggered("help.doubling_tower.item_player", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\",\"ownerOnly\":false,\"excludeSelf\":true}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ReplayHelpCardEffects\",\"targetKind\":\"Avatar\",\"deactivateSelf\":true}",
                    "[{\"atom\":\"CardZone\",\"target\":\"Self\",\"zone\":\"ItemSlots\"}]"),
                "[道具牌格] 对玩家卡使用的帮助卡生效两次，触发后永久移除本卡"));

            c.AddEffect(Impl("help.impact_tutorial.use", EffectContainerType.HelpCard,
                Triggered("help.impact_tutorial.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"RandomMonster\"}",
                    "{\"atom\":\"DealDamage\",\"value\":{\"source\":\"Player\",\"stat\":\"Hp\"},\"actor\":\"Player\"}"),
                "[使用时] 对目标怪物卡造成等同于玩家当前血量的伤害"));

            c.AddEffect(Impl("help.shield_bash_tutorial.use", EffectContainerType.HelpCard,
                Triggered("help.shield_bash_tutorial.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"RandomMonster\"}",
                    "{\"atom\":\"DealDamage\",\"value\":{\"source\":\"Player\",\"stat\":\"Armor\"},\"actor\":\"Player\"}"),
                "[使用时] 对目标怪物卡造成等同于玩家当前护甲的伤害"));

            c.AddEffect(Impl("help.gold_card.use", EffectContainerType.HelpCard,
                Triggered("help.gold_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"ModifyGold\",\"delta\":50,\"reason\":\"goldCard\"}"),
                "[使用时] 为玩家提供50金币"));
            c.AddEffect(Impl("help.blood_conversion.use", EffectContainerType.HelpCard,
                Triggered("help.blood_conversion.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"ModifyBaseStat\",\"stat\":\"MaxHp\",\"delta\":-5,\"reason\":\"bloodConversion\"},"
                    + "{\"atom\":\"WeightedRandom\",\"choices\":["
                    + "{\"weight\":1,\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"bloodConversion.attack\"}},"
                    + "{\"weight\":1,\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Armor\",\"delta\":1,\"reason\":\"bloodConversion.armor\"}},"
                    + "{\"weight\":1,\"action\":{\"atom\":\"ModifyGold\",\"delta\":50,\"reason\":\"bloodConversion.gold\"}},"
                    + "{\"weight\":1,\"action\":{\"atom\":\"GrantRewardFromPool\",\"poolId\":\"relic.blood_conversion\"}}"
                    + "]}"
                    + "]}"),
                "[使用时] 扣除5点血量上限并随机获得攻击+1、护甲+1、50金币或随机遗物"));
            c.AddEffect(Impl("help.swap_card.use", EffectContainerType.HelpCard,
                Triggered("help.swap_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"SelectedCards\",\"zone\":\"Board\",\"count\":2}",
                    "{\"atom\":\"Swap\"}"),
                "[使用时] 选择除玩家卡外的两张卡牌互换所在格子位置"));
            c.AddEffect(Impl("help.teleport_card.use", EffectContainerType.HelpCard,
                Triggered("help.teleport_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"SelectedCards\",\"zone\":\"Board\",\"count\":1}",
                    "{\"atom\":\"MoveToDrawPile\"}"),
                "[使用时] 选择除玩家卡外的一张卡牌洗回战斗卡组"));
            c.AddEffect(Impl("help.stat_boost_card.use", EffectContainerType.HelpCard,
                Triggered("help.stat_boost_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Conditional\",\"condition\":{\"atom\":\"SelectedOption\",\"option\":\"Attack\"},"
                    + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"statBoost.attack\"},"
                    + "\"elseAction\":{\"atom\":\"Conditional\",\"condition\":{\"atom\":\"SelectedOption\",\"option\":\"Armor\"},"
                    + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Armor\",\"delta\":1,\"reason\":\"statBoost.armor\"},"
                    + "\"elseAction\":{\"atom\":\"Conditional\",\"condition\":{\"atom\":\"SelectedOption\",\"option\":\"Hp\"},"
                    + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"MaxHp\",\"delta\":2,\"reason\":\"statBoost.hp\"}}}}}"),
                "[使用时] 选择攻击+1、护甲+1或血量上限与当前血量+2"));
            c.AddEffect(Impl("help.common_chest_card.use", EffectContainerType.HelpCard,
                Triggered("help.common_chest_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"OfferRewardChoice\",\"poolId\":\"relic.common_chest\"}"),
                "[使用时] 从三个遗物中选择一个获得"));
            c.AddEffect(Impl("help.blue_chest_card.use", EffectContainerType.HelpCard,
                Triggered("help.blue_chest_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"OfferRewardChoice\",\"poolId\":\"relic.blue_chest\"}"),
                "[使用时] 从三个遗物中选择一个获得"));
            c.AddEffect(Impl("help.golden_chest_card.use", EffectContainerType.HelpCard,
                Triggered("help.golden_chest_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"OfferRewardChoice\",\"poolId\":\"relic.golden_chest\"}"),
                "[使用时] 从三个遗物中选择一个获得"));

            c.AddEffect(Impl("help.brutality_card.use", EffectContainerType.HelpCard,
                Triggered("help.brutality_card.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"AddRuleModifier\",\"rule\":\"DamageMultiplier\",\"op\":\"Multiply\",\"value\":2,\"layer\":\"Temporary\",\"scope\":\"Once\",\"source\":\"help.brutality_card\",\"conditionTarget\":\"None\",\"conditionActor\":\"Player\",\"conditionTargetKind\":\"Monster\"}"),
                "[使用时] 玩家下一次对怪物造成的战斗伤害翻倍"));

            c.AddEffect(Impl("help.rolling_stone.board_slot3", EffectContainerType.HelpCard,
                Triggered("help.rolling_stone.board_slot3", "HelpCard",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":3,\"target\":\"Self\"}",
                    "{\"atom\":\"SlotCard\",\"slot\":6,\"kind\":\"Monster\",\"excludeElite\":true,\"excludeBoss\":true}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"help.rolling_stone.target\"},"
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"help.rolling_stone.self\",\"target\":{\"atom\":\"Self\"}},"
                    + "{\"atom\":\"DeactivateSelfEffect\"}"
                    + "]}",
                    "[{\"atom\":\"TargetCount\",\"min\":1,\"target\":{\"atom\":\"SlotCard\",\"slot\":6,\"kind\":\"Monster\",\"excludeElite\":true,\"excludeBoss\":true}}]"),
                "[场上] 移动到格3时，若格6为非精英非层主怪物，则移除该怪物和本卡"));
            c.AddEffect(Impl("help.rolling_stone.use", EffectContainerType.HelpCard,
                Triggered("help.rolling_stone.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"help.rolling_stone.use\"},"
                    + "{\"atom\":\"DeactivateSelfEffect\"}"
                    + "]}"),
                "[使用时] 直接移除本卡，不触发其他效果"));

            c.AddEffect(Impl("help.armor_breaking_hammer.use", EffectContainerType.HelpCard,
                Triggered("help.armor_breaking_hammer.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"SelectedCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"count\":1}",
                    "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Armor\",\"delta\":-10,\"reason\":\"help.armor_breaking_hammer\"}"),
                "[使用时] 将目标怪物卡护甲降低10点"));

            c.AddEffect(Impl("help.healing_spring.board_adjacent", EffectContainerType.HelpCard,
                Triggered("help.healing_spring.board_adjacent", "HelpCard",
                    "{\"atom\":\"OnSelfMove\",\"every\":1,\"requireAdjacentTo\":\"Player\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Heal\",\"amount\":2,\"actor\":\"Self\"}",
                    "[{\"atom\":\"CardZone\",\"target\":\"Self\",\"zone\":\"Board\"}]"),
                "[场上] 移动到玩家卡正交相邻格时，为玩家恢复2点血量"));
            c.AddEffect(Impl("help.healing_spring.item_battle", EffectContainerType.HelpCard,
                Triggered("help.healing_spring.item_battle", "HelpCard",
                    "{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Heal\",\"amount\":1,\"actor\":\"Self\"}",
                    "[{\"atom\":\"CardZone\",\"target\":\"Self\",\"zone\":\"ItemSlots\"},{\"atom\":\"EventFilter\",\"eventType\":\"DamageDealt\",\"actorIs\":\"Player\",\"targetKind\":\"Monster\"}]"),
                "[道具牌格] 玩家与怪物战斗时，恢复1点血量"));
            c.AddEffect(Impl("help.healing_spring.use", EffectContainerType.HelpCard,
                Triggered("help.healing_spring.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"help.healing_spring.use\"},"
                    + "{\"atom\":\"DeactivateSelfEffect\"}"
                    + "]}"),
                "[使用时] 直接移除本卡，不触发其他效果"));

            c.AddEffect(Impl("help.kidnapping.use", EffectContainerType.HelpCard,
                Triggered("help.kidnapping.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"SelectedCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"count\":1,\"excludeElite\":true,\"excludeBoss\":true}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"TransferArmor\",\"all\":true,\"receiver\":\"Player\",\"cause\":\"help.kidnapping\"},"
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"help.kidnapping\"}"
                    + "]}"),
                "[使用时] 移除一张非精英非层主怪物卡，并获得等同于其当前护甲的护甲"));

            c.AddEffect(Impl("help.watchtower.board_corner", EffectContainerType.HelpCard,
                Triggered("help.watchtower.board_corner", "HelpCard",
                    "{\"atom\":\"OnMoveToSlot\",\"slots\":[1,3,7,9],\"target\":\"Self\",\"counterKey\":\"help.watchtower.corner\"}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"random\":true,\"count\":1}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"DealDamage\",\"amount\":3,\"actor\":\"Self\"},"
                    + "{\"atom\":\"Conditional\",\"condition\":{\"atom\":\"CardCounter\",\"target\":\"Self\",\"key\":\"help.watchtower.corner\",\"min\":4},\"action\":{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"help.watchtower.self\",\"target\":{\"atom\":\"Self\"}},"
                    + "{\"atom\":\"DeactivateSelfEffect\"}"
                    + "]}}"
                    + "]}"),
                "[场上] 移动到角格时，对随机怪物造成3点伤害；触发4次后移除本卡"));
            c.AddEffect(Impl("help.watchtower.item_battle", EffectContainerType.HelpCard,
                Triggered("help.watchtower.item_battle", "HelpCard",
                    "{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0}",
                    "{\"atom\":\"RandomMonster\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}",
                    "[{\"atom\":\"CardZone\",\"target\":\"Self\",\"zone\":\"ItemSlots\"},{\"atom\":\"EventFilter\",\"eventType\":\"DamageDealt\",\"actorIs\":\"Player\",\"targetKind\":\"Monster\"}]"),
                "[道具牌格] 玩家与怪物战斗时，对随机一张怪物卡造成2点伤害"));
            c.AddEffect(Impl("help.watchtower.use", EffectContainerType.HelpCard,
                Triggered("help.watchtower.use", "HelpCard",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"help.watchtower.use\"},"
                    + "{\"atom\":\"DeactivateSelfEffect\"}"
                    + "]}"),
                "[使用时] 直接移除本卡，不触发其他效果"));

            c.AddEffect(Impl("skill.thorn_skin.battle", EffectContainerType.PlayerSkill,
                Triggered("skill.thorn_skin.battle", "PlayerSkill",
                    "{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0}",
                    "{\"atom\":\"EventTarget\"}",
                    "{\"atom\":\"DealDamage\",\"value\":{\"source\":\"Target\",\"stat\":\"Attack\"},\"actor\":\"Player\"}"),
                "[战斗时] 对怪物卡造成等同于该怪物卡攻击的伤害"));

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
            c.AddEffect(Impl("relic.lucky_coin.elite_kill", EffectContainerType.Relic,
                Triggered("relic.lucky_coin.elite_kill", "Relic",
                    "{\"atom\":\"OnKill\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"ShuffleInto\",\"defId\":\"help.gold_card\",\"kind\":\"HelpCard\",\"count\":1,\"top\":false}",
                    "[{\"atom\":\"CardCounter\",\"target\":\"EventCard\",\"key\":\"elite\"}]"),
                "[击杀精英时] 将一张金币卡加入战斗卡组"));
            c.AddEffect(Impl("relic.lucky_coin.boss_kill", EffectContainerType.Relic,
                Triggered("relic.lucky_coin.boss_kill", "Relic",
                    "{\"atom\":\"OnKill\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"ShuffleInto\",\"defId\":\"help.gold_card\",\"kind\":\"HelpCard\",\"count\":1,\"top\":false}",
                    "[{\"atom\":\"CardCounter\",\"target\":\"EventCard\",\"key\":\"boss\"}]"),
                "[击杀层主时] 将一张金币卡加入战斗卡组"));

            c.AddEffect(Impl("relic.heavy_armor.base", EffectContainerType.Relic,
                Modifier("relic.heavy_armor.base", "Relic", "{\"atom\":\"Player\"}", null,
                    "{\"stat\":\"Armor\",\"op\":\"Add\",\"value\":1,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "基础护甲+1"));
            c.AddEffect(Impl("relic.heavy_armor.node_start", EffectContainerType.Relic,
                Triggered("relic.heavy_armor.node_start", "Relic",
                    "{\"atom\":\"OnNodeStart\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"GainArmor\",\"value\":{\"op\":\"Floor\",\"values\":[{\"op\":\"Multiply\",\"values\":[{\"source\":\"Player\",\"stat\":\"Armor\",\"effective\":true},{\"constant\":0.5}]}]}}"),
                "[每关卡开始时] 每有两点基础护甲，额外获得1点当前护甲"));

            c.AddEffect(Impl("relic.vitality_amulet.node_end", EffectContainerType.Relic,
                Triggered("relic.vitality_amulet.node_end", "Relic",
                    "{\"atom\":\"OnNodeEnd\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Heal\",\"amount\":6,\"actor\":\"Player\"}"),
                "[每关卡结束时] 恢复6点血量"));
            c.AddEffect(Impl("relic.vitality_amulet.max_hp", EffectContainerType.Relic,
                Modifier("relic.vitality_amulet.max_hp", "Relic", "{\"atom\":\"Player\"}", null,
                    "{\"stat\":\"MaxHp\",\"op\":\"Add\",\"value\":6,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "血量上限+6"));

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
            c.AddEffect(Impl("skill.easy_road.node_end", EffectContainerType.PlayerSkill,
                Triggered("skill.easy_road.node_end", "PlayerSkill",
                    "{\"atom\":\"OnNodeEnd\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"OfferRewardChoice\",\"poolId\":\"help.white.choice\"}"),
                "[每关卡结束时] 进行一次白色帮助卡三选一"));
            c.AddEffect(Impl("skill.tower_child.node_start", EffectContainerType.PlayerSkill,
                Triggered("skill.tower_child.node_start", "PlayerSkill",
                    "{\"atom\":\"OnNodeStart\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Spawn\",\"defId\":\"help.doubling_tower\",\"kind\":\"HelpCard\",\"zone\":\"ItemSlots\"}"),
                "[每关卡开始时] 将一张倍增塔放入道具牌格"));
            c.AddEffect(Impl("skill.even_hatred.rule", EffectContainerType.PlayerSkill,
                Rule("skill.even_hatred.rule", "PlayerSkill",
                    "{\"rule\":\"DamageMultiplier\",\"op\":\"Multiply\",\"value\":2,\"layer\":\"Persistent\",\"scope\":\"Permanent\",\"source\":\"skill.even_hatred\"}",
                    "[{\"atom\":\"EventFilter\",\"targetKind\":\"Monster\",\"actorIs\":\"Player\"},{\"atom\":\"LevelParity\",\"target\":\"Self\",\"parity\":\"Even\"}]"),
                "[战斗时] 若目标怪物卡等级为偶数，玩家造成双倍伤害"));

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
            c.AddEffect(Impl("skill.stray_cub.first_strike", EffectContainerType.MonsterSkill,
                Rule("skill.stray_cub.first_strike", "MonsterSkill",
                    "{\"rule\":\"FirstStrike\",\"target\":\"Self\",\"op\":\"Override\",\"value\":1,\"layer\":\"Conditional\",\"scope\":\"Permanent\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":6}]"),
                "[场上] 处于格6时，本卡获得先攻"));
            c.AddEffect(Impl("skill.first_strike.rule", EffectContainerType.MonsterSkill,
                Rule("skill.first_strike.rule", "MonsterSkill",
                    "{\"rule\":\"FirstStrike\",\"target\":\"Self\",\"op\":\"Override\",\"value\":1,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "持有先攻技能"));
            c.AddEffect(Impl("skill.blessing.rule", EffectContainerType.MonsterSkill,
                Rule("skill.blessing.rule", "MonsterSkill",
                    "{\"rule\":\"DamageMultiplier\",\"target\":\"Self\",\"op\":\"Override\",\"value\":0,\"layer\":\"Temporary\",\"scope\":\"Once\"}"),
                "下一次受到伤害时，该次伤害变为0"));
            c.AddEffect(Impl("skill.taunt.rule", EffectContainerType.MonsterSkill,
                Rule("skill.taunt.rule", "MonsterSkill",
                    "{\"rule\":\"AttackTargetRestriction\",\"target\":\"Self\",\"op\":\"Override\",\"value\":0,\"layer\":\"Conditional\",\"scope\":\"Permanent\"}",
                    "[{\"atom\":\"Adjacent\",\"left\":\"Self\",\"right\":\"Player\"}]"),
                "[场上] 处于玩家卡正交相邻格时，玩家只能与本卡战斗"));
            c.AddEffect(Impl("skill.range_expand.rule", EffectContainerType.MonsterSkill,
                Rule("skill.range_expand.rule", "MonsterSkill",
                    "{\"rule\":\"VirtualAdjacency\",\"target\":\"Self\",\"op\":\"Override\",\"value\":1,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "[场上] 所有怪物卡视为与本卡正交相邻"));
            c.AddEffect(Impl("relic.gold_armor.rule", EffectContainerType.Relic,
                Rule("relic.gold_armor.rule", "Relic",
                    "{\"rule\":\"GoldArmorAbsorb\",\"op\":\"Override\",\"value\":1,\"layer\":\"Persistent\",\"scope\":\"Permanent\"}"),
                "[受到伤害时] 每5金币抵消1点当前护甲伤害"));
            c.AddEffect(Impl("skill.thief_claims.move", EffectContainerType.MonsterSkill,
                Triggered("skill.thief_claims.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":3}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"HelpCard\",\"zone\":\"Board\",\"adjacentTo\":\"Self\"}",
                    "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"thiefClaims\"}"),
                "每移动3次，移除正交相邻帮助卡"));
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
            c.AddEffect(Impl("skill.unstable.move", EffectContainerType.MonsterSkill,
                Triggered("skill.unstable.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":3}",
                    "{\"atom\":\"FilteredCards\",\"include\":[\"Self\"],\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"],\"random\":true,\"count\":1}",
                    "{\"atom\":\"Swap\"}"),
                "每移动3次，与九宫格上随机另一张怪物卡交换位置"));
            c.AddEffect(Impl("skill.random_walk.move", EffectContainerType.MonsterSkill,
                Triggered("skill.random_walk.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":3}",
                    "{\"atom\":\"FilteredCards\",\"include\":[\"Self\"],\"kind\":\"HelpCard\",\"zone\":\"Board\",\"random\":true,\"count\":1}",
                    "{\"atom\":\"Swap\"}"),
                "每移动3次，与九宫格上随机帮助卡交换位置"));
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
            c.AddEffect(Impl("relic.junk_launcher.volley", EffectContainerType.Relic,
                Triggered("relic.junk_launcher.volley", "Relic",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"RandomMonster\"}",
                    "{\"atom\":\"Repeat\",\"count\":2,\"action\":{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Player\"}}"),
                "[使用帮助卡时] 对随机怪物连续造成2次2点伤害"));

            c.AddEffect(Impl("relic.throwing_knife_bag.node_start", EffectContainerType.Relic,
                Triggered("relic.throwing_knife_bag.node_start", "Relic",
                    "{\"atom\":\"OnNodeStart\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Spawn\",\"defId\":\"help.throwing_knife\",\"kind\":\"HelpCard\",\"zone\":\"PlayerCardPool\",\"count\":2}"),
                "[每关卡开始时] 将两张飞刀加入到玩家侧卡组"));
            c.AddEffect(Impl("relic.potion_bag.node_start", EffectContainerType.Relic,
                Triggered("relic.potion_bag.node_start", "Relic",
                    "{\"atom\":\"OnNodeStart\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Spawn\",\"defId\":\"help.healing_potion\",\"kind\":\"HelpCard\",\"zone\":\"PlayerCardPool\",\"count\":2}"),
                "[每关卡开始时] 将两张恢复药水加入到玩家侧卡组"));

            c.AddEffect(Impl("relic.junk_slot_machine.use", EffectContainerType.Relic,
                Triggered("relic.junk_slot_machine.use", "Relic",
                    "{\"atom\":\"OnUseHelpCard\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"WeightedRandom\",\"choices\":["
                    + "{\"weight\":1,\"action\":{\"atom\":\"ModifyGold\",\"delta\":9,\"reason\":\"slot\"}},"
                    + "{\"weight\":0,\"action\":{\"atom\":\"GainArmor\",\"amount\":1}},"
                    + "{\"weight\":0,\"action\":{\"atom\":\"Heal\",\"amount\":1,\"actor\":\"Player\"}},"
                    + "{\"weight\":0,\"action\":{\"atom\":\"DealDamage\",\"amount\":1,\"actor\":\"Player\"}},"
                    + "{\"weight\":0,\"action\":{\"atom\":\"Spawn\",\"defId\":\"help.throwing_knife\",\"kind\":\"HelpCard\",\"zone\":\"ItemSlots\"}},"
                    + "{\"weight\":0,\"action\":{\"atom\":\"Spawn\",\"defId\":\"help.bomb\",\"kind\":\"HelpCard\",\"zone\":\"ItemSlots\"}},"
                    + "{\"weight\":0,\"action\":{\"atom\":\"Spawn\",\"defId\":\"help.sturdy_shield\",\"kind\":\"HelpCard\",\"zone\":\"ItemSlots\"}},"
                    + "{\"weight\":0,\"action\":{\"atom\":\"ModifyGold\",\"delta\":1,\"reason\":\"slot\"}},"
                    + "{\"weight\":0,\"action\":{\"atom\":\"Rotate\",\"count\":1}}"
                    + "]}"),
                "[使用帮助卡时] 随机触发九选一"));

            c.AddEffect(Impl("skill.recombine_head.move", EffectContainerType.MonsterSkill,
                Triggered("skill.recombine_head.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":1}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\"},"
                    + "{\"atom\":\"RemoveCard\",\"target\":{\"atom\":\"AdjacentCard\",\"origin\":\"Self\",\"defId\":\"monster.skull_head\"}},"
                    + "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.big_skeleton\",\"kind\":\"Monster\",\"count\":1,\"top\":true}"
                    + "]}",
                    "[{\"atom\":\"AdjacentHasCard\",\"origin\":\"Self\",\"defId\":\"monster.skull_head\"}]"),
                "相邻骷髅头时移除双方并洗入大骷髅"));

            c.AddEffect(Impl("skill.recombine_body.move", EffectContainerType.MonsterSkill,
                Triggered("skill.recombine_body.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":1}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\"},"
                    + "{\"atom\":\"RemoveCard\",\"target\":{\"atom\":\"AdjacentCard\",\"origin\":\"Self\",\"defId\":\"monster.headless_skeleton\"}},"
                    + "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.big_skeleton\",\"kind\":\"Monster\",\"count\":1,\"top\":true}"
                    + "]}",
                    "[{\"atom\":\"AdjacentHasCard\",\"origin\":\"Self\",\"defId\":\"monster.headless_skeleton\"}]"),
                "相邻无头骷髅时移除双方并洗入大骷髅"));

            c.AddEffect(Impl("skill.falling_rocks.cumulative", EffectContainerType.MonsterSkill,
                Triggered("skill.falling_rocks.cumulative", "MonsterSkill",
                    "{\"atom\":\"OnCumulative\",\"metric\":\"armorLost\",\"threshold\":10}", "{\"atom\":\"Player\"}",
                    "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.stone_man\",\"kind\":\"Monster\",\"count\":1,\"top\":false}"),
                "每累计损失满10点护甲，将一张石人军团怪物洗入战斗卡组"));

            c.AddEffect(Impl("skill.hard.slot1", EffectContainerType.MonsterSkill,
                Triggered("skill.hard.slot1", "MonsterSkill",
                    "{\"atom\":\"OnBattle\",\"targetKind\":\"Monster\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"value\":{\"op\":\"Negate\",\"values\":[{\"source\":\"Event\",\"field\":\"Delta\"}]},\"actor\":\"Self\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":1},{\"atom\":\"EventFilter\",\"eventType\":\"ArmorChanged\",\"targetIs\":\"Self\",\"maxDelta\":-1}]"),
                "[场上] 处于格1时，战斗损失护甲后对玩家造成等同损失护甲的伤害"));
            c.AddEffect(Impl("skill.hard.slot4", EffectContainerType.MonsterSkill,
                Triggered("skill.hard.slot4", "MonsterSkill",
                    "{\"atom\":\"OnBattle\",\"targetKind\":\"Monster\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"value\":{\"op\":\"Negate\",\"values\":[{\"source\":\"Event\",\"field\":\"Delta\"}]},\"actor\":\"Self\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":4},{\"atom\":\"EventFilter\",\"eventType\":\"ArmorChanged\",\"targetIs\":\"Self\",\"maxDelta\":-1}]"),
                "[场上] 处于格4时，战斗损失护甲后对玩家造成等同损失护甲的伤害"));
            c.AddEffect(Impl("skill.hard.slot7", EffectContainerType.MonsterSkill,
                Triggered("skill.hard.slot7", "MonsterSkill",
                    "{\"atom\":\"OnBattle\",\"targetKind\":\"Monster\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"value\":{\"op\":\"Negate\",\"values\":[{\"source\":\"Event\",\"field\":\"Delta\"}]},\"actor\":\"Self\"}",
                    "[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":7},{\"atom\":\"EventFilter\",\"eventType\":\"ArmorChanged\",\"targetIs\":\"Self\",\"maxDelta\":-1}]"),
                "[场上] 处于格7时，战斗损失护甲后对玩家造成等同损失护甲的伤害"));
            c.AddEffect(Impl("skill.swallow_stone.move", EffectContainerType.MonsterSkill,
                Triggered("skill.swallow_stone.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":2}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"adjacentTo\":\"Self\",\"exclude\":[\"Self\"]}",
                    "{\"atom\":\"TransferArmor\",\"amount\":1,\"receiver\":\"Self\",\"cause\":\"skill.swallow_stone\"}"),
                "每移动2次，扣除相邻怪物卡1点护甲并使本卡获得等量护甲"));
            c.AddEffect(Impl("skill.bloodthirst.damage", EffectContainerType.MonsterSkill,
                Triggered("skill.bloodthirst.damage", "MonsterSkill",
                    "{\"atom\":\"OnCumulative\",\"metric\":\"damageDealt\",\"threshold\":2,\"actorIs\":\"Self\",\"targetIs\":\"Player\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"skill.bloodthirst\"}"),
                "[战斗时] 每累计对玩家造成2点伤害，本卡攻击+1"));
            c.AddEffect(Impl("skill.smart.gain", EffectContainerType.MonsterSkill,
                Triggered("skill.smart.gain", "MonsterSkill",
                    "{\"atom\":\"OnEvent\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"skill.smart\"}",
                    "[{\"atom\":\"EventFilter\",\"eventTypes\":[\"BaseStatModified\",\"EffectModifierApplied\"],\"stat\":\"Attack\",\"minDelta\":1,\"targetIs\":\"Self\",\"excludeSourceDefId\":\"skill.smart\"}]"),
                "每次本卡获得攻击时，本卡额外攻击+1；不响应本效果自身加成"));
            c.AddEffect(Impl("skill.stone_lover.armor_lost", EffectContainerType.MonsterSkill,
                Triggered("skill.stone_lover.armor_lost", "MonsterSkill",
                    "{\"atom\":\"OnEvent\",\"eventType\":\"ArmorChanged\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"skill.stone_lover\"}",
                    "[{\"atom\":\"EventFilter\",\"eventType\":\"ArmorChanged\",\"maxDelta\":-1}]"),
                "每当玩家卡或怪物卡损失护甲时，本卡攻击+1"));
            c.AddEffect(Impl("skill.throw_stone.move", EffectContainerType.MonsterSkill,
                Triggered("skill.throw_stone.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":2}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Conditional\",\"condition\":{\"atom\":\"StatAtLeast\",\"target\":\"Self\",\"stat\":\"Armor\",\"value\":2},\"action\":{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"TransferArmor\",\"amount\":2,\"receiver\":\"None\",\"cause\":\"skill.throw_stone\"},"
                    + "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\",\"target\":{\"atom\":\"Player\"}}"
                    + "]}}"),
                "每移动2次，若本卡护甲至少2，扣除2点护甲并对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.stone_shelter.rule", EffectContainerType.MonsterSkill,
                Rule("skill.stone_shelter.rule", "MonsterSkill",
                    "{\"rule\":\"DamageFlatDelta\",\"op\":\"Add\",\"value\":-1,\"layer\":\"Persistent\",\"scope\":\"Permanent\",\"source\":\"skill.stone_shelter\"}",
                    "[{\"atom\":\"EventFilter\",\"targetKind\":\"Monster\",\"targetNot\":\"Self\"}]"),
                "[场上] 其他怪物卡受到的伤害减少1点"));
            c.AddEffect(Impl("skill.absorb_stone.move", EffectContainerType.MonsterSkill,
                Triggered("skill.absorb_stone.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":1}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"adjacentTo\":\"Self\",\"exclude\":[\"Self\"]}",
                    "{\"atom\":\"TransferArmor\",\"all\":true,\"receiver\":\"Self\",\"cause\":\"skill.absorb_stone\"}"),
                "[场上] 每次移动时，扣除相邻怪物卡全部护甲并使本卡获得等量护甲"));

            c.AddEffect(Impl("skill.learning_growth.gain", EffectContainerType.MonsterSkill,
                Triggered("skill.learning_growth.gain", "MonsterSkill",
                    "{\"atom\":\"OnEvent\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"skill.learning_growth\"}",
                    "[{\"atom\":\"EventFilter\",\"eventTypes\":[\"BaseStatModified\",\"EffectModifierApplied\"],\"stat\":\"Attack\",\"minDelta\":1,\"targetKind\":\"Monster\",\"targetNot\":\"Self\",\"excludeSourceDefId\":\"skill.learning_growth\"}]"),
                "每当其他怪物卡获得攻击时，本卡攻击+1，且不响应学习成长自身产生的攻击增加"));

            c.AddEffect(Impl("skill.intense_burning.flame_deal", EffectContainerType.MonsterSkill,
                Triggered("skill.intense_burning.flame_deal", "MonsterSkill",
                    "{\"atom\":\"OnDeal\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"ShuffleInto\",\"defId\":\"help.flame\",\"kind\":\"HelpCard\",\"count\":1,\"top\":false}",
                    "[{\"atom\":\"EventFilter\",\"eventType\":\"CardDealt\",\"sourceDefId\":\"help.flame\",\"excludeCause\":\"skill.intense_burning\"}]"),
                "每有一张烈焰加入战斗卡组，额外加入一张；额外加入的烈焰不再触发本效果"));

            c.AddEffect(Impl("skill.flame_boiling.rule", EffectContainerType.MonsterSkill,
                Rule("skill.flame_boiling.rule", "MonsterSkill",
                    "{\"rule\":\"DamageFlatDelta\",\"op\":\"Add\",\"value\":1,\"layer\":\"Persistent\",\"scope\":\"Permanent\",\"source\":\"skill.flame_boiling\"}",
                    "[{\"atom\":\"ActionSource\",\"action\":\"DealDamage\",\"sourceDefId\":\"help.flame\"}]"),
                "烈焰帮助卡造成的伤害+1"));

            c.AddEffect(Impl("skill.violence_maniac.move", EffectContainerType.MonsterSkill,
                Triggered("skill.violence_maniac.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":2}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"skill.violence_maniac\",\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"adjacentTo\":\"Self\",\"exclude\":[\"Self\"]}},"
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"skill.violence_maniac\",\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"HelpCard\",\"zone\":\"Board\",\"adjacentTo\":\"Self\"}}"
                    + "]}"),
                "每移动2次，移除相邻格子上的怪物卡和帮助卡"));

            c.AddEffect(Impl("skill.violence_nutrition.monster_remove", EffectContainerType.MonsterSkill,
                Triggered("skill.violence_nutrition.monster_remove", "MonsterSkill",
                    "{\"atom\":\"OnRemove\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":3,\"reason\":\"skill.violence_nutrition\"}",
                    "[{\"atom\":\"EventFilter\",\"eventType\":\"CardRemoved\",\"sourceDefId\":\"skill.violence_maniac\",\"targetKind\":\"Monster\"}]"),
                "每依靠暴力狂移除一张怪物卡，本卡攻击+3"));

            c.AddEffect(Impl("skill.violence_nutrition.help_remove", EffectContainerType.MonsterSkill,
                Triggered("skill.violence_nutrition.help_remove", "MonsterSkill",
                    "{\"atom\":\"OnRemove\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Armor\",\"delta\":5,\"reason\":\"skill.violence_nutrition\"}",
                    "[{\"atom\":\"EventFilter\",\"eventType\":\"CardRemoved\",\"sourceDefId\":\"skill.violence_maniac\",\"targetKind\":\"HelpCard\"}]"),
                "每依靠暴力狂移除一张帮助卡，本卡护甲+5"));

            c.AddEffect(Impl("skill.absorb_bone.remove", EffectContainerType.MonsterSkill,
                Triggered("skill.absorb_bone.remove", "MonsterSkill",
                    "{\"atom\":\"OnRemove\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"value\":{\"source\":\"Event\",\"field\":\"RemovedAttack\"},\"reason\":\"skill.absorb_bone.attack\"},"
                    + "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Armor\",\"value\":{\"source\":\"Event\",\"field\":\"RemovedArmor\"},\"reason\":\"skill.absorb_bone.armor\"}"
                    + "]}",
                    "[{\"atom\":\"EventFilter\",\"eventType\":\"CardRemoved\",\"targetKind\":\"Monster\"},{\"atom\":\"Adjacent\",\"left\":\"Self\",\"right\":\"EventCard\"}]"),
                "正交相邻格怪物被移除时，获得该怪物移除前的攻击和护甲"));

            c.AddEffect(Impl("skill.guide.create_blessed", EffectContainerType.MonsterSkill,
                Triggered("skill.guide.create_blessed", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":3}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"SetBoardMark\",\"mark\":\"Blessed\",\"random\":true,\"count\":1,\"onlyUnmarked\":true,\"excludeSlots\":[5]}"),
                "每移动3次，在非格5且未标记为福地的格子中随机标记1个福地"));
            c.AddEffect(Impl("skill.guide.blessed_enter", EffectContainerType.MonsterSkill,
                Triggered("skill.guide.blessed_enter", "MonsterSkill",
                    "{\"atom\":\"OnMoveToBoardMark\",\"mark\":\"Blessed\",\"targetKind\":\"Monster\"}",
                    "{\"atom\":\"BoardMarkEventCard\",\"mark\":\"Blessed\",\"targetKind\":\"Monster\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"GainArmor\",\"amount\":2},"
                    + "{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"skill.guide\"}"
                    + "]}"),
                "任意怪物移动到福地时获得2点护甲且攻击+1"));

            c.AddEffect(Impl("skill.turn_world.enter", EffectContainerType.MonsterSkill,
                Triggered("skill.turn_world.enter", "MonsterSkill",
                    "{\"atom\":\"OnEnter\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Rotate\",\"count\":1}"),
                "[登场] 旋转一次"));
            c.AddEffect(Impl("skill.hoodlum.slot1", EffectContainerType.MonsterSkill,
                Triggered("skill.hoodlum.slot1", "MonsterSkill",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":1,\"target\":\"Self\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}"),
                "[场上] 每次移动到格1时，对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.fall_apart.remove", EffectContainerType.MonsterSkill,
                Triggered("skill.fall_apart.remove", "MonsterSkill",
                    "{\"atom\":\"OnRemove\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.skull_head\",\"kind\":\"Monster\",\"count\":1,\"top\":false},"
                    + "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.headless_skeleton\",\"kind\":\"Monster\",\"count\":1,\"top\":false}"
                    + "]}"),
                "[场上] [被移除时]，将一张骷髅头和一张无头骷髅洗入战斗卡组"));
            c.AddEffect(Impl("skill.delivery.move", EffectContainerType.MonsterSkill,
                Triggered("skill.delivery.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":2}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"HelpCard\",\"zone\":\"Board\",\"adjacentTo\":\"Self\",\"random\":true,\"count\":1}",
                    "{\"atom\":\"ExchangeWithDrawPile\",\"kind\":\"Monster\"}"),
                "每移动2次，将相邻格上一张帮助卡与战斗卡组里一张怪物卡交换位置"));
            c.AddEffect(Impl("skill.hot_observation.observe", EffectContainerType.MonsterSkill,
                Triggered("skill.hot_observation.observe", "MonsterSkill",
                    "{\"atom\":\"OnEvent\",\"eventType\":\"CardMoved\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":1,\"actor\":\"Self\"}",
                    "[{\"atom\":\"EventFilter\",\"eventType\":\"CardMoved\",\"targetKind\":\"Monster\",\"sourcePrefix\":\"skill.\",\"excludeSourceDefId\":\"skill.hot_observation\"}]"),
                "怪物卡因怪物技能改变位置时，对玩家造成1点伤害"));
            c.AddEffect(Impl("skill.relentless_chase.move", EffectContainerType.MonsterSkill,
                Triggered("skill.relentless_chase.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":1,\"requireAdjacentTo\":\"Player\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ForceBattle\"}"),
                "[场上] 每次移动到玩家正交相邻格时，与玩家战斗一次"));
            c.AddEffect(Impl("skill.fight_me.battle", EffectContainerType.MonsterSkill,
                Triggered("skill.fight_me.battle", "MonsterSkill",
                    "{\"atom\":\"OnBattle\",\"sourceAction\":\"DealDamage\",\"targetKind\":\"Monster\",\"maxActionDepth\":0}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ForceBattle\"}",
                    "[{\"atom\":\"Adjacent\",\"left\":\"Self\",\"right\":\"Player\"},{\"atom\":\"EventFilter\",\"eventType\":\"DamageDealt\",\"actorIs\":\"Player\",\"targetKind\":\"Monster\",\"targetNot\":\"Self\"}]"),
                "[场上] 处于玩家正交相邻格时，如果玩家与其他怪物卡战斗，则与本卡战斗一次"));
            c.AddEffect(Impl("skill.sacrifice.move", EffectContainerType.MonsterSkill,
                Triggered("skill.sacrifice.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":2}",
                    "{\"atom\":\"FilteredCards\",\"defId\":\"monster.dragon_follower\",\"kind\":\"Monster\",\"zone\":\"Board\"}",
                    "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"skill.sacrifice\"}"),
                "每移动2次，移除九宫格上所有的龙信徒"));
            c.AddEffect(Impl("skill.fracture_fall_apart.remove", EffectContainerType.MonsterSkill,
                Triggered("skill.fracture_fall_apart.remove", "MonsterSkill",
                    "{\"atom\":\"OnRemove\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.multi_bone_worm\",\"kind\":\"Monster\",\"count\":1,\"top\":false},"
                    + "{\"atom\":\"ShuffleRandomContent\",\"kind\":\"Monster\",\"minLevel\":2,\"maxLevel\":2,\"excludeElite\":true,\"excludeBoss\":true,\"count\":1,\"top\":false}"
                    + "]}"),
                "[场上] [被移除时]，将一张多骨虫和一张等级2随机怪物卡洗入战斗卡组"));
            c.AddEffect(Impl("skill.flame_breath.move", EffectContainerType.MonsterSkill,
                Triggered("skill.flame_breath.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":4}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"],\"excludeElite\":true,\"excludeBoss\":true}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"skill.flame_breath\"},"
                    + "{\"atom\":\"ShuffleInto\",\"defId\":\"help.flame\",\"kind\":\"HelpCard\",\"count\":1,\"top\":false,\"perTarget\":true},"
                    + "{\"atom\":\"Spawn\",\"defId\":\"help.flame\",\"kind\":\"HelpCard\",\"zone\":\"ItemSlots\",\"count\":1,\"perTarget\":true}"
                    + "]}"),
                "每移动4次，移除其他所有非精英非层主怪物卡；每移除一张，洗入一张烈焰并放入一张烈焰到道具牌格"));
            c.AddEffect(Impl("skill.air_strike.slot1", EffectContainerType.MonsterSkill,
                Triggered("skill.air_strike.slot1", "MonsterSkill",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":1,\"target\":\"Self\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}"),
                "[场上] 移动到格1时对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.air_strike.slot3", EffectContainerType.MonsterSkill,
                Triggered("skill.air_strike.slot3", "MonsterSkill",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":3,\"target\":\"Self\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}"),
                "[场上] 移动到格3时对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.air_strike.slot7", EffectContainerType.MonsterSkill,
                Triggered("skill.air_strike.slot7", "MonsterSkill",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":7,\"target\":\"Self\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}"),
                "[场上] 移动到格7时对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.air_strike.slot9", EffectContainerType.MonsterSkill,
                Triggered("skill.air_strike.slot9", "MonsterSkill",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":9,\"target\":\"Self\"}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"DealDamage\",\"amount\":2,\"actor\":\"Self\"}"),
                "[场上] 移动到格9时对玩家造成2点伤害"));
            c.AddEffect(Impl("skill.space_mastery.battle", EffectContainerType.MonsterSkill,
                Triggered("skill.space_mastery.battle", "MonsterSkill",
                    "{\"atom\":\"OnBattle\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Rotate\",\"count\":1}"),
                "玩家与本卡战斗后，旋转一次"));
            c.AddEffect(Impl("skill.otherworld_help.move", EffectContainerType.MonsterSkill,
                Triggered("skill.otherworld_help.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":9}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"ShuffleRandomContent\",\"kind\":\"Monster\",\"minLevel\":1,\"maxLevel\":3,\"excludeElite\":true,\"excludeBoss\":true,\"excludeDeckId\":\"deck.void\",\"count\":1,\"top\":false}"),
                "每移动9次，从当前层其余未选中的怪物牌组中随机加入1张普通等级怪物卡到战斗卡组"));
            c.AddEffect(Impl("skill.gear_delivery.move", EffectContainerType.MonsterSkill,
                Triggered("skill.gear_delivery.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":2}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"],\"random\":true,\"count\":1}",
                    "{\"atom\":\"WeightedRandom\",\"choices\":["
                    + "{\"weight\":1,\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"skill.gear_delivery\"}},"
                    + "{\"weight\":1,\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Armor\",\"delta\":2,\"reason\":\"skill.gear_delivery\"}}"
                    + "]}"),
                "每移动2次，使随机一张其他怪物卡攻击+1或者护甲+2"));
            c.AddEffect(Impl("skill.mixed_bones.move", EffectContainerType.MonsterSkill,
                Triggered("skill.mixed_bones.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":5}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"],\"random\":true,\"count\":2}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"skill.mixed_bones\"},"
                    + "{\"atom\":\"ShuffleRandomContent\",\"kind\":\"Monster\",\"minLevel\":2,\"maxLevel\":3,\"excludeElite\":true,\"excludeBoss\":true,\"count\":1,\"top\":false}"
                    + "]}"),
                "每移动5次，移除九宫格上除本卡外随机两张怪物卡，将一张等级2或等级3随机怪物卡洗入战斗卡组"));
            c.AddEffect(Impl("skill.stone_growth.slot1", EffectContainerType.MonsterSkill,
                Triggered("skill.stone_growth.slot1", "MonsterSkill",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":1,\"target\":\"Self\"}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"]}",
                    "{\"atom\":\"GainArmor\",\"amount\":2}"),
                "[场上] 移动到格1时，其他怪物获得2点护甲"));
            c.AddEffect(Impl("skill.stone_growth.slot4", EffectContainerType.MonsterSkill,
                Triggered("skill.stone_growth.slot4", "MonsterSkill",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":4,\"target\":\"Self\"}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"]}",
                    "{\"atom\":\"GainArmor\",\"amount\":2}"),
                "[场上] 移动到格4时，其他怪物获得2点护甲"));
            c.AddEffect(Impl("skill.stone_growth.slot7", EffectContainerType.MonsterSkill,
                Triggered("skill.stone_growth.slot7", "MonsterSkill",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":7,\"target\":\"Self\"}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"exclude\":[\"Self\"]}",
                    "{\"atom\":\"GainArmor\",\"amount\":2}"),
                "[场上] 移动到格7时，其他怪物获得2点护甲"));

            c.AddEffect(Impl("skill.rascality.armor_lost", EffectContainerType.MonsterSkill,
                Triggered("skill.rascality.armor_lost", "MonsterSkill",
                    "{\"atom\":\"OnCumulative\",\"metric\":\"armorLost\",\"threshold\":3,\"targetIs\":\"Self\"}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"adjacentTo\":\"Self\",\"exclude\":[\"Self\"]}",
                    "{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":1,\"layer\":\"Conditional\",\"scope\":\"Permanent\",\"source\":\"skill.rascality\",\"activeWhileAdjacentTo\":\"Self\"}"),
                "[场上] 每损失3点护甲，相邻怪物攻击+1；离开相邻格后加成失效"));
            c.AddEffect(Impl("skill.fire_power.aura", EffectContainerType.MonsterSkill,
                Modifier("skill.fire_power.aura", "MonsterSkill", "{\"atom\":\"Self\"}", null,
                    "{\"stat\":\"Attack\",\"op\":\"Add\",\"value\":{\"op\":\"Multiply\",\"values\":[{\"constant\":2},{\"source\":\"CardCount\",\"defId\":\"help.flame\",\"zones\":[\"Board\",\"ItemSlots\"]}]},\"layer\":\"Conditional\",\"scope\":\"Permanent\"}"),
                "[场上/道具牌格] 每有一张烈焰，本卡攻击+2"));
            c.AddEffect(Impl("skill.rolling_crush.slot3", EffectContainerType.MonsterSkill,
                Triggered("skill.rolling_crush.slot3", "MonsterSkill",
                    "{\"atom\":\"OnMoveToSlot\",\"slot\":3,\"target\":\"Self\"}",
                    "{\"atom\":\"SlotCard\",\"slot\":6,\"kind\":\"HelpCard\"}",
                    "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"skill.rolling_crush\"}"),
                "[场上] 移动到格3时，若格6为帮助卡，则移除该帮助卡"));
            c.AddEffect(Impl("skill.strong_combo.move", EffectContainerType.MonsterSkill,
                Triggered("skill.strong_combo.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":1}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"skill.strong_combo.self\"},"
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"skill.strong_combo.material\",\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"adjacentTo\":\"Self\",\"exclude\":[\"Self\"],\"count\":2}},"
                    + "{\"atom\":\"ShuffleInto\",\"defId\":\"monster.giant_skeleton\",\"kind\":\"Monster\",\"count\":1,\"top\":true}"
                    + "]}",
                    "[{\"atom\":\"TargetCount\",\"min\":2,\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"adjacentTo\":\"Self\",\"exclude\":[\"Self\"]}}]"),
                "[场上] 每移动1次，若正交相邻有两张怪物，则移除本卡与两张怪物并洗入巨大骷髅"));
            c.AddEffect(Impl("skill.stocking.move", EffectContainerType.MonsterSkill,
                Triggered("skill.stocking.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":3,\"requireAdjacentTo\":\"Player\"}",
                    "{\"atom\":\"Self\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"RemoveCard\",\"destination\":\"Removed\",\"reason\":\"skill.stocking\",\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"HelpCard\",\"zone\":\"DrawPile\",\"random\":true,\"count\":1}},"
                    + "{\"atom\":\"GainArmor\",\"amount\":5}"
                    + "]}",
                    "[{\"atom\":\"TargetCount\",\"min\":1,\"target\":{\"atom\":\"FilteredCards\",\"kind\":\"HelpCard\",\"zone\":\"DrawPile\"}}]"),
                "[场上] 累计移动到玩家正交相邻3次时，从战斗卡组移除一张帮助卡并获得5点护甲"));
            c.AddEffect(Impl("skill.orc_tactics.move", EffectContainerType.MonsterSkill,
                Triggered("skill.orc_tactics.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":1}",
                    "{\"atom\":\"FilteredCards\",\"kind\":\"Monster\",\"zone\":\"Board\",\"adjacentTo\":\"Self\",\"exclude\":[\"Self\"]}",
                    "{\"atom\":\"AddModifier\",\"stat\":\"Attack\",\"op\":\"Add\",\"value\":1,\"layer\":\"Conditional\",\"scope\":\"Permanent\",\"source\":\"skill.orc_tactics\",\"activeWhileAdjacentTo\":\"Self\"}"),
                "[场上] 每移动1次，相邻怪物攻击+1；离开相邻格后加成失效"));
            c.AddEffect(Impl("skill.find_weakness.move", EffectContainerType.MonsterSkill,
                Triggered("skill.find_weakness.move", "MonsterSkill",
                    "{\"atom\":\"OnSelfMove\",\"every\":1}",
                    "{\"atom\":\"Player\"}",
                    "{\"atom\":\"Sequence\",\"actions\":["
                    + "{\"atom\":\"DealDamage\",\"amount\":99,\"actor\":\"Self\"},"
                    + "{\"atom\":\"DeactivateSelfEffect\"}"
                    + "]}",
                    "[{\"atom\":\"BoardMarkCount\",\"mark\":\"Blessed\",\"min\":3}]"),
                "[场上] 福地数量达到3后，本卡下一次移动对玩家造成99点伤害"));
        }

        private static void AddHelpCards(GameContentCatalog c)
        {
            Help(c, "help.healing_potion", "恢复药水", ContentRarity.White, 30, "恢复").AddEffect("help.healing_potion.use");
            Help(c, "help.ward_magic_card", "庇佑魔法卡", ContentRarity.White, 20, "护甲").AddEffect("help.ward_magic_card.use");
            Help(c, "help.throwing_knife", "飞刀", ContentRarity.White, 20, "直伤").AddEffect("help.throwing_knife.use");
            Help(c, "help.fireball", "火球术", ContentRarity.White, 30, "直伤").AddEffect("help.fireball.use");
            Help(c, "help.rotation_wheel", "旋转轮", ContentRarity.White, 20, "位移").AddEffect("help.rotation_wheel.use");
            Help(c, "help.brutality_card", "暴力卡", ContentRarity.White, 30, "攻击").AddEffect("help.brutality_card.use");
            Help(c, "help.rolling_stone", "滚石", ContentRarity.White, 50, "直伤").AddEffect("help.rolling_stone.board_slot3").AddEffect("help.rolling_stone.use");
            Help(c, "help.bomb", "爆弹", ContentRarity.White, 50, "直伤").AddEffect("help.bomb.use");
            Help(c, "help.swap_card", "交换卡", ContentRarity.White, 50, "位移").AddEffect("help.swap_card.use");
            Help(c, "help.armor_breaking_hammer", "破击锤", ContentRarity.White, 50, "直伤").AddEffect("help.armor_breaking_hammer.use");
            Help(c, "help.sturdy_shield", "耐用盾牌", ContentRarity.White, 50, "护甲").AddEffect("help.sturdy_shield.use");
            Help(c, "help.bear_trap", "捕熊陷阱", ContentRarity.White, 50, "直伤").AddEffect("help.bear_trap.use");
            Help(c, "help.teleport_card", "传送卡", ContentRarity.White, 30, "位移").AddEffect("help.teleport_card.use");
            Help(c, "help.blood_conversion", "血液转换", ContentRarity.White, 50, "特殊").AddEffect("help.blood_conversion.use");
            Help(c, "help.gold_card", "金币卡", ContentRarity.Blue, 30, "经济").AddEffect("help.gold_card.use");
            Help(c, "help.food_card", "食品卡", ContentRarity.Blue, 50, "恢复").AddEffect("help.food_card.use");
            Help(c, "help.common_chest_card", "普通宝箱卡", ContentRarity.Blue, 100, "经济").AddEffect("help.common_chest_card.use");
            Help(c, "help.healing_spring", "治疗泉", ContentRarity.Blue, 80, "恢复").AddEffect("help.healing_spring.board_adjacent").AddEffect("help.healing_spring.item_battle").AddEffect("help.healing_spring.use");
            Help(c, "help.impact_tutorial", "撞击教程", ContentRarity.Blue, 80, "血量").AddEffect("help.impact_tutorial.use");
            Help(c, "help.shield_bash_tutorial", "盾击教程", ContentRarity.Blue, 80, "护甲").AddEffect("help.shield_bash_tutorial.use");
            Help(c, "help.kidnapping", "绑票", ContentRarity.Blue, 100, "护甲").AddEffect("help.kidnapping.use");
            Help(c, "help.blue_chest_card", "蓝色宝箱卡", ContentRarity.Gold, 150, "经济").AddEffect("help.blue_chest_card.use");
            Help(c, "help.watchtower", "瞭望塔", ContentRarity.Gold, 150, "直伤").AddEffect("help.watchtower.board_corner").AddEffect("help.watchtower.item_battle").AddEffect("help.watchtower.use");
            Help(c, "help.doubling_tower", "倍增塔", ContentRarity.Gold, 150, "特殊").AddEffect("help.doubling_tower.board_monster").AddEffect("help.doubling_tower.item_player");
            Help(c, "help.stat_boost_card", "属性提升卡", ContentRarity.Gold, 100, "特殊").AddEffect("help.stat_boost_card.use");
            Help(c, "help.golden_chest_card", "金色宝箱卡", ContentRarity.Red, 400, "特殊").AddEffect("help.golden_chest_card.use");
            Help(c, "help.flame", "烈焰", ContentRarity.Red, 400, "特殊").AddEffect("help.flame.use");
        }

        private static void AddRelics(GameContentCatalog c)
        {
            Relic(c, "relic.junk_recycler", "废物利用机", ContentRarity.White, "使用帮助卡时恢复2点血量").AddEffect("relic.junk_recycler.use");
            Relic(c, "relic.wood_shield", "木盾", ContentRarity.White, "基础护甲+1，木套装额外+2").AddEffect("relic.wood_shield.base").AddEffect("relic.wood_shield.set");
            Relic(c, "relic.wood_sword", "木剑", ContentRarity.White, "攻击+1，木套装额外+2").AddEffect("relic.wood_sword.base").AddEffect("relic.wood_sword.set");
            Relic(c, "relic.wood_armor", "木甲", ContentRarity.White, "血量上限+2，木套装额外+8").AddEffect("relic.wood_armor.base").AddEffect("relic.wood_armor.set");
            Relic(c, "relic.lucky_coin", "幸运硬币", ContentRarity.White, "击杀精英/层主时加入金币卡").AddEffect("relic.lucky_coin.elite_kill").AddEffect("relic.lucky_coin.boss_kill");
            Relic(c, "relic.throwing_knife_bag", "飞刀袋", ContentRarity.White, "每关卡开始加入两张飞刀").AddEffect("relic.throwing_knife_bag.node_start");
            Relic(c, "relic.potion_bag", "药水袋", ContentRarity.White, "每关卡开始加入两张恢复药水").AddEffect("relic.potion_bag.node_start");
            Relic(c, "relic.junk_launcher", "废物发射器", ContentRarity.White, "使用帮助卡时随机伤害").AddEffect("relic.junk_launcher.use");
            Relic(c, "relic.junk_coating", "废物涂层", ContentRarity.White, "使用帮助卡时获得护甲").AddEffect("relic.junk_coating.use");
            Relic(c, "relic.sling", "弹弓", ContentRarity.White, "击杀怪物时随机伤害").AddEffect("relic.sling.kill");
            Relic(c, "relic.shield_knife", "打盾刀", ContentRarity.White, "击杀怪物时获得护甲").AddEffect("relic.shield_knife.kill");
            Relic(c, "relic.gold_knife", "打金刀", ContentRarity.White, "击杀怪物时获得2金币").AddEffect("relic.gold_knife.kill");
            Relic(c, "relic.heavy_armor", "重盔甲", ContentRarity.White, "基础护甲+1，按基础护甲补当前护甲").AddEffect("relic.heavy_armor.base").AddEffect("relic.heavy_armor.node_start");
            Relic(c, "relic.gold_armor", "金币盔甲", ContentRarity.White, "金币抵消护甲伤害").AddEffect("relic.gold_armor.rule");
            Relic(c, "relic.vitality_amulet", "活力护符", ContentRarity.Blue, "血量上限+6，关卡结束恢复6").AddEffect("relic.vitality_amulet.max_hp").AddEffect("relic.vitality_amulet.node_end");
            Relic(c, "relic.dragon_scale_armor", "龙鳞甲", ContentRarity.Gold, "所有怪物攻击-1").AddEffect("relic.dragon_scale_armor.rule");
            Relic(c, "relic.phoenix_feather", "凤凰羽毛", ContentRarity.Gold, "致命伤害免死").AddEffect("relic.phoenix_feather.fatal");
            Relic(c, "relic.craving", "渴望", ContentRarity.Gold, "所有恢复血量效果翻倍").AddEffect("relic.craving.rule");
            Relic(c, "relic.junk_slot_machine", "废物老虎机", ContentRarity.Gold, "使用帮助卡时随机触发九选一").AddEffect("relic.junk_slot_machine.use");
        }

        private static void AddPlayerSkills(GameContentCatalog c)
        {
            Skill(c, "skill.thorn_skin", "刺皮", EffectContainerType.PlayerSkill, "战斗时对怪物造成等同其攻击的伤害")
                .AddEffect("skill.thorn_skin.battle");
            Skill(c, "skill.hard_skin", "硬皮", EffectContainerType.PlayerSkill, "血量上限+10，关卡结束恢复10")
                .AddEffect("skill.hard_skin.max_hp").AddEffect("skill.hard_skin.node_end");
            Skill(c, "skill.battle_hardened", "历战", EffectContainerType.PlayerSkill, "战斗时攻击+2，换敌复原")
                .AddEffect("skill.battle_hardened.battle");
            Skill(c, "skill.arsenal", "军械库", EffectContainerType.PlayerSkill, "关卡结束加入飞刀/爆弹/破击锤之一")
                .AddEffect("skill.arsenal.node_end");
            Skill(c, "skill.even_hatred", "偶数仇恨", EffectContainerType.PlayerSkill, "对偶数等级怪物造成双倍伤害")
                .AddEffect("skill.even_hatred.rule");
            Skill(c, "skill.tower_child", "塔之子", EffectContainerType.PlayerSkill, "关卡开始放入倍增塔")
                .AddEffect("skill.tower_child.node_start");
            Skill(c, "skill.easy_road", "轻车熟路", EffectContainerType.PlayerSkill, "关卡结束白色帮助卡三选一")
                .AddEffect("skill.easy_road.node_end");
        }

        private static void AddMonsterSkills(GameContentCatalog c)
        {
            Skill(c, "skill.beggar_bond", "丐帮同心", EffectContainerType.MonsterSkill, "每移动5次洗入乞丐").AddEffect("skill.beggar_bond.move");
            Skill(c, "skill.stray_cub", "流浪幼崽", EffectContainerType.MonsterSkill, "格6攻击+2并获得先攻").AddEffect("skill.stray_cub.slot6").AddEffect("skill.stray_cub.first_strike");
            Skill(c, "skill.thief_claims", "东西归我了！", EffectContainerType.MonsterSkill, "每移动3次移除相邻帮助卡").AddEffect("skill.thief_claims.move");
            Skill(c, "skill.monster_battle_hardened", "历战怪物", EffectContainerType.MonsterSkill, "战斗时本卡攻击+2").AddEffect("skill.monster_battle_hardened.battle");
            Skill(c, "skill.learning_growth", "学习成长", EffectContainerType.MonsterSkill, "其他怪物获得攻击时本卡攻击+1").AddEffect("skill.learning_growth.gain");
            Skill(c, "skill.survival_wisdom", "生存智慧", EffectContainerType.MonsterSkill, "战斗时恢复1且攻击+1").AddEffect("skill.survival_wisdom.battle");
            Skill(c, "skill.devotion", "献身", EffectContainerType.MonsterSkill, "被移除时加入烈焰").AddEffect("skill.devotion.remove");
            Skill(c, "skill.love_fire", "恋火", EffectContainerType.MonsterSkill, "有烈焰时攻击+4").AddEffect("skill.love_fire.aura");
            Skill(c, "skill.breathe_fire", "喷火", EffectContainerType.MonsterSkill, "每移动3次放烈焰").AddEffect("skill.breathe_fire.move");
            Skill(c, "skill.sharp_stone", "尖石", EffectContainerType.MonsterSkill, "护甲归零时伤害玩家").AddEffect("skill.sharp_stone.armor_break");
            Skill(c, "skill.hard", "坚硬", EffectContainerType.MonsterSkill, "左列战斗时按损失护甲伤害玩家")
                .AddEffect("skill.hard.slot1").AddEffect("skill.hard.slot4").AddEffect("skill.hard.slot7");
            Skill(c, "skill.swallow_stone", "吞石", EffectContainerType.MonsterSkill, "每移动2次吸相邻怪物护甲").AddEffect("skill.swallow_stone.move");
            Skill(c, "skill.taunt", "嘲讽", EffectContainerType.MonsterSkill, "相邻时只能与本卡战斗").AddEffect("skill.taunt.rule");
            Skill(c, "skill.recombine_head", "重新组合头", EffectContainerType.MonsterSkill, "相邻骷髅头组合").AddEffect("skill.recombine_head.move");
            Skill(c, "skill.recombine_body", "重新组合身", EffectContainerType.MonsterSkill, "相邻无头骷髅组合").AddEffect("skill.recombine_body.move");
            Skill(c, "skill.unstable", "不稳定", EffectContainerType.MonsterSkill, "每移动3次随机交换怪物").AddEffect("skill.unstable.move");
            Skill(c, "skill.rotate_lover", "爱好旋转", EffectContainerType.MonsterSkill, "每移动3次旋转").AddEffect("skill.rotate_lover.move");
            Skill(c, "skill.random_walk", "乱步", EffectContainerType.MonsterSkill, "每移动3次与随机帮助卡交换").AddEffect("skill.random_walk.move");
            Skill(c, "skill.give_punch", "给你一拳", EffectContainerType.MonsterSkill, "登场在偶数边位时伤害玩家").AddEffect("skill.give_punch.enter2").AddEffect("skill.give_punch.enter4").AddEffect("skill.give_punch.enter6").AddEffect("skill.give_punch.enter8");
            Skill(c, "skill.call_followers", "呼唤信徒", EffectContainerType.MonsterSkill, "每移动3次加入龙信徒").AddEffect("skill.call_followers.move");
            Skill(c, "skill.gift", "礼物", EffectContainerType.MonsterSkill, "每移动3次放旋转轮").AddEffect("skill.gift.move");
            Skill(c, "skill.stroll", "漫步", EffectContainerType.MonsterSkill, "每移动1次攻击+2").AddEffect("skill.stroll.move");
            Skill(c, "skill.falling_rocks", "落石", EffectContainerType.MonsterSkill, "累计损失10护甲洗入石人").AddEffect("skill.falling_rocks.cumulative");

            Skill(c, "skill.hoodlum", "混的人", EffectContainerType.MonsterSkill, "每次移动到格1时对玩家造成2点伤害").AddEffect("skill.hoodlum.slot1");
            Skill(c, "skill.rascality", "痞气", EffectContainerType.MonsterSkill, "每损失3点护甲，相邻怪物攻击+1").AddEffect("skill.rascality.armor_lost");
            Skill(c, "skill.bloodthirst", "嗜血", EffectContainerType.MonsterSkill, "战斗时每造成2点伤害，本卡攻击+1").AddEffect("skill.bloodthirst.damage");
            Skill(c, "skill.smart", "大聪明", EffectContainerType.MonsterSkill, "每次获得攻击时，本卡额外攻击+1且不递归").AddEffect("skill.smart.gain");
            Skill(c, "skill.gear_delivery", "发装备了！", EffectContainerType.MonsterSkill, "每移动2次随机强化其他怪物").AddEffect("skill.gear_delivery.move");
            Skill(c, "skill.fire_power", "火之力", EffectContainerType.MonsterSkill, "每有一张烈焰，本卡攻击+2").AddEffect("skill.fire_power.aura");
            Skill(c, "skill.sacrifice", "献祭", EffectContainerType.MonsterSkill, "每移动2次，移除九宫格上所有的龙信徒").AddEffect("skill.sacrifice.move");
            Skill(c, "skill.rolling_crush", "滚动碾压", EffectContainerType.MonsterSkill, "移动到格3时移除格6帮助卡").AddEffect("skill.rolling_crush.slot3");
            Skill(c, "skill.stone_lover", "石头爱好者", EffectContainerType.MonsterSkill, "玩家卡和怪物卡损失护甲时本卡攻击+1").AddEffect("skill.stone_lover.armor_lost");
            Skill(c, "skill.throw_stone", "丢石头", EffectContainerType.MonsterSkill, "每移动2次消耗本卡2护甲并伤害玩家").AddEffect("skill.throw_stone.move");
            Skill(c, "skill.fall_apart", "散架", EffectContainerType.MonsterSkill, "被移除时洗入骷髅头与无头骷髅").AddEffect("skill.fall_apart.remove");
            Skill(c, "skill.strong_combo", "强力组合", EffectContainerType.MonsterSkill, "移动时与相邻两张怪物合成为巨大骷髅").AddEffect("skill.strong_combo.move");
            Skill(c, "skill.delivery", "快递", EffectContainerType.MonsterSkill, "每移动2次，将相邻帮助卡与战斗卡组怪物交换").AddEffect("skill.delivery.move");
            Skill(c, "skill.turn_world", "转动", EffectContainerType.MonsterSkill, "登场旋转一次").AddEffect("skill.turn_world.enter");
            Skill(c, "skill.hot_observation", "灼热观察", EffectContainerType.MonsterSkill, "怪物卡因怪物技能位移时伤害玩家").AddEffect("skill.hot_observation.observe");
            Skill(c, "skill.air_strike", "空中打击", EffectContainerType.MonsterSkill, "移动到角格时对玩家造成2点伤害")
                .AddEffect("skill.air_strike.slot1").AddEffect("skill.air_strike.slot3").AddEffect("skill.air_strike.slot7").AddEffect("skill.air_strike.slot9");
            Skill(c, "skill.relentless_chase", "不休追击", EffectContainerType.MonsterSkill, "移动到玩家相邻格时强制战斗").AddEffect("skill.relentless_chase.move");
            Skill(c, "skill.stocking", "进货", EffectContainerType.MonsterSkill, "累计移动到玩家相邻3次时移除帮助卡并获得护甲").AddEffect("skill.stocking.move");
            Skill(c, "skill.orc_tactics", "兽人战术", EffectContainerType.MonsterSkill, "每移动1次，相邻怪物攻击+1").AddEffect("skill.orc_tactics.move");
            Skill(c, "skill.fight_me", "和我打！", EffectContainerType.MonsterSkill, "相邻玩家与其他怪物战斗时改为与本卡战斗").AddEffect("skill.fight_me.battle");
            Skill(c, "skill.intense_burning", "剧烈燃烧", EffectContainerType.MonsterSkill, "每有一张烈焰加入战斗卡组，额外加入一张且不递归").AddEffect("skill.intense_burning.flame_deal");
            Skill(c, "skill.stone_growth", "石增长", EffectContainerType.MonsterSkill, "移动到左列时其他怪物获得2点护甲")
                .AddEffect("skill.stone_growth.slot1").AddEffect("skill.stone_growth.slot4").AddEffect("skill.stone_growth.slot7");
            Skill(c, "skill.stone_shelter", "石庇护", EffectContainerType.MonsterSkill, "其他怪物卡受到的伤害减少1点").AddEffect("skill.stone_shelter.rule");
            Skill(c, "skill.fracture_fall_apart", "折损散架", EffectContainerType.MonsterSkill, "被移除时洗入多骨虫和等级2随机怪物").AddEffect("skill.fracture_fall_apart.remove");
            Skill(c, "skill.range_expand", "范围扩大", EffectContainerType.MonsterSkill, "所有怪物卡视为与本卡正交相邻").AddEffect("skill.range_expand.rule");
            Skill(c, "skill.flame_breath", "烈焰吐息", EffectContainerType.MonsterSkill, "每移动4次移除其他普通怪物并按数量加入烈焰").AddEffect("skill.flame_breath.move");
            Skill(c, "skill.flame_boiling", "烈焰沸腾", EffectContainerType.MonsterSkill, "烈焰帮助卡造成的伤害+1").AddEffect("skill.flame_boiling.rule");
            Skill(c, "skill.space_mastery", "空间掌握", EffectContainerType.MonsterSkill, "玩家与本卡战斗后旋转一次").AddEffect("skill.space_mastery.battle");
            Skill(c, "skill.otherworld_help", "异界帮助", EffectContainerType.MonsterSkill, "每移动9次从其他怪物牌组加入普通怪物").AddEffect("skill.otherworld_help.move");
            Skill(c, "skill.absorb_stone", "吸石", EffectContainerType.MonsterSkill, "每次移动吸取相邻怪物全部护甲").AddEffect("skill.absorb_stone.move");
            Skill(c, "skill.guide", "引路", EffectContainerType.MonsterSkill, "每移动3次创建福地，怪物进入福地获得护甲和攻击")
                .AddEffect("skill.guide.create_blessed")
                .AddEffect("skill.guide.blessed_enter");
            Skill(c, "skill.find_weakness", "发现弱点", EffectContainerType.MonsterSkill, "福地达到3后下一次移动造成99伤害").AddEffect("skill.find_weakness.move");
            Skill(c, "skill.violence_maniac", "暴力狂", EffectContainerType.MonsterSkill, "每移动2次，移除相邻格子上的怪物卡和帮助卡").AddEffect("skill.violence_maniac.move");
            Skill(c, "skill.violence_nutrition", "暴力即养分", EffectContainerType.MonsterSkill, "依靠暴力狂移除怪物得攻击，移除帮助卡得护甲").AddEffect("skill.violence_nutrition.monster_remove").AddEffect("skill.violence_nutrition.help_remove");
            Skill(c, "skill.absorb_bone", "吸骨", EffectContainerType.MonsterSkill, "相邻怪物被移除时获得其移除前攻击和护甲").AddEffect("skill.absorb_bone.remove");
            Skill(c, "skill.mixed_bones", "混合骨头", EffectContainerType.MonsterSkill, "每移动5次移除随机两张其他怪物并洗入等级2/3随机怪物").AddEffect("skill.mixed_bones.move");
            Skill(c, "skill.first_strike", "先攻", EffectContainerType.MonsterSkill, "持有先攻技能").AddEffect("skill.first_strike.rule");
            Skill(c, "skill.blessing", "庇佑", EffectContainerType.MonsterSkill, "下一次受到伤害时，该次伤害变为0").AddEffect("skill.blessing.rule");
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
                .AddPool(new RewardPoolDefinition("help.white.choice", 3)
                    .Add("help.healing_potion", CardKind.HelpCard, 20)
                    .Add("help.ward_magic_card", CardKind.HelpCard, 20)
                    .Add("help.throwing_knife", CardKind.HelpCard, 20)
                    .Add("help.fireball", CardKind.HelpCard, 20)
                    .Add("help.rotation_wheel", CardKind.HelpCard, 20)
                    .Add("help.bomb", CardKind.HelpCard, 8)
                    .Add("help.sturdy_shield", CardKind.HelpCard, 20))
                .AddPool(new RewardPoolDefinition("relic.common_chest", 3)
                    .Add("relic.wood_shield", CardKind.Relic, 65)
                    .Add("relic.vitality_amulet", CardKind.Relic, 30)
                    .Add("relic.dragon_scale_armor", CardKind.Relic, 5))
                .AddPool(new RewardPoolDefinition("relic.blue_chest", 3)
                    .Add("relic.wood_shield", CardKind.Relic, 40)
                    .Add("relic.vitality_amulet", CardKind.Relic, 50)
                    .Add("relic.dragon_scale_armor", CardKind.Relic, 10))
                .AddPool(new RewardPoolDefinition("relic.golden_chest", 3)
                    .Add("relic.vitality_amulet", CardKind.Relic, 50)
                    .Add("relic.dragon_scale_armor", CardKind.Relic, 50)
                    .Add("relic.phoenix_feather", CardKind.Relic, 50))
                .AddPool(new RewardPoolDefinition("relic.blood_conversion", 1)
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
            return Rule(id, container, rule, null);
        }

        private static string Rule(string id, string container, string rule, string conditions)
        {
            return "{"
                + "\"id\":\"" + id + "\","
                + "\"typeTag\":\"" + TypeTag(container) + "\","
                + "\"containerType\":\"" + container + "\","
                + "\"kind\":\"RuleModifier\","
                + (string.IsNullOrEmpty(conditions) ? string.Empty : "\"conditions\":" + conditions + ",")
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
