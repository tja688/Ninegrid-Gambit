using NUnit.Framework;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;

namespace NineGrid.Cards.Tests
{
    public sealed class BoardPresentationMergeTests
    {
        private const int VictimUid = 100;
        private const int OtherRemovedUid = 200;

        [Test]
        public void MergeLethalHitAndPostKill_PreservesHitBeforePostKillStepOrder()
        {
            var hit = new CombatHitPresentationResult
            {
                Accepted = true,
                Steps = new[]
                {
                    new BoardPresentationStep
                    {
                        Kind = BoardPresentationStepKind.Move,
                        ActionId = 1,
                    },
                },
            };
            var postKill = new PostKillBoardPresentationResult
            {
                Accepted = true,
                Steps = new[]
                {
                    new BoardPresentationStep
                    {
                        Kind = BoardPresentationStepKind.Rotate,
                        ActionId = 2,
                        Clockwise = true,
                    },
                    new BoardPresentationStep
                    {
                        Kind = BoardPresentationStepKind.Deal,
                        ActionId = 3,
                    },
                },
            };

            var merged = BoardPresentationMerge.MergeLethalHitAndPostKill(hit, postKill, VictimUid);

            Assert.AreEqual(3, merged.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Move, merged.Steps[0].Kind);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, merged.Steps[1].Kind);
            Assert.AreEqual(BoardPresentationStepKind.Deal, merged.Steps[2].Kind);
        }

        [Test]
        public void MergeLethalHitAndPostKill_FiltersVictimRemoveStep()
        {
            var hit = new CombatHitPresentationResult
            {
                Accepted = true,
                Steps = new[]
                {
                    new BoardPresentationStep
                    {
                        Kind = BoardPresentationStepKind.Remove,
                        RemovedUids = new[] { VictimUid },
                    },
                },
            };
            var postKill = new PostKillBoardPresentationResult
            {
                Accepted = true,
                Steps = new[]
                {
                    new BoardPresentationStep
                    {
                        Kind = BoardPresentationStepKind.Rotate,
                        Clockwise = true,
                    },
                },
            };

            var merged = BoardPresentationMerge.MergeLethalHitAndPostKill(hit, postKill, VictimUid);

            Assert.AreEqual(1, merged.Steps.Length);
            Assert.AreEqual(BoardPresentationStepKind.Rotate, merged.Steps[0].Kind);
            Assert.AreEqual(0, merged.RemovedUids.Length);
        }

        [Test]
        public void MergeLethalHitAndPostKill_KeepsNonVictimRemovedUids()
        {
            var hit = new CombatHitPresentationResult
            {
                Accepted = true,
                RemovedUids = new[] { VictimUid, OtherRemovedUid },
            };
            var postKill = new PostKillBoardPresentationResult
            {
                Accepted = true,
                Steps = new[]
                {
                    new BoardPresentationStep
                    {
                        Kind = BoardPresentationStepKind.Rotate,
                        Clockwise = false,
                    },
                },
            };

            var merged = BoardPresentationMerge.MergeLethalHitAndPostKill(hit, postKill, VictimUid);

            Assert.AreEqual(1, merged.RemovedUids.Length);
            Assert.AreEqual(OtherRemovedUid, merged.RemovedUids[0]);
        }

        [Test]
        public void MergeLethalHitAndPostKill_FlatFallback_ExcludesVictimFromRemovedUids()
        {
            var hit = new CombatHitPresentationResult
            {
                Accepted = true,
                RemovedUids = new[] { VictimUid },
            };
            var postKill = new PostKillBoardPresentationResult
            {
                Accepted = true,
                Deals = new[]
                {
                    new PostKillCardDeal { Uid = 301, Slot = 2 },
                },
            };

            var merged = BoardPresentationMerge.MergeLethalHitAndPostKill(hit, postKill, VictimUid);

            Assert.AreEqual(1, merged.Deals.Length);
            Assert.AreEqual(0, merged.RemovedUids.Length);
        }
    }
}
