using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Cards.Convergence;
using NineGrid.Cards.Presentation;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 翻牌串行队列 + PresentStep 通道前 Idle 门控（ADR-0016）。
    /// </summary>
    public sealed class FlipPlaybackCoordinatorTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            FlipPlaybackCoordinator.Reset();
            BattleBeatHook.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FlipPlaybackCoordinator.Reset();
            BattleBeatHook.Reset();
            for (var i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();
        }

        [Test]
        public void Enqueue_WhilePlaying_QueuesSecondFlip_DoesNotDrop()
        {
            var a = CreatePresenter("FlipA");
            var b = CreatePresenter("FlipB");

            FlipPlaybackCoordinator.Enqueue(a, targetFaceUp: false);
            FlipPlaybackCoordinator.Enqueue(b, targetFaceUp: false);

            Assert.IsFalse(FlipPlaybackCoordinator.IsIdle, "入队后应非 Idle");
            Assert.GreaterOrEqual(FlipPlaybackCoordinator.PendingCount, 1, "第二张应仍在队列（或泵尚未取完）");
        }

        [Test]
        public void PresentStep_DoesNotBeginChannel_UntilFlipIdle()
        {
            var gate = new FakeBatchGate();
            gate.Open(7);
            var channel = new RecordingPresentChannel(ticksUntilComplete: 1);
            var step = new PresentStep(gate, channel);

            // 占用 coordinator，模拟 FaceUp 刷后仍在播。
            var blocker = CreatePresenter("FlipBlocker");
            FlipPlaybackCoordinator.Enqueue(blocker, targetFaceUp: false);
            Assert.IsFalse(FlipPlaybackCoordinator.IsIdle);

            Assert.AreEqual(TimelineStepStatus.Continue, step.Tick(0.016f));
            Assert.IsFalse(channel.Began, "FaceUp 未 Idle 前不得 channel.Begin");

            FlipPlaybackCoordinator.Reset();
            Assert.IsTrue(FlipPlaybackCoordinator.IsIdle);

            Assert.AreEqual(TimelineStepStatus.Finished, step.Tick(0.016f));
            Assert.IsTrue(channel.Began, "Idle 后门控应 Begin");
            Assert.IsTrue(gate.Acknowledged);
        }

        [Test]
        public void PresentStep_DeferFaceUp_DoesNotFlushBeforeBegin_FlushesOnBeats()
        {
            var faceUpClaimed = false;
            BattleBeatHook.FlushUpdateFaceUp = () => faceUpClaimed = true;
            BattleBeatHook.ReportBeat = _ => { };

            var gate = new FakeBatchGate();
            gate.Open(11);
            var channel = new RecordingPresentChannel(ticksUntilComplete: 1);
            var step = new PresentStep(gate, channel, flushFaceUpBeforeBegin: false);

            Assert.AreEqual(TimelineStepStatus.Finished, step.Tick(0.016f));
            Assert.IsTrue(channel.Began, "延后 FaceUp 时仍应 Begin");
            Assert.IsFalse(faceUpClaimed, "战斗通道不得在 Begin 前 FlushUpdateFaceUp");
            Assert.IsTrue(gate.Acknowledged);
        }

        [Test]
        public void FlushUpdateFaceUp_ConsumesOnlyFaceUp_LeavesOtherPending()
        {
            var faceUp = new PresentationInstruction(
                new CoreGameEvent(CoreEventType.CardFaceChanged, 1, "Flip")
                    .WithCard(1)
                    .WithResultValue(0),
                PresentationEventMap.Get(CoreEventType.CardFaceChanged));
            var gold = new PresentationInstruction(
                new CoreGameEvent(CoreEventType.GoldModified, 2, "ModifyGold")
                    .WithAmount(3),
                PresentationEventMap.Get(CoreEventType.GoldModified));

            var claimedFaceUp = false;
            var claimedGold = false;
            var scheduler = new BattleBeatScheduler(
                new RecordingHandler(PresentationInstructionKind.UpdateFaceUp, () => claimedFaceUp = true),
                new RecordingHandler(PresentationInstructionKind.UpdateGold, () => claimedGold = true));

            scheduler.OnBatchOpened(new PresentationBatch(1, new[] { faceUp, gold }, null));
            scheduler.FlushUpdateFaceUp();

            Assert.IsTrue(claimedFaceUp, "FlushUpdateFaceUp 应消费 FaceUp");
            Assert.IsFalse(claimedGold, "不得提前消费金币");

            scheduler.ReportBeat(PresentationBeat.Settled);
            Assert.IsTrue(claimedGold, "Settled 后应消费金币");
        }

        [Test]
        public void FlushImpactExcept_SkipsTriggerEffect_LeavesItForLaterImpact()
        {
            var damage = new PresentationInstruction(
                new CoreGameEvent(CoreEventType.DamageDealt, 1, "DealDamage")
                    .WithTarget(1)
                    .WithAmount(2),
                PresentationEventMap.Get(CoreEventType.DamageDealt));
            var trigger = new PresentationInstruction(
                new CoreGameEvent(CoreEventType.EffectTriggered, 2, "ExecuteEffect")
                    .WithCard(3),
                PresentationEventMap.Get(CoreEventType.EffectTriggered));

            var claimedDamage = false;
            var claimedTrigger = false;
            var scheduler = new BattleBeatScheduler(
                new RecordingHandler(PresentationInstructionKind.ShowDamage, () => claimedDamage = true),
                new RecordingHandler(PresentationInstructionKind.TriggerEffect, () => claimedTrigger = true));

            scheduler.OnBatchOpened(new PresentationBatch(1, new[] { damage, trigger }, null));
            scheduler.FlushImpactExcept(PresentationInstructionKind.TriggerEffect);

            Assert.IsTrue(claimedDamage, "FlushImpactExcept 应消费 ShowDamage");
            Assert.IsFalse(claimedTrigger, "不得提前消费 TriggerEffect");

            scheduler.ReportBeat(PresentationBeat.Impact);
            Assert.IsTrue(claimedTrigger, "后续 Impact 应消费 TriggerEffect");
        }

        private CardFaceFlipPresenter CreatePresenter(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            var tower = go.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var face = new GameObject("Face");
            face.transform.SetParent(tower.FacePivot, false);
            new GameObject("Front").transform.SetParent(face.transform, false);
            var back = new GameObject("back");
            back.transform.SetParent(face.transform, false);
            back.SetActive(false);
            return go.AddComponent<CardFaceFlipPresenter>();
        }

        private sealed class RecordingHandler : IBattleBeatHandler
        {
            private readonly PresentationInstructionKind _kind;
            private readonly System.Action _onApply;

            public RecordingHandler(PresentationInstructionKind kind, System.Action onApply)
            {
                _kind = kind;
                _onApply = onApply;
            }

            public bool TryApply(PresentationInstruction instruction)
            {
                if (instruction == null || instruction.Kind != _kind)
                {
                    return false;
                }

                _onApply?.Invoke();
                return true;
            }
        }

        private sealed class FakeBatchGate : IPresentationBatchGate
        {
            private int _active;

            public bool HasOpenBatch => _active > 0;
            public int ActiveBatchId => _active;
            public bool Acknowledged { get; private set; }

            public void Open(int batchId)
            {
                _active = batchId;
                Acknowledged = false;
            }

            public BatchOpenResult TryOpenNextBatch(out int batchId)
            {
                batchId = 0;
                return BatchOpenResult.Failed;
            }

            public bool TryAcknowledge(int batchId)
            {
                if (_active <= 0 || batchId != _active)
                {
                    return false;
                }

                _active = 0;
                Acknowledged = true;
                return true;
            }
        }

        private sealed class RecordingPresentChannel : IPresentChannel
        {
            private readonly int _ticksUntilComplete;
            private int _ticks;

            public RecordingPresentChannel(int ticksUntilComplete)
            {
                _ticksUntilComplete = ticksUntilComplete;
            }

            public bool Began { get; private set; }
            public bool IsComplete => Began && _ticks >= _ticksUntilComplete;
            public int ActiveBatchId { get; private set; }

            public void Begin(int batchId)
            {
                Began = true;
                ActiveBatchId = batchId;
                _ticks = 0;
            }

            public void Tick(float deltaTime)
            {
                if (Began)
                {
                    _ticks++;
                }
            }
        }
    }
}
