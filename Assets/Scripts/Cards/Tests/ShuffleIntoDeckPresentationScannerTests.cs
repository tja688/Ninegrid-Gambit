using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Core;
using NineGrid.Flow;

namespace NineGrid.Cards.Tests
{
    public sealed class ShuffleIntoDeckPresentationScannerTests
    {
        [Test]
        public void TryParseShuffleIntoEvent_NewCard_ReturnsTrue()
        {
            var entry = new CoreGameEvent(CoreEventType.CardDealt, 2, "ShuffleIntoDrawPile")
                .WithCard(201)
                .WithMessage("shuffleInto:monster.skull_head")
                .WithSource("monster.skull_head", "skill.fall_apart.remove");

            Assert.IsTrue(
                ShuffleIntoDeckPresentationScanner.TryParseShuffleIntoEvent(
                    entry,
                    out var kind,
                    out var defId));
            Assert.AreEqual(ShuffleIntoDeckEventKind.NewCard, kind);
            Assert.AreEqual("monster.skull_head", defId);
        }

        [Test]
        public void TryParseShuffleIntoEvent_RandomCard_ReturnsTrue()
        {
            var entry = new CoreGameEvent(CoreEventType.CardDealt, 3, "ShuffleRandomContentIntoDrawPile")
                .WithCard(301)
                .WithMessage("shuffleRandom:monster.beggar");

            Assert.IsTrue(
                ShuffleIntoDeckPresentationScanner.TryParseShuffleIntoEvent(
                    entry,
                    out var kind,
                    out var defId));
            Assert.AreEqual(ShuffleIntoDeckEventKind.RandomCard, kind);
            Assert.AreEqual("monster.beggar", defId);
        }

        [Test]
        public void TryParseShuffleIntoEvent_ExistingCard_ReturnsTrue()
        {
            var entry = new CoreGameEvent(CoreEventType.CardDealt, 4, "ShuffleCardIntoDrawPile")
                .WithCard(401)
                .WithSlots(SlotId.Board(2), SlotId.None)
                .WithMessage("shuffleExisting:help.flame");

            Assert.IsTrue(
                ShuffleIntoDeckPresentationScanner.TryParseShuffleIntoEvent(
                    entry,
                    out var kind,
                    out var defId));
            Assert.AreEqual(ShuffleIntoDeckEventKind.ExistingCard, kind);
            Assert.AreEqual("help.flame", defId);
        }

        [Test]
        public void TryParseShuffleIntoEvent_BoardDeal_Ignored()
        {
            var entry = new CoreGameEvent(CoreEventType.CardDealt, 5, "DealCard")
                .WithCard(501)
                .WithSlots(SlotId.None, SlotId.Board(1));

            Assert.IsFalse(
                ShuffleIntoDeckPresentationScanner.TryParseShuffleIntoEvent(
                    entry,
                    out _,
                    out _));
        }

        [Test]
        public void Collect_ShuffleInto_ResolvesTriggerCardAndSkipsDeckDuplicates()
        {
            var entries = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.EffectTriggered, 1, "ExecuteEffect")
                    .WithCard(100)
                    .WithMessage("skill.fall_apart.remove"),
                new CoreGameEvent(CoreEventType.CardKilled, 1, "KillCard")
                    .WithCard(100)
                    .WithSlots(SlotId.Board(4), SlotId.None),
                new CoreGameEvent(CoreEventType.CardDealt, 2, "ShuffleIntoDrawPile")
                    .WithCard(201)
                    .WithMessage("shuffleInto:monster.skull_head")
                    .WithSource("monster.skull_head", "skill.fall_apart.remove"),
                new CoreGameEvent(CoreEventType.CardDealt, 3, "ShuffleIntoDrawPile")
                    .WithCard(202)
                    .WithMessage("shuffleInto:monster.headless_skeleton")
                    .WithSource("monster.headless_skeleton", "skill.fall_apart.remove"),
            };

            var collected = ShuffleIntoDeckPresentationScanner.Collect(entries, 0, uid => uid == 201);
            Assert.AreEqual(1, collected.Count);
            Assert.AreEqual(202, collected[0].Uid);
            Assert.AreEqual(100, collected[0].TriggerCardUid);
            Assert.AreEqual(4, collected[0].FromBoardSlot);
            Assert.AreEqual("monster.headless_skeleton", collected[0].DefId);
        }

        [Test]
        public void Collect_DuplicateUidInBatch_OnlyFirstIsKept()
        {
            var entries = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.CardDealt, 1, "ShuffleIntoDrawPile")
                    .WithCard(301)
                    .WithMessage("shuffleInto:monster.beggar"),
                new CoreGameEvent(CoreEventType.CardDealt, 2, "ShuffleIntoDrawPile")
                    .WithCard(301)
                    .WithMessage("shuffleInto:monster.beggar"),
            };

            var collected = ShuffleIntoDeckPresentationScanner.Collect(entries, 0, _ => false);
            Assert.AreEqual(1, collected.Count);
            Assert.AreEqual(301, collected[0].Uid);
        }
    }
}
