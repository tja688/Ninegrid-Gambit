using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 嘲讽 AttackTargetRestriction：ResolvePlayerAttackTargetUid 与 ApplyCombatHit 防御。
    /// </summary>
    public sealed class TauntRedirectRegressionTests
    {
        private static readonly SlotId sTaunterSlot = SlotId.Board(4);
        private static readonly SlotId sOtherMonsterSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 13UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void ResolvePlayerAttackTargetUid_AdjacentTaunter_RedirectsToTaunter()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.skeleton_taunter", sTaunterSlot);
            SpawnOnBoard("monster.headless_skeleton", sOtherMonsterSlot);

            var board = mArch.GetModel<BoardModel>();
            var otherUid = board.GetCardUid(sOtherMonsterSlot);
            var taunterUid = board.GetCardUid(sTaunterSlot);
            Assert.Greater(otherUid, 0);
            Assert.Greater(taunterUid, 0);

            Assert.AreEqual(taunterUid, mPhase.ResolvePlayerAttackTargetUid(otherUid));
        }

        [Test]
        public void ResolvePlayerAttackTargetUid_ClickTaunter_ReturnsSelf()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.skeleton_taunter", sTaunterSlot);
            SpawnOnBoard("monster.headless_skeleton", sOtherMonsterSlot);

            var taunterUid = mArch.GetModel<BoardModel>().GetCardUid(sTaunterSlot);
            Assert.AreEqual(taunterUid, mPhase.ResolvePlayerAttackTargetUid(taunterUid));
        }

        [Test]
        public void ResolvePlayerAttackTargetUid_NoTaunter_ReturnsIntended()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.headless_skeleton", sOtherMonsterSlot);

            var otherUid = mArch.GetModel<BoardModel>().GetCardUid(sOtherMonsterSlot);
            Assert.AreEqual(otherUid, mPhase.ResolvePlayerAttackTargetUid(otherUid));
        }

        [Test]
        public void ApplyCombatHit_WrongTargetWhileTauntActive_Rejects()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.skeleton_taunter", sTaunterSlot);
            SpawnOnBoard("monster.headless_skeleton", sOtherMonsterSlot);

            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            var otherUid = board.GetCardUid(sOtherMonsterSlot);

            var hit = mPhase.ApplyCombatHit(avatarUid, otherUid);
            Assert.IsFalse(hit.Accepted);
            Assert.IsTrue(hit.Reason.Contains("taunt"), hit.Reason);
        }

        [Test]
        public void ApplyCombatHit_TauntTarget_Accepts()
        {
            Assert.IsTrue(mPhase.StartNode(CreateEmptyEnemyNode()).Accepted);
            SpawnOnBoard("monster.skeleton_taunter", sTaunterSlot);
            SpawnOnBoard("monster.headless_skeleton", sOtherMonsterSlot);

            var board = mArch.GetModel<BoardModel>();
            var avatarUid = board.AvatarUid.Value;
            var taunterUid = board.GetCardUid(sTaunterSlot);

            var hit = mPhase.ApplyCombatHit(avatarUid, taunterUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);
        }

        private static NodeDeckOptions CreateEmptyEnemyNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 0
            };
        }

        private void SpawnOnBoard(string defId, SlotId slot)
        {
            mPipeline.Enqueue(new SpawnCardAction(defId, CardKind.Monster, ZoneId.Board, slot, 1, "test"));
            Assert.Greater(mPipeline.RunToCompletion(), 0, "应成功生成 " + defId);
            Assert.AreEqual(defId, mArch.GetModel<CardRegistry>().Get(mArch.GetModel<BoardModel>().GetCardUid(slot)).DefId);
        }
    }
}
