using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Flow;
using NineGrid.Flow.Presentation;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    public sealed class ShuffleBurstGrouperTests
    {
        [Test]
        public void Partition_SameActionIdTwoNewCards_FormsBurstGroup()
        {
            var pending = new List<ShuffleIntoDeckPresentationEntry>
            {
                Entry(101, "monster.skull_head", 7, ShuffleIntoDeckEventKind.NewCard),
                Entry(102, "monster.headless_skeleton", 7, ShuffleIntoDeckEventKind.NewCard),
            };
            var bursts = new List<ShuffleBurstGroup>();
            var leftovers = new List<ShuffleIntoDeckPresentationEntry>();

            ShuffleBurstGrouper.Partition(pending, bursts, leftovers);

            Assert.AreEqual(1, bursts.Count);
            Assert.AreEqual(7, bursts[0].ActionId);
            Assert.AreEqual(2, bursts[0].Entries.Count);
            Assert.AreEqual(0, leftovers.Count);
        }

        [Test]
        public void Partition_FallApartStyle_DifferentActionId_SameTrigger_Bursts()
        {
            // Sequence 展开为两条独立 ShuffleInto，ActionId 不同但 TriggerCardUid 相同。
            var pending = new List<ShuffleIntoDeckPresentationEntry>
            {
                Entry(101, "monster.skull_head", 11, ShuffleIntoDeckEventKind.NewCard, triggerCardUid: 50),
                Entry(102, "monster.headless_skeleton", 12, ShuffleIntoDeckEventKind.NewCard, triggerCardUid: 50),
            };
            var bursts = new List<ShuffleBurstGroup>();
            var leftovers = new List<ShuffleIntoDeckPresentationEntry>();

            ShuffleBurstGrouper.Partition(pending, bursts, leftovers);

            Assert.AreEqual(1, bursts.Count);
            Assert.AreEqual(2, bursts[0].Entries.Count);
            Assert.AreEqual(0, leftovers.Count);
            Assert.AreEqual(101, bursts[0].Entries[0].Uid);
            Assert.AreEqual(102, bursts[0].Entries[1].Uid);
        }

        [Test]
        public void Partition_SingleNewCard_GoesToLeftovers()
        {
            var pending = new List<ShuffleIntoDeckPresentationEntry>
            {
                Entry(101, "monster.skull_head", 7, ShuffleIntoDeckEventKind.NewCard),
            };
            var bursts = new List<ShuffleBurstGroup>();
            var leftovers = new List<ShuffleIntoDeckPresentationEntry>();

            ShuffleBurstGrouper.Partition(pending, bursts, leftovers);

            Assert.AreEqual(0, bursts.Count);
            Assert.AreEqual(1, leftovers.Count);
            Assert.AreEqual(101, leftovers[0].Uid);
        }

        [Test]
        public void Partition_ExistingCard_NeverBursts()
        {
            var pending = new List<ShuffleIntoDeckPresentationEntry>
            {
                Entry(201, "help.teleport", 9, ShuffleIntoDeckEventKind.ExistingCard),
                Entry(202, "help.flame", 9, ShuffleIntoDeckEventKind.ExistingCard),
            };
            var bursts = new List<ShuffleBurstGroup>();
            var leftovers = new List<ShuffleIntoDeckPresentationEntry>();

            ShuffleBurstGrouper.Partition(pending, bursts, leftovers);

            Assert.AreEqual(0, bursts.Count);
            Assert.AreEqual(2, leftovers.Count);
        }

        [Test]
        public void Partition_MixedRandomAndNew_SameActionId_Bursts()
        {
            var pending = new List<ShuffleIntoDeckPresentationEntry>
            {
                Entry(301, "monster.multibone", 11, ShuffleIntoDeckEventKind.NewCard),
                Entry(302, "monster.orc", 11, ShuffleIntoDeckEventKind.RandomCard),
            };
            var bursts = new List<ShuffleBurstGroup>();
            var leftovers = new List<ShuffleIntoDeckPresentationEntry>();

            ShuffleBurstGrouper.Partition(pending, bursts, leftovers);

            Assert.AreEqual(1, bursts.Count);
            Assert.AreEqual(2, bursts[0].Entries.Count);
            Assert.AreEqual(0, leftovers.Count);
        }

        [Test]
        public void Partition_MultipleActionIds_SeparateGroups()
        {
            // TriggerCardUid==0 时回退 ActionId 分桶。
            var pending = new List<ShuffleIntoDeckPresentationEntry>
            {
                Entry(101, "a", 1, ShuffleIntoDeckEventKind.NewCard, triggerCardUid: 0),
                Entry(102, "b", 1, ShuffleIntoDeckEventKind.NewCard, triggerCardUid: 0),
                Entry(201, "c", 2, ShuffleIntoDeckEventKind.NewCard, triggerCardUid: 0),
                Entry(202, "d", 2, ShuffleIntoDeckEventKind.NewCard, triggerCardUid: 0),
                Entry(301, "solo", 3, ShuffleIntoDeckEventKind.NewCard, triggerCardUid: 0),
            };
            var bursts = new List<ShuffleBurstGroup>();
            var leftovers = new List<ShuffleIntoDeckPresentationEntry>();

            ShuffleBurstGrouper.Partition(pending, bursts, leftovers);

            Assert.AreEqual(2, bursts.Count);
            Assert.AreEqual(1, leftovers.Count);
            Assert.AreEqual(301, leftovers[0].Uid);
            Assert.AreEqual(1, bursts[0].ActionId);
            Assert.AreEqual(2, bursts[1].ActionId);
        }

        [Test]
        public void Partition_ActionIdZero_GoesToLeftovers()
        {
            var pending = new List<ShuffleIntoDeckPresentationEntry>
            {
                Entry(101, "a", 0, ShuffleIntoDeckEventKind.NewCard),
                Entry(102, "b", 0, ShuffleIntoDeckEventKind.NewCard),
            };
            var bursts = new List<ShuffleBurstGroup>();
            var leftovers = new List<ShuffleIntoDeckPresentationEntry>();

            ShuffleBurstGrouper.Partition(pending, bursts, leftovers);

            Assert.AreEqual(0, bursts.Count);
            Assert.AreEqual(2, leftovers.Count);
        }

        private static ShuffleIntoDeckPresentationEntry Entry(
            int uid,
            string defId,
            int actionId,
            ShuffleIntoDeckEventKind kind,
            int triggerCardUid = 50)
        {
            return new ShuffleIntoDeckPresentationEntry(
                uid,
                defId,
                cause: string.Empty,
                actionId,
                eventSequence: uid,
                kind,
                triggerCardUid,
                fromBoardSlot: 3);
        }
    }
}
