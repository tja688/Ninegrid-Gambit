using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.UseItem
{
    /// <summary>
    /// V3：SubmitUseItemIntentCommand → Runtime → Director 接缝。
    /// </summary>
    public sealed class SubmitUseItemIntentCommandTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        [Test]
        public void Command_LegalUseItem_Kill_Lockstep_UseThenFillRotate()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                var targetUid = arch.Board.GetCardUid(sAdjacentSlot);
                var knifeUid = SpawnHelpIntoItemSlots(arch, "help.throwing_knife");

                var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new UseItemIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    usePresent,
                    boardPresent);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitUseItemIntentCommand(knifeUid, new[] { targetUid }, null)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    runtime.Tick();
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(0, usePresent.BeginCount);

                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(1, usePresent.BeginCount);

                    runtime.TickUntilIdle();
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                    Assert.AreEqual(
                        0,
                        OccupancyForceSyncGuard.InvocationCount,
                        "锁步用牌不得触发占格强制对账");
                }
            }
        }

        [Test]
        public void Command_IllegalUseItem_RejectsWithoutSubmitting()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);

                var usePresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new UseItemIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    usePresent,
                    boardPresent);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsFalse(arch.Architecture.SendCommand(
                        new SubmitUseItemIntentCommand(99999, null, null)));
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                    Assert.AreEqual(0, usePresent.BeginCount);
                }
            }
        }

        [Test]
        public void Command_BusyBuffersEarliestLegalUseItem_ThirdRejected()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                var targetUid = arch.Board.GetCardUid(sAdjacentSlot);
                var knifeA = SpawnHelpIntoItemSlots(arch, "help.throwing_knife");
                var knifeB = SpawnHelpIntoItemSlots(arch, "help.throwing_knife");
                var knifeC = SpawnHelpIntoItemSlots(arch, "help.throwing_knife");

                var scriptFactory = new RecordingScriptFactory(continueTicks: 2);
                using (var runtime = PresentationRuntimeFixture.Install(
                           arch,
                           scriptFactory,
                           out RecordingUiPickSink uiPick))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitUseItemIntentCommand(knifeA, new[] { targetUid }, null)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitUseItemIntentCommand(knifeB, new[] { targetUid }, null)));
                    Assert.AreEqual(1, uiPick.Previews.Count);
                    Assert.AreEqual(knifeB, uiPick.Previews[0].TargetId);

                    Assert.IsFalse(arch.Architecture.SendCommand(
                        new SubmitUseItemIntentCommand(knifeC, new[] { targetUid }, null)));
                    Assert.AreEqual(1, scriptFactory.Built.Count);
                    Assert.AreEqual(knifeA, scriptFactory.Built[0].TargetId);
                    Assert.AreEqual(InputIntentKinds.UseItem, scriptFactory.Built[0].Kind);

                    runtime.TickUntilIdle();
                    Assert.AreEqual(2, scriptFactory.Built.Count);
                    Assert.AreEqual(knifeB, scriptFactory.Built[1].TargetId);
                }
            }
        }

        private static int SpawnHelpIntoItemSlots(PresentationArchitectureFixture arch, string defId)
        {
            arch.Pipeline.Enqueue(
                new SpawnCardAction(defId, CardKind.HelpCard, ZoneId.ItemSlots, SlotId.None, 1, "test"));
            Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
            var deck = arch.Architecture.GetModel<DeckModel>();
            Assert.Greater(deck.ItemSlotUids.Count, 0);
            return deck.ItemSlotUids[deck.ItemSlotUids.Count - 1];
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
