using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using NineGrid.Cards.Convergence;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    /// <summary>
    /// 致死卸格 UID 安全、输入根对齐门禁回归。
    /// </summary>
    public sealed class LethalPresentationRegressionTests
    {
        private const int VictimUid = 8101;
        private const int NewOccupantUid = 8102;
        private const int StaleSlot = 2;

        private GameObject _prefab;
        private GameObject _anchorRoot;
        private CardManagerSingleton _cardManager;
        private GroundFieldManagerSingleton _fieldManager;

        [SetUp]
        public void SetUp()
        {
            DestroyAllSingletonsInScene();
            ResetPresentationSingletons();
            CardZoneOwnershipSink.Reset();

            var cardGo = new GameObject("CardManagerTest");
            var fieldGo = new GameObject("GroundFieldTest");

            _cardManager = cardGo.AddComponent<CardManagerSingleton>();
            _fieldManager = fieldGo.AddComponent<GroundFieldManagerSingleton>();

            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);
            SeedGroundAnchors();
        }

        [TearDown]
        public void TearDown()
        {
            CardZoneOwnershipSink.Reset();
            DestroyManagerObject(_fieldManager);
            DestroyManagerObject(_cardManager);

            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
                _prefab = null;
            }

            if (_anchorRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_anchorRoot);
                _anchorRoot = null;
            }

            ResetPresentationSingletons();
        }

        [Test]
        public void VacateSlotForExplore_RefusesWhenOccupantUidMismatch()
        {
            var victim = _cardManager.SpawnView(VictimUid, CardManagerSingleton.StandardDefId);
            var newCard = _cardManager.SpawnView(NewOccupantUid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(victim);
            Assert.IsNotNull(newCard);

            SeedOccupancy(newCard, StaleSlot);

            var vacate = typeof(GroundFieldManagerSingleton).GetMethod(
                "VacateSlotForExplore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(vacate, "VacateSlotForExplore missing");

            vacate.Invoke(
                _fieldManager,
                new object[] { StaleSlot, victim, false, true, false });

            Assert.IsTrue(_fieldManager.TryGetSlotOf(NewOccupantUid, out var slot));
            Assert.AreEqual(StaleSlot, slot);
            Assert.IsFalse(_fieldManager.TryGetSlotOf(VictimUid, out _));
        }

        [Test]
        public void GroundInputGate_BlocksWhenL0RootMisalignedWithRegisteredAnchor()
        {
            var card = _cardManager.SpawnView(8201, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(card);
            Assert.IsTrue(SlotFrameConvergence.TryEnsureInfrastructure(card, out var tower, out _, "test"));

            SeedOccupancy(card, StaleSlot);
            var anchor = _fieldManager.GetGroundAnchor(StaleSlot);
            Assert.IsNotNull(anchor);

            tower.CardRoot.position = anchor.position + new Vector3(2f, 0f, 0f);
            tower.SlotFrame.localPosition = Vector3.zero;

            var proxyGo = new GameObject("HitProxyTest");
            try
            {
                proxyGo.transform.SetParent(card.Transform, false);
                var proxy = proxyGo.AddComponent<GroundCardHitProxy>();
                var gate = typeof(GroundCardHitProxy).GetMethod(
                    "TryPassGroundInputGate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(gate, "TryPassGroundInputGate missing");

                var args = new object[] { card, null };
                var passed = (bool)gate.Invoke(proxy, args);
                var blockReason = args[1] as string;

                Assert.IsFalse(passed);
                Assert.AreEqual("rootMisaligned", blockReason);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(proxyGo);
            }
        }

        [Test]
        public void GroundInputGate_BlocksWhenSlotConvergenceActive()
        {
            var card = _cardManager.SpawnView(8202, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(card);
            Assert.IsTrue(SlotFrameConvergence.TryEnsureInfrastructure(card, out var tower, out var driver, "test"));

            SeedOccupancy(card, StaleSlot);
            var anchor = _fieldManager.GetGroundAnchor(StaleSlot);
            Assert.IsNotNull(anchor);

            tower.CardRoot.position = anchor.position;
            driver.ConvergeTo(Vector3.one, 1f);

            var proxyGo = new GameObject("HitProxyConvergeTest");
            try
            {
                proxyGo.transform.SetParent(card.Transform, false);
                var proxy = proxyGo.AddComponent<GroundCardHitProxy>();
                var gate = typeof(GroundCardHitProxy).GetMethod(
                    "TryPassGroundInputGate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(gate);

                var args = new object[] { card, null };
                var passed = (bool)gate.Invoke(proxy, args);
                var blockReason = args[1] as string;

                Assert.IsFalse(passed);
                Assert.AreEqual("slotConverging", blockReason);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(proxyGo);
            }
        }

        private void SeedGroundAnchors()
        {
            _anchorRoot = new GameObject("GroundAnchorsTest");
            var anchorsField = typeof(GroundFieldManagerSingleton).GetField(
                "_groundAnchors",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(anchorsField, "_groundAnchors missing");
            var anchors = (List<Transform>)anchorsField.GetValue(_fieldManager);
            anchors.Clear();

            for (var i = 0; i < GroundSlotTopology.MaxSlot; i++)
            {
                var go = new GameObject("Slot" + (i + 1));
                go.transform.SetParent(_anchorRoot.transform);
                go.transform.position = new Vector3(i, 0f, 0f);
                anchors.Add(go.transform);
            }
        }

        private void SeedOccupancy(ManagedCard card, int slot)
        {
            var register = typeof(GroundFieldManagerSingleton).GetMethod(
                "TryRegisterCardAtSlot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(register, "TryRegisterCardAtSlot missing");

            var registered = (bool)register.Invoke(_fieldManager, new object[] { slot, card.Uid, false });
            Assert.IsTrue(registered);
        }

        private static void DestroyAllSingletonsInScene()
        {
            DestroyAll<CardManagerSingleton>();
            DestroyAll<CardHandManagerSingleton>();
            DestroyAll<CardDeckManagerSingleton>();
            DestroyAll<GroundFieldManagerSingleton>();
        }

        private static void DestroyAll<T>() where T : MonoBehaviour
        {
            var found = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(found[i].gameObject);
                }
            }
        }

        private static void ResetPresentationSingletons()
        {
            ResetStaticInstance(typeof(CardManagerSingleton), "_instance");
            ResetStaticInstance(typeof(CardHandManagerSingleton), "_instance");
            ResetStaticInstance(typeof(CardDeckManagerSingleton), "_instance");
            ResetStaticInstance(typeof(GroundFieldManagerSingleton), "_instance");
        }

        private static void ResetStaticInstance(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }

        private static void DestroyManagerObject(MonoBehaviour manager)
        {
            if (manager == null)
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(manager.gameObject);
        }
    }
}
