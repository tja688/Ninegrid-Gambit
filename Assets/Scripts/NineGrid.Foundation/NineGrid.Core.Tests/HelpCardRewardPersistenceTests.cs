using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// 通关三选一 / 玩家侧 run 卡组跨节点持久化契约。
    /// </summary>
    public sealed class HelpCardRewardPersistenceTests
    {
        private const string RewardDefId = "help.gold_card";
        private static readonly SlotId sAdjacentSlot = SlotId.Board(2);

        private IArchitecture mArch;
        private IPhaseSystem mPhase;
        private IRewardSystem mReward;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, BuildTestCatalog());
            InitialGameFactory.Create(mArch, new InitialGameOptions { Seed = 11UL });
            mPhase = mArch.GetSystem<IPhaseSystem>();
            mReward = mArch.GetSystem<IRewardSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Bootstrap_SeedsProfessionInitialCardsOnce()
        {
            var player = mArch.GetModel<PlayerModel>();
            Assert.AreEqual(8, player.TotalHelpCardCount(), "小丑开局玩家侧卡组应为 8 张");
        }

        [Test]
        public void BuildNodeDeckOptions_DoesNotDuplicateInitialCardsWithoutReturn()
        {
            var player = mArch.GetModel<PlayerModel>();
            Assert.AreEqual(8, player.TotalHelpCardCount());

            var first = mReward.BuildNodeDeckOptions(0, null);
            Assert.AreEqual(8, first.PlayerCards.Count);
            Assert.AreEqual(0, player.TotalHelpCardCount(), "汇入节点后 run 卡组应被消耗");

            var second = mReward.BuildNodeDeckOptions(1, null);
            Assert.AreEqual(0, second.PlayerCards.Count, "未回库时不应重复注入职业初始牌");
        }

        [Test]
        public void CompleteNodeIfCleared_GoesToRoomChoice_WithoutHelpChoicePersistPath()
        {
            Assert.IsTrue(mPhase.StartNode(CreateSingleMonsterNode(hp: 1, attack: 0)).Accepted);
            PlaceSoleBoardCardAt(sAdjacentSlot);
            Assert.IsTrue(mPhase.Attack(sAdjacentSlot).Accepted);
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.AreEqual(PendingChoiceKind.Room, mArch.GetModel<PendingChoiceModel>().Kind.Value);

            AdvancePastRoomChoice();

            var options = mReward.BuildNodeDeckOptions(1, null);
            options.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = 20, Attack = 0 });
            options.EnemyOpeningCount = 1;
            Assert.IsTrue(mPhase.StartNode(options).Accepted);
            Assert.AreEqual(GamePhase.InteractionLoop, mPhase.CurrentPhase);
        }

        private void AdvancePastRoomChoice()
        {
            Assert.AreEqual(GamePhase.RoomChoice, mPhase.CurrentPhase);
            Assert.IsTrue(mPhase.SelectRoom(0).Accepted);
            Assert.AreEqual(GamePhase.RoomEvent, mPhase.CurrentPhase);
            Assert.IsTrue(mPhase.EnterRoom().Accepted);
            Assert.AreEqual(GamePhase.NodeCompleted, mPhase.CurrentPhase);
        }

        private int FindRewardOptionIndex(string defId)
        {
            var pending = mArch.GetModel<PendingChoiceModel>();
            for (var i = 0; i < pending.RewardOptions.Count; i++)
            {
                if (pending.RewardOptions[i].DefId == defId)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool HasHelpCardInDeck(string defId)
        {
            var registry = mArch.GetModel<CardRegistry>();
            var deck = mArch.GetModel<DeckModel>();
            var board = mArch.GetModel<BoardModel>();
            if (ContainsDef(registry, deck.DrawPileUids, defId)
                || ContainsDef(registry, deck.PlayerCardPoolUids, defId)
                || ContainsDef(registry, deck.ItemSlotUids, defId))
            {
                return true;
            }

            foreach (var uid in board.BoardCardUids())
            {
                CardInstance card;
                if (registry.TryGet(uid, out card) && card != null && card.DefId == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsDef(
            CardRegistry registry,
            System.Collections.Generic.IReadOnlyList<int> uids,
            string defId)
        {
            for (var i = 0; i < uids.Count; i++)
            {
                CardInstance card;
                if (registry.TryGet(uids[i], out card)
                    && card != null
                    && card.DefId == defId)
                {
                    return true;
                }
            }

            return false;
        }

        private static NodeDeckOptions CreateSingleMonsterNode(int hp, int attack)
        {
            return new NodeDeckOptions
            {
                PlayerOpeningCount = 0,
                EnemyOpeningCount = 1
            }.AddEnemyCard(new CardDraft("monster.test", CardKind.Monster) { MaxHp = hp, Attack = attack });
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

        private static GameContentCatalog BuildTestCatalog()
        {
            var catalog = new GameContentCatalog();
            catalog.Economy.SkipHelpChoiceGold = 10;
            catalog.Economy.UnusedHelpCardGold = 0;
            catalog.Economy.MonsterRemovedGold = 5;

            catalog.AddCard(new CardContentDefinition("help.healing_potion", "恢复药水", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.common_chest_card", "普通宝箱", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.throwing_knife", "飞刀", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.stat_boost_card", "属性提升", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("help.gold_card", "金币卡", CardKind.HelpCard));
            catalog.AddCard(new CardContentDefinition("monster.test", "测试怪", CardKind.Monster));

            catalog.Rewards
                .AddPool(new RewardPoolDefinition("help.choice", 1)
                    .Add(RewardDefId, CardKind.HelpCard, 1))
                .AddRoom(new RoomDefinition(RoomKind.Battle, "战斗房") { Weight = 1 })
                .AddRoom(new RoomDefinition(RoomKind.Gold, "金币房")
                {
                    Weight = 1,
                    GoldDelta = 5
                });

            return catalog;
        }
    }
}
