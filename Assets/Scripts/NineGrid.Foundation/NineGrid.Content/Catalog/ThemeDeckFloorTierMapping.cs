using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Content
{
    /// <summary>
    /// 七套正式主题卡组的难度档契约（策划：普通 2 / 中等 3 / 困难 2），与
    /// <c>monster_decks.json</c> 的 <c>deck_kind</c> 对齐。
    /// </summary>
    public static class ThemeDeckFloorTierMapping
    {
        public static MonsterDeckKind GetExpectedKind(string deckId)
        {
            if (string.IsNullOrEmpty(deckId))
            {
                return MonsterDeckKind.Unknown;
            }

            switch (deckId)
            {
                case "deck.dragon":
                case "deck.orc_legion":
                    return MonsterDeckKind.WeakElite;
                case "deck.insect":
                case "deck.stone_legion":
                case "deck.void":
                    return MonsterDeckKind.StrongElite;
                case "deck.skeleton_legion":
                case "deck.smallanimal":
                    return MonsterDeckKind.Boss;
                default:
                    return MonsterDeckKind.Unknown;
            }
        }

        public static int GetExpectedPoolFloor(string deckId)
        {
            return MonsterDeckFloorPool.DifficultyToFloor(GetExpectedKind(deckId));
        }
    }
}
