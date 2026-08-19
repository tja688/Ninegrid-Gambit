using System.Collections.Generic;
using NineGrid.Cards.Slots;
using NineGrid.Content.CardPresentation;
using NineGrid.Core;
using UnityEngine;

namespace NineGrid.Cards.Presentation
{
    /// <summary>
    /// 预览卡面上机制图标对应的词条名路由（攻击 / 护甲 / 血量 / 节奏计数 / 攻击模式）。
    /// 词条查询键一律是词条名（<c>displayNameZh</c>）；攻击模式与节奏源直接复用 Core token 常量。
    /// 右键详情不再用 hover 填槽（ADR-0037）；本表仍给卡面「实际显示了哪些图标」只读辅助。
    /// </summary>
    public static class CardFaceIconGlossaryTargets
    {
        /// <summary>基础属性图标的词条名匹配键。</summary>
        public const string AttackTermName = "攻击";

        public const string ArmorTermName = "护甲";
        public const string HpTermName = "血量";

        /// <summary>攻击模式为「无」而只有同步技能时，攻击模式槽升格显示的子图标语义（ADR-0038）。</summary>
        public const string SyncRhythmTermName = "技能同步触发";

        /// <summary>
        /// 卡级共享倒计时图标节点：怪物与机关模板都叫「行动计数」，
        /// 语义按卡级节奏源在行动计数 / 移动计数之间切换，故不进槽代号表。
        /// </summary>
        public static readonly string[] RhythmCountIconNodeNames =
        {
            "行动计数", "Action_Count_Icon", "ActionCountIcon",
        };

        public readonly struct Target
        {
            public Target(SpriteRenderer renderer, string termName)
            {
                Renderer = renderer;
                TermName = termName;
            }

            public SpriteRenderer Renderer { get; }

            /// <summary>词条表 <c>displayNameZh</c> 查询键。</summary>
            public string TermName { get; }
        }

        /// <summary>
        /// 收集本次预览卡面上的图标靶；显隐随投影变化的槽（攻击模式 / 同步子图标）
        /// 只在投影认为该显示时入表，实际命中仍按 renderer 启用状态复核。
        /// </summary>
        public static void Collect(
            Transform faceRoot,
            CardPresentationSnapshot snapshot,
            List<Target> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            if (faceRoot == null)
            {
                return;
            }

            AddCompanionIcon(faceRoot, CardFaceSlotCodes.Attack, AttackTermName, results);
            AddCompanionIcon(faceRoot, CardFaceSlotCodes.Armor, ArmorTermName, results);
            AddCompanionIcon(faceRoot, CardFaceSlotCodes.Hp, HpTermName, results);
            AddRhythmCountIcon(faceRoot, snapshot, results);
            AddAttackPatternIcon(faceRoot, snapshot, results);
            AddSyncRhythmIcon(faceRoot, results);
        }

        private static void AddCompanionIcon(
            Transform faceRoot,
            string numericSlotCode,
            string termName,
            List<Target> results)
        {
            if (!CardFaceSlotNodeMap.TryFindCompanionIcon(faceRoot, numericSlotCode, out var icon)
                || icon == null)
            {
                return;
            }

            var renderer = icon.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                results.Add(new Target(renderer, termName));
            }
        }

        private static void AddRhythmCountIcon(
            Transform faceRoot,
            CardPresentationSnapshot snapshot,
            List<Target> results)
        {
            if (!CardFaceSlotNodeMap.TryFindRendererByNodeNames(
                    faceRoot,
                    RhythmCountIconNodeNames,
                    out var renderer)
                || renderer == null)
            {
                return;
            }

            results.Add(new Target(renderer, ResolveRhythmTermName(snapshot)));
        }

        private static void AddAttackPatternIcon(
            Transform faceRoot,
            CardPresentationSnapshot snapshot,
            List<Target> results)
        {
            if (!CardFaceSlotNodeMap.TryFindRenderer(
                    faceRoot,
                    CardFaceSlotCodes.ActionIcon,
                    out var renderer)
                || renderer == null)
            {
                return;
            }

            var pattern = snapshot != null ? snapshot.AttackPattern : AttackPattern.Unspecified;
            if (TryResolveAttackPatternTermName(pattern, out var termName))
            {
                results.Add(new Target(renderer, termName));
                return;
            }

            // 攻击模式为「无」但挂同步技能：该槽被升格成同步子图标（见 Binder 图标矩阵）。
            if (snapshot != null && snapshot.HasSyncRhythmSkills)
            {
                results.Add(new Target(renderer, SyncRhythmTermName));
            }
        }

        private static void AddSyncRhythmIcon(Transform faceRoot, List<Target> results)
        {
            if (CardFaceSlotNodeMap.TryFindRenderer(
                    faceRoot,
                    CardFaceSlotCodes.SyncRhythmIcon,
                    out var renderer)
                && renderer != null)
            {
                results.Add(new Target(renderer, SyncRhythmTermName));
            }
        }

        /// <summary>
        /// 倒计时图标语义：卡级节奏源为移动计数时读「移动计数」，
        /// 其余（含机关效果倒计时、节奏源未声明）读「行动计数」。
        /// </summary>
        private static string ResolveRhythmTermName(CardPresentationSnapshot snapshot)
        {
            var defId = snapshot != null ? snapshot.DefId : null;
            if (!string.IsNullOrWhiteSpace(defId)
                && CardPresentationConfigCatalog.TryGet(defId.Trim(), out var dto)
                && dto != null
                && CardRhythmRules.TryParse(dto.rhythmSource, out var source)
                && source == CardRhythmSource.Move)
            {
                return CardRhythmRules.TokenMove;
            }

            return CardRhythmRules.TokenAction;
        }

        private static bool TryResolveAttackPatternTermName(AttackPattern pattern, out string termName)
        {
            switch (pattern)
            {
                case AttackPattern.OrthogonalMelee:
                    termName = AttackPatternRules.TokenOrthogonalMelee;
                    return true;
                case AttackPattern.DiagonalMelee:
                    termName = AttackPatternRules.TokenDiagonalMelee;
                    return true;
                case AttackPattern.OmnidirectionalMelee:
                    termName = AttackPatternRules.TokenOmnidirectionalMelee;
                    return true;
                default:
                    termName = null;
                    return false;
            }
        }
    }
}
