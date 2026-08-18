using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教程第一关正式内容保序开局载荷计划（ADR-0042 / Issue #228）：
    /// 保证以下三项核心开局不变量：
    /// 1. 开局发牌后 Avatar（Slot 5）正交邻格必有真怪（Slot 2 为 <see cref="FirstTargetMonsterDefId"/>，HP 4，战士一击必杀）。
    /// 2. 首次击杀 Slot 2 怪物并盘面稳定化（补牌 + 顺时针旋转）后，Avatar 正交邻格必有可拾道具（Slot 1 <see cref="FirstCollectItemDefId"/> 旋转至 Slot 2）。
    /// 3. 离开机关（<see cref="LeaveTrapDefId"/>）固定在抽牌堆后半段（第 13 张 / 抽牌堆第 5 张），随击杀推进自然出场。
    /// 
    /// 全载荷均由 Catalog 正式内容（<c>deck.dragon</c>、<c>deck.help</c>、<c>deck.trap</c>）组成，不引入教学假卡。
    /// </summary>
    public static class TutorialDeckPlan
    {
        public const int PhaseCount = 5;

        public const string FirstTargetMonsterDefId = "monster.melee_3";
        public const string FirstCollectItemDefId = "help.healing_potion";
        public const string LeaveTrapDefId = RegularTrapPool.LeaveTrapDefId;

        /// <summary>
        /// 教程第一关 16 张正式卡牌保序序列：
        /// 前 8 张开局按 FillOrder（1, 2, 3, 6, 9, 8, 7, 4）发满场地；
        /// 后 8 张按序存入抽牌堆供击杀后补牌。
        /// </summary>
        public static readonly IReadOnlyList<string> OpeningCardSequence = new[]
        {
            // --- 开局 8 张盘面卡（按 FillOrder 分配） ---
            "help.healing_potion",      // [0] -> Slot 1（道具，首杀 Slot 2 旋转后落入 Slot 2 正交可拾）
            "monster.melee_3",          // [1] -> Slot 2（真怪，Avatar 上方正交邻格，首杀目标）
            "monster.dragon_follower",   // [2] -> Slot 3（真怪）
            "trap.spike",               // [3] -> Slot 6（陷阱，右方正交邻格）
            "help.throwing_knife",      // [4] -> Slot 9（道具）
            "monster.beggar",           // [5] -> Slot 8（真怪，下方正交邻格）
            "help.gold_card",           // [6] -> Slot 7（道具）
            "trap.bear_trap",           // [7] -> Slot 4（陷阱，左方正交邻格）

            // --- 抽牌堆 8 张（补牌序） ---
            "monster.melee_3",          // [8]  -> DrawPile[0]（首杀 Slot 2 后补入 Slot 2，旋转至 Slot 3）
            "help.sturdy_shield",       // [9]  -> DrawPile[1]（道具）
            "trap.flame",               // [10] -> DrawPile[2]（陷阱）
            "monster.dragon_follower",  // [11] -> DrawPile[3]（真怪）
            "trap.leave",               // [12] -> DrawPile[4]（离开机关，抽牌堆后半段：第 5 / 8 张）
            "monster.beggar",           // [13] -> DrawPile[5]（真怪）
            "help.throwing_knife",      // [14] -> DrawPile[6]（道具）
            "monster.melee_3"           // [15] -> DrawPile[7]（真怪）
        };

        /// <summary>
        /// 构造教程第一关保序开局选项。
        /// </summary>
        public static NodeDeckOptions BuildOpeningOptions(IContentSystem content)
        {
            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0,
                RequireElite = false,
                PreserveDealOrder = true,
            };

            for (var i = 0; i < OpeningCardSequence.Count; i++)
            {
                var defId = OpeningCardSequence[i];
                var draft = content?.CreateDraft(defId) ?? new CardDraft(defId, CardKind.None);
                options.AddEnemyCard(draft);
            }

            return options;
        }
    }
}
