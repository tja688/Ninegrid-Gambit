using System.Collections.Generic;
using NineGrid.Core.Utilities;

namespace NineGrid.Core.Content
{
    /// <summary>
    /// 常规机关装填契约（#135）：正式战斗卡组开局随机注入 <see cref="PerBattleCount"/> 张常规机关。
    /// 池过滤按策划「稀有度」标记：Kind=Trap 且 Rarity=White（常规）且非 Reserve；
    /// 离开机关（trap.leave，None）与技能/遗物专用特殊机关（烈焰/治疗泉 Red、复活石 None）不入池。
    /// 抽取为无放回（同一场不重复），顺序由 IRngUtility 种子决定（同种子可复现）；
    /// 池不足 <see cref="PerBattleCount"/> 时按池量注入，池空则注入零张。
    /// 仅经 Core 装填路径（<c>RewardSystem.BuildNodeDeckOptions</c>）消费；QuickTest \1–\9 定向注入另计。
    /// </summary>
    public static class RegularTrapPool
    {
        /// <summary>每场正式战斗开局随机注入的常规机关张数（策划：随机三张）。</summary>
        public const int PerBattleCount = 3;

        /// <summary>离开机关 contentId（唯一清关手段；绝不允许出现在常规机关池）。</summary>
        public const string LeaveTrapDefId = "trap.leave";

        /// <summary>
        /// 常规机关池（过滤后）：Kind=Trap、稀有度 White（常规）、非 Reserve。
        /// 返回集合顺序为 Catalog 枚举序，不构成抽取顺序。
        /// </summary>
        public static IReadOnlyList<CardContentDefinition> CollectRegularTraps(GameContentCatalog catalog)
        {
            var result = new List<CardContentDefinition>();
            if (catalog == null)
            {
                return result;
            }

            foreach (var pair in catalog.Cards)
            {
                var card = pair.Value;
                if (card == null
                    || card.Kind != CardKind.Trap
                    || FormalContentWiring.IsExcludedFromRandomPools(card)
                    || card.Rarity != ContentRarity.White)
                {
                    continue;
                }

                result.Add(card);
            }

            return result;
        }

        /// <summary>
        /// 无放回随机抽取至多 <paramref name="count"/> 张常规机关；
        /// 返回实际抽到的 defId（顺序即抽取顺序，同种子可复现）。
        /// </summary>
        public static IReadOnlyList<string> RollRegularTrapDefIds(
            GameContentCatalog catalog,
            IRngUtility rng,
            int count)
        {
            var result = new List<string>();
            if (catalog == null || rng == null || count <= 0)
            {
                return result;
            }

            var pool = new List<CardContentDefinition>(CollectRegularTraps(catalog));
            if (pool.Count == 0)
            {
                return result;
            }

            var draw = pool.Count < count ? pool.Count : count;
            for (var i = 0; i < draw; i++)
            {
                var index = rng.Range(0, pool.Count);
                result.Add(pool[index].DefId);
                pool.RemoveAt(index);
            }

            return result;
        }
    }
}
