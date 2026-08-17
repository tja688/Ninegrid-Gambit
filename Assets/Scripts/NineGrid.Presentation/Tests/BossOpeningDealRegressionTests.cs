using System.Linq;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NineGrid.Presentation.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 一键跨层壳层节点错位 → 层主房按节点 1 组敌池、无门软锁（2026-08-17）回归。
    /// </summary>
    public class BossOpeningDealRegressionTests
    {
        private const string InsectDeckId = "deck.insect";
        private const string InsectBossDefId = "monster.rogue";

        private IArchitecture mArch;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Interface;
            var content = mArch.GetSystem<IContentSystem>();
            content.Load(NineGrid.Content.ContentCatalogBootstrap.Load());
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, content.Catalog);
            Assert.IsTrue(content.HasCatalog, "需要真实内容目录");
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void SyncShellNodeIndexForCheat_ThenIncrement_LandsOnFloorFirstBattleGlobalIndex()
        {
            var shell = GameFlowShellSystem.EnsureRegistered(mArch);
            shell.SyncShellNodeIndexForCheat(floor: 2, nodeIndex: 0);
            shell.IncrementNodeIndex();

            Assert.AreEqual(9, shell.NodeIndex, "第 2 层第 1 战全局序应为 9");
        }

        [Test]
        public void ResolveFormalBattleContentNodeIndex_UsesCoreFloorAndNode_NotShellDrift()
        {
            var run = mArch.GetModel<RunModel>();
            run.Floor.Value = 2;
            run.NodeIndex.Value = 7;

            var contentNodeIndex = GameFlowShellSystem.ResolveFormalBattleContentNodeIndex(run);

            Assert.AreEqual(16, contentNodeIndex, "层 2 展示节点 8 → 全局序 16");
        }

        [Test]
        public void BuildNodeDeckOptions_BossNode_IncludesSeq5BossAndRequireElite()
        {
            BindBossRoom(InsectDeckId);
            var contentNodeIndex = GameFlowShellSystem.ResolveFormalBattleContentNodeIndex(mArch.GetModel<RunModel>());

            var options = mArch.GetSystem<IRewardSystem>().BuildNodeDeckOptions(contentNodeIndex, InsectDeckId);

            Assert.IsTrue(options.RequireElite, "节点 8 规则 seq5>0 须 RequireElite");
            Assert.IsTrue(
                options.EnemyCards.Any(d => d != null && d.DefId == InsectBossDefId && d.IsBoss),
                "召唤卡组层主须编入敌池");
            Assert.IsFalse(
                options.EnemyCards.Any(d => d != null && d.DefId == RegularTrapPool.LeaveTrapDefId),
                "正常层主房开局不编入离开机关");
        }

        [Test]
        public void BuildNodeDeckOptions_BossRoomWithoutOpeningElite_AppendsLeaveTrapGuard()
        {
            BindBossRoom(InsectDeckId);
            const int wrongShellGlobalIndex = 17;

            var options = mArch.GetSystem<IRewardSystem>().BuildNodeDeckOptions(
                wrongShellGlobalIndex,
                InsectDeckId);

            Assert.IsFalse(options.RequireElite, "错位全局序 17 会落到节点 1 规则");
            Assert.IsFalse(
                options.EnemyCards.Any(d => d != null && d.DefId == InsectBossDefId),
                "错位规则不应编入层主");
            Assert.IsTrue(
                options.EnemyCards.Any(d => d != null && d.DefId == RegularTrapPool.LeaveTrapDefId),
                "Boss 房缺层主时须护栏编入离开机关，避免无门软锁");
        }

        private void BindBossRoom(string deckId)
        {
            var run = mArch.GetModel<RunModel>();
            run.Floor.Value = 2;
            run.NodeIndex.Value = 7;
            run.Room.Value = RoomKind.Boss;
            Assert.IsTrue(run.TryBindFloorMonsterDeck(deckId), "须绑定本层主题卡组");
        }
    }
}
