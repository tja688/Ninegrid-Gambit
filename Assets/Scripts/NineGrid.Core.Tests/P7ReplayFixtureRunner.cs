using System.Collections.Generic;
using System;
using NineGrid.Core.Commands;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class ReplayFixture
    {
        private readonly List<IReplayStep> mSteps = new List<IReplayStep>();

        public ReplayFixture(ulong seed)
        {
            Seed = seed;
        }

        public ulong Seed { get; private set; }
        public Action<IArchitecture> Bootstrap { get; private set; }
        public IReadOnlyList<IReplayStep> Steps
        {
            get { return mSteps; }
        }

        public ReplayFixture WithBootstrap(Action<IArchitecture> bootstrap)
        {
            Bootstrap = bootstrap;
            return this;
        }

        public ReplayFixture Add(IReplayStep step)
        {
            mSteps.Add(step);
            return this;
        }
    }

    public interface IReplayStep
    {
        CoreCommandResult Execute(IArchitecture architecture);
    }

    public sealed class StartNodeReplayStep : IReplayStep
    {
        private readonly NodeDeckOptions mOptions;

        public StartNodeReplayStep(NodeDeckOptions options)
        {
            mOptions = options;
        }

        public CoreCommandResult Execute(IArchitecture architecture)
        {
            return architecture.SendCommand(new StartNodeCommand(mOptions));
        }
    }

    public sealed class AttackFirstMonsterReplayStep : IReplayStep
    {
        public CoreCommandResult Execute(IArchitecture architecture)
        {
            var registry = architecture.GetModel<CardRegistry>();
            var board = architecture.GetModel<BoardModel>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid != 0 && registry.Get(uid).Kind == CardKind.Monster)
                {
                    return architecture.SendCommand(new AttackCommand(slot));
                }
            }

            Assert.Fail("Replay could not find a monster to attack.");
            return CoreCommandResult.Reject("No monster.");
        }
    }

    public sealed class SkipHelpChoiceReplayStep : IReplayStep
    {
        public CoreCommandResult Execute(IArchitecture architecture)
        {
            return architecture.SendCommand(new SkipHelpChoiceCommand());
        }
    }

    public sealed class SelectRoomReplayStep : IReplayStep
    {
        private readonly int mOptionIndex;

        public SelectRoomReplayStep(int optionIndex)
        {
            mOptionIndex = optionIndex;
        }

        public CoreCommandResult Execute(IArchitecture architecture)
        {
            return architecture.SendCommand(new SelectRoomCommand(mOptionIndex));
        }
    }

    public sealed class EnterRoomReplayStep : IReplayStep
    {
        public CoreCommandResult Execute(IArchitecture architecture)
        {
            return architecture.SendCommand(new EnterRoomCommand());
        }
    }

    public sealed class ReplayFixtureResult
    {
        public ReplayFixtureResult(int acceptedSteps, CoreViewSnapshot snapshot, IReadOnlyList<ActionLogRow> actionLogRows)
        {
            AcceptedSteps = acceptedSteps;
            Snapshot = snapshot;
            ActionLogRows = actionLogRows;
        }

        public int AcceptedSteps { get; private set; }
        public CoreViewSnapshot Snapshot { get; private set; }
        public IReadOnlyList<ActionLogRow> ActionLogRows { get; private set; }
    }

    public static class ReplayFixtureRunner
    {
        public static ReplayFixtureResult Run(ReplayFixture fixture)
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(
                NineGridArchitecture.Current,
                new InitialGameOptions { Seed = fixture.Seed });

            var architecture = NineGridArchitecture.Current;
            if (fixture.Bootstrap != null)
            {
                fixture.Bootstrap(architecture);
            }

            var accepted = 0;
            for (var i = 0; i < fixture.Steps.Count; i++)
            {
                var result = fixture.Steps[i].Execute(architecture);
                Assert.IsTrue(result.Accepted, "Replay step " + i + " was rejected: " + result.Reason);
                accepted++;
            }

            return new ReplayFixtureResult(
                accepted,
                CoreViewSnapshotFactory.Capture(architecture),
                ActionLogProjector.FromEventLog(architecture.GetSystem<NineGrid.Core.Systems.IActionPipelineSystem>().EventLog));
        }
    }
}
