using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    public sealed class P0InfrastructureTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ArchitectureBootstrapsAndResolvesCoreServices()
        {
            var architecture = NineGridArchitecture.Current;

            Assert.NotNull(architecture.GetUtility<IRngUtility>());
            Assert.NotNull(architecture.GetUtility<ILogUtility>());
            Assert.NotNull(architecture.GetUtility<IConfigUtility>());
            Assert.NotNull(architecture.GetModel<CardRegistry>());
            Assert.NotNull(architecture.GetModel<BoardModel>());
            Assert.NotNull(architecture.GetModel<DeckModel>());
            Assert.NotNull(architecture.GetModel<PlayerModel>());
            Assert.NotNull(architecture.GetModel<RunModel>());
            Assert.NotNull(architecture.GetSystem<IStatSystem>());
        }

        [Test]
        public void RngSameSeedReplaysSameSequence()
        {
            var left = new DeterministicRngUtility(20260617UL);
            var right = new DeterministicRngUtility(20260617UL);

            for (var i = 0; i < 32; i++)
            {
                Assert.AreEqual(left.NextUInt(), right.NextUInt());
            }
        }

        [Test]
        public void RngStateCanBeCapturedAndRestored()
        {
            var rng = new DeterministicRngUtility(42UL);
            rng.NextUInt();
            rng.NextUInt();

            var state = rng.CaptureState();
            var expected = rng.NextUInt();
            rng.NextUInt();
            rng.RestoreState(state);

            Assert.AreEqual(expected, rng.NextUInt());
        }

        [Test]
        public void ConfigAndLogUtilitiesUseInMemoryStubs()
        {
            var architecture = NineGridArchitecture.Current;
            var config = architecture.GetUtility<IConfigUtility>();
            var log = architecture.GetUtility<ILogUtility>();

            config.Set("avatar.hp", 30);
            log.Log(CoreLogLevel.Info, "test", "hello");

            Assert.AreEqual(30, config.Get<int>("avatar.hp"));
            Assert.AreEqual(1, log.Entries.Count);
            Assert.AreEqual("hello", log.Entries[0].Message);
        }
    }
}
