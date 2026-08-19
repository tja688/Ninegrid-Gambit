using System;
using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    public sealed class ProfessionDefinition
    {
        public ProfessionDefinition(
            string defId,
            int maxHp,
            int attack,
            int armor,
            int recovery,
            string initialRelicDefId,
            IReadOnlyList<string> itemSourceDeckIds,
            string avatarDefId = "avatar.default")
        {
            DefId = defId ?? string.Empty;
            MaxHp = maxHp;
            Attack = attack;
            Armor = armor;
            Recovery = recovery;
            InitialRelicDefId = initialRelicDefId ?? string.Empty;
            ItemSourceDeckIds = itemSourceDeckIds ?? new string[0];
            AvatarDefId = string.IsNullOrEmpty(avatarDefId) ? "avatar.default" : avatarDefId;
        }

        public string DefId { get; private set; }
        public int MaxHp { get; private set; }
        public int Attack { get; private set; }
        public int Armor { get; private set; }
        public int Recovery { get; private set; }
        public string InitialRelicDefId { get; private set; }
        public string AvatarDefId { get; private set; }

        /// <summary>道具卡来源池所属卡组 id（通用 + 角色）。</summary>
        public IReadOnlyList<string> ItemSourceDeckIds { get; private set; }
    }

    /// <summary>
    /// 职业目录：属性、初始遗物、化身、道具卡来源卡组。
    /// </summary>
    public static class ProfessionCatalog
    {
        public const string Jester = "profession.jester";
        public const string Assassin = "profession.assassin";
        public const string GenericItemDeckId = "deck.help";
        public const string WarriorItemDeckId = "deck.player";
        public const string AssassinItemDeckId = "deck.player_assassin";

        // #116：开局遗物 = 腐朽顺劈斧；战士基础护甲 0（策划案）。
        private static readonly ProfessionDefinition sWarrior = new ProfessionDefinition(
            Jester,
            maxHp: 10,
            attack: 3,
            armor: 0,
            recovery: 1,
            initialRelicDefId: "relic.rotten_cleave_axe",
            itemSourceDeckIds: new[] { GenericItemDeckId, WarriorItemDeckId },
            avatarDefId: "avatar.default");

        // #223：第二职业（刺客/Icey，内容 id 仍为 avatar.layla）：开局遗物 = 空间振荡器；专属白卡「换位」进该角色专属组。
        private static readonly ProfessionDefinition sAssassin = new ProfessionDefinition(
            Assassin,
            maxHp: 10,
            attack: 3,
            armor: 0,
            recovery: 1,
            initialRelicDefId: "relic.space_oscillator",
            itemSourceDeckIds: new[] { GenericItemDeckId, AssassinItemDeckId },
            avatarDefId: "avatar.layla");

        public static ProfessionDefinition Default
        {
            get { return sWarrior; }
        }

        public static ProfessionDefinition Get(string professionId)
        {
            ProfessionDefinition definition;
            return TryGet(professionId, out definition) ? definition : sWarrior;
        }

        public static bool TryGet(string professionId, out ProfessionDefinition definition)
        {
            if (professionId == Jester)
            {
                definition = sWarrior;
                return true;
            }
            if (professionId == Assassin)
            {
                definition = sAssassin;
                return true;
            }

            definition = null;
            return false;
        }

        /// <summary>
        /// 开局写入生成规则：容量 + 从来源卡组收集**常规稀有度**（白/蓝/金 = 高/中/低，ADR-0033 修订）
        /// 道具卡 defId。特殊（红）道具卡不进随机来源池，仅经宝箱房/金币房/属性房注入、商店固定货架、
        /// 击杀掉落等定向渠道投放。装填时按 高60/中30/低10 加权（RewardSystem）。
        /// </summary>
        public static void SeedItemGenerationRules(
            PlayerModel player,
            GameContentCatalog catalog,
            string professionId)
        {
            if (player == null)
            {
                return;
            }

            var profession = Get(professionId);
            player.SetItemDeckCapacity(PlayerModel.DefaultItemDeckCapacity);
            player.ReplaceFixedItemCards(null);

            var pool = new List<string>();
            if (catalog != null && catalog.Cards != null)
            {
                for (var i = 0; i < profession.ItemSourceDeckIds.Count; i++)
                {
                    AppendHelpCardsFromDeck(catalog, profession.ItemSourceDeckIds[i], pool);
                }
            }

            player.ReplaceItemSourcePool(pool);
        }

        /// <summary>当前局职业；未写入时回退战士（历史默认职业）。</summary>
        public static string ResolveProfessionId(PlayerModel player)
        {
            if (player == null || string.IsNullOrEmpty(player.ProfessionId.Value))
            {
                return Jester;
            }

            return player.ProfessionId.Value;
        }

        /// <summary>战士 / 刺客专属道具卡组。通用 <see cref="GenericItemDeckId"/> 不在此列。</summary>
        public static bool IsProfessionExclusiveItemDeck(string deckId)
        {
            return string.Equals(deckId, WarriorItemDeckId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(deckId, AssassinItemDeckId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 专属道具卡组只对拥有该卡组的职业放行；通用 / 非专属卡组对所有职业放行。
        /// </summary>
        public static bool AllowsItemDeckForProfession(string deckId, string professionId)
        {
            if (!IsProfessionExclusiveItemDeck(deckId))
            {
                return true;
            }

            var profession = Get(professionId);
            for (var i = 0; i < profession.ItemSourceDeckIds.Count; i++)
            {
                if (string.Equals(profession.ItemSourceDeckIds[i], deckId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool AllowsHelpCardForProfession(CardContentDefinition card, string professionId)
        {
            if (card == null || card.Kind != CardKind.HelpCard)
            {
                return true;
            }

            return AllowsItemDeckForProfession(card.DeckId, professionId);
        }

        /// <summary>
        /// 具名授予 / 奖池抽取：Catalog 未登记的 defId 放行（测试夹具）；
        /// 已登记的专属道具卡只对拥有该卡组的职业放行。
        /// </summary>
        public static bool AllowsHelpCardGrant(
            GameContentCatalog catalog,
            string defId,
            string professionId)
        {
            if (catalog == null || string.IsNullOrEmpty(defId))
            {
                return true;
            }

            CardContentDefinition card;
            if (!catalog.TryGetCard(defId, out card) || card == null)
            {
                return true;
            }

            return AllowsHelpCardForProfession(card, professionId);
        }

        private static void AppendHelpCardsFromDeck(
            GameContentCatalog catalog,
            string deckId,
            List<string> pool)
        {
            if (string.IsNullOrEmpty(deckId))
            {
                return;
            }

            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (card == null
                    || card.Kind != CardKind.HelpCard
                    || string.IsNullOrEmpty(card.DefId)
                    || FormalContentWiring.IsUnofficialDeck(card.DeckId)
                    || !string.Equals(card.DeckId, deckId, System.StringComparison.OrdinalIgnoreCase)
                    || !HelpCardDecks.IsRegularRarity(card.Rarity))
                {
                    continue;
                }

                if (!pool.Contains(card.DefId))
                {
                    pool.Add(card.DefId);
                }
            }
        }
    }
}
