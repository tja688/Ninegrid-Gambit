using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Output
{
    /// <summary>
    /// V9：金币反馈经 Command 扫描 EventLog，并广播单向表现事件。
    /// </summary>
    public sealed class PresentGoldGainsFromEventLogCommandTests
    {
        [Test]
        public void Command_PositiveGoldModified_EmitsGoldGainPresentationRequested()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                var start = arch.Pipeline.EventLog.Entries.Count;
                arch.Pipeline.Enqueue(new ModifyGoldAction(5, "test.gain"));
                Assert.Greater(arch.Pipeline.RunToCompletion(), 0);

                var received = new List<GoldGainPresentationRequested>();
                var unreg = arch.Architecture.RegisterEvent<GoldGainPresentationRequested>(
                    e => received.Add(e));

                try
                {
                    var count = arch.Architecture.SendCommand(
                        new PresentGoldGainsFromEventLogCommand(start));

                    Assert.AreEqual(1, count);
                    Assert.AreEqual(1, received.Count);
                    Assert.AreEqual(5, received[0].Delta);
                    Assert.Greater(received[0].AmountAfter, 0);
                    Assert.AreEqual("test.gain", received[0].Reason);
                    Assert.IsFalse(received[0].IsSpend);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        [Test]
        public void Command_NegativeGoldModified_EmitsSpendEvent()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                arch.Pipeline.Enqueue(new ModifyGoldAction(10, "seed"));
                Assert.Greater(arch.Pipeline.RunToCompletion(), 0);
                var start = arch.Pipeline.EventLog.Entries.Count;
                arch.Pipeline.Enqueue(new ModifyGoldAction(-3, "test.spend"));
                Assert.Greater(arch.Pipeline.RunToCompletion(), 0);

                var received = new List<GoldGainPresentationRequested>();
                var unreg = arch.Architecture.RegisterEvent<GoldGainPresentationRequested>(
                    e => received.Add(e));

                try
                {
                    var count = arch.Architecture.SendCommand(
                        new PresentGoldGainsFromEventLogCommand(start));

                    Assert.AreEqual(1, count);
                    Assert.AreEqual(1, received.Count);
                    Assert.AreEqual(-3, received[0].Delta);
                    Assert.IsTrue(received[0].IsSpend);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        [Test]
        public void Command_SkipUnusedHelpCardsReason_DoesNotEmit()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);
                var start = arch.Pipeline.EventLog.Entries.Count;
                arch.Pipeline.Enqueue(
                    new ModifyGoldAction(4, GoldGainPresentationScheduler.UnusedHelpCardsGoldReason));
                Assert.Greater(arch.Pipeline.RunToCompletion(), 0);

                var count = 0;
                var unreg = arch.Architecture.RegisterEvent<GoldGainPresentationRequested>(_ => count++);

                try
                {
                    var emitted = arch.Architecture.SendCommand(
                        new PresentGoldGainsFromEventLogCommand(start));

                    Assert.AreEqual(0, emitted);
                    Assert.AreEqual(0, count);
                }
                finally
                {
                    unreg.UnRegister();
                }
            }
        }

        private static NodeDeckOptions CreateEmptyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }
    }
}
