using NineGrid.Cards;
using NineGrid.Core;
using NineGrid.Presentation.Systems;
using NineGrid.Presentation.Tests.Fixtures;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.BehaviorBaseline
{
    /// <summary>
    /// #43 批次0：Ground 双向索引（slot→uid / uid→slot）一致性基线。
    /// </summary>
    public sealed class GroundBidirectionalIndexBaselineTests
    {
        private const int Uid = 43001;
        private const int Slot = 4;

        private GroundFieldManagerSingleton _field;
        private CardManagerSingleton _cards;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            CardEntityLifecycleHook.Reset();
            GroundFieldGeometryHook.Reset();

            _field = new GameObject("Ground_Bidirectional").AddComponent<GroundFieldManagerSingleton>();
            _cards = new GameObject("Cards_Bidirectional").AddComponent<CardManagerSingleton>();
            _prefab = new GameObject("StandardCardPrefab");
            _prefab.AddComponent<StandardCardView>();
            _cards.RegisterPrefab(CardManagerSingleton.StandardDefId, _prefab);
            CardEntityLifecycleHook.RequestWire(_cards, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            GroundFieldGeometryHook.Reset();
            CardEntityLifecycleHook.Reset();
            if (_field != null)
            {
                Object.DestroyImmediate(_field.gameObject);
                _field = null;
            }

            if (_cards != null)
            {
                Object.DestroyImmediate(_cards.gameObject);
                _cards = null;
            }

            if (_prefab != null)
            {
                Object.DestroyImmediate(_prefab);
                _prefab = null;
            }
        }

        [Test]
        public void RegisterThenLookup_SlotAndUidStayBidirectional()
        {
            using (PresentationArchitectureFixture.CreateBare())
            {
                var system = new GroundFieldGeometrySystem();
                NineGridArchitecture.Interface.RegisterSystem<IGroundFieldGeometrySystem>(system);
                system.Bind(_field);
                GroundFieldGeometryHook.RequestWire(_field);

                var card = _cards.SpawnView(Uid, CardManagerSingleton.StandardDefId);
                Assert.IsNotNull(card);

                Assert.IsTrue(system.TryRegisterCardAtSlot(Slot, Uid));

                Assert.IsTrue(system.TryGetCardAt(Slot, out var atSlot));
                Assert.AreSame(card, atSlot);
                Assert.IsTrue(system.TryGetSlotOf(Uid, out var slotOf));
                Assert.AreEqual(Slot, slotOf);
                Assert.IsFalse(system.IsEmpty(Slot));
            }
        }
    }
}
