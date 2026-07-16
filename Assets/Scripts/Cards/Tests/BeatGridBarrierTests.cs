using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    public sealed class BeatGridBarrierTests
    {
        [Test]
        public void OpenBeat_SharesSourceTimeAcrossRegistrations()
        {
            var clock = new PresentationClock();
            var grid = new BeatGrid(clock);

            var beatId = grid.OpenBeat(0.4f);
            var a = grid.Register(beatId, committed: true);
            var b = grid.Register(beatId, committed: true);

            Assert.AreEqual(0.4f, grid.GetSharedSourceTime(beatId), 1e-5f);
            Assert.AreEqual(0.4f, a.SourceTime, 1e-5f);
            Assert.AreEqual(0.4f, b.SourceTime, 1e-5f);
            Assert.AreEqual(a.SourceTime, b.SourceTime);
        }

        [Test]
        public void Barrier_RejectsSharedSourceTimeExceedingBudget()
        {
            var clock = new PresentationClock();
            var grid = new BeatGrid(clock);

            var beatId = grid.OpenBeat(0.6f);
            grid.PlaceBarrier(beatId, barrierWallTime: 0.5f);
            grid.Register(beatId);

            Assert.IsFalse(grid.TryValidateBarrier(beatId, out var error));
            Assert.IsNotNull(error);
            StringAssert.Contains("exceeds barrier budget", error);
        }

        [Test]
        public void Barrier_AllSourceTimesAtOrBelowBarrier_AndCompleteAtBarrierTime()
        {
            var clock = new PresentationClock();
            var grid = new BeatGrid(clock);

            const float sourceTime = 0.4f;
            const float barrierAt = 0.5f;

            var beatId = grid.OpenBeat(sourceTime);
            grid.PlaceBarrier(beatId, barrierAt);
            var a = grid.Register(beatId, committed: true);
            var b = grid.Register(beatId, committed: true);
            var c = grid.Register(beatId, committed: true);

            Assert.IsTrue(grid.TryValidateBarrier(beatId, out var error), error);
            Assert.LessOrEqual(a.SourceTime, barrierAt);
            Assert.LessOrEqual(b.SourceTime, barrierAt);
            Assert.LessOrEqual(c.SourceTime, barrierAt);

            // 栅栏前：尚未全员完成
            clock.Seek(0.39f);
            grid.TickBeat(beatId);
            Assert.IsFalse(grid.AreAllComplete(beatId));
            Assert.IsFalse(grid.IsBarrierSatisfied(beatId));

            // 到栅栏时刻：全员 sourceTime 已过 → 全部完成
            clock.Seek(barrierAt);
            Assert.IsTrue(grid.IsBarrierSatisfied(beatId));
            Assert.IsTrue(a.IsComplete);
            Assert.IsTrue(b.IsComplete);
            Assert.IsTrue(c.IsComplete);
        }

        [Test]
        public void SameBeat_AfterStartOffset_StillSharesSourceTimeRelativeToBeatStart()
        {
            var clock = new PresentationClock();
            clock.Seek(2f);
            var grid = new BeatGrid(clock);

            var beatId = grid.OpenBeat(0.3f);
            grid.PlaceBarrier(beatId, barrierWallTime: 2.3f);
            var slot = grid.Register(beatId);

            Assert.IsTrue(grid.TryValidateBarrier(beatId, out _));

            clock.Seek(2.29f);
            grid.TickBeat(beatId);
            Assert.IsFalse(slot.IsComplete);

            clock.Seek(2.3f);
            Assert.IsTrue(grid.IsBarrierSatisfied(beatId));
            Assert.IsTrue(slot.IsComplete);
        }

        [Test]
        public void Barrier_DriverNotComplete_KeepsBarrierUnsatisfied()
        {
            var clock = new PresentationClock();
            var grid = new BeatGrid(clock);
            var root = new GameObject("BeatGridDriverBarrier");
            try
            {
                var tower = root.AddComponent<CardTransformTower>();
                tower.EnsureTower();
                var driver = LayerConvergenceDriver.Ensure(root.transform, TowerLayer.SlotFrame);

                const float sourceTime = 0.2f;
                var beatId = grid.OpenBeat(sourceTime);
                grid.PlaceBarrier(beatId, barrierWallTime: sourceTime);
                var slot = grid.Register(beatId, committed: true, driver);

                driver.ConvergeTo(new Vector3(1f, 0f, 0f), sourceTime);

                // 时间到但未 Tick 完 driver → barrier 不满足
                clock.Seek(sourceTime);
                grid.TickBeat(beatId);
                Assert.IsTrue(slot.Elapsed + 1e-5f >= slot.SourceTime);
                Assert.IsFalse(slot.IsDriverComplete);
                Assert.IsFalse(grid.AreAllComplete(beatId));
                Assert.IsFalse(grid.IsBarrierSatisfied(beatId));

                // driver 走完 → Completed 触发 → barrier 满足
                var remaining = sourceTime;
                while (remaining > 0f)
                {
                    var step = Mathf.Min(1f / 60f, remaining);
                    driver.Tick(step);
                    remaining -= step;
                }

                Assert.IsTrue(slot.IsDriverComplete);
                Assert.IsTrue(grid.IsBarrierSatisfied(beatId));
            }
            finally
            {
                grid.Clear();
                Object.DestroyImmediate(root);
            }
        }
    }
}
