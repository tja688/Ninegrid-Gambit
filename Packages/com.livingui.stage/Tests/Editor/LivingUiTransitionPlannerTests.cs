using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.LivingUI.Tests
{
    public sealed class LivingUiTransitionPlannerTests
    {
        private static readonly Rect Stage = Rect.MinMaxRect(-8f, -5f, 8f, 5f);

        [Test]
        public void Plan_HitsAuthoritativeTerminalExactly()
        {
            var planner = new TransitionPlanner();
            var plan = planner.Plan(States(State(1, Vector2.zero, Vector2.one)), Layout(1, new Vector2(4f, 2f), new Vector2(3f, 2f)), Stage, Style());
            var terminal = plan.GetProgram(1).Sample(plan.Makespan);

            Assert.AreEqual(new Vector2(4f, 2f), terminal.Position);
            Assert.AreEqual(new Vector2(3f, 2f), terminal.Size);
            Assert.AreEqual(Vector2.zero, terminal.Velocity);
        }

        [Test]
        public void Plan_IncrementsGenerationAndPlayerDropsStalePlan()
        {
            var planner = new TransitionPlanner();
            var first = planner.Plan(States(State(1, Vector2.zero, Vector2.one)), Layout(1, Vector2.one, Vector2.one), Stage, Style());
            var second = planner.Plan(States(State(1, Vector2.zero, Vector2.one)), Layout(1, Vector2.right, Vector2.one), Stage, Style());
            var player = new LivingUiTransitionPlayer();

            Assert.Greater(second.Generation, first.Generation);
            Assert.IsTrue(player.TryBegin(second));
            Assert.IsFalse(player.TryBegin(first));
            Assert.AreEqual(second.Generation, player.ActivePlan.Generation);
        }

        [Test]
        public void Redirect_PreservesLivePositionAndVelocity()
        {
            var planner = new TransitionPlanner();
            var style = Style();
            style.CanonSpan = 0f;
            style.ExpelledLead = 0f;
            var first = planner.Plan(States(State(1, Vector2.zero, new Vector2(2f, 1f))), Layout(1, new Vector2(5f, 3f), new Vector2(2f, 2f)), Stage, style);
            var live = first.GetProgram(1).Sample(first.Makespan * 0.35f);
            var redirected = planner.Plan(States(live), Layout(1, new Vector2(-3f, 2f), new Vector2(2f, 2f)), Stage, style);
            var redirectedStart = redirected.GetProgram(1).Sample(0f);

            Assert.That(Vector2.Distance(live.Position, redirectedStart.Position), Is.LessThan(0.0001f));
            Assert.That(Vector2.Distance(live.Velocity, redirectedStart.Velocity), Is.LessThan(0.001f));
        }

        [Test]
        public void SizeMotion_NeverCrossesFlowFloor()
        {
            var planner = new TransitionPlanner();
            var style = Style();
            style.FlowWidthFloor = 0.4f;
            style.FlowHeightFloor = 0.3f;
            var source = new LivingUiCarrierState(1, Vector2.zero, Vector2.zero, new Vector2(2f, 1.5f), new Vector2(-8f, -5f));
            var plan = planner.Plan(States(source), Layout(1, Vector2.one, new Vector2(0.8f, 0.6f)), Stage, style);
            var program = plan.GetProgram(1);

            for (var index = 0; index <= 100; index++)
            {
                var sample = program.Sample(program.EndTime * index / 100f);
                Assert.GreaterOrEqual(sample.Size.x, style.FlowWidthFloor - 0.0001f);
                Assert.GreaterOrEqual(sample.Size.y, style.FlowHeightFloor - 0.0001f);
            }
        }

        [Test]
        public void ExpelledCarrier_MovesOutWithoutChangingSize()
        {
            var planner = new TransitionPlanner();
            var sourceSize = new Vector2(2.4f, 1.2f);
            var plan = planner.Plan(States(State(1, Vector2.zero, sourceSize)), Layout(1, new Vector2(12f, 0f), new Vector2(8f, 8f)), Stage, Style());
            var program = plan.GetProgram(1);

            Assert.IsTrue(program.IsExpelled);
            for (var index = 0; index <= 20; index++)
            {
                Assert.AreEqual(sourceSize, program.Sample(program.EndTime * index / 20f).Size);
            }
        }

        [Test]
        public void CanonProgramsOverlapInsteadOfBecomingFullySerial()
        {
            var planner = new TransitionPlanner();
            var states = States(
                State(1, new Vector2(-4f, 0f), Vector2.one),
                State(2, Vector2.zero, Vector2.one),
                State(3, new Vector2(4f, 0f), Vector2.one));
            var terminals = new Dictionary<int, LivingUiTerminal>
            {
                [1] = new LivingUiTerminal(1, new Vector2(-3f, 2f), Vector2.one, 0, 0),
                [2] = new LivingUiTerminal(2, new Vector2(0f, 2f), Vector2.one, 0, 0),
                [3] = new LivingUiTerminal(3, new Vector2(3f, 2f), Vector2.one, 0, 0),
            };
            var plan = planner.Plan(states, new LivingUiLayout(LivingUiLayoutId.Room, terminals), Stage, Style());
            var sumDuration = 0f;
            foreach (var program in plan.Programs.Values) sumDuration += program.Duration;

            Assert.Less(plan.Makespan, sumDuration * 0.65f);
        }

        private static LivingUiTransitionStyle Style()
        {
            return new LivingUiTransitionStyle
            {
                BaseDuration = 0.5f,
                DistanceSecondsPerUnit = 0.01f,
                MaximumDistanceAddition = 0.1f,
                CanonSpan = 0.12f,
                ExpelledLead = 0.04f,
                EnteringDelay = 0.06f,
                FlowWidthFloor = 0.2f,
                FlowHeightFloor = 0.2f,
                SizeCeilingMultiplier = 4f,
            };
        }

        private static LivingUiCarrierState State(int id, Vector2 position, Vector2 size)
        {
            return new LivingUiCarrierState(id, position, Vector2.zero, size, Vector2.zero);
        }

        private static IReadOnlyList<LivingUiCarrierState> States(params LivingUiCarrierState[] states) => states;

        private static LivingUiLayout Layout(int id, Vector2 position, Vector2 size)
        {
            return new LivingUiLayout(
                LivingUiLayoutId.Route,
                new Dictionary<int, LivingUiTerminal>
                {
                    [id] = new LivingUiTerminal(id, position, size, 0, 0),
                });
        }
    }
}
