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
            Assert.Greater(CountFlow(plan, FlowId.PlayerAppear), 0, "StartNode should reveal avatar during opening deal.");
            Assert.Greater(CountFlow(plan, FlowId.CardDeckEntry), 0, "Opening deal should route deck entry.");
            Assert.Greater(CountFlow(plan, FlowId.CardDeal), 0, "Fill slots should route card deal.");
            Assert.IsTrue(
                ShareActionGroup(plan, FlowId.PlayerAppear, FlowId.CardDeal),
                "Player appear should play in parallel with opening card deal.");
            Assert.Less(
                FindFirstGroupIndex(plan, FlowId.CardDeckEntry),
                FindFirstGroupIndex(plan, FlowId.PlayerAppear),
                "Deck entry should finish before player appear + deal.");
        }

        [Test]
        public void StartNodeBatch_CoalescesFillEmptySlotsIntoSingleBatchedCardDeal()
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
            int cardDealSteps = CountFlow(plan, FlowId.CardDeal);
            Assert.AreEqual(1, cardDealSteps, "Eight parallel deals should coalesce into one batched CardDeal step.");

            FlowPayload batchedPayload = FindBatchedCardDealPayload(plan);
            Assert.IsNotNull(batchedPayload?.BatchedDeals);
            Assert.AreEqual(8, batchedPayload.BatchedDeals.Count);
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
            Assert.IsTrue(trace.Contains("PlayerAppear"));
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

        private static FlowPayload FindBatchedCardDealPayload(PresentationPlan plan)
        {
            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                IReadOnlyList<PlanStep> steps = plan.Groups[groupIndex].Steps;
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    PlanStep step = steps[stepIndex];
                    if (step.FlowId == FlowId.CardDeal && step.Payload?.BatchedDeals != null)
                    {
                        return step.Payload;
                    }
                }
            }

            return null;
        }

        private static bool ShareActionGroup(PresentationPlan plan, FlowId left, FlowId right)
        {
            return FindFirstGroupIndex(plan, left) == FindFirstGroupIndex(plan, right);
        }

        private static int FindFirstGroupIndex(PresentationPlan plan, FlowId flowId)
        {
            for (var groupIndex = 0; groupIndex < plan.Groups.Count; groupIndex++)
            {
                IReadOnlyList<PlanStep> steps = plan.Groups[groupIndex].Steps;
                for (var stepIndex = 0; stepIndex < steps.Count; stepIndex++)
                {
                    if (steps[stepIndex].FlowId == flowId)
                    {
                        return groupIndex;
                    }
                }
            }

            return -1;
        }
    }
}
