using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;
using UnityEngine.Rendering;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    public sealed class FlightSortingChannelTests
    {
        [TearDown]
        public void TearDown()
        {
            FlightSortingChannel.Disarm(42);
            FlightSortingChannel.Disarm(9301);
            FlightSortingChannel.Disarm(9401);
            FlightSortingChannel.Disarm(9402);
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
        public void HandSlotContainer_ComputeSortingOrder_IsLeftHighRightLow()
        {
            var settings = new CardHandLayoutSettings
            {
                sortingOrderBase = 10,
                sortingOrderStep = 1,
            };
            var container = new CardHandSlotContainer(settings);
            Assert.AreEqual(10, container.ComputeSortingOrder(0));
            Assert.AreEqual(9, container.ComputeSortingOrder(1));
            Assert.AreEqual(8, container.ComputeSortingOrder(2));
        }

        [Test]
        public void ResolveTargetOrder_HandModeWithoutSlot_FallsBackToDisplayModeDefault()
        {
            foreach (var m in Object.FindObjectsByType<CardManagerSingleton>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(m.gameObject);
            }

            var instanceField = typeof(CardManagerSingleton).GetField(
                "_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);

            var cardGo = new GameObject("FlightSortHandFallback");
            var cardManager = cardGo.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("FlightSortHandPrefab");
            prefab.AddComponent<StandardCardView>();
            prefab.AddComponent<SortingGroup>();
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            const int uid = 9401;
            try
            {
                var card = cardManager.SpawnView(uid, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(card);
                cardManager.SetDisplayMode(card, CardDisplayMode.HandCardMode);

                // 未入槽时无法委托手牌域，回落 DisplayMode 默认（Hand=0）。
                Assert.AreEqual(0, FlightSortingChannel.ResolveTargetOrder(card));
            }
            finally
            {
                Object.DestroyImmediate(cardGo);
                Object.DestroyImmediate(prefab);
                instanceField?.SetValue(null, null);
            }
        }

        [Test]
        public void Restore_AfterDisplayModeChange_ReResolvesLiveTargetNotFrozenArmOrder()
        {
            foreach (var m in Object.FindObjectsByType<CardManagerSingleton>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(m.gameObject);
            }

            var instanceField = typeof(CardManagerSingleton).GetField(
                "_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);

            var cardGo = new GameObject("FlightSortLiveRestore");
            var cardManager = cardGo.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("FlightSortLivePrefab");
            prefab.AddComponent<StandardCardView>();
            prefab.AddComponent<SortingGroup>();
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            const int uid = 9402;
            try
            {
                var card = cardManager.SpawnView(uid, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(card);
                cardManager.SetDisplayMode(card, CardDisplayMode.GroundCardMode);

                var tower = card.Transform.GetComponent<CardTransformTower>();
                Assert.IsNotNull(tower);
                tower.EnsureTower();
                var driver = LayerConvergenceDriver.Ensure(card.Transform, TowerLayer.SlotFrame);
                var group = card.View.GetComponent<SortingGroup>();
                Assert.IsNotNull(group);

                FlightSortingChannel.ArmForSlotConvergence(card, driver);
                Assert.GreaterOrEqual(group.sortingOrder, FlightSortingChannel.GlobalFlightSortingOrder);

                // 模拟入手：DisplayMode 已切到手牌，再 Restore（SanitizeForSanctuary 路径）。
                // 必须按就位瞬间重解析，不能沿用起飞时冻结的 Ground order（-10）。
                cardManager.SetDisplayMode(card, CardDisplayMode.HandCardMode);
                FlightSortingChannel.Restore(card);

                Assert.IsFalse(FlightSortingChannel.IsArmed(uid));
                Assert.AreEqual(0, group.sortingOrder);
                Assert.AreNotEqual(-10, group.sortingOrder);
            }
            finally
            {
                FlightSortingChannel.Disarm(uid);
                Object.DestroyImmediate(cardGo);
                Object.DestroyImmediate(prefab);
                instanceField?.SetValue(null, null);
            }
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
