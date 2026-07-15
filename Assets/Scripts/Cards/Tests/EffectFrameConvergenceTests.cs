using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Tests
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
    }
}
