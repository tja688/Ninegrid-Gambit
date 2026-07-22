using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using QFramework;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 场地占格残留时，手牌视图不得经 AddCardAt fieldExit 误入卡组。
    /// </summary>
    public sealed class StaleOccupancyTests
    {
        private const int TestUid = 7201;
        private const int StaleGroundSlot = 3;

        private GameObject _prefab;
        private CardManagerSingleton _cardManager;
        private CardDeckManagerSingleton _deckManager;
        private GroundFieldManagerSingleton _fieldManager;

        [SetUp]
        public void SetUp()
        {
            DestroyAllSingletonsInScene();
            ResetPresentationSingletons();
            CardZoneOwnershipHook.Reset();

            var cardGo = new GameObject("CardManagerTest");
            var deckGo = new GameObject("DeckManagerTest");
            var fieldGo = new GameObject("GroundFieldTest");

            _cardManager = cardGo.AddComponent<CardManagerSingleton>();
            _deckManager = deckGo.AddComponent<CardDeckManagerSingleton>();
            _fieldManager = fieldGo.AddComponent<GroundFieldManagerSingleton>();
            CardEntityLifecycleHook.RequestWire(_cardManager, null, _deckManager);
            GroundFieldGeometryHook.RequestWire(_fieldManager);

            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _cardManager.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);

            ForceDeckMode(_deckManager, CardDeckMode.InGame);
        }

        [TearDown]
        public void TearDown()
        {
            CardZoneOwnershipHook.Reset();
            CardEntityLifecycleHook.Reset();
            GroundFieldGeometryHook.Reset();
            DestroyManagerObject(_fieldManager);
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
        public void AddCardAtFromOrigin_ClearsStaleOccupancyWithoutDeckInsertForHandCard()
        {
            var card = _cardManager.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(card);
            _cardManager.SetDisplayMode(card, CardDisplayMode.HandCardMode);

            SeedStaleOccupancy(card, StaleGroundSlot);
            Assert.IsTrue(_fieldManager.TryGetSlotOf(TestUid, out var slot));
            Assert.AreEqual(StaleGroundSlot, slot);

            var ok = _deckManager
                .AddCardAtFromOriginAsync(0, card, null, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            Assert.IsFalse(ok);
            Assert.IsFalse(_deckManager.ContainsUid(TestUid));
            Assert.IsFalse(
                _fieldManager.TryGetSlotOf(TestUid, out _),
                "stale occupancy should be cleared without deck insert");
        }

        private void SeedStaleOccupancy(ManagedCard card, int slot)
        {
            var system = EnsureGeometrySystemBound(_fieldManager);
            Assert.IsTrue(system.TryRegisterCardAtSlot(slot, card.Uid, logConflict: false));
        }

        private static IGroundFieldGeometrySystem EnsureGeometrySystemBound(GroundFieldManagerSingleton field)
        {
            var architecture = NineGridArchitecture.Interface;
            Assert.IsNotNull(architecture, "NineGridArchitecture missing");
            var system = architecture.GetSystem<IGroundFieldGeometrySystem>();
            if (system == null)
            {
                system = new GroundFieldGeometrySystem();
                architecture.RegisterSystem<IGroundFieldGeometrySystem>(system);
            }

            if (!system.IsBound)
            {
                system.Bind(field);
            }

            return system;
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

        private static void ForceDeckMode(CardDeckManagerSingleton deck, CardDeckMode mode)
        {
            var backing = typeof(CardDeckManagerSingleton).GetField(
                "<CurrentMode>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(backing, "CurrentMode backing field missing");
            backing.SetValue(deck, mode);
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
