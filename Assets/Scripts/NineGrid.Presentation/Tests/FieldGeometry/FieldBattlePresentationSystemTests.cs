using NineGrid.Cards;
using NineGrid.Cards.Convergence;
using NineGrid.Core;
using NineGrid.Presentation.Commands;
using NineGrid.Presentation.Controllers;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.FieldGeometry
{
    /// <summary>
    /// IFieldBattlePresentationSystem 拥有 busy/Present；View 仅 Adapter/Catalog；Handoff 在 Lifecycle。
    /// </summary>
    public sealed class FieldBattlePresentationSystemTests
    {
        private GameObject _prefab;
        private CardManagerSingleton _cards;
        private CardHandManagerSingleton _hand;
        private CardDeckManagerSingleton _deck;
        private FieldBattleView _battle;

        [SetUp]
        public void SetUp()
        {
            FieldBattlePresentationHook.Reset();
            CardEntityLifecycleHook.Reset();
            DestroyControllers();
            DestroyManagers();

            _cards = new GameObject("CardManager_V7Battle").AddComponent<CardManagerSingleton>();
            _hand = new GameObject("CardHand_V7Battle").AddComponent<CardHandManagerSingleton>();
            _deck = new GameObject("CardDeck_V7Battle").AddComponent<CardDeckManagerSingleton>();
            _battle = new GameObject("FieldBattle_V7").AddComponent<FieldBattleView>();

            _prefab = new GameObject("StandardCardPrefab_V7Battle");
            _prefab.AddComponent<StandardCardView>();
            _cards.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);
        }

        [TearDown]
        public void TearDown()
        {
            FieldBattlePresentationHook.Reset();
            CardEntityLifecycleHook.Reset();
            DestroyControllers();
            if (_prefab != null)
            {
                Object.DestroyImmediate(_prefab);
                _prefab = null;
            }

            DestroyManagers();
        }

        [Test]
        public void Bind_ExposesBoundWithoutConcreteViewLeak()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var system = RegisterBattleSystem();
                system.Bind(_battle);

                Assert.IsTrue(system.IsBound);
                Assert.IsFalse(system.IsBusy);
            }
        }

        [Test]
        public void HandoffCommand_EvictHandAdmitBattle_ThroughLifecycle()
        {
            using (var fixture = PresentationArchitectureFixture.CreateBare())
            {
                var lifecycle = new CardEntityLifecycleSystem();
                fixture.Architecture.RegisterSystem<ICardEntityLifecycleSystem>(lifecycle);
                lifecycle.Bind(_cards, _hand, _deck);

                var battle = RegisterBattleSystem();
                battle.Bind(_battle);

                var card = _cards.SpawnView(7701, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(card);
                card.Transform.localPosition = new Vector3(2f, -1f, 0f);

                var ok = fixture.Architecture.SendCommand(
                    new HandoffCardBetweenZonesCommand(
                        card,
                        HandoffCardBetweenZonesCommand.SanctuaryEndpoint.Hand,
                        HandoffCardBetweenZonesCommand.SanctuaryEndpoint.Battle));

                Assert.IsTrue(ok);
                Assert.AreEqual(new Vector3(2f, -1f, 0f), card.Transform.localPosition);
            }
        }

        [Test]
        public void LifecycleEvictAdmitBattle_MatchesCPhaseAtRestContract()
        {
            using (var fixture = PresentationArchitectureFixture.CreateBare())
            {
                var lifecycle = new CardEntityLifecycleSystem();
                fixture.Architecture.RegisterSystem<ICardEntityLifecycleSystem>(lifecycle);

                var card = _cards.SpawnView(7702, CardManagerSingleton.StandardDefId);
                card.Transform.localPosition = new Vector3(4f, 5f, 0f);

                var state = lifecycle.EvictFromBattle(card);
                Assert.AreEqual(Vector3.zero, state.LocalVelocity);
                Assert.AreEqual(new Vector3(4f, 5f, 0f), state.LocalPosition);

                var shifted = HandoffState.AtRest(new Vector3(8f, 0f, 0f));
                lifecycle.AdmitToBattle(card, in shifted);
                Assert.AreEqual(new Vector3(8f, 0f, 0f), card.Transform.localPosition);
            }
        }

        [Test]
        public void Controller_BindBattle_WiresHookAndSystem()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var host = new GameObject(nameof(FieldBattlePresentationController));
                var controller = host.AddComponent<FieldBattlePresentationController>();
                controller.BindBattle(_battle);

                var system = NineGridArchitecture.Interface.GetSystem<IFieldBattlePresentationSystem>();
                Assert.IsNotNull(system);
                Assert.IsTrue(system.IsBound);
                Assert.AreSame(_battle, FieldBattlePresentationHook.BattleOrNull());

                Object.DestroyImmediate(host);
            }
        }

        private static IFieldBattlePresentationSystem RegisterBattleSystem()
        {
            var system = new FieldBattlePresentationSystem();
            NineGridArchitecture.Interface.RegisterSystem<IFieldBattlePresentationSystem>(system);
            return system;
        }

        private static void DestroyControllers()
        {
            var found = Object.FindObjectsByType<FieldBattlePresentationController>(FindObjectsSortMode.None);
            for (var i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    Object.DestroyImmediate(found[i].gameObject);
                }
            }
        }

        private void DestroyManagers()
        {
            DestroyManager(_cards);
            DestroyManager(_hand);
            DestroyManager(_deck);
            DestroyManager(_battle);
            DestroyAllOfType<CardManagerSingleton>();
            DestroyAllOfType<CardHandManagerSingleton>();
            DestroyAllOfType<CardDeckManagerSingleton>();
            DestroyAllOfType<FieldBattleView>();
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
