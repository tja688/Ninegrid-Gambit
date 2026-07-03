using System.Collections.Generic;
using NineGrid.Core;
using NineGrid.Presentation.Debugging.Slices;
using NineGrid.Presentation.Orchestration;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 牌组入场+发牌切片：StartNode batch plan 应含 CardDeckEntry 与 CardDeal。
    /// </summary>
    public sealed class DeckEntryDealSlicePlanTests
    {
        [Test]
        public void StartNodeBatch_WithFullEnemyDeck_ContainsDeckEntryAndDealFlows()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);

            var architecture = NineGridArchitecture.Current;
            var dispatcher = new CoreCommandDispatcher(architecture);
            var options = BuildSliceDeck(20);

            var result = dispatcher.Send(new NineGrid.Core.Commands.StartNodeCommand(options));
            Assert.IsTrue(result.Accepted);
            Assert.IsNotNull(result.Batch);

            var plan = new PerformancePlanBuilder().Build(result.Batch);
            Assert.Greater(CountFlow(plan, FlowId.CardDeckEntry), 0, "Opening deal should route deck entry.");
            Assert.Greater(CountFlow(plan, FlowId.CardDeal), 0, "Fill slots should route card deal.");
        }

        [Test]
        public void PlaybackTrace_DumpStartNodeBatch_DoesNotThrow()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);

            var dispatcher = new CoreCommandDispatcher(NineGridArchitecture.Current);
            var result = dispatcher.Send(new NineGrid.Core.Commands.StartNodeCommand(BuildSliceDeck(15)));
            Assert.IsNotNull(result.Batch);

            string trace = PlaybackTrace.Dump(result.Batch, logParallelWarnings: false);
            Assert.IsTrue(trace.Contains("CardDeckEntry"));
        }

        private static NodeDeckOptions BuildSliceDeck(int count)
        {
            var options = new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = count,
            };

            for (var i = 0; i < count; i++)
            {
                options.AddEnemyCard(new CardDraft($"slice.test.{i:D2}", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0,
                });
            }

            return options;
        }

        private static int CountFlow(PresentationPlan plan, FlowId flowId)
        {
            var total = 0;
            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                IReadOnlyList<PlanStep> steps = plan.Groups[groupIndex].Steps;
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    if (steps[stepIndex].FlowId == flowId)
                    {
                        total++;
                    }
                }
            }

            return total;
        }
    }
}
