using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Explore
{
    /// <summary>
    /// #48 flush 前 Core 合法性重校：缓冲 explore 在 drain 时若已非法则安静丢弃。
    /// </summary>
    public sealed class BufferedIntentFlushLegalityTests
    {
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);
        private static readonly int sBufferedExploreSlot = 4;

        [Test]
        public void BoardGate_ExploreBecomesIllegal_AfterOccupantPlaced()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sFarCornerSlot);
                Assert.IsTrue(arch.Board.IsEmpty(SlotId.Board(sBufferedExploreSlot)));

                var gate = new BoardBufferedIntentLegality(arch.Architecture);
                var intent = new InputIntent(InputIntentKinds.Explore, sBufferedExploreSlot);
                Assert.IsTrue(gate.IsStillLegal(intent));

                arch.PlaceSoleBoardCardAt(SlotId.Board(sBufferedExploreSlot));
                Assert.IsFalse(gate.IsStillLegal(intent));
            }
        }

        [Test]
        public void Runtime_FlushDropsBufferedExplore_WhenTargetBecomesIllegal()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sFarCornerSlot);

                var scriptFactory = new RecordingScriptFactory(continueTicks: 2);
                var uiPick = new RecordingUiPickSink();
                using (var runtime = PresentationRuntimeFixture.Install(
                           arch,
                           scriptFactory,
                           uiPick,
                           new BoardBufferedIntentLegality(arch.Architecture)))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(new SubmitExploreIntentCommand(2)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitExploreIntentCommand(sBufferedExploreSlot)));
                    Assert.AreEqual(1, uiPick.Previews.Count);
                    Assert.AreEqual(sBufferedExploreSlot, uiPick.Previews[0].TargetId);

                    arch.PlaceSoleBoardCardAt(SlotId.Board(sBufferedExploreSlot));

                    runtime.TickUntilIdle();
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                    Assert.AreEqual(1, scriptFactory.Built.Count);
                    Assert.AreEqual(2, scriptFactory.Built[0].TargetId);
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
