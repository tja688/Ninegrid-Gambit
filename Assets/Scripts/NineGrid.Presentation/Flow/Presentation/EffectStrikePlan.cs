using System;
using System.Collections.Generic;
using NineGrid.Core;

namespace NineGrid.Flow.Presentation
{
    /// <summary>
    /// 一个 (打击者, 受击者, 效果实例) 的打击组：串行播一次「攻击动作 + 受击反馈」，
    /// 命中帧冲刷本组暂扣的伤害/血甲指令。
    /// </summary>
    public sealed class EffectStrikeGroup
    {
        public EffectStrikeGroup(int strikerUid, int victimUid, string sourceDefId, long firstSequence)
        {
            StrikerUid = strikerUid;
            VictimUid = victimUid;
            SourceDefId = sourceDefId ?? string.Empty;
            FirstSequence = firstSequence;
        }

        public int StrikerUid { get; }
        public int VictimUid { get; }
        public string SourceDefId { get; }
        public long FirstSequence { get; }

        /// <summary>本组暂扣的 Impact 指令（ShowDamage / UpdateHp / UpdateArmor）；纯移除组可为空。</summary>
        public List<PresentationInstruction> Instructions { get; } = new List<PresentationInstruction>(4);

        /// <summary>受击者在本批被移除/击杀（打击后由移除呈现接手退场，不回锚）。</summary>
        public bool VictimRemoved { get; set; }

        /// <summary>已播过（或已降级冲刷），不再重复。</summary>
        public bool Played { get; set; }

        public bool ContainsInstruction(PresentationInstruction instruction)
        {
            return instruction != null && Instructions.Contains(instruction);
        }
    }

    /// <summary>
    /// 效果打击计划（ADR-0050）：开批时从整批结算指令构建。
    /// 归因规则：效果伤害/移除事件带 SourceDefId（效果实例 defId），
    /// 同批更早的 EffectTriggered（Message==SourceDefId）的 CardUid 即效果持有卡 = 打击者。
    /// 只对「打击者是场上卡且不打自己」的伤害/破坏建组；交战/单向打击（无 SourceDefId）不入组。
    /// </summary>
    public sealed class EffectStrikePlan
    {
        private EffectStrikePlan(int batchId)
        {
            BatchId = batchId;
        }

        public int BatchId { get; }

        /// <summary>按首条事件 Sequence 升序的打击组。</summary>
        public List<EffectStrikeGroup> Groups { get; } = new List<EffectStrikeGroup>(4);

        /// <summary>需要移入排期器打击暂扣区的指令全集（引用相等）。</summary>
        public HashSet<PresentationInstruction> HeldInstructions { get; } =
            new HashSet<PresentationInstruction>();

        public bool HasWork
        {
            get { return Groups.Count > 0; }
        }

        /// <summary>神圣决斗有独立的隔离区编排（ADR-0048 例外），不并入打击计划。</summary>
        private const string HolyDuelSourceDefId = "skill.holy_duel";

        /// <summary>
        /// 已有专属表演的来源不入打击计划：神圣决斗（隔离区编排）、骷髅融合（专用融合动画，
        /// 参与者由融合 purge 呈现，若建组只会在参与者视图卸场后降级）。
        /// </summary>
        private static bool HasDedicatedPresentation(string sourceDefId)
        {
            return string.Equals(sourceDefId, HolyDuelSourceDefId, StringComparison.Ordinal)
                   || NineGrid.Flow.SkeletonFusionPresentationScanner.IsFusionSkillId(sourceDefId);
        }

        public static EffectStrikePlan Build(
            PresentationBatch batch,
            Func<int, bool> isStrikerPresentable)
        {
            var plan = new EffectStrikePlan(batch != null ? batch.BatchId : 0);
            var instructions = batch?.Instructions;
            if (instructions == null || instructions.Count == 0)
            {
                return plan;
            }

            // Message=效果实例 defId → 最近一次触发的持有卡 uid（同 defId 多次触发取最新，Sequence 顺序扫描保证）。
            var holderBySource = new Dictionary<string, int>(StringComparer.Ordinal);
            var groupByKey = new Dictionary<(int striker, int victim, string source), EffectStrikeGroup>();

            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                var gameEvent = instruction?.Event;
                if (gameEvent == null || instruction.MapEntry == null)
                {
                    continue;
                }

                if (gameEvent.Type == CoreEventType.EffectTriggered)
                {
                    // 双键索引：Message=效果实例 defId（如 trap.rolling_stone.slot3），
                    // SourceDefId=效果容器 defId（如 trap.rolling_stone）——
                    // 伤害/移除事件只携带容器 defId（EffectRuntimeContext.SourceDefId），须按容器键命中。
                    if (gameEvent.CardUid > 0)
                    {
                        if (!string.IsNullOrEmpty(gameEvent.Message))
                        {
                            holderBySource[gameEvent.Message] = gameEvent.CardUid;
                        }

                        if (!string.IsNullOrEmpty(gameEvent.SourceDefId))
                        {
                            holderBySource[gameEvent.SourceDefId] = gameEvent.CardUid;
                        }
                    }

                    continue;
                }

                if (IsStrikeDamageInstruction(instruction, gameEvent))
                {
                    // 按卡回退（ADR-0050 补记）：登记为直伤的来源不建组、不暂扣，
                    // 指令保持常规 Impact 锚点冲刷。
                    if (EffectStrikePresentationRules.UseDirectFlush(gameEvent.SourceDefId))
                    {
                        continue;
                    }

                    if (!TryResolveStriker(
                            holderBySource,
                            gameEvent.SourceDefId,
                            gameEvent.CardUid,
                            isStrikerPresentable,
                            out var strikerUid))
                    {
                        continue;
                    }

                    var group = GetOrAddGroup(
                        plan,
                        groupByKey,
                        strikerUid,
                        gameEvent.CardUid,
                        gameEvent.SourceDefId,
                        gameEvent.Sequence);
                    group.Instructions.Add(instruction);
                    plan.HeldInstructions.Add(instruction);
                    continue;
                }

                if (gameEvent.Type == CoreEventType.CardRemoved
                    || gameEvent.Type == CoreEventType.CardKilled)
                {
                    var victimUid = gameEvent.CardUid;
                    if (victimUid <= 0)
                    {
                        continue;
                    }

                    // 已建组的受害者：标记「打击后即退场」。
                    MarkVictimRemoved(plan, victimUid);

                    // 直接移除类（滚石破坏卡片等）：无伤害指令，仍需一次打击表演。
                    if (gameEvent.Type == CoreEventType.CardRemoved
                        && !string.IsNullOrEmpty(gameEvent.SourceDefId)
                        && !HasDedicatedPresentation(gameEvent.SourceDefId)
                        && !EffectStrikePresentationRules.UseDirectFlush(gameEvent.SourceDefId)
                        && TryResolveStriker(
                            holderBySource,
                            gameEvent.SourceDefId,
                            victimUid,
                            isStrikerPresentable,
                            out var removalStriker))
                    {
                        var group = GetOrAddGroup(
                            plan,
                            groupByKey,
                            removalStriker,
                            victimUid,
                            gameEvent.SourceDefId,
                            gameEvent.Sequence);
                        group.VictimRemoved = true;
                    }
                }
            }

