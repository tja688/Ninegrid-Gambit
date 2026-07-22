using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Lifecycle
{
    /// <summary>
    /// V6 Seam2：CardEntityLifecycleController ↔ CardEntityLifecycleHook 接线。
    /// </summary>
    public sealed class CardEntityLifecycleControllerTests
    {
        private CardManagerSingleton _cards;
        private CardHandManagerSingleton _hand;
        private CardDeckManagerSingleton _deck;

        [SetUp]
        public void SetUp()
        {
            CardEntityLifecycleHook.Reset();
            DestroyControllers();
            DestroyManagers();

            _cards = new GameObject("CardManager_Ctrl").AddComponent<CardManagerSingleton>();
            _hand = new GameObject("CardHand_Ctrl").AddComponent<CardHandManagerSingleton>();
            _deck = new GameObject("CardDeck_Ctrl").AddComponent<CardDeckManagerSingleton>();
        }

        [TearDown]
        public void TearDown()
        {
            CardEntityLifecycleHook.Reset();
            DestroyControllers();
            DestroyManager(_cards);
            DestroyManager(_hand);
            DestroyManager(_deck);
            DestroyManagers();
        }

        [Test]
        public void BindManagers_ExposesHookResolversAndRegistersSystem()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var host = new GameObject(nameof(CardEntityLifecycleController) + "_Test");
                var controller = host.AddComponent<CardEntityLifecycleController>();
                controller.BindManagers(_cards, _hand, _deck);

                Assert.AreSame(_cards, CardEntityLifecycleHook.CardsOrNull());
                Assert.AreSame(_hand, CardEntityLifecycleHook.HandOrNull());
                Assert.AreSame(_deck, CardEntityLifecycleHook.DeckOrNull());

                var system = NineGridArchitecture.Interface.GetSystem<ICardEntityLifecycleSystem>();
                Assert.IsNotNull(system);
                Assert.IsTrue(system.IsBound);
                Assert.AreSame(_cards, system.Cards);
                Assert.AreSame(_hand, system.Hand);
                Assert.AreSame(_deck, system.Deck);
            }
        }

        [Test]
        public void RequestWire_WithoutExistingController_CreatesAndBinds()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                CardEntityLifecycleHook.Wire = null;
                // 恢复安装钩子（SetUp Reset 清掉了 SubsystemRegistration 赋值）。
                CardEntityLifecycleHook.Wire = (cards, hand, deck) =>
                {
                    var existing = Object.FindObjectOfType<CardEntityLifecycleController>();
                    if (existing == null)
                    {
                        var host = new GameObject(nameof(CardEntityLifecycleController));
                        existing = host.AddComponent<CardEntityLifecycleController>();
                    }

                    existing.BindManagers(cards, hand, deck);
                };

                CardEntityLifecycleHook.RequestWire(_cards, _hand, _deck);

                Assert.AreSame(_cards, CardEntityLifecycleHook.CardsOrNull());
                Assert.AreSame(_hand, CardEntityLifecycleHook.HandOrNull());
                Assert.AreSame(_deck, CardEntityLifecycleHook.DeckOrNull());

                var system = NineGridArchitecture.Interface.GetSystem<ICardEntityLifecycleSystem>();
                Assert.IsNotNull(system);
                Assert.IsTrue(system.IsBound);
            }
        }

        [Test]
        public void RequestNotifyReleased_PrefersHookOverInstance()
        {
            var calls = 0;
            CardEntityLifecycleHook.NotifyCardReleased = uid =>
            {
                Assert.AreEqual(55, uid);
                calls++;
            };

            CardEntityLifecycleHook.RequestNotifyReleased(55);
            Assert.AreEqual(1, calls);
        }

        private static void DestroyControllers()
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

        private static void DestroyManagers()
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
