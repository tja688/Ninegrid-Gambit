using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// ADR-0048 表现时序契约：
    /// ① 两相 Impact——FlushImpactOnly(TriggerEffect) 先演触发脉冲，其余 Impact（飘字/血甲）后冲；
    /// ② FlushImpactExcept(TriggerEffect) 不消费触发指令（留给运动落地后的第一相）；
    /// ③ 链级去重——同一连锁内同一持有卡的同一效果只演一次触发反馈；
    /// ④ Core 因果深度——动作管线给事件盖章 CausalDepth（根 0，触发/FollowUp 子动作递增）。
    /// </summary>
    public class PresentationBeatOrderingContractTests
    {
        private sealed class RecordingBeatHandler : IBattleBeatHandler
        {
            public readonly List<PresentationInstructionKind> Applied = new List<PresentationInstructionKind>();

            public bool TryApply(PresentationInstruction instruction)
            {
                Applied.Add(instruction.Kind);
                return true;
            }
        }

        [SetUp]
        public void SetUp()
        {
            TriggerPulseChainDedup.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            TriggerPulseChainDedup.Reset();
        }

        // ==================== ① 两相 Impact ====================

        [Test]
        public void FlushImpactOnly_TriggerFirst_ThenRemainingImpact_KeepsPulseBeforeFloaters()
        {
            var handler = new RecordingBeatHandler();
            var scheduler = new BattleBeatScheduler(handler);
            scheduler.OnBatchOpened(MakeBatch(
                MakeEvent(CoreEventType.DamageDealt, seq: 1),
                MakeEvent(CoreEventType.EffectTriggered, seq: 2),
                MakeEvent(CoreEventType.ArmorChanged, seq: 3, delta: 2)));

            var dispatched = scheduler.FlushImpactOnly(PresentationInstructionKind.TriggerEffect);

            Assert.AreEqual(1, dispatched, "第一相只派发 TriggerEffect 并返回条数（供节拍间隔判定）");
            Assert.AreEqual(
                new[] { PresentationInstructionKind.TriggerEffect },
                handler.Applied.ToArray(),
                "第一相不得夹带飘字/血甲指令");

            scheduler.ReportBeat(PresentationBeat.Impact);
            scheduler.ReportBeat(PresentationBeat.Settled);

            Assert.AreEqual(
                PresentationInstructionKind.TriggerEffect,
                handler.Applied[0],
                "触发脉冲必须先于其余 Impact 演出（旋转停稳 → 脉冲 → 飘字）");
            Assert.IsTrue(
                handler.Applied.Contains(PresentationInstructionKind.ShowDamage),
                "第二相必须补齐伤害飘字");
        }

        [Test]
        public void FlushImpactOnly_NoTriggerPending_ReturnsZero()
        {
            var handler = new RecordingBeatHandler();
            var scheduler = new BattleBeatScheduler(handler);
            scheduler.OnBatchOpened(MakeBatch(MakeEvent(CoreEventType.DamageDealt, seq: 1)));

            Assert.AreEqual(
                0,
                scheduler.FlushImpactOnly(PresentationInstructionKind.TriggerEffect),
                "无触发指令时返回 0（PresentStep 不插入节拍间隔）");
            Assert.AreEqual(0, handler.Applied.Count, "不得误派发其它 Impact");

            scheduler.ReportBeat(PresentationBeat.Impact);
            scheduler.ReportBeat(PresentationBeat.Settled);
        }

        // ==================== ② 命中帧 / Drain 锚点收窄 ====================

        [Test]
        public void FlushImpactExcept_TriggerEffect_LeavesTriggerForMotionSettledPhase()
        {
            var handler = new RecordingBeatHandler();
            var scheduler = new BattleBeatScheduler(handler);
            scheduler.OnBatchOpened(MakeBatch(
                MakeEvent(CoreEventType.EffectTriggered, seq: 1),
                MakeEvent(CoreEventType.DamageDealt, seq: 2)));

            // 命中帧 / Remove 前锚点：只冲飘字血甲。
            scheduler.FlushImpactExcept(PresentationInstructionKind.TriggerEffect);

            Assert.AreEqual(
                new[] { PresentationInstructionKind.ShowDamage },
                handler.Applied.ToArray(),
                "命中帧只演伤害反馈，触发脉冲留给运动落地后");

            // 运动落地后第一相：触发脉冲此时才演。
            var dispatched = scheduler.FlushImpactOnly(PresentationInstructionKind.TriggerEffect);
            Assert.AreEqual(1, dispatched, "被保留的 TriggerEffect 必须在第一相被消费");

            scheduler.ReportBeat(PresentationBeat.Settled);
        }

        // ==================== ③ 链级触发去重 ====================

        [Test]
        public void ChainDedup_SameCardSameEffect_OnlyFirstPlays_ResetReopensWindow()
        {
            Assert.IsTrue(TriggerPulseChainDedup.TryMarkFirst(7, "skill.link_tactics.refresh"), "链内首次触发应演出");
            Assert.IsFalse(TriggerPulseChainDedup.TryMarkFirst(7, "skill.link_tactics.refresh"), "链内重复触发必须静默");
            Assert.IsTrue(TriggerPulseChainDedup.TryMarkFirst(7, "skill.link_tactics.activate"), "同卡不同效果不受牵连");
            Assert.IsTrue(TriggerPulseChainDedup.TryMarkFirst(8, "skill.link_tactics.refresh"), "不同卡同效果不受牵连");

            TriggerPulseChainDedup.Reset();
            Assert.IsTrue(
                TriggerPulseChainDedup.TryMarkFirst(7, "skill.link_tactics.refresh"),
                "链结束复位后，下一次交互允许再次演出");
        }

        // ==================== ⑤ 效果打击暂扣（ADR-0050） ====================

        [Test]
        public void StrikeHold_SkipsEarlyAnchors_FlushedByGroupPredicate()
        {
            var handler = new RecordingBeatHandler();
            var scheduler = new BattleBeatScheduler(handler);
            var damage = MakeEvent(CoreEventType.DamageDealt, seq: 2);
            scheduler.OnBatchOpened(MakeBatch(
                MakeEvent(CoreEventType.EffectTriggered, seq: 1),
                damage));

            // 暂扣效果伤害后，命中帧锚点（FlushImpactExcept）不得提前冲刷它。
            scheduler.HoldStrikeImpactWhere(i => i.Event == damage);
            scheduler.FlushImpactExcept(PresentationInstructionKind.TriggerEffect);
            Assert.AreEqual(0, handler.Applied.Count, "暂扣的效果伤害不得在命中帧锚点被提前冲刷");

            // 打击组命中帧按谓词放行。
            var dispatched = scheduler.FlushStrikeHeldWhere(i => i.Event == damage);
            Assert.AreEqual(1, dispatched, "打击组命中帧必须派发本组暂扣指令");
            Assert.AreEqual(
                new[] { PresentationInstructionKind.ShowDamage },
                handler.Applied.ToArray());

            scheduler.FlushImpactOnly(PresentationInstructionKind.TriggerEffect);
            scheduler.ReportBeat(PresentationBeat.Impact);
            scheduler.ReportBeat(PresentationBeat.Settled);
        }

        [Test]
        public void StrikeHold_LeftoverFlushedBeforeSettled_NeverLost()
        {
            var handler = new RecordingBeatHandler();
            var scheduler = new BattleBeatScheduler(handler);
            var damage = MakeEvent(CoreEventType.DamageDealt, seq: 1);
            scheduler.OnBatchOpened(MakeBatch(damage));
            scheduler.HoldStrikeImpactWhere(i => i.Event == damage);

            scheduler.ReportBeat(PresentationBeat.Impact);
            Assert.AreEqual(0, handler.Applied.Count, "全量 Impact 报点也不消费暂扣区（决斗帧等不误伤）");

            scheduler.ReportBeat(PresentationBeat.Settled);
            Assert.IsTrue(
                handler.Applied.Contains(PresentationInstructionKind.ShowDamage),
                "打击编排未消费时，Settled 前兜底放行，飘字不丢失");
        }

        [Test]
        public void StrikePlan_AttributesDamageToEffectHolder_AndMarksRemovedVictim()
        {
            // 真实事件语义：EffectTriggered.Message=效果实例 defId、SourceDefId=容器 defId；
            // 伤害/移除事件只携带容器 defId（EffectRuntimeContext.SourceDefId）。
            var trigger = new CoreGameEvent(CoreEventType.EffectTriggered, actionId: 1, actionName: "ExecuteEffect")
                .WithCard(5)
                .WithMessage("trap.spike.move")
                .WithSource("trap.spike", "trap.spike.move");
            var damage = new CoreGameEvent(CoreEventType.DamageDealt, actionId: 2, actionName: "DealDamage")
                .WithActor(5)
                .WithTarget(7)
                .WithCard(7)
                .WithAmount(2)
                .WithDelta(2)
                .WithSource("trap.spike", "trap.spike.move");
            var removed = new CoreGameEvent(CoreEventType.CardRemoved, actionId: 3, actionName: "RemoveCard")
                .WithCard(7)
                .WithSource("trap.spike", "kill");
            var batch = MakeBatch(trigger, damage, removed);

            var plan = EffectStrikePlan.Build(batch, uid => true);

            Assert.AreEqual(1, plan.Groups.Count, "同源同受击者归入一个打击组");
            Assert.AreEqual(5, plan.Groups[0].StrikerUid, "打击者 = 同批 EffectTriggered 的持有卡（容器 defId 键命中）");
            Assert.AreEqual(7, plan.Groups[0].VictimUid);
            Assert.IsTrue(plan.Groups[0].VictimRemoved, "受击者同批被移除须标记（打击后由退场呈现接手）");
            Assert.AreEqual(1, plan.HeldInstructions.Count, "伤害指令入暂扣区；CardRemoved（Beat=None）不入");
        }

        [Test]
        public void StrikePlan_PureRemoval_BuildsStrikeGroup_RollingStoneStyle()
        {
            // 滚石：无伤害指令，直接 RemoveCard——仍须一次打击表演（容器 defId 归因）。
            var trigger = new CoreGameEvent(CoreEventType.EffectTriggered, actionId: 1, actionName: "ExecuteEffect")
                .WithCard(28)
                .WithMessage("trap.rolling_stone.slot3")
                .WithSource("trap.rolling_stone", "trap.rolling_stone.slot3");
            var removed = new CoreGameEvent(CoreEventType.CardRemoved, actionId: 2, actionName: "RemoveCard")
                .WithCard(24)
                .WithSource("trap.rolling_stone", "trap.rolling_stone");
            var batch = MakeBatch(trigger, removed);

            var plan = EffectStrikePlan.Build(batch, uid => true);

            Assert.AreEqual(1, plan.Groups.Count, "纯移除也建打击组（先撞击再碎裂）");
            Assert.AreEqual(28, plan.Groups[0].StrikerUid);
            Assert.AreEqual(24, plan.Groups[0].VictimUid);
            Assert.IsTrue(plan.Groups[0].VictimRemoved);
            Assert.AreEqual(0, plan.HeldInstructions.Count, "纯移除组无暂扣指令");
        }

        [Test]
        public void StrikePlan_DirectFlushFallback_SkipsGroupingAndHold()
        {
            // 按卡回退（ADR-0050 补记）：登记为直伤的容器 defId 不建打击组、不暂扣——
            // 伤害/移除保持旧「直接掉血/直接破坏」冲刷路径；未登记来源不受影响。
            EffectStrikePresentationRules.SetOverrideForTests(new[] { "trap.rolling_stone" });
            try
            {
                var stoneTrigger = new CoreGameEvent(CoreEventType.EffectTriggered, actionId: 1, actionName: "ExecuteEffect")
                    .WithCard(28)
                    .WithMessage("trap.rolling_stone.slot3")
                    .WithSource("trap.rolling_stone", "trap.rolling_stone.slot3");
                var stoneRemoved = new CoreGameEvent(CoreEventType.CardRemoved, actionId: 2, actionName: "RemoveCard")
                    .WithCard(24)
                    .WithSource("trap.rolling_stone", "trap.rolling_stone");
                var spikeTrigger = new CoreGameEvent(CoreEventType.EffectTriggered, actionId: 3, actionName: "ExecuteEffect")
                    .WithCard(5)
                    .WithMessage("trap.spike.move")
                    .WithSource("trap.spike", "trap.spike.move");
                var spikeDamage = new CoreGameEvent(CoreEventType.DamageDealt, actionId: 4, actionName: "DealDamage")
                    .WithActor(5)
                    .WithTarget(7)
                    .WithCard(7)
                    .WithAmount(1)
                    .WithDelta(1)
                    .WithSource("trap.spike", "trap.spike.move");
                var batch = MakeBatch(stoneTrigger, stoneRemoved, spikeTrigger, spikeDamage);

                var plan = EffectStrikePlan.Build(batch, uid => true);

                Assert.AreEqual(1, plan.Groups.Count, "回退来源不建组；未登记来源（倒刺）照常建组");
                Assert.AreEqual(5, plan.Groups[0].StrikerUid, "剩余打击组应属未登记来源");
                Assert.AreEqual(1, plan.HeldInstructions.Count, "回退来源伤害不暂扣（保持常规 Impact 锚点）");
            }
            finally
            {
                EffectStrikePresentationRules.ResetForTests();
            }
        }

        [Test]
        public void StrikePlan_ExcludesCombatDamage_OffFieldStriker_AndHolyDuel()
        {
            var combatDamage = new CoreGameEvent(CoreEventType.DamageDealt, actionId: 1, actionName: "DealDamage")
                .WithActor(9)
                .WithTarget(7)
                .WithCard(7)
                .WithAmount(3); // 交战/单向打击：无 SourceDefId
            var duelTrigger = new CoreGameEvent(CoreEventType.EffectTriggered, actionId: 2, actionName: "ExecuteEffect")
                .WithCard(6)
                .WithMessage("skill.holy_duel");
            var duelDamage = new CoreGameEvent(CoreEventType.DamageDealt, actionId: 3, actionName: "DealDamage")
                .WithActor(6)
                .WithTarget(1)
                .WithCard(1)
                .WithAmount(2)
                .WithSource("skill.holy_duel", "skill.holy_duel");
            var offFieldTrigger = new CoreGameEvent(CoreEventType.EffectTriggered, actionId: 4, actionName: "ExecuteEffect")
                .WithCard(30)
                .WithMessage("help.bomb.use");
            var offFieldDamage = new CoreGameEvent(CoreEventType.DamageDealt, actionId: 5, actionName: "DealDamage")
                .WithActor(1)
                .WithTarget(7)
                .WithCard(7)
                .WithAmount(4)
                .WithSource("help.bomb.use", "help.bomb.use");
            var batch = MakeBatch(combatDamage, duelTrigger, duelDamage, offFieldTrigger, offFieldDamage);

            // 只有 uid<=20 在场（30 = 用出的道具卡，不在场）。
            var plan = EffectStrikePlan.Build(batch, uid => uid <= 20);

            Assert.AreEqual(0, plan.Groups.Count, "交战伤害 / 神圣决斗 / 离场来源均不入打击计划");
            Assert.AreEqual(0, plan.HeldInstructions.Count, "上述指令保持原冲刷路径（命中帧 / 决斗帧）");
        }

        [Test]
        public void CausalDepth_RootActionZero_TriggeredEffectDeeper()
        {
            NineGridArchitecture.ResetForTests();
            try
            {
                var arch = NineGridArchitecture.Interface;
                arch.GetModel<RunModel>().SetPhase(GamePhase.InteractionLoop);

                var owner = arch.GetModel<CardRegistry>().Create("monster.test.depth_owner", CardKind.Monster);
                arch.GetModel<BoardModel>().PlaceCard(owner, SlotId.Board(1));
                ActivateOnDealProbeEffect(arch, owner);

                var pipeline = arch.GetSystem<IActionPipelineSystem>();
                pipeline.Execute(new ShuffleIntoDrawPileAction("trap.test_flame", CardKind.Trap, 1, false, "test.seed"));

                var entries = pipeline.EventLog.Entries;
                Assert.Greater(entries.Count, 0, "管线应产出事件");
                Assert.AreEqual(
                    0,
                    entries[0].CausalDepth,
                    "根动作事件 CausalDepth 必须为 0（首条 ActionStarted）");

                var foundTriggered = false;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (entries[i].Type != CoreEventType.EffectTriggered)
                    {
                        continue;
                    }

                    foundTriggered = true;
                    Assert.GreaterOrEqual(
                        entries[i].CausalDepth,
                        1,
                        "触发子动作产出的 EffectTriggered 必须携带更深的因果深度（派生层级可辨）");
                    break;
                }

                Assert.IsTrue(foundTriggered, "OnDeal 探针效果应产生 EffectTriggered 事件");
            }
            finally
            {
                NineGridArchitecture.ResetForTests();
            }
        }

        // ==================== 基建 ====================

        /// <summary>OnDeal→ShuffleInto 自触发探针（无防环排除，由 ADR-0047 熔断收尾）：
        /// 用于制造带因果深度的 EffectTriggered，不依赖内容目录。</summary>
        private static void ActivateOnDealProbeEffect(IArchitecture arch, CardInstance owner)
        {
            const string json =
                "{\"id\":\"test.depth_probe\","
                + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],\"containerType\":\"MonsterSkill\","
                + "\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnDeal\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"ShuffleInto\",\"defId\":\"trap.test_flame\",\"kind\":\"Trap\",\"count\":1,\"top\":false}}";
            var effects = arch.GetSystem<NineGrid.Core.Effects.IEffectSystem>();
            effects.Activate(
                effects.ParseJson(json),
                new NineGrid.Core.Effects.EffectOwner(
                    NineGrid.Core.Effects.EffectContainerType.MonsterSkill,
                    "monster.test.depth_owner",
                    owner.Uid));
        }

        private static PresentationBatch MakeBatch(params CoreGameEvent[] events)
        {
            var instructions = new List<PresentationInstruction>(events.Length);
            for (var i = 0; i < events.Length; i++)
            {
                instructions.Add(new PresentationInstruction(events[i], PresentationEventMap.Get(events[i].Type)));
            }

            return new PresentationBatch(1, instructions, snapshot: null);
        }

        private static CoreGameEvent MakeEvent(CoreEventType type, long seq, int delta = 1)
        {
            var gameEvent = new CoreGameEvent(type, actionId: 1, actionName: "Test")
                .WithCard(7)
                .WithDelta(delta)
                .WithAmount(1)
                .WithMessage("test.effect");
            // Sequence 由 EventLog 赋值；测试直接构造时无需真实序号。
            _ = seq;
            return gameEvent;
        }
    }
}
