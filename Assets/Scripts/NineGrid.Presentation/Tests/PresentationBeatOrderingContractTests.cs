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

        // ==================== ④ Core 因果深度盖章 ====================

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
