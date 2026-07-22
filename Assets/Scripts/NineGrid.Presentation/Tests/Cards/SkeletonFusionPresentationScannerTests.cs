using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Core;
using NineGrid.Flow;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    public sealed class SkeletonFusionPresentationScannerTests
    {
        [Test]
        public void Collect_RecombineHead_BuildsTwoParticipantFusion()
        {
            var entries = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.EffectTriggered, 7, "ExecuteEffect")
                    .WithCard(100)
                    .WithSource("skill.recombine_head", "skill.recombine_head.move"),
                new CoreGameEvent(CoreEventType.CardRemoved, 7, "RemoveCard")
                    .WithCard(100)
                    .WithSlots(SlotId.Board(2), SlotId.None),
                new CoreGameEvent(CoreEventType.CardRemoved, 7, "RemoveCard")
                    .WithCard(101)
                    .WithSlots(SlotId.Board(3), SlotId.None),
                new CoreGameEvent(CoreEventType.CardDealt, 7, "ShuffleIntoDrawPile")
                    .WithCard(202)
                    .WithMessage("shuffleInto:monster.big_skeleton_reborn"),
            };

            var collected = SkeletonFusionPresentationScanner.Collect(entries, 0);
            Assert.AreEqual(1, collected.Count);
            Assert.AreEqual("skill.recombine_head", collected[0].SkillId);
            Assert.AreEqual(2, collected[0].ParticipantUids.Length);
            Assert.Contains(100, collected[0].ParticipantUids);
            Assert.Contains(101, collected[0].ParticipantUids);
            Assert.AreEqual(202, collected[0].ResultUid);
            Assert.AreEqual("monster.big_skeleton_reborn", collected[0].ResultDefId);
        }

        [Test]
        public void Collect_StrongCombo_BuildsThreeParticipantFusion()
        {
            var entries = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.EffectTriggered, 9, "ExecuteEffect")
                    .WithCard(300)
                    .WithSource("skill.strong_combo", "skill.strong_combo.move"),
                new CoreGameEvent(CoreEventType.CardRemoved, 9, "RemoveCard").WithCard(300),
                new CoreGameEvent(CoreEventType.CardRemoved, 9, "RemoveCard").WithCard(301),
                new CoreGameEvent(CoreEventType.CardRemoved, 9, "RemoveCard").WithCard(302),
                new CoreGameEvent(CoreEventType.CardDealt, 9, "ShuffleIntoDrawPile")
                    .WithCard(303)
                    .WithMessage("shuffleInto:monster.giant_skeleton"),
            };

            var collected = SkeletonFusionPresentationScanner.Collect(entries, 0);
            Assert.AreEqual(1, collected.Count);
            Assert.AreEqual(3, collected[0].ParticipantUids.Length);
            Assert.AreEqual(303, collected[0].ResultUid);
        }

        [Test]
        public void Collect_RecombineHead_DistinctActionIds_BuildsFusion()
        {
            var entries = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.EffectTriggered, 7, "ExecuteEffect")
                    .WithCard(100)
                    .WithSource("skill.recombine_head", "skill.recombine_head.move"),
                new CoreGameEvent(CoreEventType.CardRemoved, 8, "RemoveCard")
                    .WithCard(100)
                    .WithSlots(SlotId.Board(2), SlotId.None),
                new CoreGameEvent(CoreEventType.CardRemoved, 9, "RemoveCard")
                    .WithCard(101)
                    .WithSlots(SlotId.Board(3), SlotId.None),
                new CoreGameEvent(CoreEventType.CardDealt, 10, "ShuffleIntoDrawPile")
                    .WithCard(202)
                    .WithMessage("shuffleInto:monster.big_skeleton_reborn"),
            };

            var collected = SkeletonFusionPresentationScanner.Collect(entries, 0);
            Assert.AreEqual(1, collected.Count);
            Assert.AreEqual(2, collected[0].ParticipantUids.Length);
            Assert.AreEqual(202, collected[0].ResultUid);
        }

        [Test]
        public void BuildParticipantIndex_MapsAllParticipants()
        {
            var fusion = new SkeletonFusionPresentationEntry(
                1,
                "skill.recombine_body",
                10,
                new[] { 10, 11 },
                20,
                "monster.big_skeleton_reborn");
            var index = SkeletonFusionPresentationScanner.BuildParticipantIndex(new[] { fusion });

            Assert.IsTrue(index.TryGetValue(10, out var a));
            Assert.IsTrue(index.TryGetValue(11, out var b));
            Assert.AreEqual(10, a.TriggerCardUid);
            Assert.AreEqual(10, b.TriggerCardUid);
        }

        [Test]
        public void Collect_FusionThenFallApart_DoesNotAbsorbLaterShuffleIntoAsResult()
        {
            var entries = new List<CoreGameEvent>
            {
                new CoreGameEvent(CoreEventType.EffectTriggered, 7, "ExecuteEffect")
                    .WithCard(100)
                    .WithSource("skill.recombine_head", "skill.recombine_head.move"),
                new CoreGameEvent(CoreEventType.CardRemoved, 8, "RemoveCard").WithCard(100),
                new CoreGameEvent(CoreEventType.CardRemoved, 9, "RemoveCard").WithCard(101),
                new CoreGameEvent(CoreEventType.CardDealt, 10, "ShuffleIntoDrawPile")
                    .WithCard(202)
                    .WithMessage("shuffleInto:monster.big_skeleton_reborn"),
                // 同节点后续散架：不得把碎片 uid 写成融合 ResultUid。
                new CoreGameEvent(CoreEventType.EffectTriggered, 20, "ExecuteEffect")
                    .WithCard(202)
                    .WithSource("skill.fall_apart", "skill.fall_apart.remove"),
                new CoreGameEvent(CoreEventType.CardKilled, 21, "KillCard").WithCard(202),
                new CoreGameEvent(CoreEventType.CardDealt, 22, "ShuffleIntoDrawPile")
                    .WithCard(301)
                    .WithMessage("shuffleInto:monster.skull_head"),
                new CoreGameEvent(CoreEventType.CardDealt, 23, "ShuffleIntoDrawPile")
                    .WithCard(302)
                    .WithMessage("shuffleInto:monster.headless_skeleton"),
            };

            var collected = SkeletonFusionPresentationScanner.Collect(entries, 0);
            Assert.AreEqual(1, collected.Count);
            Assert.AreEqual(202, collected[0].ResultUid);
            Assert.AreEqual(2, collected[0].ParticipantUids.Length);
            Assert.Contains(100, collected[0].ParticipantUids);
            Assert.Contains(101, collected[0].ParticipantUids);
        }
    }
}
