using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #30 行为基线：EventLog → 投影 → Present/Commit → ack 的因果顺序。
    /// Commit 用 Present 窗探针模拟 ADR-0002 编排内提交，不依赖 CardManagerSingleton 反射。
    /// </summary>
    public sealed class EventLogCommitBaselineTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

        [Test]
        public void Explore_EventLogThenProjectThenCommitDuringPresent_BeforeAck()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sFarCornerSlot);
                Assert.IsTrue(arch.Board.IsEmpty(sAdjacentSlot));

                var phases = new List<string>();
                var commit = new RecordingCommitProbe();
                var present = new CommittingPresentChannel(
                    ticksUntilComplete: 1,
                    commit,
                    () => arch.Pipeline.EventLog.Entries.Count,
                    onBegin: batchId => phases.Add("present:" + batchId));

                var factory = new ExploreIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    present,
                    onBatchProjected: (startIndex, slot, projection) =>
                    {
                        phases.Add("project:" + arch.Sync.ActiveBatchId);
                        Assert.IsNotNull(projection);
                        Assert.GreaterOrEqual(arch.Pipeline.EventLog.Entries.Count, startIndex);
                        Assert.AreEqual(sAdjacentSlot.Index, slot);
                    });

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    bool preview;
                    Assert.IsTrue(runtime.TrySubmitIntent(
                        new InputIntent(InputIntentKinds.Explore, sAdjacentSlot.Index),
                        out preview));
                    Assert.IsFalse(preview);

                    // Resolve ClickEmpty：写 EventLog + 投影，尚未 Present/Commit
                    var clickStart = arch.Pipeline.EventLog.Entries.Count;
                    runtime.Tick();
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(0, present.BeginCount);
                    Assert.AreEqual(0, commit.CommitCount);
                    CollectionAssert.AreEqual(new[] { "project:1" }, phases);
                    Assert.IsFalse(EventLogAssert.ContainsTypeSince(
                        arch.Pipeline.EventLog, clickStart, CoreEventType.SlotsFilled));

                    // Present → Commit → ack
                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(1, present.BeginCount);
                    Assert.AreEqual(1, commit.CommitCount);
                    CollectionAssert.AreEqual(
                        new[] { "project:1", "present:1" },
                        phases);

                    // Fill：EventLog SlotsFilled → project → present/commit
                    var fillStart = arch.Pipeline.EventLog.Entries.Count;
                    runtime.Tick();
                    Assert.AreEqual(2, arch.Sync.ActiveBatchId);
                    Assert.IsTrue(EventLogAssert.ContainsTypeSince(
                        arch.Pipeline.EventLog, fillStart, CoreEventType.SlotsFilled));
                    Assert.AreEqual(1, commit.CommitCount);
                    Assert.AreEqual("project:2", phases[phases.Count - 1]);

                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(2, commit.CommitCount);

                    // Rotate：EventLog BoardRotated → project → present/commit → idle
                    var rotateStart = arch.Pipeline.EventLog.Entries.Count;
                    runtime.Tick();
                    Assert.AreEqual(3, arch.Sync.ActiveBatchId);
                    Assert.IsTrue(EventLogAssert.ContainsTypeSince(
                        arch.Pipeline.EventLog, rotateStart, CoreEventType.BoardRotated));
                    Assert.IsTrue(
                        EventLogAssert.IndexOfTypeSince(
                            arch.Pipeline.EventLog, clickStart, CoreEventType.SlotsFilled)
                        < EventLogAssert.IndexOfTypeSince(
                            arch.Pipeline.EventLog, clickStart, CoreEventType.BoardRotated),
                        "SlotsFilled 必须先于 BoardRotated");

                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(3, commit.CommitCount);

                    runtime.TickUntilIdle();
                    Assert.IsFalse(runtime.MainlineBusy.Value);

                    CollectionAssert.AreEqual(
                        new[]
                        {
                            "project:1", "present:1",
                            "project:2", "present:2",
                            "project:3", "present:3"
                        },
                        phases);
                    CollectionAssert.AreEqual(new[] { 1, 2, 3 }, commit.CommittedBatchIds);
                }
            }
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
        }
    }
}
