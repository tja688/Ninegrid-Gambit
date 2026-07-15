using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    public sealed class SlotFrameConvergenceTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("SlotFrameConvergenceCard");
            _root.transform.position = new Vector3(1f, 2f, 0f);
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
        public void WorldToSlotLocal_AtRestZero_WhenWorldEqualsRoot()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();

            var local = SlotFrameConvergence.WorldToSlotLocal(tower, _root.transform.position);

            Assert.AreEqual(0f, local.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(0f, local.y, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(0f, local.z, ConvergenceCurve1D.PositionEpsilon);
        }

        [Test]
        public void WorldToSlotLocal_MatchesOffset_WhenRootParked()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var targetWorld = _root.transform.position + new Vector3(3f, -1.5f, 0f);

            var local = SlotFrameConvergence.WorldToSlotLocal(tower, targetWorld);

            Assert.AreEqual(3f, local.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(-1.5f, local.y, ConvergenceCurve1D.PositionEpsilon);
        }

        [Test]
        public void BeginDealFromLaunch_ConvergesL2Home_WithoutMovingRootDuringCurve()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var driver = _root.AddComponent<LayerConvergenceDriver>();

            var slotWorld = new Vector3(5f, 0f, 0f);
            var launchWorld = new Vector3(0f, 4f, 0f);
            const float sourceTime = 0.3f;

            // ManagedCard 在 EditMode 不便构造：直接走塔/驱动器约定路径。
            CardDeckTween.KillMotion(_root.transform);
            tower.CardRoot.position = slotWorld;
            var launchLocal = SlotFrameConvergence.WorldToSlotLocal(tower, launchWorld);
            driver.Admit(HandoffState.AtRest(launchLocal));
            driver.ConvergeTo(Vector3.zero, sourceTime);

            var rootAtStart = tower.CardRoot.position;
            Assert.AreEqual(slotWorld.x, rootAtStart.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(launchLocal.x, tower.SlotFrame.localPosition.x, 0.01f);

            var remaining = sourceTime;
            while (remaining > 0f)
            {
                var step = Mathf.Min(1f / 60f, remaining);
                driver.Tick(step);
                remaining -= step;
                Assert.AreEqual(slotWorld.x, tower.CardRoot.position.x, ConvergenceCurve1D.PositionEpsilon);
            }

            Assert.IsTrue(driver.IsComplete);
            Assert.AreEqual(0f, tower.SlotFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(0f, tower.SlotFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
        }

        [Test]
        public void Redirect_RetargetsWithoutReparent()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var driver = _root.AddComponent<LayerConvergenceDriver>();
            var parentBefore = tower.SlotFrame.parent;

            driver.ConvergeTo(new Vector3(2f, 0f, 0f), 0.5f);
            driver.Tick(0.1f);
            driver.Redirect(new Vector3(-1f, 1f, 0f), 0.4f);

            var remaining = 0.4f;
            while (remaining > 0f)
            {
                var step = Mathf.Min(1f / 60f, remaining);
                driver.Tick(step);
                remaining -= step;
            }

            Assert.AreSame(parentBefore, tower.SlotFrame.parent);
            Assert.AreEqual(-1f, tower.SlotFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(1f, tower.SlotFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
        }
    }
}
