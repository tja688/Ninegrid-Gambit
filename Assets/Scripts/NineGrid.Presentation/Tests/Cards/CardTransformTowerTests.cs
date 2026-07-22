using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    public sealed class CardTransformTowerTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TowerTestCard");
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }
        }

        [Test]
        public void EnsureTower_CreatesFixedNamedChain_NeverReparentsRoot()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();

            Assert.AreSame(_root.transform, tower.CardRoot);
            Assert.AreEqual(CardTransformTower.BoardFrameName, tower.BoardFrame.name);
            Assert.AreEqual(CardTransformTower.SlotFrameName, tower.SlotFrame.name);
            Assert.AreEqual(CardTransformTower.EffectFrameName, tower.EffectFrame.name);
            Assert.AreEqual(CardTransformTower.CardVisualName, tower.CardVisual.name);
            Assert.AreEqual(CardTransformTower.FacePivotName, tower.FacePivot.name);

            Assert.AreSame(_root.transform, tower.BoardFrame.parent);
            Assert.AreSame(tower.BoardFrame, tower.SlotFrame.parent);
            Assert.AreSame(tower.SlotFrame, tower.EffectFrame.parent);
            Assert.AreSame(tower.EffectFrame, tower.CardVisual.parent);
            Assert.AreSame(tower.CardVisual, tower.FacePivot.parent);
        }

        [Test]
        public void EnsureTower_IsIdempotent()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var board = tower.BoardFrame;
            var visual = tower.CardVisual;

            tower.EnsureTower();

            Assert.AreSame(board, tower.BoardFrame);
            Assert.AreSame(visual, tower.CardVisual);
            Assert.AreEqual(1, _root.transform.childCount);
        }

        [Test]
        public void LayerDriver_ConvergeTo_ExactHitAtSourceTime()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var driver = _root.AddComponent<LayerConvergenceDriver>();

            var target = new Vector3(2f, -1.5f, 0f);
            const float sourceTime = 0.4f;
            driver.ConvergeTo(target, sourceTime);

            Assert.IsTrue(driver.IsActive);

            // 模拟变长帧推进到 sourceTime。
            var remaining = sourceTime;
            while (remaining > 0f)
            {
                var step = Mathf.Min(1f / 60f, remaining);
                driver.Tick(step);
                remaining -= step;
            }

            Assert.IsTrue(driver.IsComplete);
            Assert.AreEqual(target.x, tower.SlotFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(target.y, tower.SlotFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(Vector3.zero, driver.SampleVelocity());
        }

        [Test]
        public void LayerDriver_Evict_PhaseB_PassesVelocity()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var driver = _root.AddComponent<LayerConvergenceDriver>();

            driver.ConvergeTo(new Vector3(3f, 0f, 0f), 1f);
            driver.Tick(0.2f);

            var mid = tower.SlotFrame.localPosition;
            var expectedVelocity = driver.SampleVelocity();
            Assert.Greater(expectedVelocity.magnitude, 1e-3f, "中段应有非零速度");

            var handoff = driver.Evict();

            Assert.AreEqual(mid.x, handoff.LocalPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(mid.y, handoff.LocalPosition.y, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(expectedVelocity.x, handoff.LocalVelocity.x, 1e-4f);
            Assert.AreEqual(expectedVelocity.y, handoff.LocalVelocity.y, 1e-4f);
            Assert.AreEqual(Vector3.zero, driver.SampleVelocity());
            Assert.IsTrue(driver.IsComplete);
        }

        [Test]
        public void LayerDriver_AdmitThenConverge_StartsFromAdmittedPose()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var driver = _root.AddComponent<LayerConvergenceDriver>();

            driver.Admit(HandoffState.AtRest(new Vector3(1f, 2f, 0f)));
            Assert.AreEqual(1f, tower.SlotFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(2f, tower.SlotFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);

            driver.ConvergeTo(Vector3.zero, 0.3f);
            driver.Tick(0.3f);

            Assert.AreEqual(0f, tower.SlotFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(0f, tower.SlotFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
        }
    }
}
