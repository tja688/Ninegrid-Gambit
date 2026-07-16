using System.Collections.Generic;
using System.Text;
using NineGrid.Core;
using NineGrid.Core.Content;

namespace NineGrid.Flow
{
    /// <summary>
    /// DevTest 快速测试：策划文档顺序的怪物牌组编号与菜单文案。
    /// <para>
    /// 首发版路由排除：<see cref="LevelRouteDeckPolicy.SkeletonLegionDeckId"/> 已从随机路由移除；
    /// 本目录中仅选关编号 4（<c>sDesignOrderDeckIds[3]</c>）可在主菜单 \ 路径下首关固定进入骷髅牌组。
    /// 选 0 正式顺序或其它编号时，StrongElite 节点只会随机到兽人军团等非排除牌组。
    /// </para>
    /// </summary>
    public static class QuickTestDeckCatalog
    {
        public const int FormalOrderPickerCode = 0;
        public const int MaxPickerCode = 6;

        private static readonly string[] sDesignOrderDeckIds =
        {
            "deck.wandering_legion",
            "deck.stone_legion",
            "deck.orc_legion",
            "deck.skeleton_legion",
            "deck.dragon",
            "deck.void",
        };

        public static bool TryResolvePickerCode(
            int code,
            GameContentCatalog catalog,
            out string deckId,
            out string displayName)
        {
            deckId = null;
            displayName = null;

            if (code == FormalOrderPickerCode)
            {
                displayName = "正式顺序";
                return true;
            }

            if (code < 1 || code > MaxPickerCode)
            {
                return false;
            }

            deckId = sDesignOrderDeckIds[code - 1];
            displayName = ResolveDisplayName(catalog, deckId);
            return true;
        }

        public static int GetDefaultNodeIndexForDeckKind(MonsterDeckKind kind)
        {
            switch (kind)
            {
                case MonsterDeckKind.WeakElite:
                    return 1;
                case MonsterDeckKind.StrongElite:
                    return 4;
                case MonsterDeckKind.Boss:
                    return 7;
                default:
                    return 1;
            }
        }

        public static int GetDefaultNodeIndexForDeckId(GameContentCatalog catalog, string deckId)
        {
            if (catalog != null
                && !string.IsNullOrEmpty(deckId)
                && catalog.MonsterDecks.TryGetValue(deckId, out var deck)
                && deck != null)
            {
                return GetDefaultNodeIndexForDeckKind(deck.Kind);
            }

            return 1;
        }

        public static string BuildPickerMenuText(GameContentCatalog catalog)
        {
            var builder = new StringBuilder(256);
            builder.AppendLine("[快速测试]");
            builder.AppendLine("0 正式顺序 (HP99 ATK5 x1)");
            AppendDeckLine(builder, catalog, 1, 2);
            builder.AppendLine();
            AppendDeckLine(builder, catalog, 3, 4);
            builder.AppendLine();
            AppendDeckLine(builder, catalog, 5, 6);
            builder.AppendLine("长按 \\ 选关，释放确认");
            return builder.ToString().TrimEnd();
        }

        private static void AppendDeckLine(
            StringBuilder builder,
            GameContentCatalog catalog,
            int leftCode,
            int rightCode)
        {
            builder.Append(leftCode).Append(' ');
            builder.Append(ShortDisplayName(catalog, leftCode));
            builder.Append("  ");
            builder.Append(rightCode).Append(' ');
            builder.Append(ShortDisplayName(catalog, rightCode));
        }

        private static string ShortDisplayName(GameContentCatalog catalog, int code)
        {
            if (!TryResolvePickerCode(code, catalog, out _, out var displayName))
            {
                return "?";
            }

            if (string.IsNullOrEmpty(displayName))
            {
                return code.ToString();
            }

            return displayName
                .Replace("牌组", string.Empty)
                .Trim();
        }

        private static string ResolveDisplayName(GameContentCatalog catalog, string deckId)
        {
            if (catalog != null
                && catalog.MonsterDecks.TryGetValue(deckId, out var deck)
                && deck != null
                && !string.IsNullOrWhiteSpace(deck.DisplayName))
            {
                return deck.DisplayName;
            }

            for (var i = 0; i < sDesignOrderDeckIds.Length; i++)
            {
                if (sDesignOrderDeckIds[i] == deckId)
                {
                    return FallbackDisplayNames[i];
                }
            }

            return deckId ?? string.Empty;
        }

        private static readonly string[] FallbackDisplayNames =
        {
            "流浪军团牌组",
            "石人军团牌组",
            "兽人军团牌组",
            "骷髅军团牌组",
            "巨龙牌组",
            "虚空牌组",
        };
    }
}
