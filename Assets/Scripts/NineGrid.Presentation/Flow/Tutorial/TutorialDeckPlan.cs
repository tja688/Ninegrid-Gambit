using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using UnityEngine;

namespace NineGrid.Flow.Tutorial
{
    /// <summary>
    /// 教学关卡内容计划：四波受控发牌的卡表与「阅读序 → 抽牌堆序」换算。
    /// 教学机关卡统一在 <c>deck.tutorial</c>（一卡一文件 JSON）；怪物 / 道具 / 离开机关复用既有内容。
    /// </summary>
    /// <remarks>
    /// FillEmptySlots 的补格顺序为 1,2,3,6,9,8,7,4（跳过 Avatar 格 5），
    /// 玩家阅读习惯为格 1,2,3 / 4,6 / 7,8,9 逐行；
    /// 因此「阅读序第 i 张」需按 <see cref="sReadingIndexForPile"/> 重排后入抽牌堆。
    /// </remarks>
    public static class TutorialDeckPlan
    {
        public const string TutorialTrapPrefix = "trap.tutorial.";
        public const string MonsterDefId = "monster.melee_3";
        public const string KnifeDefId = "help.throwing_knife";
        public const string PotionDefId = "help.healing_potion";
        public const string LeaveTrapDefId = "trap.leave";
        public const string FillerDefId = "trap.tutorial.filler";
        public const string Wave1SpareDefId = "trap.tutorial.spare";

        /// <summary>pile[i] = reading[sReadingIndexForPile[i]]；由 FillOrder(1,2,3,6,9,8,7,4) 反推。</summary>
        private static readonly int[] sReadingIndexForPile = { 0, 1, 2, 4, 7, 6, 5, 3 };

        /// <summary>第一波（阅读序，格 1,2,3,4,6,7,8,9）：基础互动讲解，全部教学机关卡。</summary>
        private static readonly string[] sWave1Reading =
        {
            "trap.tutorial.welcome",
            "trap.tutorial.deal",
            "trap.tutorial.rotate",
            "trap.tutorial.attack",
            "trap.tutorial.hp",
            "trap.tutorial.no_counter",
            "trap.tutorial.refill",
            "trap.tutorial.advance",
        };

        /// <summary>第二波（阅读序）：怪物机制讲解，格 9 为教学怪物。</summary>
        private static readonly string[] sWave2Reading =
        {
            "trap.tutorial.monster_intro",
            "trap.tutorial.countdown",
            "trap.tutorial.tick",
            "trap.tutorial.fire",
            "trap.tutorial.inspect",
            "trap.tutorial.reposition",
            "trap.tutorial.hunt",
            MonsterDefId,
        };

        /// <summary>第三波（阅读序）：道具讲解，格 7/8 为飞刀与恢复药水、格 9 为练习靶怪物。</summary>
        private static readonly string[] sWave3Reading =
        {
            "trap.tutorial.item_intro",
            "trap.tutorial.pickup",
            "trap.tutorial.use_item",
            "trap.tutorial.aim",
            "trap.tutorial.practice",
            KnifeDefId,
            PotionDefId,
            MonsterDefId,
        };

        /// <summary>第四波（阅读序）：清关与变卖讲解，格 9 为离开机关。</summary>
        private static readonly string[] sWave4Reading =
        {
            "trap.tutorial.door",
            "trap.tutorial.sell_rule",
            "trap.tutorial.sell_gain",
            "trap.tutorial.early_leave",
            "trap.tutorial.greed",
            "trap.tutorial.retreat",
            "trap.tutorial.boss_door",
            LeaveTrapDefId,
        };

        /// <summary>把阅读序换算成抽牌堆序（index 0 = 第一张被补出的牌）。</summary>
        public static List<string> ToPileOrder(IReadOnlyList<string> reading)
        {
            var pile = new List<string>(reading.Count);
            for (var i = 0; i < sReadingIndexForPile.Length && i < reading.Count; i++)
            {
                pile.Add(reading[sReadingIndexForPile[i]]);
            }

            return pile;
        }

        public static List<string> GetWavePileOrder(int wave)
        {
            switch (wave)
            {
                case 2: return ToPileOrder(sWave2Reading);
                case 3: return ToPileOrder(sWave3Reading);
                case 4: return ToPileOrder(sWave4Reading);
                default: return ToPileOrder(sWave1Reading);
            }
        }

        /// <summary>
        /// 第一波开局装填：0 张直摆 + 保序抽牌堆（8 张铺场 + 1 张补位卡），
        /// 经既有 OpeningDeal → FillEmptySlots 常规发牌链路铺满场地。
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

            var pile = ToPileOrder(sWave1Reading);
            pile.Add(Wave1SpareDefId);
            for (var i = 0; i < pile.Count; i++)
            {
                var draft = content.CreateDraft(pile[i]);
                if (draft == null || draft.Kind == CardKind.Unknown)
                {
                    Debug.LogError("[Tutorial] 教学内容缺失：" + pile[i]);
                    continue;
                }

                options.AddEnemyCard(draft);
            }

            return options;
        }
    }
}
