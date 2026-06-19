using NineGrid.Core.Commands;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class P7PresentationContractTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void EveryCoreEventHasPresentationMapping()
        {
            var missing = PresentationEventMap.FindMissingCoreEvents();

            Assert.AreEqual(0, missing.Count, string.Join(",", missing));
            Assert.AreEqual(PresentationInstructionKind.ShowDamage, PresentationEventMap.Get(CoreEventType.DamageDealt).InstructionKind);
            Assert.AreEqual(PresentationInstructionKind.MoveCard, PresentationEventMap.Get(CoreEventType.CardMoved).InstructionKind);
            Assert.AreEqual(PresentationInstructionKind.RotateBoard, PresentationEventMap.Get(CoreEventType.BoardRotated).InstructionKind);
            Assert.AreEqual(PresentationInstructionKind.KillCard, PresentationEventMap.Get(CoreEventType.CardKilled).InstructionKind);
            Assert.AreEqual(PresentationInstructionKind.ChangePhase, PresentationEventMap.Get(CoreEventType.PhaseChanged).InstructionKind);
            Assert.AreEqual(PresentationInstructionKind.OfferReward, PresentationEventMap.Get(CoreEventType.RewardOffered).InstructionKind);
        }

        [Test]
        public void BatchPlaybackLocksInputUntilPresentationFinishedCommand()
        {
            var architecture = NineGridArchitecture.Current;
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var sync = architecture.GetSystem<IPresentationSyncSystem>();

            pipeline.Execute(new DealDamageAction(
                architecture.GetModel<BoardModel>().AvatarUid.Value,
                architecture.GetModel<BoardModel>().AvatarUid.Value,
                1));

            var batch = PresentationBatchFactory.FromEventLog(
                pipeline.EventLog,
                0,
                7,
                CoreViewSnapshotFactory.Capture(architecture));
            sync.OpenBatch(batch);

            Assert.IsTrue(sync.IsInputLocked);
            Assert.AreEqual(7, sync.ActiveBatchId);
            Assert.IsFalse(architecture.GetSystem<IPhaseSystem>().CanExecute(GameCommandKind.Attack));
            Assert.IsTrue(architecture.GetSystem<IPhaseSystem>().CanExecute(GameCommandKind.PresentationFinished));

            var wrong = architecture.SendCommand(new PresentationFinishedCommand(8));
            Assert.IsFalse(wrong.Accepted);
            Assert.IsTrue(sync.IsInputLocked);

            var finished = architecture.SendCommand(new PresentationFinishedCommand(7));
            Assert.IsTrue(finished.Accepted);
            Assert.IsFalse(sync.IsInputLocked);
        }

        [Test]
        public void MinimalViewSnapshotExposesBoardAndPendingChoice()
        {
            var architecture = NineGridArchitecture.Current;
            P5CatalogTestSupport.RegisterCatalog(architecture);
            var phaseSystem = architecture.GetSystem<IPhaseSystem>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.snapshot", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0
                });

            architecture.SendCommand(new StartNodeCommand(options));
            architecture.SendCommand(new AttackCommand(FindFirstMonsterSlot()));

            var snapshot = CoreViewSnapshotFactory.Capture(architecture);

            Assert.AreEqual(GamePhase.RewardItemChoice, phaseSystem.CurrentPhase);
            Assert.AreEqual(GamePhase.RewardItemChoice, snapshot.Phase);
            Assert.AreEqual(PendingChoiceKind.Reward, snapshot.PendingChoiceKind);
            Assert.AreEqual(9, snapshot.BoardSlots.Count);
            Assert.AreEqual(3, snapshot.RewardOptions.Count);
            Assert.AreEqual(architecture.GetModel<BoardModel>().AvatarUid.Value, snapshot.AvatarUid);
        }

        [Test]
        public void ActionLogAndStubAdapterCanInspectRealReplayEvents()
        {
            var architecture = NineGridArchitecture.Current;
            var pipeline = architecture.GetSystem<IActionPipelineSystem>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.log", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0
                });

            architecture.SendCommand(new StartNodeCommand(options));
            var attackResult = architecture.SendCommand(new AttackCommand(FindFirstMonsterSlot()));
            Assert.IsTrue(attackResult.Accepted);

            var rows = ActionLogProjector.FromEventLog(pipeline.EventLog);
            var batch = PresentationBatchFactory.FromEventLog(
                pipeline.EventLog,
                0,
                1,
                CoreViewSnapshotFactory.Capture(architecture));
            var adapter = new RecordingPresentationAdapter();
            var playback = adapter.Play(batch);

            Assert.Greater(rows.Count, 0);
            Assert.Greater(playback.InstructionCount, 0);
            Assert.IsTrue(ContainsInstruction(rows, PresentationInstructionKind.ShowDamage));
            Assert.IsTrue(ContainsInstruction(rows, PresentationInstructionKind.KillCard));
            Assert.IsTrue(ContainsInstruction(rows, PresentationInstructionKind.OfferReward));
            Assert.Greater(adapter.Lines.Count, 1);
        }

        [Test]
        public void LocalReplayFixtureRunnerExecutesNodeTailInCoreTests()
        {
            var fixture = new ReplayFixture(11)
                .WithBootstrap(P5CatalogTestSupport.RegisterCatalog)
                .Add(new StartNodeReplayStep(new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                    .AddEnemyCard(new CardDraft("monster.fixture", CardKind.Monster)
                    {
                        MaxHp = 1,
                        Attack = 0
                    })))
                .Add(new AttackFirstMonsterReplayStep())
                .Add(new SkipHelpChoiceReplayStep())
                .Add(new SelectRoomReplayStep(0))
                .Add(new EnterRoomReplayStep());

            var result = ReplayFixtureRunner.Run(fixture);

            Assert.AreEqual(5, result.AcceptedSteps);
            Assert.AreEqual(GamePhase.NodeCompleted, result.Snapshot.Phase);
            Assert.AreEqual(1, result.Snapshot.NodeIndex);
            Assert.IsTrue(ContainsInstruction(result.ActionLogRows, PresentationInstructionKind.AdvanceNode));
        }

        private static bool ContainsInstruction(System.Collections.Generic.IReadOnlyList<ActionLogRow> rows, PresentationInstructionKind kind)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].InstructionKind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static SlotId FindFirstMonsterSlot()
        {
            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var board = NineGridArchitecture.Current.GetModel<BoardModel>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid != 0 && registry.Get(uid).Kind == CardKind.Monster)
                {
                    return slot;
                }
            }

            Assert.Fail("Could not find monster slot.");
            return SlotId.None;
        }
    }
}
