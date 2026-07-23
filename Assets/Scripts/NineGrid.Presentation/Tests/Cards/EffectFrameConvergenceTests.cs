using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    public sealed class EffectFrameConvergenceTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("EffectFrameConvergenceCard");
            _root.transform.position = new Vector3(2f, 1f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        private LayerConvergenceDriver InstallTowerWithDrivers()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            LayerConvergenceDriver.Ensure(_root.transform, TowerLayer.SlotFrame);
            return LayerConvergenceDriver.Ensure(_root.transform, TowerLayer.EffectFrame);
        }

        [Test]
        public void WorldToEffectLocal_MatchesOffset_WhenAtRest()
        {
            InstallTowerWithDrivers();
            Assert.IsTrue(EffectFrameConvergence.TryGetTower(_root.transform, out var tower));

            var targetWorld = _root.transform.position + new Vector3(1.5f, -0.5f, 0f);
            var local = EffectFrameConvergence.WorldToEffectLocal(tower, targetWorld);

            Assert.AreEqual(1.5f, local.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(-0.5f, local.y, ConvergenceCurve1D.PositionEpsilon);
        }

        [Test]
        public void ConvergeToWorld_HitsTargetOnL3_WithoutMovingRootOrL2()
        {
            var effectDriver = InstallTowerWithDrivers();
            Assert.IsTrue(EffectFrameConvergence.TryGetTower(_root.transform, out var tower));

            var rootStart = tower.CardRoot.position;
            var slotStart = tower.SlotFrame.localPosition;
            var targetWorld = rootStart + new Vector3(3f, 1f, 0f);
            const float sourceTime = 0.25f;

            EffectFrameConvergence.ConvergeVisualToWorld(_root.transform, targetWorld, sourceTime);

            var remaining = sourceTime;
            while (remaining > 0f)
            {
                var step = Mathf.Min(1f / 60f, remaining);
                effectDriver.Tick(step);
                remaining -= step;
                Assert.AreEqual(rootStart.x, tower.CardRoot.position.x, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(slotStart.x, tower.SlotFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            }

            Assert.IsTrue(effectDriver.IsComplete);
            var expectedLocal = EffectFrameConvergence.WorldToEffectLocal(tower, targetWorld);
            Assert.AreEqual(expectedLocal.x, tower.EffectFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(expectedLocal.y, tower.EffectFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
        }

        [Test]
        public void ConvergeHome_ReturnsL3ToZero()
        {
            var effectDriver = InstallTowerWithDrivers();
            Assert.IsTrue(EffectFrameConvergence.TryGetTower(_root.transform, out var tower));

            effectDriver.Admit(HandoffState.AtRest(new Vector3(2f, -1f, 0f)));
            EffectFrameConvergence.ConvergeHome(_root.transform, 0.2f);

            var remaining = 0.2f;
            while (remaining > 0f)
            {
                var step = Mathf.Min(1f / 60f, remaining);
                effectDriver.Tick(step);
                remaining -= step;
            }

            Assert.AreEqual(0f, tower.EffectFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(0f, tower.EffectFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
        }

        [Test]
        public void ParkRootAtWorld_MovesL0AndZerosL3()
        {
            InstallTowerWithDrivers();
            Assert.IsTrue(EffectFrameConvergence.TryGetTower(_root.transform, out var tower));

            tower.EffectFrame.localPosition = new Vector3(4f, 2f, 0f);
            var park = new Vector3(10f, -3f, 0f);
            EffectFrameConvergence.ParkRootAtWorld(_root.transform, park, "test");

            Assert.AreEqual(park.x, tower.CardRoot.position.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(park.y, tower.CardRoot.position.y, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(0f, tower.EffectFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(0f, tower.EffectFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
        }

        [Test]
        public void DualDrivers_L2AndL3_DoNotCrossWrite()
        {
            var effectDriver = InstallTowerWithDrivers();
            Assert.IsTrue(LayerConvergenceDriver.TryGet(_root.transform, TowerLayer.SlotFrame, out var slotDriver));
            Assert.IsTrue(EffectFrameConvergence.TryGetTower(_root.transform, out var tower));

            slotDriver.ConvergeTo(new Vector3(1f, 0f, 0f), 0.3f);
            effectDriver.ConvergeTo(new Vector3(0f, 2f, 0f), 0.3f);

            slotDriver.Tick(0.3f);
            effectDriver.Tick(0.3f);

            Assert.AreEqual(1f, tower.SlotFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(0f, tower.SlotFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(0f, tower.EffectFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(2f, tower.EffectFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
        }

        [Test]
        public void FusionMidpoint_TwoCardsShareArrival_NoReparent()
        {
            var a = new GameObject("FusionA");
            var b = new GameObject("FusionB");
            try
            {
                a.transform.position = new Vector3(-2f, 0f, 0f);
                b.transform.position = new Vector3(2f, 0f, 0f);

                var towerA = a.AddComponent<CardTransformTower>();
                towerA.EnsureTower();
                var driverA = LayerConvergenceDriver.Ensure(a.transform, TowerLayer.EffectFrame);

                var towerB = b.AddComponent<CardTransformTower>();
                towerB.EnsureTower();
                var driverB = LayerConvergenceDriver.Ensure(b.transform, TowerLayer.EffectFrame);

                var merge = (a.transform.position + b.transform.position) * 0.5f;
                const float sourceTime = 0.4f;
                var parentA = towerA.EffectFrame.parent;
                var parentB = towerB.EffectFrame.parent;

                EffectFrameConvergence.ConvergeVisualToWorld(a.transform, merge, sourceTime);
                EffectFrameConvergence.ConvergeVisualToWorld(b.transform, merge, sourceTime);

                var remaining = sourceTime;
                while (remaining > 0f)
                {
                    var step = Mathf.Min(1f / 60f, remaining);
                    driverA.Tick(step);
                    driverB.Tick(step);
                    remaining -= step;
                }

                Assert.IsTrue(driverA.IsComplete);
                Assert.IsTrue(driverB.IsComplete);
                Assert.AreSame(parentA, towerA.EffectFrame.parent);
                Assert.AreSame(parentB, towerB.EffectFrame.parent);

                var visualA = towerA.EffectFrame.position;
                var visualB = towerB.EffectFrame.position;
                Assert.AreEqual(merge.x, visualA.x, 0.01f);
                Assert.AreEqual(merge.y, visualA.y, 0.01f);
                Assert.AreEqual(merge.x, visualB.x, 0.01f);
                Assert.AreEqual(merge.y, visualB.y, 0.01f);

                EffectFrameConvergence.ParkRootAtWorld(a.transform, merge);
                EffectFrameConvergence.ParkRootAtWorld(b.transform, merge);
                Assert.AreEqual(0f, towerA.EffectFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, towerB.EffectFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void HardStick_AfterParkWithL2Residue_ForcesVisualCoincidence()
        {
            // 复现病灶：ParkRoot 只清 L3，残留 L2 时两卡叠不齐；HardStick（Slot+Effect SnapHome）必须贴死。
            DestroyAllSingletonsInScene();
            var host = new GameObject("HardStickHost");
            var cardManager = host.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("HardStickPrefab");
            prefab.AddComponent<StandardCardView>();
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            ManagedCard cardA = null;
            ManagedCard cardB = null;
            try
            {
                cardA = cardManager.SpawnView(9401, CardManagerSingleton.StandardDefId);
                cardB = cardManager.SpawnView(9402, CardManagerSingleton.StandardDefId);
                Assert.IsTrue(SlotFrameConvergence.TryEnsureInfrastructure(cardA, out var towerA, out _, "test"));
                Assert.IsTrue(EffectFrameConvergence.TryEnsureInfrastructure(cardA, out _, out _, "test"));
                Assert.IsTrue(SlotFrameConvergence.TryEnsureInfrastructure(cardB, out var towerB, out _, "test"));
                Assert.IsTrue(EffectFrameConvergence.TryEnsureInfrastructure(cardB, out _, out _, "test"));

                towerA.CardRoot.position = new Vector3(-2f, 0f, 0f);
                towerB.CardRoot.position = new Vector3(2f, 1f, 0f);
                towerA.SlotFrame.localPosition = new Vector3(0.4f, -0.2f, 0f);
                towerA.EffectFrame.localPosition = new Vector3(0.1f, 0.3f, 0f);
                towerB.SlotFrame.localPosition = new Vector3(-0.5f, 0.25f, 0f);
                towerB.EffectFrame.localPosition = new Vector3(0.2f, -0.15f, 0f);

                var visualBeforeA = SlotFrameConvergence.GetVisualWorldPosition(cardA);
                var visualBeforeB = SlotFrameConvergence.GetVisualWorldPosition(cardB);
                var merge = (visualBeforeA + visualBeforeB) * 0.5f;

                EffectFrameConvergence.ParkRootAtWorld(cardA, merge, "test.Park");
                EffectFrameConvergence.ParkRootAtWorld(cardB, merge, "test.Park");

                var afterParkA = SlotFrameConvergence.GetVisualWorldPosition(cardA);
                var afterParkB = SlotFrameConvergence.GetVisualWorldPosition(cardB);
                Assert.Greater(
                    Vector3.Distance(afterParkA, afterParkB),
                    1e-3f,
                    "ParkRoot alone must leave L2 residue misalignment (documents the bug).");

                SlotFrameConvergence.SnapHome(cardA, merge, "Skeleton.HardStick", cardA.Uid);
                EffectFrameConvergence.SnapHome(cardA, "Skeleton.HardStick");
                SlotFrameConvergence.SnapHome(cardB, merge, "Skeleton.HardStick", cardB.Uid);
                EffectFrameConvergence.SnapHome(cardB, "Skeleton.HardStick");

                var visualA = SlotFrameConvergence.GetVisualWorldPosition(cardA);
                var visualB = SlotFrameConvergence.GetVisualWorldPosition(cardB);
                Assert.Less(Vector3.Distance(visualA, visualB), 1e-3f);
                Assert.Less(Vector3.Distance(visualA, merge), 1e-3f);
                Assert.AreEqual(0f, towerA.SlotFrame.localPosition.magnitude, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, towerB.SlotFrame.localPosition.magnitude, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, towerA.EffectFrame.localPosition.magnitude, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, towerB.EffectFrame.localPosition.magnitude, ConvergenceCurve1D.PositionEpsilon);
            }
            finally
            {
                FlightSortingChannel.Disarm(9401);
                FlightSortingChannel.Disarm(9402);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void RevealResult_SnapHomeSlotAndEffect_LandsOnMergeCenter()
        {
            DestroyAllSingletonsInScene();
            var host = new GameObject("RevealResultHost");
            var cardManager = host.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("RevealResultPrefab");
            prefab.AddComponent<StandardCardView>();
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            ManagedCard resultCard = null;
            try
            {
                resultCard = cardManager.SpawnView(9403, CardManagerSingleton.StandardDefId);
                Assert.IsTrue(SlotFrameConvergence.TryEnsureInfrastructure(resultCard, out var tower, out _, "test"));
                Assert.IsTrue(EffectFrameConvergence.TryEnsureInfrastructure(resultCard, out _, out _, "test"));

                tower.CardRoot.position = new Vector3(5f, -4f, 0f);
                tower.SlotFrame.localPosition = new Vector3(1.2f, -0.8f, 0f);
                tower.EffectFrame.localPosition = new Vector3(-0.3f, 0.6f, 0f);

                var mergeCenter = new Vector3(0.5f, 1.5f, 0f);
                SlotFrameConvergence.SnapHome(resultCard, mergeCenter, "Skeleton.RevealResult", resultCard.Uid);
                EffectFrameConvergence.SnapHome(resultCard, "Skeleton.RevealResult");

                var visual = SlotFrameConvergence.GetVisualWorldPosition(resultCard);
                Assert.Less(Vector3.Distance(visual, mergeCenter), 1e-3f);
                Assert.AreEqual(0f, tower.EffectFrame.localPosition.magnitude, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, tower.SlotFrame.localPosition.magnitude, ConvergenceCurve1D.PositionEpsilon);
            }
            finally
            {
                FlightSortingChannel.Disarm(9403);
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(prefab);
            }
        }

        private static void DestroyAllSingletonsInScene()
        {
            foreach (var m in Object.FindObjectsByType<CardManagerSingleton>(FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(m.gameObject);
            }

            var field = typeof(CardManagerSingleton).GetField(
                "_instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }
    }
}
