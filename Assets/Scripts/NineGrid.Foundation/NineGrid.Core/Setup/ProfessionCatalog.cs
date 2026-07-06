using System.Collections.Generic;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;

namespace NineGrid.Core
{
    public sealed class ProfessionCardEntry
    {
        public ProfessionCardEntry(string defId, int count)
        {
            DefId = defId ?? string.Empty;
            Count = count < 1 ? 1 : count;
        }

        public string DefId { get; private set; }
        public int Count { get; private set; }
    }

    public sealed class ProfessionDefinition
    {
        public ProfessionDefinition(
            string defId,
            int maxHp,
            int attack,
            int armor,
            int recovery,
            string initialSkillDefId,
            IReadOnlyList<ProfessionCardEntry> initialCards)
        {
            DefId = defId ?? string.Empty;
            MaxHp = maxHp;
            Attack = attack;
            Armor = armor;
            Recovery = recovery;
            InitialSkillDefId = initialSkillDefId ?? string.Empty;
            InitialCards = initialCards ?? new ProfessionCardEntry[0];
        }

        public string DefId { get; private set; }
        public int MaxHp { get; private set; }
        public int Attack { get; private set; }
        public int Armor { get; private set; }
        public int Recovery { get; private set; }
        public string InitialSkillDefId { get; private set; }
        public IReadOnlyList<ProfessionCardEntry> InitialCards { get; private set; }
    }

    /// <summary>
    /// 首版唯一职业（小丑）配置：属性、初始技能、开局帮助卡池。
    /// </summary>
    public static class ProfessionCatalog
    {
        public const string Jester = "profession.jester";

        private static readonly ProfessionDefinition sJester = new ProfessionDefinition(
            Jester,
            maxHp: 10,
            attack: 3,
            armor: 1,
            recovery: 1,
            initialSkillDefId: "skill.easy_road",
            initialCards: new[]
            {
                new ProfessionCardEntry("help.healing_potion", 3),
                new ProfessionCardEntry("help.common_chest_card", 1),
                new ProfessionCardEntry("help.throwing_knife", 3),
                new ProfessionCardEntry("help.stat_boost_card", 1),
            });

        public static ProfessionDefinition Default
        {
            get { return sJester; }
        }

        public static ProfessionDefinition Get(string professionId)
        {
            ProfessionDefinition definition;
            return TryGet(professionId, out definition) ? definition : sJester;
        }

        public static bool TryGet(string professionId, out ProfessionDefinition definition)
        {
            if (professionId == Jester)
            {
                definition = sJester;
                return true;
            }

            definition = null;
            return false;
        }

        public static void AppendInitialPlayerCards(
            IContentSystem content,
            GameContentCatalog catalog,
            NodeDeckOptions options,
            string professionId)
        {
            if (content == null || catalog == null || options == null)
            {
                return;
            }

            var profession = Get(professionId);
            for (var i = 0; i < profession.InitialCards.Count; i++)
            {
                var entry = profession.InitialCards[i];
                if (string.IsNullOrEmpty(entry.DefId) || !catalog.Cards.ContainsKey(entry.DefId))
                {
                    continue;
                }

                for (var count = 0; count < entry.Count; count++)
                {
                    options.AddPlayerCard(content.CreateDraft(entry.DefId));
                }
            }
        }
    }
}
