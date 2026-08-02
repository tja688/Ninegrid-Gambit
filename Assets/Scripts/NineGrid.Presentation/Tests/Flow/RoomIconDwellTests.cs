using System.Collections.Generic;
using NineGrid.Content.CardPresentation;
using NineGrid.Flow.RoomIcons;
using NUnit.Framework;

namespace NineGrid.Presentation.Tests.Flow
{
    public sealed class RoomIconBoardSlotResolverTests
    {
        [TearDown]
        public void TearDown()
        {
            CardPresentationConfigCatalog.Invalidate();
        }

        [Test]
        public void Solo_UsesConfiguredOrDefault2()
        {
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "Leave",
                kind = "Room",
                boardSlot = 2
            });
            var slots = RoomIconBoardSlotResolver.ResolveSlots(new[] { "Leave" });
            Assert.AreEqual(1, slots.Length);
            Assert.AreEqual(2, slots[0]);
        }

        [Test]
        public void Dual_Collision_FallsBackToAlternate()
        {
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "Shop",
                kind = "Room",
                boardSlot = 1
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "Elite",
                kind = "Room",
                boardSlot = 1
            });
            var slots = RoomIconBoardSlotResolver.ResolveSlots(new[] { "Shop", "Elite" });
            Assert.AreEqual(2, slots.Length);
            Assert.AreEqual(1, slots[0]);
            Assert.AreEqual(3, slots[1]);
        }

        [Test]
        public void Dual_Unconfigured_UsesOneAndThree()
        {
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "Gold",
                kind = "Room",
                boardSlot = 0
            });
            CardPresentationConfigCatalog.UpsertForTests(new CardPresentationConfigDto
            {
                contentId = "Treasure",
                kind = "Room",
                boardSlot = 0
            });
            var slots = RoomIconBoardSlotResolver.ResolveSlots(new[] { "Gold", "Treasure" });
            Assert.AreEqual(1, slots[0]);
            Assert.AreEqual(3, slots[1]);
        }
    }

    public sealed class RoomIconDwellSessionTests
    {
        [Test]
        public void Cancel_ClearsArmed()
        {
            var session = new RoomIconDwellSession();
            session.Begin(1, 0);
            Assert.IsTrue(session.IsArmed);
            session.Cancel();
            Assert.IsFalse(session.IsArmed);
            Assert.IsFalse(session.TryConsumeArmed(out _));
        }

        [Test]
        public void TryConsumeArmed_ThenMarkSubmitted_OnlyOnce()
        {
            var session = new RoomIconDwellSession();
            session.Begin(3, 1);
            Assert.IsTrue(session.TryConsumeArmed(out var first));
            Assert.AreEqual(1, first);
            Assert.IsFalse(session.HasSubmitted);
            session.MarkSubmitted();
            Assert.IsTrue(session.HasSubmitted);
            Assert.IsFalse(session.TryConsumeArmed(out _));
        }

        [Test]
        public void TryConsumeArmed_FailureAllowsReBegin()
        {
            var session = new RoomIconDwellSession();
            session.Begin(1, 0);
            Assert.IsTrue(session.TryConsumeArmed(out _));
            session.Begin(1, 0);
            Assert.IsTrue(session.IsArmed);
            Assert.IsTrue(session.TryConsumeArmed(out var again));
            Assert.AreEqual(0, again);
        }

        [Test]
        public void LeaveIcon_CancelsBeforeSubmit()
        {
            var session = new RoomIconDwellSession();
            session.Begin(1, 0);
            session.Cancel();
            Assert.IsFalse(session.TryConsumeArmed(out _));
        }
    }

    public sealed class RoomIconOccupancyPathTests
    {
        [SetUp]
        public void SetUp()
        {
            RoomIconOccupancy.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            RoomIconOccupancy.ResetForTests();
        }

        [Test]
        public void SoftBlocked_OtherIcon_NotDestination()
        {
            RoomIconOccupancy.Current.Register(1, 0, "Shop");
            RoomIconOccupancy.Current.Register(3, 1, "Tavern");
            Assert.IsTrue(RoomIconOccupancy.Current.IsSoftBlocked(NineGrid.Core.SlotId.Board(1)));
            Assert.IsTrue(RoomIconOccupancy.Current.IsIconSlot(3));
        }
    }
}
