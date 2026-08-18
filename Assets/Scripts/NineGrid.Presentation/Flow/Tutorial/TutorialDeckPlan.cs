using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教程第一关正式内容保序开局载荷计划（ADR-0042）：
    /// 保证以下三项核心开局不变量：
    /// 1. 开局发牌后 Avatar（Slot 5）正交邻格必有真怪（Slot 6 为 <see cref="FirstTargetMonsterDefId"/>，HP 4，战士一击必杀）。
    /// 2. 首次击杀 Slot 6 怪物并盘面稳定化（补牌 + 顺时针旋转）后，Avatar 正交邻格必有可拾道具（Slot 3 <see cref="FirstCollectItemDefId"/> 旋转至 Slot 6）。
    /// 3. 离开机关（<see cref="LeaveTrapDefId"/>）固定在抽牌堆后半段（第 13 张 / 抽牌堆第 5 张），随击杀推进自然出场。
    ///
    /// 全载荷均为第一关会见到的正式内容：莱姆卡组 sequence 1 小小莱姆、White 道具、后置离开机关。
    /// 不含常规机关、烈焰等特殊机关、亦不含莱姆卡组后段怪物。
    /// </summary>
    public static class TutorialDeckPlan
    {
        public const int PhaseCount = 5;

        public const string LimeFloorDeckId = "deck.dragon";
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
            "monster.melee_3",          // [0] -> Slot 1（小小莱姆）
            "monster.melee_3",          // [1] -> Slot 2（小小莱姆）
            "help.healing_potion",      // [2] -> Slot 3（药水，首杀 Slot 6 旋转后落入 Slot 6 正交可拾）
            "monster.melee_3",          // [3] -> Slot 6（真怪，Avatar 正右方正交邻格，第 1 轮教学目标）
            "help.throwing_knife",      // [4] -> Slot 9（White 道具）
            "monster.melee_3",          // [5] -> Slot 8（小小莱姆）
            "help.sturdy_shield",       // [6] -> Slot 7（White 道具）
            "monster.melee_3",          // [7] -> Slot 4（小小莱姆）

            // --- 抽牌堆 8 张（补牌序） ---
            "monster.melee_3",          // [8]  -> DrawPile[0]（首杀 Slot 6 后补入 Slot 6，旋转至 Slot 9）
            "help.throwing_knife",      // [9]  -> DrawPile[1]（White 道具）
            "monster.melee_3",          // [10] -> DrawPile[2]（小小莱姆）
            "help.sturdy_shield",       // [11] -> DrawPile[3]（White 道具）
            "trap.leave",               // [12] -> DrawPile[4]（离开机关，抽牌堆后半段：第 5 / 8 张）
            "monster.melee_3",          // [13] -> DrawPile[5]（小小莱姆）
            "help.healing_potion",      // [14] -> DrawPile[6]（White 道具）
            "monster.melee_3"           // [15] -> DrawPile[7]（小小莱姆）
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
                var draft = content?.CreateDraft(defId) ?? new CardDraft(defId, CardKind.Unknown);
                options.AddEnemyCard(draft);
            }

            return options;
        }

        /// <summary>
        /// 把本层主题卡组钉为莱姆（<see cref="LimeFloorDeckId"/>），供 1-2 起走正式编组并恢复常规机关。
        /// 同层重复绑定由 <see cref="RunModel.TryBindFloorMonsterDeck"/> 保留首次结果。
        /// </summary>
        public static bool TryPinLimeFloorDeck(RunModel run)
        {
            return run != null && run.TryBindFloorMonsterDeck(LimeFloorDeckId);
        }
    }
}
