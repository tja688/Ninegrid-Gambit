using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 回归：InitialGameFactory Clear TriggerSystem 后必须重绑 Economy.removeGold，
    /// 否则击杀怪物不会发 MonsterRemovedGold。
    /// </summary>
    public sealed class EconomyKillGoldRegressionTests
    {
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IActionPipelineSystem mPipeline;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildEconomyCatalog());
            // 模拟真局：Create 会 Clear TriggerSystem，必须仍能发杀怪金币。
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mPipeline = mArch.GetSystem<IActionPipelineSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void KillMonster_WithoutGoldRewardCounter_AwardsMonsterRemovedGold()
        {
            var economyGold = mArch.GetSystem<IContentSystem>().Catalog.Economy.MonsterRemovedGold;
            Assert.AreEqual(5, economyGold);

            Assert.IsTrue(mPhase.StartNode(CreateOneHpMonsterNode()).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);

            var board = mArch.GetModel<BoardModel>();
            var player = mArch.GetModel<PlayerModel>();
            var monsterUid = board.GetCardUid(sAdjacentSlot);
            Assert.Greater(monsterUid, 0);

            var coinsBefore = player.Coins.Value;
            var startIndex = mPipeline.EventLog.Entries.Count;
            var hit = mPhase.ApplyCombatHit(board.AvatarUid.Value, monsterUid);
            Assert.IsTrue(hit.Accepted, hit.Reason);

            Assert.AreEqual(
                coinsBefore + economyGold,
                player.Coins.Value,
                "击杀无 GoldReward 计数的怪物应发 MonsterRemovedGold");

            var sawGold = false;
            var entries = mPipeline.EventLog.Entries;
            for (var i = startIndex; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Type == CoreEventType.GoldModified && e.Delta == economyGold)
                {
                    sawGold = true;
                    Assert.IsTrue(
                        (e.Message ?? string.Empty).StartsWith("remove:"),
                        "期望 reason 为 remove:<defId>，实际=" + e.Message);
                    break;
                }
            }

            Assert.IsTrue(sawGold, "EventLog 应有 GoldModified");
        }

        private static GameContentCatalog BuildEconomyCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.MonsterRemovedGold = 5;
            return catalog;
        }

        private static NodeDeckOptions CreateOneHpMonsterNode()
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster)
            {
                MaxHp = 1,
                Hp = 1,
                Attack = 0
            });
        }

        private void PlaceSoleBoardCardAt(SlotId targetSlot)
        {
            var board = mArch.GetModel<BoardModel>();
            var registry = mArch.GetModel<CardRegistry>();
            CardInstance sole = null;
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot == board.AvatarSlot.Value)
                {
                    continue;
                }

                var uid = board.GetCardUid(slot);
                if (uid == 0)
                {
                    continue;
                }

                Assert.IsNull(sole, "Expected at most one non-avatar board card.");
                sole = registry.Get(uid);
            }

            Assert.IsNotNull(sole, "No board card to relocate.");
            if (sole.Slot.Value == targetSlot)
            {
                return;
            }

            board.ClearSlot(sole.Slot.Value);
            board.PlaceCard(sole, targetSlot);
        }
    }
}
