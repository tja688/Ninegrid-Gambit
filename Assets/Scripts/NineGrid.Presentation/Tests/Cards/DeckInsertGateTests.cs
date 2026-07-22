using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NineGrid.Cards;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 牌组入组索引门禁：随机落点、非空禁止最左 slot 0、显式末尾保留。
    /// </summary>
    public sealed class DeckInsertGateTests
    {
        private const int BaseUid = 8200;

        private GameObject _prefab;
        private CardManagerSingleton _cardManager;
        private CardDeckManagerSingleton _deckManager;

        [SetUp]
        public void SetUp()
        {
            DestroyAllSingletonsInScene();
            ResetPresentationSingletons();

            var cardGo = new GameObject("CardManagerTest");
            var deckGo = new GameObject("DeckManagerTest");

            _cardManager = cardGo.AddComponent<CardManagerSingleton>();
            _deckManager = deckGo.AddComponent<CardDeckManagerSingleton>();
            EnsureDeckManagerInitialized(_deckManager);

            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);

            ForceDeckMode(_deckManager, CardDeckMode.InGame);
        }

        [TearDown]
        public void TearDown()
        {
            DestroyManagerObject(_deckManager);
            DestroyManagerObject(_cardManager);

            if (_prefab != null)
            {
                UnityEngine.Object.DestroyImmediate(_prefab);
                _prefab = null;
            }

            ResetPresentationSingletons();
        }

        [Test]
        public void ResolveDeckInsertIndex_EmptyDeck_ReturnsZero()
        {
            Assert.AreEqual(0, InvokeResolveDeckInsertIndex(CardDeckManagerSingleton.RandomInsertIndex));
            Assert.AreEqual(0, InvokeResolveDeckInsertIndex(0));
        }

        [Test]
        public void ResolveDeckInsertIndex_NonEmptyRandom_NeverReturnsZero()
        {
            SeedDeck(3);

            for (var i = 0; i < 32; i++)
            {
                var index = InvokeResolveDeckInsertIndex(CardDeckManagerSingleton.RandomInsertIndex);
                Assert.GreaterOrEqual(index, 1);
                Assert.LessOrEqual(index, 3);
            }
        }

        [Test]
        public void ResolveDeckInsertIndex_NonEmptyExplicitZero_BumpsToOne()
        {
            SeedDeck(2);
            Assert.AreEqual(1, InvokeResolveDeckInsertIndex(0));
        }

        [Test]
        public void ResolveDeckInsertIndex_NonEmptyExplicitAppend_PreservesEnd()
        {
            SeedDeck(3);
            Assert.AreEqual(3, InvokeResolveDeckInsertIndex(3));
        }

        [Test]
        public void InsertViaAddAnchorAsync_NonEmptyDeck_NeverInsertsAtSlotZero()
        {
            SeedDeck(2);
            var incoming = SpawnDeckCard(BaseUid + 10);
            var slotIndex = InvokeResolveDeckInsertIndex(CardDeckManagerSingleton.RandomInsertIndex);
            Assert.Greater(slotIndex, 0);
            InvokeSlotContainerInsert(slotIndex, incoming);
            Assert.IsTrue(TryFindDeckSlotByUid(incoming.Uid, out var slot));
            Assert.Greater(slot, 0);
        }

        [Test]
        public void InsertViaAddAnchorAsync_EmptyDeck_InsertsAtSlotZero()
        {
            var incoming = SpawnDeckCard(BaseUid + 11);
            var slotIndex = InvokeResolveDeckInsertIndex(CardDeckManagerSingleton.RandomInsertIndex);
            Assert.AreEqual(0, slotIndex);
            InvokeSlotContainerInsert(slotIndex, incoming);
            Assert.IsTrue(TryFindDeckSlotByUid(incoming.Uid, out var slot));
            Assert.AreEqual(0, slot);
        }

        [Test]
        public void TryGetCardAtSlotZero_IsReachableForHover()
        {
            SeedDeck(1);
            Assert.IsTrue(_deckManager.TryGetFirstDeckSlot(out var deckSlot));
            Assert.AreEqual(0, deckSlot);
        }

        private void SeedDeck(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var card = SpawnDeckCard(BaseUid + i);
                var appendIndex = InvokeResolveDeckInsertIndex(_deckManager.DeckCount);
                InvokeSlotContainerInsert(appendIndex, card);
            }

            Assert.AreEqual(count, _deckManager.DeckCount);
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

        private ManagedCard SpawnDeckCard(int uid)
        {
            var card = _cardManager.SpawnView(uid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(card);
            _cardManager.SetDisplayMode(card, CardDisplayMode.CardDeckMode);
            return card;
        }

        private int InvokeResolveDeckInsertIndex(int requestedIndex)
        {
            var method = typeof(CardDeckManagerSingleton).GetMethod(
                "ResolveDeckInsertIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            return (int)method.Invoke(_deckManager, new object[] { requestedIndex });
        }

        private bool TryFindDeckSlotByUid(int uid, out int deckSlotIndex)
        {
            deckSlotIndex = -1;
            var method = typeof(CardDeckManagerSingleton).GetMethod(
                "TryFindDeckSlotByUid",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            var args = new object[] { uid, -1 };
            var found = (bool)method.Invoke(_deckManager, args);
            deckSlotIndex = (int)args[1];
            return found;
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
            Assert.IsNotNull(backing);
            backing.SetValue(deck, mode);
        }

        private static void DestroyAllSingletonsInScene()
        {
            DestroyAll<CardManagerSingleton>();
            DestroyAll<CardHandManagerSingleton>();
            DestroyAll<CardDeckManagerSingleton>();
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
