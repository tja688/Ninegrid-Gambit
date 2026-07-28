using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Flow.Presentation;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests.Output
{
    /// <summary>
    /// #61：金币经 Settled 装饰处理器广播单向表现事件，不再扫 EventLog Command。
    /// </summary>
    public sealed class GoldGainBeatHandlerTests
    {
        [Test]
        public void Handler_PositiveGold_EmitsGoldGainPresentationRequested()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);

                var received = new List<GoldGainPresentationRequested>();
                var unreg = arch.Architecture.RegisterEvent<GoldGainPresentationRequested>(
                    e => received.Add(e));
                try
                {
                    var instruction = new PresentationInstruction(
                        new CoreGameEvent(CoreEventType.GoldModified, 1, "ModifyGold")
                            .WithDelta(5)
                            .WithAmount(9)
                            .WithMessage("test.gain"),
                        PresentationEventMap.Get(CoreEventType.GoldModified));
                    Assert.IsTrue(new GoldGainBeatHandler().TryApply(instruction));
                    Assert.AreEqual(1, received.Count);
                    Assert.AreEqual(5, received[0].Delta);
                    Assert.AreEqual(9, received[0].AmountAfter);
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
        public void Handler_NegativeGold_EmitsSpendEvent()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);

                var received = new List<GoldGainPresentationRequested>();
                var unreg = arch.Architecture.RegisterEvent<GoldGainPresentationRequested>(
                    e => received.Add(e));
                try
                {
                    var instruction = new PresentationInstruction(
                        new CoreGameEvent(CoreEventType.GoldModified, 1, "ModifyGold")
                            .WithDelta(-3)
                            .WithAmount(7)
                            .WithMessage("test.spend"),
                        PresentationEventMap.Get(CoreEventType.GoldModified));
                    Assert.IsTrue(new GoldGainBeatHandler().TryApply(instruction));
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
        public void Handler_SkipUnusedHelpCardsReason_DoesNotEmit()
        {
            using (var arch = PresentationArchitectureFixture.CreateStartedGame(seed: 42UL))
            {
                Assert.IsTrue(arch.Phase.StartNode(CreateEmptyNode()).Accepted);

                var count = 0;
                var unreg = arch.Architecture.RegisterEvent<GoldGainPresentationRequested>(_ => count++);
                try
                {
                    var instruction = new PresentationInstruction(
                        new CoreGameEvent(CoreEventType.GoldModified, 1, "ModifyGold")
                            .WithDelta(4)
                            .WithAmount(4)
                            .WithMessage(GoldGainPresentationScheduler.UnusedHelpCardsGoldReason),
                        PresentationEventMap.Get(CoreEventType.GoldModified));
                    Assert.IsTrue(new GoldGainBeatHandler().TryApply(instruction));
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
