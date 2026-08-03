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
            IReadOnlyList<string> itemSourceDeckIds)
        {
            DefId = defId ?? string.Empty;
            MaxHp = maxHp;
            Attack = attack;
            Armor = armor;
            Recovery = recovery;
            InitialRelicDefId = initialRelicDefId ?? string.Empty;
            ItemSourceDeckIds = itemSourceDeckIds ?? new string[0];
        }

        public string DefId { get; private set; }
        public int MaxHp { get; private set; }
        public int Attack { get; private set; }
        public int Armor { get; private set; }
        public int Recovery { get; private set; }
        public string InitialRelicDefId { get; private set; }

        /// <summary>道具卡来源池所属卡组 id（通用 + 角色）。</summary>
        public IReadOnlyList<string> ItemSourceDeckIds { get; private set; }
    }

    /// <summary>
    /// 本轮固定职业（战士）：属性、初始遗物、道具卡来源卡组。
    /// </summary>
    public static class ProfessionCatalog
    {
        public const string Jester = "profession.jester";
        public const string GenericItemDeckId = "deck.help";
        public const string WarriorItemDeckId = "deck.player";

        private static readonly ProfessionDefinition sWarrior = new ProfessionDefinition(
            Jester,
            maxHp: 10,
            attack: 3,
            armor: 1,
            recovery: 1,
            initialRelicDefId: "relic.easy_road",
            itemSourceDeckIds: new[] { GenericItemDeckId, WarriorItemDeckId });

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

            definition = null;
            return false;
        }

        /// <summary>
        /// 开局写入生成规则：容量 + 从来源卡组收集道具卡 defId。
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
                    || !string.Equals(card.DeckId, deckId, System.StringComparison.OrdinalIgnoreCase))
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
