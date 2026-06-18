using System.Linq;
using NUnit.Framework;

namespace NineGrid.Core.Tests
{
    public sealed class P1ModelTests
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
        public void InitialFactoryCreatesAvatarAtSlotFiveAndEmptyPools()
        {
            var architecture = NineGridArchitecture.Current;
            var snapshot = InitialGameFactory.Create(architecture, new InitialGameOptions { Seed = 7UL });
            var board = architecture.GetModel<BoardModel>();
            var deck = architecture.GetModel<DeckModel>();
            var run = architecture.GetModel<RunModel>();

            Assert.AreEqual(SlotId.Board(5), snapshot.AvatarSlot);
            Assert.AreEqual(SlotId.Board(5), board.AvatarSlot.Value);
            Assert.IsFalse(board.BoardCardUids().Any());
            Assert.AreEqual(0, deck.DrawPileUids.Count);
            Assert.AreEqual(0, deck.PlayerCardPoolUids.Count);
            Assert.AreEqual(0, deck.EnemyCardPoolUids.Count);
            Assert.AreEqual(7UL, run.Seed.Value);
            StringAssert.Contains("Avatar: #1 avatar.default @ Board5", snapshot.Report);
            StringAssert.Contains("BoardCards: empty", snapshot.Report);
        }

        [Test]
        public void CardMigratesFromDrawPileToBoardSlot()
        {
            var architecture = NineGridArchitecture.Current;
            InitialGameFactory.Create(architecture);
            var registry = architecture.GetModel<CardRegistry>();
            var deck = architecture.GetModel<DeckModel>();
            var board = architecture.GetModel<BoardModel>();
            var monster = registry.Create("monster.slime", CardKind.Monster);

            deck.AddToDrawPile(monster, false);
            Assert.AreEqual(ZoneId.DrawPile, monster.Zone.Value);
            Assert.AreEqual(SlotId.None, monster.Slot.Value);
            Assert.IsTrue(deck.DrawPileUids.Contains(monster.Uid));

            Assert.IsTrue(deck.RemoveCard(monster));
            board.PlaceCard(monster, SlotId.Board(1));

            Assert.AreEqual(ZoneId.Board, monster.Zone.Value);
            Assert.AreEqual(SlotId.Board(1), monster.Slot.Value);
            Assert.AreEqual(monster.Uid, board.GetCardUid(SlotId.Board(1)));
            Assert.IsFalse(deck.DrawPileUids.Contains(monster.Uid));
        }
    }
}
