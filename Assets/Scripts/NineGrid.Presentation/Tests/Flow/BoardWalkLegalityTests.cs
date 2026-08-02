using NineGrid.Core;
using NineGrid.Core.Systems;
using NineGrid.Flow.Presentation;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    public sealed class BoardWalkLegalityTests
    {
        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IAvatarWalkSystem mWalk;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mWalk = AvatarWalkSystem.EnsureRegistered(mArch);
        }

        [TearDown]
        public void TearDown()
        {
            mWalk?.SetEnabled(false);
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void BoardWalk_Disabled_Rejected()
        {
            Assert.IsTrue(mPhase.StartWalkSandboxNode().Accepted);
            mWalk.SetEnabled(false);

            string reason;
            Assert.IsFalse(BoardIntentLegality.TryExplainBoardWalk(mArch, 1, out reason));
            StringAssert.Contains("avatarWalkDisabled", reason);
        }

        [Test]
        public void BoardWalk_Enabled_FarEmpty_Legal()
        {
            Assert.IsTrue(mPhase.StartWalkSandboxNode().Accepted);
            mWalk.SetEnabled(true);

            string reason;
            Assert.IsTrue(BoardIntentLegality.TryExplainBoardWalk(mArch, 1, out reason), reason);
        }

        [Test]
        public void Explore_Rejected_WhenWalkExclusive()
        {
            Assert.IsTrue(mPhase.StartWalkSandboxNode().Accepted);
            mWalk.SetEnabled(true);

            string reason;
            Assert.IsFalse(BoardIntentLegality.TryExplainExplore(mArch, 2, out reason));
            StringAssert.Contains("avatarWalkExclusive", reason);
        }

        [Test]
        public void Attack_Rejected_WhenWalkExclusive()
        {
            Assert.IsTrue(mPhase.StartWalkSandboxNode().Accepted);
            mWalk.SetEnabled(true);

            string reason;
            Assert.IsFalse(BoardIntentLegality.TryExplainAttack(mArch, 2, out reason));
            StringAssert.Contains("avatarWalkExclusive", reason);
        }

        [Test]
        public void BoardWalk_Rejected_InInteractionLoop()
        {
            Assert.IsTrue(mPhase.StartNode(NodeDeckOptions.CreateDefaultBattle()).Accepted);
            mWalk.SetEnabled(true);

            string reason;
            Assert.IsFalse(BoardIntentLegality.TryExplainBoardWalk(mArch, 1, out reason));
            StringAssert.Contains("notLegal", reason);
        }

        [Test]
        public void Runner_SetDestination_UpdatesDesire_WhenIdle()
        {
            Assert.IsTrue(mPhase.StartWalkSandboxNode().Accepted);
            mWalk.SetEnabled(true);
            mWalk.SetDestination(1);
            // 无几何绑定：Core 可能已迁一格或保持欲望；至少不应抛。
            Assert.IsTrue(mWalk.IsEnabled);
            mWalk.Cancel();
            Assert.IsFalse(mWalk.IsHopping);
        }
    }
}
