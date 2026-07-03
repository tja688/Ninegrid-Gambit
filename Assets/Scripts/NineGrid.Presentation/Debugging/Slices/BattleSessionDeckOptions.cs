using NineGrid.Core;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Presentation.Debugging.Slices
{
    /// <summary>
    /// 战斗闭环测试用牌组：保留 catalog 玩家初始牌，敌人缩为少量真实 defId。
    /// </summary>
    public static class BattleSessionDeckOptions
    {
        public const int DefaultMinimalMonsterCount = 3;

        private static readonly string[] sMinimalMonsterDefIds =
        {
            "monster.wandering_child",
            "monster.pickpocket",
            "monster.vagrant",
        };

        public static NodeDeckOptions BuildMinimalClearDeck(
            IArchitecture architecture,
            NodeDeckOptions source = null,
            int monsterCount = DefaultMinimalMonsterCount)
        {
            var content = architecture.GetSystem<IContentSystem>();
            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = source?.PlayerOpeningCount ?? 3,
                EnemyOpeningCount = monsterCount,
                RequireElite = false,
            };

            if (source != null)
            {
                for (var i = 0; i < source.PlayerCards.Count; i++)
                {
                    options.AddPlayerCard(source.PlayerCards[i]);
                }
            }
            else
            {
                AddDefaultPlayerCards(content, options);
            }

            var added = 0;
            for (var i = 0; i < sMinimalMonsterDefIds.Length && added < monsterCount; i++)
            {
                CardDraft draft = content.CreateDraft(sMinimalMonsterDefIds[i]);
                if (draft.Kind != CardKind.Monster)
                {
                    continue;
                }

                draft.MaxHp = 1;
                draft.Hp = 1;
                draft.Attack = 0;
                options.AddEnemyCard(draft);
                added++;
            }

            while (added < monsterCount)
            {
                options.AddEnemyCard(new CardDraft("monster.wandering_child", CardKind.Monster)
                {
                    MaxHp = 1,
                    Hp = 1,
                    Attack = 0,
                });
                added++;
            }

            return options;
        }

        public static bool UsesCatalogMonsterDefIds(NodeDeckOptions options)
        {
            if (options == null || options.EnemyCards.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < options.EnemyCards.Count; i++)
            {
                string defId = options.EnemyCards[i].DefId;
                if (string.IsNullOrEmpty(defId) || defId.StartsWith("monster.slice", System.StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AddDefaultPlayerCards(IContentSystem content, NodeDeckOptions options)
        {
            AddPlayerCardIfExists(content, options, "help.throwing_knife");
            AddPlayerCardIfExists(content, options, "help.healing_potion");
            AddPlayerCardIfExists(content, options, "help.sturdy_shield");
        }

        private static void AddPlayerCardIfExists(IContentSystem content, NodeDeckOptions options, string defId)
        {
            CardDraft draft = content.CreateDraft(defId);
            if (draft.Kind != CardKind.Unknown)
            {
                options.AddPlayerCard(draft);
            }
        }
    }
}
