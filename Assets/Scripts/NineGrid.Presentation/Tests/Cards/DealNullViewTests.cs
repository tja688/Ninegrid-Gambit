using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// View 为空的僵尸句柄发牌：须返回 false 且不残留场地占格。
    /// </summary>
    public sealed class DealNullViewTests
    {
        private const int TestUid = 9401;
        private const int GroundSlot = 1;

        private GameObject _prefab;
        private GameObject _anchorRoot;
        private CardManagerSingleton _cardManager;
        private CardDeckManagerSingleton _deckManager;
        private GroundFieldManagerSingleton _fieldManager;

        [SetUp]
        public void SetUp()
        {
            DestroyAllSingletonsInScene();
            ResetPresentationSingletons();

            var cardGo = new GameObject("CardManagerTest");
            var deckGo = new GameObject("DeckManagerTest");
            var fieldGo = new GameObject("GroundFieldTest");

            _cardManager = cardGo.AddComponent<CardManagerSingleton>();
            _deckManager = deckGo.AddComponent<CardDeckManagerSingleton>();
            _fieldManager = fieldGo.AddComponent<GroundFieldManagerSingleton>();
            EnsureDeckManagerInitialized(_deckManager);

            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);

            SeedGroundAnchors(9);
            ForceDeckMode(_deckManager, CardDeckMode.InGame);
        }

        [TearDown]
        public void TearDown()
        {
            DestroyManagerObject(_fieldManager);
            DestroyManagerObject(_deckManager);
            DestroyManagerObject(_cardManager);

            if (_anchorRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_anchorRoot);
                _anchorRoot = null;
            }

            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
                _prefab = null;
            }

            ResetPresentationSingletons();
        }

        [Test]
        public void DealCardByUid_WithDestroyedView_ReturnsFalseAndLeavesSlotEmpty()
        {
            var card = _cardManager.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(card);
            _cardManager.SetDisplayMode(card, CardDisplayMode.CardDeckMode);
            InvokeSlotContainerInsert(0, card);
            Assert.IsTrue(_deckManager.ContainsUid(TestUid));

            // 模拟僵尸句柄：注册表仍有 ManagedCard，但 View 已被销毁。
            UnityEngine.Object.DestroyImmediate(card.GameObject);
            Assert.IsTrue(card.View == null || card.Transform == null);

            var (ok, handle) = _deckManager
                .DealCardByUidWithFlightAsync(
                    TestUid,
                    GroundSlot,
                    ensureCard: null,
                    skipBusyGuard: true,
                    flightContext: null,
                    cancellationToken: CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.IsFalse(ok);
            Assert.IsNull(handle);
            Assert.IsFalse(
                _fieldManager.TryGetCardAt(GroundSlot, out _),
                "nullView deal must not leave ground occupancy");
            Assert.IsFalse(
                _fieldManager.TryGetSlotOf(TestUid, out _),
                "nullView deal must not register uid on field");
        }

        private void SeedGroundAnchors(int count)
        {
            _anchorRoot = new GameObject("GroundAnchorsTest");
            var anchors = new List<Transform>(count);
            for (var i = 0; i < count; i++)
            {
                var go = new GameObject($"Slot{i + 1}");
                go.transform.SetParent(_anchorRoot.transform);
                go.transform.position = new Vector3(i, 0f, 0f);
                anchors.Add(go.transform);
            }

            var field = typeof(CardDeckManagerSingleton).GetField(
                "_groundAnchors",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_groundAnchors missing");
            field.SetValue(_deckManager, anchors);
        }

        private void InvokeSlotContainerInsert(int slotIndex, ManagedCard card)
        {
            var slotField = typeof(CardDeckManagerSingleton).GetField(
                "_slotContainer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(slotField);
            var container = slotField.GetValue(_deckManager);
            Assert.IsNotNull(container);

            var tryInsert = container.GetType().GetMethod("TryInsertAt");
            Assert.IsNotNull(tryInsert);
            var args = new object[] { slotIndex, card, null };
            var ok = (bool)tryInsert.Invoke(container, args);
            Assert.IsTrue(ok);
        }

        private static void EnsureDeckManagerInitialized(CardDeckManagerSingleton deckManager)
        {
            var slotField = typeof(CardDeckManagerSingleton).GetField(
                "_slotContainer",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(slotField);
            if (slotField.GetValue(deckManager) != null)
            {
                return;
            }

            var awake = typeof(CardDeckManagerSingleton).GetMethod(
                "Awake",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(awake);
            awake.Invoke(deckManager, null);
            Assert.IsNotNull(slotField.GetValue(deckManager));
        }

        private static void ForceDeckMode(CardDeckManagerSingleton deck, CardDeckMode mode)
        {
            var backing = typeof(CardDeckManagerSingleton).GetField(
                "<CurrentMode>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(backing, "CurrentMode backing field missing");
            backing.SetValue(deck, mode);
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
