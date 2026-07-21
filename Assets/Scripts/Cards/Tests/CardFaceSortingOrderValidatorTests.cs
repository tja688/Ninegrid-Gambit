using NUnit.Framework;
using NineGrid.Cards.Slots;
using UnityEngine;
using UnityEngine.Rendering;

namespace NineGrid.Cards.Tests
{
    public sealed class CardFaceSortingOrderValidatorTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("SortValidateRoot");
            _root.AddComponent<SortingGroup>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }

        [Test]
        public void FindDuplicates_SameOrderInSameGroup_ReportsHit()
        {
            CreateChildRenderer("A", sortingOrder: 5);
            CreateChildRenderer("B", sortingOrder: 5);

            var hits = CardFaceSortingOrderValidator.FindDuplicates(_root.transform);
            Assert.AreEqual(1, hits.Count);
            Assert.AreEqual(5, hits[0].SortingOrder);
        }

        [Test]
        public void FindDuplicates_DistinctOrders_NoHit()
        {
            CreateChildRenderer("A", sortingOrder: 1);
            CreateChildRenderer("B", sortingOrder: 2);

            var hits = CardFaceSortingOrderValidator.FindDuplicates(_root.transform);
            Assert.AreEqual(0, hits.Count);
        }

        private void CreateChildRenderer(string name, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_root.transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
        }
    }
}
