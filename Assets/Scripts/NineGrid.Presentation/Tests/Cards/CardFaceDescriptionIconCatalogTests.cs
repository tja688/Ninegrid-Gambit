using NUnit.Framework;
using NineGrid.Cards.Presentation;
using NineGrid.Cards.Slots;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class CardFaceDescriptionIconCatalogTests
    {
        private CardFaceDescriptionIconCatalogSO _catalog;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _catalog = ScriptableObject.CreateInstance<CardFaceDescriptionIconCatalogSO>();
            _sprite = CreateSprite("catalog-sprite");
        }

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null)
            {
                Object.DestroyImmediate(_catalog);
                _catalog = null;
            }

            if (_sprite != null)
            {
                var texture = _sprite.texture;
                Object.DestroyImmediate(_sprite);
                if (texture != null)
                {
                    Object.DestroyImmediate(texture);
                }

                _sprite = null;
            }
        }

        [Test]
        public void TryAddOrUpdate_RejectsReservedAssemblySlotCodes()
        {
            Assert.IsTrue(CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(
                CardFaceSlotCodes.MainIcon));
            Assert.IsTrue(CardFaceDescriptionIconCatalogSO.IsReservedAssemblySlotCode(
                CardFaceSlotCodes.Attack));

            Assert.IsFalse(
                _catalog.TryAddOrUpdate(CardFaceSlotCodes.MainIcon, _sprite, null, out var error));
            Assert.IsTrue(error != null && error.Contains("保留"));
            Assert.IsFalse(_catalog.TryGet(CardFaceSlotCodes.MainIcon, out _));
        }

        [Test]
        public void TryGet_IgnoresReservedEvenIfForcedIntoEntries()
        {
            _catalog.ReplaceEntries(new[]
            {
                new CardFaceDescriptionIconCatalogSO.Entry
                {
                    code = CardFaceSlotCodes.ActionIcon,
                    sprite = _sprite,
                },
            });

            Assert.AreEqual(0, _catalog.Entries.Count, "ReplaceEntries 应跳过保留代号");
            Assert.IsFalse(_catalog.TryGet(CardFaceSlotCodes.ActionIcon, out _));
        }

        [Test]
        public void TryAddOrUpdate_AndTryGet_RoundTrip()
        {
            Assert.IsTrue(_catalog.TryAddOrUpdate("Poison", _sprite, "毒素", out _));
            Assert.IsTrue(_catalog.TryGet("Poison", out var sprite));
            Assert.AreSame(_sprite, sprite);
            Assert.IsTrue(_catalog.TryGetEntry("Poison", out var entry));
            Assert.AreEqual("毒素", entry.displayNameZh);
        }

        [Test]
        public void TryGet_EmptySprite_ReturnsFalse()
        {
            Assert.IsTrue(_catalog.TryAddOrUpdate("Blank", null, null, out _));
            Assert.IsFalse(_catalog.TryGet("Blank", out _));
        }

        private static Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(2, 2);
            texture.name = name + "-tex";
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }
    }
}
