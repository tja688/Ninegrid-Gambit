using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards.Tests
{
    public sealed class FlightSortingChannelTests
    {
        [TearDown]
        public void TearDown()
        {
            FlightSortingChannel.Disarm(42);
        }

        [Test]
        public void GlobalFlightOrder_IsWellAboveTypicalGroundOrder()
        {
            // GroundCardSortingOrder = -10；飞行层须远高于盘面。
            Assert.Greater(FlightSortingChannel.GlobalFlightSortingOrder, 0);
            Assert.GreaterOrEqual(FlightSortingChannel.GlobalFlightSortingOrder, 50);
        }

        [Test]
        public void ResolveFlightOrder_NullCard_UsesBaseFlightLayer()
        {
            Assert.AreEqual(
                FlightSortingChannel.GlobalFlightSortingOrder,
                FlightSortingChannel.ResolveFlightOrder(null));
        }

        [Test]
        public void SlotConvergenceComplete_CanHostDiscreteSortingJump()
        {
            var root = new GameObject("FlightSortingDiscrete");
            try
            {
                var tower = root.AddComponent<CardTransformTower>();
                tower.EnsureTower();
                var driver = LayerConvergenceDriver.Ensure(root.transform, TowerLayer.SlotFrame);
                var group = root.AddComponent<SortingGroup>();
                group.sortingOrder = -10;

                // 方案 A 语义：收敛开始抬升，完成事件掉回目标序。
                group.sortingOrder = FlightSortingChannel.GlobalFlightSortingOrder;
                var restored = false;
                driver.Completed += () =>
                {
                    group.sortingOrder = -10;
                    restored = true;
                };

                driver.ConvergeTo(new Vector3(1f, 0f, 0f), 0.15f);
                var remaining = 0.15f;
                while (remaining > 0f)
                {
                    var step = Mathf.Min(1f / 60f, remaining);
                    driver.Tick(step);
                    remaining -= step;
                }

                Assert.IsTrue(restored);
                Assert.AreEqual(-10, group.sortingOrder);
                Assert.IsTrue(driver.IsComplete);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Disarm_IsIdempotent()
        {
            Assert.IsFalse(FlightSortingChannel.IsArmed(42));
            FlightSortingChannel.Disarm(42);
            Assert.IsFalse(FlightSortingChannel.IsArmed(42));
        }
    }
}
