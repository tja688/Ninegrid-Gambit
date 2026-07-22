using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    /// <summary>
    /// 手牌与卡组容器互斥：禁止 LaunchReturn / AddCardAt 将手牌视图吸纳进卡组槽。
    /// </summary>
    public sealed class HandDeckOwnershipTests
    {
        private const int TestUid = 7101;

        private GameObject _prefab;
        private CardManagerSingleton _cardManager;
        private CardDeckManagerSingleton _deckManager;

        [SetUp]
        public void SetUp()
        {
            DestroyAllSingletonsInScene();
            ResetPresentationSingletons();
            CardZoneOwnershipHook.Reset();

            var cardGo = new GameObject("CardManagerTest");
            var deckGo = new GameObject("DeckManagerTest");

            _cardManager = cardGo.AddComponent<CardManagerSingleton>();
            _deckManager = deckGo.AddComponent<CardDeckManagerSingleton>();

            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);

            ForceDeckMode(_deckManager, CardDeckMode.InGame);
        }

        [TearDown]
        public void TearDown()
        {
            CardZoneOwnershipHook.Reset();
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
        public void LaunchReturnFieldCardToDeck_RejectsHandCardDisplayMode()
        {
            var card = SpawnGroundCard(TestUid);
            _cardManager.SetDisplayMode(card, CardDisplayMode.HandCardMode);

            var launched = _deckManager.LaunchReturnFieldCardToDeck(card, 0);

            Assert.IsFalse(launched);
            Assert.IsFalse(_deckManager.ContainsUid(TestUid));
        }

        [Test]
        public void LaunchReturnFieldCardToDeck_RejectsCoreItemSlotsViaHook()
        {
            var card = SpawnGroundCard(TestUid);
            CardZoneOwnershipHook.IsCoreItemSlots = uid => uid == TestUid;

            var launched = _deckManager.LaunchReturnFieldCardToDeck(card, 0);

            Assert.IsFalse(launched);
            Assert.IsFalse(_deckManager.ContainsUid(TestUid));
        }

        [Test]
        public void AddCardAtFromOriginAsync_RejectsHandCardDisplayMode()
        {
            var card = SpawnGroundCard(TestUid);
            _cardManager.SetDisplayMode(card, CardDisplayMode.HandCardMode);

            var ok = _deckManager
                .AddCardAtFromOriginAsync(0, card, null, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.IsFalse(ok);
            Assert.IsFalse(_deckManager.ContainsUid(TestUid));
        }

        private ManagedCard SpawnGroundCard(int uid)
        {
            var card = _cardManager.SpawnView(uid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(card, "SpawnView failed — prefab not registered?");
            _cardManager.SetDisplayMode(card, CardDisplayMode.GroundCardMode);
            return card;
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
