using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Queries;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Lifecycle
{
    /// <summary>
    /// V6 Seam1：ICardEntityLifecycleSystem 是卡牌注册查找的单一 QF 所有者。
    /// </summary>
    public sealed class CardEntityLifecycleSystemTests
    {
        private GameObject _prefab;
        private CardManagerSingleton _cards;
        private CardHandManagerSingleton _hand;
        private CardDeckManagerSingleton _deck;

        [SetUp]
        public void SetUp()
        {
            CardEntityLifecycleHook.Reset();
            DestroyLifecycleControllers();
            DestroyAllManagers();

            _cards = new GameObject("CardManager_V6").AddComponent<CardManagerSingleton>();
            _hand = new GameObject("CardHand_V6").AddComponent<CardHandManagerSingleton>();
            _deck = new GameObject("CardDeck_V6").AddComponent<CardDeckManagerSingleton>();

            _prefab = new GameObject("StandardCardPrefab_V6");
            _prefab.AddComponent<StandardCardView>();
            _cards.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            CardEntityLifecycleHook.Reset();
            DestroyLifecycleControllers();
            if (_prefab != null)
            {
                Object.DestroyImmediate(_prefab);
                _prefab = null;
            }

            DestroyManager(_cards);
            DestroyManager(_hand);
            DestroyManager(_deck);
            DestroyAllManagers();
        }

        [Test]
        public void Bind_TryGet_UsesBoundCardManager()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var system = RegisterSystem();
                system.Bind(_cards, _hand, _deck);

                var spawned = _cards.SpawnView(4201, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(spawned);

                Assert.IsTrue(system.TryGet(4201, out var found));
                Assert.AreSame(spawned, found);
                Assert.IsTrue(system.IsBound);
            }
        }

        [Test]
        public void NotifyCardReleased_UsesBoundHandReference()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var system = RegisterSystem();
                system.Bind(_cards, _hand, _deck);

                Assert.AreSame(_hand, system.Hand);
                Assert.DoesNotThrow(() => system.NotifyCardReleased(77));
            }
        }

        [Test]
        public void TryGetManagedCardQuery_ReadsThroughLifecycleSystem()
        {
            using (var fixture = PresentationArchitectureFixture.CreateBare())
            {
                var system = RegisterSystem();
                system.Bind(_cards, _hand, _deck);

                var spawned = _cards.SpawnView(4202, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(spawned);

                var queried = fixture.Architecture.SendQuery(new TryGetManagedCardQuery(4202));
                Assert.AreSame(spawned, queried);
                Assert.IsNull(fixture.Architecture.SendQuery(new TryGetManagedCardQuery(9999)));
            }
        }

        private static ICardEntityLifecycleSystem RegisterSystem()
        {
            var system = new CardEntityLifecycleSystem();
            NineGridArchitecture.Interface.RegisterSystem<ICardEntityLifecycleSystem>(system);
            return system;
        }

        private static void DestroyLifecycleControllers()
        {
            var found = Object.FindObjectsByType<CardEntityLifecycleController>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    Object.DestroyImmediate(found[i].gameObject);
                }
            }
        }

        private static void DestroyAllManagers()
        {
            DestroyAllOfType<CardManagerSingleton>();
            DestroyAllOfType<CardHandManagerSingleton>();
            DestroyAllOfType<CardDeckManagerSingleton>();
        }

        private static void DestroyAllOfType<T>() where T : MonoBehaviour
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    Object.DestroyImmediate(found[i].gameObject);
                }
            }
        }

        private static void DestroyManager(MonoBehaviour manager)
        {
            if (manager != null)
            {
                Object.DestroyImmediate(manager.gameObject);
            }
        }
    }
}
