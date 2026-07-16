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

        [Test]
        public void RefreshDisplayMode_DoesNotOverwriteSortingOrder_WhileArmed()
        {
            foreach (var m in Object.FindObjectsByType<CardManagerSingleton>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(m.gameObject);
            }

            var instanceField = typeof(CardManagerSingleton).GetField(
                "_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);

            var cardGo = new GameObject("FlightSortArmedGuard");
            var cardManager = cardGo.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("FlightSortPrefab");
            prefab.AddComponent<StandardCardView>();
            prefab.AddComponent<SortingGroup>();
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            const int uid = 9301;
            ManagedCard card = null;
            try
            {
                card = cardManager.SpawnView(uid, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(card);
                cardManager.SetDisplayMode(card, CardDisplayMode.GroundCardMode);

                var tower = card.Transform.GetComponent<CardTransformTower>();
                Assert.IsNotNull(tower);
                tower.EnsureTower();
                var driver = LayerConvergenceDriver.Ensure(card.Transform, TowerLayer.SlotFrame);
                var group = card.View.GetComponent<SortingGroup>();
                Assert.IsNotNull(group);

                FlightSortingChannel.ArmForSlotConvergence(card, driver);
                Assert.IsTrue(FlightSortingChannel.IsArmed(uid));
                var flightOrder = group.sortingOrder;
                Assert.GreaterOrEqual(flightOrder, FlightSortingChannel.GlobalFlightSortingOrder);

                cardManager.RefreshDisplayMode(card);

                Assert.AreEqual(flightOrder, group.sortingOrder);
                Assert.IsTrue(FlightSortingChannel.IsArmed(uid));
            }
            finally
            {
                FlightSortingChannel.Disarm(uid);
                Object.DestroyImmediate(cardGo);
                Object.DestroyImmediate(prefab);
                instanceField?.SetValue(null, null);
            }
        }
    }
}
