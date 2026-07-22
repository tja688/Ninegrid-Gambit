using System.Reflection;
using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Lifecycle
{
    /// <summary>
    /// V6 Seam3：Hand/Deck Admit/Evict/Handoff 经 System/Hook，不依赖互取 Instance。
    /// </summary>
    public sealed class HandoffWithoutInstanceTests
    {
        private const int TestUid = 8801;

        private GameObject _prefab;
        private CardManagerSingleton _cards;
        private CardHandManagerSingleton _hand;
        private CardDeckManagerSingleton _deck;

        [SetUp]
        public void SetUp()
        {
            CardEntityLifecycleHook.Reset();
            DestroyAllManagers();

            _cards = new GameObject("CardManager_Handoff").AddComponent<CardManagerSingleton>();
            _hand = new GameObject("CardHand_Handoff").AddComponent<CardHandManagerSingleton>();
            _deck = new GameObject("CardDeck_Handoff").AddComponent<CardDeckManagerSingleton>();

            _prefab = new GameObject("StandardCardPrefab_Handoff");
            _prefab.AddComponent<StandardCardView>();
            _cards.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            CardEntityLifecycleHook.Reset();
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
        public void HandoffCommand_EvictHandAdmitDeck_ThroughLifecycleSystem()
        {
            using (var fixture = PresentationArchitectureFixture.CreateBare())
            {
                var system = new CardEntityLifecycleSystem();
                fixture.Architecture.RegisterSystem<ICardEntityLifecycleSystem>(system);
                system.Bind(_cards, _hand, _deck);

                var card = _cards.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(card);
                card.Transform.localPosition = new Vector3(3f, 4f, 0f);

                var ok = fixture.Architecture.SendCommand(
                    new HandoffCardBetweenZonesCommand(
                        card,
                        HandoffCardBetweenZonesCommand.SanctuaryEndpoint.Hand,
                        HandoffCardBetweenZonesCommand.SanctuaryEndpoint.Deck));

                Assert.IsTrue(ok);
                Assert.AreEqual(new Vector3(3f, 4f, 0f), card.Transform.localPosition);
            }
        }

        [Test]
        public void HookResolvers_WorkWithoutStaticInstanceFields()
        {
            Assert.IsNull(
                typeof(CardManagerSingleton).GetField(
                    "_instance", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.IsNull(
                typeof(CardHandManagerSingleton).GetField(
                    "_instance", BindingFlags.Static | BindingFlags.NonPublic));
            Assert.IsNull(
                typeof(CardDeckManagerSingleton).GetField(
                    "_instance", BindingFlags.Static | BindingFlags.NonPublic));

            CardEntityLifecycleHook.RequestWire(_cards, _hand, _deck);

            Assert.AreSame(_cards, CardEntityLifecycleHook.CardsOrNull());
            Assert.AreSame(_hand, CardEntityLifecycleHook.HandOrNull());
            Assert.AreSame(_deck, CardEntityLifecycleHook.DeckOrNull());

            var spawned = _cards.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(spawned);
            Assert.IsTrue(CardEntityLifecycleHook.TryGetCard(TestUid, out var found));
            Assert.AreSame(spawned, found);
        }

        [Test]
        public void Release_NotifiesViaHook_WithoutStaticInstance()
        {
            var card = _cards.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
            Assert.IsNotNull(card);

            var notified = 0;
            CardEntityLifecycleHook.NotifyCardReleased = uid => notified = uid;

            _cards.Release(card, "V6.HandoffTest");
            Assert.AreEqual(TestUid, notified);
            Assert.IsFalse(_cards.TryGet(TestUid, out _));
        }

        [Test]
        public void SystemEvictAdmit_MatchesCPhaseAtRestContract()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var system = new CardEntityLifecycleSystem();
                NineGridArchitecture.Interface.RegisterSystem<ICardEntityLifecycleSystem>(system);
                system.Bind(_cards, _hand, _deck);

                var card = _cards.SpawnView(TestUid, CardManagerSingleton.StandardDefId);
                card.Transform.localPosition = new Vector3(1.5f, -2f, 0f);

                var state = system.EvictFromHand(card);
                Assert.AreEqual(Vector3.zero, state.LocalVelocity);
                Assert.AreEqual(new Vector3(1.5f, -2f, 0f), state.LocalPosition);

                var shifted = HandoffState.AtRest(new Vector3(9f, 0f, 0f));
                system.AdmitToDeck(card, in shifted);
                Assert.AreEqual(new Vector3(9f, 0f, 0f), card.Transform.localPosition);
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