            plan.Groups.Sort((a, b) => a.FirstSequence.CompareTo(b.FirstSequence));
            return plan;
        }

        public bool TryGetNextUnplayed(out EffectStrikeGroup group)
        {
            for (var i = 0; i < Groups.Count; i++)
            {
                if (!Groups[i].Played)
                {
                    group = Groups[i];
                    return true;
                }
            }

            group = null;
            return false;
        }

        public void CollectUnplayedInvolving(int cardUid, List<EffectStrikeGroup> buffer)
        {
            if (buffer == null || cardUid <= 0)
            {
                return;
            }

            for (var i = 0; i < Groups.Count; i++)
            {
                var group = Groups[i];
                if (!group.Played && (group.StrikerUid == cardUid || group.VictimUid == cardUid))
                {
                    buffer.Add(group);
                }
            }
        }

        /// <summary>
        /// 打击级伤害指令判定：Impact 上的 ShowDamage / UpdateHp / UpdateArmor，
        /// 事件类型为伤害家族且带效果来源（交战/单向打击 SourceDefId 为空，不入组）。
        /// </summary>
        private static bool IsStrikeDamageInstruction(
            PresentationInstruction instruction,
            CoreGameEvent gameEvent)
        {
            if (instruction.MapEntry.Beat != PresentationBeat.Impact)
            {
                return false;
            }

            if (instruction.Kind != PresentationInstructionKind.ShowDamage
                && instruction.Kind != PresentationInstructionKind.UpdateHp
                && instruction.Kind != PresentationInstructionKind.UpdateArmor)
            {
                return false;
            }

            if (gameEvent.Type != CoreEventType.DamageDealt
                && gameEvent.Type != CoreEventType.HpChanged
                && gameEvent.Type != CoreEventType.ArmorChanged)
            {
                return false;
            }

            // 增益不属于打击：HpChanged 正 Delta = 治疗、ArmorChanged 正 Delta = 加甲。
            // DamageDealt 的 Delta 为正的总伤害量，不参与本判定。
            if ((gameEvent.Type == CoreEventType.HpChanged
                    || gameEvent.Type == CoreEventType.ArmorChanged)
                && gameEvent.Delta >= 0)
            {
                return false;
            }

            if (gameEvent.CardUid <= 0
                || string.IsNullOrEmpty(gameEvent.SourceDefId)
                || HasDedicatedPresentation(gameEvent.SourceDefId))
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveStriker(
            Dictionary<string, int> holderBySource,
            string sourceDefId,
            int victimUid,
            Func<int, bool> isStrikerPresentable,
            out int strikerUid)
        {
            strikerUid = 0;
            if (string.IsNullOrEmpty(sourceDefId)
                || !holderBySource.TryGetValue(sourceDefId, out var holderUid)
                || holderUid <= 0
                || holderUid == victimUid)
            {
                return false;
            }

            if (isStrikerPresentable != null && !isStrikerPresentable(holderUid))
            {
                return false;
            }

            strikerUid = holderUid;
            return true;
        }

        private static EffectStrikeGroup GetOrAddGroup(
            EffectStrikePlan plan,
            Dictionary<(int striker, int victim, string source), EffectStrikeGroup> groupByKey,
            int strikerUid,
            int victimUid,
            string sourceDefId,
            long sequence)
        {
            var key = (strikerUid, victimUid, sourceDefId ?? string.Empty);
            if (!groupByKey.TryGetValue(key, out var group))
            {
                group = new EffectStrikeGroup(strikerUid, victimUid, sourceDefId, sequence);
                groupByKey[key] = group;
                plan.Groups.Add(group);
            }

            return group;
        }

        private static void MarkVictimRemoved(EffectStrikePlan plan, int victimUid)
        {
            for (var i = 0; i < plan.Groups.Count; i++)
            {
                if (plan.Groups[i].VictimUid == victimUid)
                {
                    plan.Groups[i].VictimRemoved = true;
                }
            }
        }
    }
}
