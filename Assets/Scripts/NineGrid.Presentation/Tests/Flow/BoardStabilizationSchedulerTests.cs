using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    public sealed class BoardStabilizationSchedulerTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(
                ContentConfigKeys.DefaultCatalog,
                ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 35UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void TriggerCreatedVacancy_WaitsForFirstAckBeforeResolvingSecondSlice()
        {
            Assert.IsTrue(mPhase.StartNode(new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            }).Accepted);
            SpawnBoardCard("trap.bear_trap", CardKind.Trap, 2);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 3);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 4);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 6);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 7);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 8);
            SpawnBoardCard("help.bomb", CardKind.HelpCard, 9);
            SpawnDrawCard("help.bomb", CardKind.HelpCard);
            SpawnDrawCard("help.bomb", CardKind.HelpCard);
            var eventStart = mPipeline.EventLog.Entries.Count;

            var timeline = new BattleTimeline();
            var channel = new RecordingPresentChannel(ticksUntilComplete: 1);
            new BoardStabilizationScheduler().Append(
                timeline,
                mArch,
                new CoreCommandDispatcher(mArch),
                channel,
                boardSlot: 1,
                onBoardBatchProjected: null);

            timeline.Tick(0.016f); // check -> enqueue first slice
            timeline.Tick(0.016f); // first resolve
            Assert.AreEqual(1, mArch.GetSystem<IPresentationSyncSystem>().ActiveBatchId);
            Assert.AreEqual(1, CountEventsSince(eventStart, CoreEventType.SlotsFilled));

            timeline.Tick(0.016f); // first present + ack
            Assert.AreEqual(0, mArch.GetSystem<IPresentationSyncSystem>().ActiveBatchId);
            Assert.AreEqual(1, channel.BeginCount);
            Assert.AreEqual(1, CountEventsSince(eventStart, CoreEventType.SlotsFilled));

            timeline.Tick(0.016f); // check -> enqueue second slice
            Assert.AreEqual(1, CountEventsSince(eventStart, CoreEventType.SlotsFilled));
            timeline.Tick(0.016f); // second resolve
            Assert.AreEqual(2, mArch.GetSystem<IPresentationSyncSystem>().ActiveBatchId);
            Assert.AreEqual(2, CountEventsSince(eventStart, CoreEventType.SlotsFilled));

            timeline.Tick(0.016f); // second present + ack
            timeline.Tick(0.016f); // stable check
            Assert.AreEqual(2, channel.BeginCount);
            Assert.IsFalse(timeline.IsBusy);
        }

        private void SpawnBoardCard(string defId, CardKind kind, int slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(
                defId,
                kind,
                ZoneId.Board,
                SlotId.Board(slot),
                1,
                "boardStabilization.test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private void SpawnDrawCard(string defId, CardKind kind)
        {
            mPipeline.Enqueue(new SpawnCardAction(
                defId,
                kind,
                ZoneId.DrawPile,
                SlotId.None,
                1,
                "boardStabilization.test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0);
        }

        private int CountEventsSince(int startIndex, CoreEventType type)
        {
            var count = 0;
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
