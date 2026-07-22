using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Attack
{
    /// <summary>
    /// V2：SubmitAttackIntentCommand → Runtime → Director 接缝（含未击杀反击锁步）。
    /// </summary>
    public sealed class SubmitAttackIntentCommandTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);
        private static readonly SlotId sFarCornerSlot = SlotId.Board(1);

        [Test]
        public void Command_LegalAttack_Kill_Lockstep_HitFillRotate()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);

                var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new AttackIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    hitPresent,
                    boardPresent);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitAttackIntentCommand(sAdjacentSlot.Index)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    runtime.Tick();
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(0, hitPresent.BeginCount);

                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(1, hitPresent.BeginCount);

                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);

                    runtime.Tick();
                    Assert.AreEqual(2, arch.Sync.ActiveBatchId);

                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(1, boardPresent.BeginCount);

                    runtime.Tick();
                    Assert.AreEqual(3, arch.Sync.ActiveBatchId);

                    runtime.Tick();
                    Assert.AreEqual(0, arch.Sync.ActiveBatchId);
                    Assert.AreEqual(2, boardPresent.BeginCount);

                    runtime.TickUntilIdle();
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                    Assert.AreEqual(
                        0,
                        OccupancyForceSyncGuard.InvocationCount,
                        "锁步攻击不得触发占格强制对账");
                }
            }
        }

        [Test]
        public void Command_LegalAttack_NonKill_CounterOnMainline()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 99, attack: 3)).Accepted);
                arch.PlaceSoleBoardCardAt(sAdjacentSlot);
                var avatar = arch.Registry.Get(arch.Board.AvatarUid.Value);
                avatar.Stats.SetBase(StatId.Attack, 1);

                var counterProjected = false;
                var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var counterPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new AttackIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    hitPresent,
                    boardPresent,
                    counterPresent,
                    onCounterBatchProjected: (start, slot, attackerUid, projection) =>
                    {
                        counterProjected = true;
                    });

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(
                        new SubmitAttackIntentCommand(sAdjacentSlot.Index)));

                    runtime.Tick(); // resolve hit
                    Assert.AreEqual(1, arch.Sync.ActiveBatchId);
                    runtime.Tick(); // present hit
                    Assert.AreEqual(1, hitPresent.BeginCount);

                    runtime.Tick(); // branch → enqueue counter
                    Assert.IsTrue(runtime.MainlineBusy.Value);
                    Assert.AreEqual(0, boardPresent.BeginCount);

                    runtime.Tick(); // resolve counter
                    Assert.IsTrue(counterProjected);
                    Assert.AreEqual(0, counterPresent.BeginCount);

                    runtime.Tick(); // present counter
                    Assert.AreEqual(1, counterPresent.BeginCount);
                    Assert.AreEqual(0, boardPresent.BeginCount);

                    runtime.TickUntilIdle();
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                }
            }
        }

        [Test]
        public void Command_IllegalAttack_RejectsWithoutSubmitting()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
                arch.PlaceSoleBoardCardAt(sFarCornerSlot);

                var hitPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var boardPresent = new RecordingPresentChannel(ticksUntilComplete: 1);
                var factory = new AttackIntentScriptFactory(
                    arch.Architecture,
                    arch.Dispatcher,
                    hitPresent,
                    boardPresent);

                using (var runtime = PresentationRuntimeFixture.Install(arch, factory))
                {
                    Assert.IsFalse(arch.Architecture.SendCommand(
                        new SubmitAttackIntentCommand(sFarCornerSlot.Index)));
                    Assert.IsFalse(runtime.MainlineBusy.Value);
                    Assert.AreEqual(0, hitPresent.BeginCount);
                }
            }
        }

        [Test]
        public void Command_BusyBuffersEarliestLegalAttack_ThirdRejected()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateThreeMonsterNode()).Accepted);
                PlaceMonstersAt(arch, SlotId.Board(2), SlotId.Board(4), SlotId.Board(6));

                var scriptFactory = new RecordingScriptFactory(continueTicks: 2);
                using (var runtime = PresentationRuntimeFixture.Install(
                           arch,
                           scriptFactory,
                           out RecordingUiPickSink uiPick))
                {
                    Assert.IsTrue(arch.Architecture.SendCommand(new SubmitAttackIntentCommand(2)));
                    Assert.IsTrue(runtime.MainlineBusy.Value);

                    Assert.IsTrue(arch.Architecture.SendCommand(new SubmitAttackIntentCommand(4)));
                    Assert.AreEqual(1, uiPick.Previews.Count);
                    Assert.AreEqual(4, uiPick.Previews[0].TargetId);

                    Assert.IsFalse(arch.Architecture.SendCommand(new SubmitAttackIntentCommand(6)));
                    Assert.AreEqual(1, scriptFactory.Built.Count);
                    Assert.AreEqual(2, scriptFactory.Built[0].TargetId);
                    Assert.AreEqual(InputIntentKinds.Attack, scriptFactory.Built[0].Kind);

                    runtime.TickUntilIdle();
                    Assert.AreEqual(2, scriptFactory.Built.Count);
                    Assert.AreEqual(4, scriptFactory.Built[1].TargetId);
                }
            }
        }

        private static void PlaceMonstersAt(
            PresentationArchitectureFixture arch,
            params SlotId[] targets)
        {
            var monsters = new System.Collections.Generic.List<CardInstance>(targets.Length);
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var boardSlot = SlotId.Board(i);
                if (boardSlot == arch.Board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = arch.Board.GetCardUid(boardSlot);
                if (uid == 0)
                {
                    continue;
                }

                monsters.Add(arch.Registry.Get(uid));
            }

            Assert.GreaterOrEqual(monsters.Count, targets.Length);
            for (var i = 0; i < monsters.Count; i++)
            {
                arch.Board.ClearSlot(monsters[i].Slot.Value);
            }

            for (var i = 0; i < targets.Length; i++)
            {
                arch.Board.PlaceCard(monsters[i], targets[i]);
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

        private static NodeDeckOptions CreateThreeMonsterNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 3
            }
                .AddEnemyCard(new CardDraft("monster.a", CardKind.Monster) { MaxHp = 5, Attack = 1 })
                .AddEnemyCard(new CardDraft("monster.b", CardKind.Monster) { MaxHp = 5, Attack = 1 })
                .AddEnemyCard(new CardDraft("monster.c", CardKind.Monster) { MaxHp = 5, Attack = 1 });
        }
    }
}
