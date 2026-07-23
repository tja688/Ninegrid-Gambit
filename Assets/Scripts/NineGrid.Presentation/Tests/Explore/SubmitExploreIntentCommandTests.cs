using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Explore
{
    /// <summary>
    /// V1：SubmitExploreIntentCommand → Runtime → Director 接缝。
    /// </summary>
    public sealed class SubmitExploreIntentCommandTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

        [Test]
        public void Command_LegalExplore_SubmitsToRuntime_AndKeepsLockstep()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sFarCornerSlot);
                Assert.IsTrue(arch.Board.IsEmpty(sAdjacentSlot));

                var present = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new ExploreIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    present);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitExploreIntentCommand(sAdjacentSlot.Index)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    runtime.Tick();
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(0, present.BeginCount);

                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(1, present.BeginCount);

                    runtime.TickUntilIdle();
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                    Assert.AreEqual(3, present.BeginCount);
                    Assert.AreEqual(
                        0,
                        OccupancyForceSyncGuard.InvocationCount,
                        "锁步探索不得触发占格强制对账");
                }
            }
        }

        [Test]
        public void Command_IllegalExplore_RejectsWithoutSubmitting()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);

                var present = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new ExploreIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    present);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsFalse(arch.Architecture.SendCommand(
                        new SubmitExploreIntentCommand(sFarCornerSlot.Index)));
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                    Assert.AreEqual(0, present.BeginCount);
                }
            }
        }

        [Test]
        public void Command_BusyBuffersLatestWinsLegalExplore_ThirdOverwritesSecond()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sFarCornerSlot);

                // 中心 Avatar 正交邻格：2/4/6/8（1 被怪占）。
                var scriptFactory = new RecordingScriptFactory(continueTicks: 2);
                using (var runtime = PresentationRuntimeFixture.Install(
                           arch,
                           scriptFactory,
                           out RecordingUiPickSink uiPick))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(new SubmitExploreIntentCommand(2)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    Assert.IsTrue(arch.Architecture.SendCommand(new SubmitExploreIntentCommand(4)));
                    Assert.AreEqual(1, uiPick.Previews.Count);
                    Assert.AreEqual(4, uiPick.Previews[0].TargetId);

                    Assert.IsTrue(arch.Architecture.SendCommand(new SubmitExploreIntentCommand(6)));
                    Assert.AreEqual(2, uiPick.Previews.Count);
                    Assert.AreEqual(6, uiPick.Previews[1].TargetId);
                    Assert.AreEqual(1, scriptFactory.Built.Count);
                    Assert.AreEqual(2, scriptFactory.Built[0].TargetId);

                    runtime.TickUntilIdle();
                    Assert.AreEqual(2, scriptFactory.Built.Count);
                    Assert.AreEqual(6, scriptFactory.Built[1].TargetId);
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
