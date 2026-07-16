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

        [Test]
        public void ConvergeVisualToWorld_EnsuresMissingTowerAndDriver_ThenRunsCurve()
        {
            // 无塔无 driver 的裸根 + ManagedCard：入口应补齐后走 L2 曲线。
            DestroyAllSingletonsInScene();
            var cardGo = new GameObject("CardManagerEnsureTest");
            var cardManager = cardGo.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("EnsurePrefab");
            prefab.AddComponent<StandardCardView>();
            // 故意不挂塔/driver，由 SpawnView 补齐；再剥掉以模拟 race。
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            ManagedCard card = null;
            try
            {
                card = cardManager.SpawnView(9201, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(card);

                var tower = card.Transform.GetComponent<CardTransformTower>();
                var drivers = card.Transform.GetComponents<LayerConvergenceDriver>();
                Assert.IsNotNull(tower);
                Assert.Greater(drivers.Length, 0);

                // 剥掉驱动器模拟 timing race；塔保留但 Ensure 仍应补回 SlotFrame driver。
                foreach (var d in drivers)
                {
                    Object.DestroyImmediate(d);
                }

                Assert.IsFalse(SlotFrameConvergence.TryGetDriver(card, out _));

                var target = card.Transform.position + new Vector3(2f, 1f, 0f);
                const float sourceTime = 0.2f;
                SlotFrameConvergence.ConvergeVisualToWorld(card, target, sourceTime);

                Assert.IsTrue(SlotFrameConvergence.TryGetDriver(card, out var driver));
                Assert.IsTrue(driver.IsActive);

                var remaining = sourceTime;
                while (remaining > 0f)
                {
                    var step = Mathf.Min(1f / 60f, remaining);
                    driver.Tick(step);
                    remaining -= step;
                }

                Assert.IsTrue(driver.IsComplete);
            }
            finally
            {
                FlightSortingChannel.Disarm(9201);
                Object.DestroyImmediate(cardGo);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void GetVisualWorldPosition_IncludesSlotFrameOffset_WhileRootParked()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var slotWorld = new Vector3(5f, 0f, 0f);
            tower.CardRoot.position = slotWorld;
            tower.SlotFrame.localPosition = new Vector3(0f, 2.5f, 0f);

            // 无 ManagedCard：用塔直接验证合成；入口 API 对 ManagedCard 走同一优先序。
            var visual = tower.CardVisual != null
                ? tower.CardVisual.position
                : tower.SlotFrame.position;
            Assert.AreEqual(5f, visual.x, 0.01f);
            Assert.AreEqual(2.5f, visual.y, 0.01f);
            Assert.AreEqual(slotWorld.x, tower.CardRoot.position.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.Greater(Mathf.Abs(tower.CardRoot.position.y - visual.y), 0.01f);
        }

        [Test]
        public void BeginDeal_VisualNearTarget_WhileRootStillAtBirth_IsNotRootMismatch()
        {
            var tower = _root.AddComponent<CardTransformTower>();
            tower.EnsureTower();
            var driver = _root.AddComponent<LayerConvergenceDriver>();

            var birthWorld = new Vector3(-2f, 0f, 0f);
            var newSlotWorld = new Vector3(-2f, 2.5f, 0f);
            tower.CardRoot.position = birthWorld;
            var targetLocal = SlotFrameConvergence.WorldToSlotLocal(tower, newSlotWorld);
            driver.Admit(HandoffState.AtRest(Vector3.zero));
            driver.ConvergeTo(targetLocal, 0.01f);
            driver.Tick(0.01f);

            Assert.IsTrue(driver.IsComplete);
            var visual = tower.SlotFrame.position;
            Assert.AreEqual(newSlotWorld.x, visual.x, 0.05f);
            Assert.AreEqual(newSlotWorld.y, visual.y, 0.05f);
            // L0 仍停在 birth：若 Sync 只读 root 会假 mismatch。
            Assert.AreEqual(birthWorld.x, tower.CardRoot.position.x, ConvergenceCurve1D.PositionEpsilon);
            Assert.AreEqual(birthWorld.y, tower.CardRoot.position.y, ConvergenceCurve1D.PositionEpsilon);
            Assert.Greater((tower.CardRoot.position - newSlotWorld).sqrMagnitude, 0.01f);
            Assert.Less((visual - newSlotWorld).sqrMagnitude, 0.01f);
        }

        [Test]
        public void SnapHome_ZerosSlotFrame_UnlikeRawRootWrite()
        {
            DestroyAllSingletonsInScene();
            var cardGo = new GameObject("SnapHomeVsRaw");
            var cardManager = cardGo.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("SnapHomePrefab");
            prefab.AddComponent<StandardCardView>();
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            ManagedCard card = null;
            try
            {
                card = cardManager.SpawnView(9301, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(card);
                Assert.IsTrue(SlotFrameConvergence.TryEnsureInfrastructure(card, out var tower, out _, "test"));

                tower.CardRoot.position = new Vector3(1f, 0f, 0f);
                tower.SlotFrame.localPosition = new Vector3(0f, 2f, 0f);
                var anchor = new Vector3(3f, 4f, 0f);

                SlotFrameConvergence.SnapHome(card, anchor, "test.SnapHome", card.Uid);

                Assert.AreEqual(anchor.x, tower.CardRoot.position.x, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(anchor.y, tower.CardRoot.position.y, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, tower.SlotFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, tower.SlotFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);

                // 裸写 root 不归零 L2 —— 这正是旧 hardSet 的病灶。
                tower.SlotFrame.localPosition = new Vector3(0f, 2f, 0f);
                card.Transform.position = anchor;
                Assert.AreEqual(2f, tower.SlotFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
            }
            finally
            {
                FlightSortingChannel.Disarm(9301);
                Object.DestroyImmediate(cardGo);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void GetVisualWorldPosition_ManagedCard_MatchesSlotFrameWhenOffset()
        {
            DestroyAllSingletonsInScene();
            var cardGo = new GameObject("VisualWorldPos");
            var cardManager = cardGo.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("VisualPrefab");
            prefab.AddComponent<StandardCardView>();
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            ManagedCard card = null;
            try
            {
                card = cardManager.SpawnView(9302, CardManagerSingleton.StandardDefId);
                Assert.IsTrue(SlotFrameConvergence.TryGetTower(card, out var tower));
                tower.CardRoot.position = new Vector3(1f, 1f, 0f);
                tower.SlotFrame.localPosition = new Vector3(2f, 3f, 0f);

                var visual = SlotFrameConvergence.GetVisualWorldPosition(card);
                Assert.AreEqual(tower.CardVisual != null ? tower.CardVisual.position.x : tower.SlotFrame.position.x,
                    visual.x, 0.01f);
                Assert.AreEqual(tower.CardVisual != null ? tower.CardVisual.position.y : tower.SlotFrame.position.y,
                    visual.y, 0.01f);
                Assert.Greater(Mathf.Abs(card.Transform.position.x - visual.x), 0.01f);
            }
            finally
            {
                FlightSortingChannel.Disarm(9302);
                Object.DestroyImmediate(cardGo);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void SanitizeForSanctuary_PreservesVisualWorld_AndZerosL2L3()
        {
            DestroyAllSingletonsInScene();
            var cardGo = new GameObject("SanitizeSanctuary");
            var cardManager = cardGo.AddComponent<CardManagerSingleton>();
            var prefab = new GameObject("SanitizePrefab");
            prefab.AddComponent<StandardCardView>();
            cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, prefab);

            ManagedCard card = null;
            try
            {
                card = cardManager.SpawnView(9303, CardManagerSingleton.StandardDefId);
                Assert.IsTrue(SlotFrameConvergence.TryEnsureInfrastructure(card, out var tower, out _, "test"));
                Assert.IsTrue(EffectFrameConvergence.TryEnsureInfrastructure(card, out _, out _, "test"));

                tower.CardRoot.position = new Vector3(1f, 2f, 0f);
                tower.SlotFrame.localPosition = new Vector3(3f, -1f, 0f);
                tower.EffectFrame.localPosition = new Vector3(0.5f, 0.25f, 0f);
                var expectedVisual = SlotFrameConvergence.GetVisualWorldPosition(card);

                SlotFrameConvergence.SanitizeForSanctuary(card, "test.Sanitize");

                Assert.AreEqual(0f, tower.SlotFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, tower.SlotFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, tower.EffectFrame.localPosition.x, ConvergenceCurve1D.PositionEpsilon);
                Assert.AreEqual(0f, tower.EffectFrame.localPosition.y, ConvergenceCurve1D.PositionEpsilon);

                var afterVisual = SlotFrameConvergence.GetVisualWorldPosition(card);
                Assert.AreEqual(expectedVisual.x, afterVisual.x, 0.05f);
                Assert.AreEqual(expectedVisual.y, afterVisual.y, 0.05f);
                Assert.AreEqual(expectedVisual.x, tower.CardRoot.position.x, 0.05f);
                Assert.AreEqual(expectedVisual.y, tower.CardRoot.position.y, 0.05f);
            }
            finally
            {
                FlightSortingChannel.Disarm(9303);
                Object.DestroyImmediate(cardGo);
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
