using System.Collections.Generic;
using NineGrid.Core.Commands;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    public sealed class P7SnapshotCompletenessTests
    {
        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            InitialGameFactory.Create(NineGridArchitecture.Current);
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void Snapshot_IncludesDeckAndHandAfterPickup()
        {
            var architecture = NineGridArchitecture.Current;
            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.pickup", CardKind.Monster)
                {
                    MaxHp = 5,
                    Attack = 0
                });

            architecture.SendCommand(new StartNodeCommand(options));

            var avatarSlot = board.AvatarSlot.Value;
            var pickupSlot = FindAdjacentEmptySlot(avatarSlot, board);
            var help = registry.Create("help.pickup", CardKind.HelpCard);
            board.PlaceCard(help, pickupSlot);

            var pickupResult = architecture.SendCommand(new PickupItemCommand(pickupSlot));
            Assert.IsTrue(pickupResult.Accepted);

            var snapshot = CoreViewSnapshotFactory.Capture(architecture);

            CollectionAssert.Contains(snapshot.Deck.ItemSlotUids, help.Uid);
            Assert.IsTrue(snapshot.TryGetCard(help.Uid, out var cardView));
            Assert.AreEqual("help.pickup", cardView.DefId);
            Assert.AreEqual(CardKind.HelpCard, cardView.Kind);
            Assert.AreEqual(ZoneId.ItemSlots, cardView.Zone);
        }

        [Test]
        public void Snapshot_ExposesEffectiveStats()
        {
            var architecture = NineGridArchitecture.Current;
            var statSystem = architecture.GetSystem<IStatSystem>();
            var board = architecture.GetModel<BoardModel>();
            var registry = architecture.GetModel<CardRegistry>();
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.effective", CardKind.Monster)
                {
                    MaxHp = 5,
                    Attack = 2,
                    Armor = 1
                });

            architecture.SendCommand(new StartNodeCommand(options));

            var monsterSlot = FindFirstMonsterSlot();
            var monsterUid = board.GetCardUid(monsterSlot);
            var monster = registry.Get(monsterUid);
            statSystem.AddModifier(monster, new StatModifier(
                StatId.Attack,
                ModifierOp.Add,
                3,
                ModifierLayer.Persistent,
                new ModifierSource("test.buff"),
                ModifierScope.Permanent));

            var snapshot = CoreViewSnapshotFactory.Capture(architecture);
            var slotView = snapshot.GetSlot(monsterSlot);

            Assert.NotNull(slotView);
            Assert.AreEqual(2, slotView.Attack);
            Assert.AreEqual(5, slotView.EffectiveAttack);
            Assert.IsTrue(snapshot.TryGetCard(monsterUid, out var cardView));
            Assert.AreEqual(5, cardView.Stats.EffectiveAttack);
        }

        [Test]
        public void Snapshot_IncludesRunMetaAndPlayerRelics()
        {
            var architecture = NineGridArchitecture.Current;
            var run = architecture.GetModel<RunModel>();
            var player = architecture.GetModel<PlayerModel>();

            player.AddRelic("relic.test_shield");
            player.AddSkill("skill.test_strike");

            var snapshot = CoreViewSnapshotFactory.Capture(architecture);

            Assert.AreEqual(run.Floor.Value, snapshot.Run.Floor);
            Assert.AreEqual(run.Room.Value, snapshot.Run.Room);
            Assert.AreEqual(run.Seed.Value, snapshot.Run.Seed);
            Assert.AreEqual(run.Phase.Value, snapshot.Run.Phase);
            CollectionAssert.Contains(snapshot.Player.RelicDefIds, "relic.test_shield");
            CollectionAssert.Contains(snapshot.Player.SkillDefIds, "skill.test_strike");
            Assert.Greater(snapshot.Player.AvatarStats.EffectiveHp, 0);
        }

        [Test]
        public void Snapshot_ChoiceViewIncludesPoolId()
        {
            var architecture = NineGridArchitecture.Current;
            P5CatalogTestSupport.RegisterCatalog(architecture);
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.reward", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0
                });

            architecture.SendCommand(new StartNodeCommand(options));
            architecture.SendCommand(new AttackCommand(FindFirstMonsterSlot()));

            var snapshot = CoreViewSnapshotFactory.Capture(architecture);

            Assert.AreEqual(PendingChoiceKind.Reward, snapshot.Choice.Kind);
            Assert.AreEqual(PendingChoiceKind.Reward, snapshot.PendingChoiceKind);
            Assert.IsFalse(string.IsNullOrEmpty(snapshot.Choice.PoolId));
            Assert.Greater(snapshot.Choice.RewardOptions.Count, 0);
        }

        [Test]
        public void Snapshot_VersionBumpsOnDeckChange()
        {
            var architecture = NineGridArchitecture.Current;
            var deck = architecture.GetModel<DeckModel>();
            var registry = architecture.GetModel<CardRegistry>();

            var before = CoreViewSnapshotFactory.Capture(architecture);
            var card = registry.Create("deck.version", CardKind.Monster);
            deck.AddToDrawPile(card, top: false);
            var after = CoreViewSnapshotFactory.Capture(architecture);

            Assert.Greater(after.Version, before.Version);
            CollectionAssert.Contains(after.Deck.DrawPileUids, card.Uid);
            Assert.IsTrue(after.TryGetCard(card.Uid, out var cardView));
            Assert.AreEqual(ZoneId.DrawPile, cardView.Zone);
        }

        [Test]
        public void LegacyTopLevelFieldsMirrorNestedViews()
        {
            var architecture = NineGridArchitecture.Current;
            P5CatalogTestSupport.RegisterCatalog(architecture);
            var options = new NodeDeckOptions { PlayerOpeningCount = 0, EnemyOpeningCount = 1 }
                .AddEnemyCard(new CardDraft("monster.legacy", CardKind.Monster)
                {
                    MaxHp = 1,
                    Attack = 0
                });

            architecture.SendCommand(new StartNodeCommand(options));
            architecture.SendCommand(new AttackCommand(FindFirstMonsterSlot()));

            var snapshot = CoreViewSnapshotFactory.Capture(architecture);

            Assert.AreEqual(snapshot.Run.Phase, snapshot.Phase);
            Assert.AreEqual(snapshot.Run.NodeIndex, snapshot.NodeIndex);
            Assert.AreEqual(snapshot.Player.Coins, snapshot.Coins);
            Assert.AreEqual(snapshot.Player.InteractionCount, snapshot.InteractionCount);
            Assert.AreEqual(snapshot.Board.AvatarUid, snapshot.AvatarUid);
            Assert.AreEqual(snapshot.Board.AvatarSlot, snapshot.AvatarSlot);
            Assert.AreEqual(snapshot.Choice.Kind, snapshot.PendingChoiceKind);
            Assert.AreEqual(snapshot.Board.Slots, snapshot.BoardSlots);
            Assert.AreEqual(snapshot.Choice.RewardOptions, snapshot.RewardOptions);
            Assert.AreEqual(snapshot.Choice.RoomOptions, snapshot.RoomOptions);
            Assert.AreEqual(snapshot.Choice.SelectedRoom, snapshot.SelectedRoom);
            Assert.AreEqual(9, snapshot.BoardSlots.Count);
        }

        private static SlotId FindAdjacentEmptySlot(SlotId avatarSlot, BoardModel board)
        {
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                if (slot != avatarSlot && avatarSlot.IsAdjacentTo(slot) && board.IsEmpty(slot))
                {
                    return slot;
                }
            }

            Assert.Fail("Could not find adjacent empty slot.");
            return SlotId.None;
        }

        private static SlotId FindFirstMonsterSlot()
        {
            var registry = NineGridArchitecture.Current.GetModel<CardRegistry>();
            var board = NineGridArchitecture.Current.GetModel<BoardModel>();
            for (var i = SlotId.MinBoardIndex; i <= SlotId.MaxBoardIndex; i++)
            {
                var slot = SlotId.Board(i);
                var uid = board.GetCardUid(slot);
                if (uid != 0 && registry.Get(uid).Kind == CardKind.Monster)
                {
                    return slot;
                }
            }

            Assert.Fail("Could not find monster slot.");
            return SlotId.None;
        }
    }
}
