using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Cards.Slots;
using UnityEngine;

namespace NineGrid.Cards.Tests
{
    /// <summary>
    /// 旁路 Seam：装配槽注册表解析（纯数据）— 代号查找与图标缺省回退。
    /// 对齐 #14 / ADR-0002；不测 Inspector UI 布局。
    /// </summary>
    public sealed class CardFaceSlotRegistryTests
    {
        private CardFaceSlotRegistrySO _registry;
        private Sprite _contentIcon;
        private Sprite _templateIcon;
        private Sprite _templateBack;

        [SetUp]
        public void SetUp()
        {
            _registry = ScriptableObject.CreateInstance<CardFaceSlotRegistrySO>();
            _registry.ApplyDefaultCatalog();
            _contentIcon = CreateSprite("content-main-icon");
            _templateIcon = CreateSprite("template-main-icon");
            _templateBack = CreateSprite("template-back-shirt");
        }

        [TearDown]
        public void TearDown()
        {
            if (_registry != null)
            {
                Object.DestroyImmediate(_registry);
                _registry = null;
            }

            DestroySprite(ref _contentIcon);
            DestroySprite(ref _templateIcon);
            DestroySprite(ref _templateBack);
        }

        [Test]
        public void TryGet_KnownCode_ReturnsDefinitionWithRoles()
        {
            Assert.IsTrue(_registry.TryGet(CardFaceSlotCodes.MainIcon, out var main));
            Assert.AreEqual("主图标", main.DisplayNameZh);
            Assert.IsTrue(main.HasRole(CardFaceSlotRole.DirectExpose));
            Assert.IsTrue(main.HasRole(CardFaceSlotRole.Icon));

            Assert.IsTrue(_registry.TryGet(CardFaceSlotCodes.Attack, out var attack));
            Assert.IsTrue(attack.HasRole(CardFaceSlotRole.Numeric));
            Assert.IsTrue(attack.HasRole(CardFaceSlotRole.Collapsed));
            Assert.IsFalse(attack.HasRole(CardFaceSlotRole.DirectExpose));

            Assert.IsTrue(_registry.TryGet(CardFaceSlotCodes.BackShirt, out var back));
            Assert.IsTrue(back.HasRole(CardFaceSlotRole.DirectExpose));
        }

        [Test]
        public void TryGet_UnknownCode_ReturnsFalse()
        {
            Assert.IsFalse(_registry.TryGet("Not_A_Slot", out _));
            Assert.IsFalse(_registry.Contains(string.Empty));
        }

        [Test]
        public void ResolveIcon_ContentOverride_WinsOverTemplate()
        {
            var content = CardFaceSlotResolver.BuildContentOverrides(
                _contentIcon, null, null, null, null);
            var template = new Dictionary<string, Sprite>
            {
                { CardFaceSlotCodes.MainIcon, _templateIcon }
            };

            var resolved = CardFaceSlotResolver.ResolveIcon(
                CardFaceSlotCodes.MainIcon, content, template);

            Assert.AreSame(_contentIcon, resolved);
        }

        [Test]
        public void ResolveIcon_MissingContent_FallsBackToTemplateDefault()
        {
            var content = CardFaceSlotResolver.BuildContentOverrides(
                null, null, null, null, null);
            var template = new Dictionary<string, Sprite>
            {
                { CardFaceSlotCodes.MainIcon, _templateIcon },
                { CardFaceSlotCodes.BackShirt, _templateBack }
            };

            Assert.AreSame(
                _templateIcon,
                CardFaceSlotResolver.ResolveIcon(CardFaceSlotCodes.MainIcon, content, template));
            Assert.AreSame(
                _templateBack,
                CardFaceSlotResolver.ResolveIcon(CardFaceSlotCodes.BackShirt, content, template),
                "卡背无自定时回退该卡面模板兜底，不走全局卡背源");
        }

        [Test]
        public void ResolveNumeric_MissingContent_DefaultsToZero()
        {
            Assert.AreEqual(0, CardFaceSlotResolver.ResolveNumeric(CardFaceSlotCodes.Attack, null));
            Assert.AreEqual(
                0,
                CardFaceSlotResolver.ResolveNumeric(
                    CardFaceSlotCodes.Hp,
                    new Dictionary<string, int>()));
            Assert.AreEqual(
                3,
                CardFaceSlotResolver.ResolveNumeric(
                    CardFaceSlotCodes.Armor,
                    new Dictionary<string, int> { { CardFaceSlotCodes.Armor, 3 } }));
        }

        private static Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(2, 2);
            texture.name = name + "-tex";
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = name;
            return sprite;
        }

        private static void DestroySprite(ref Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            var texture = sprite.texture;
            Object.DestroyImmediate(sprite);
            if (texture != null)
            {
                Object.DestroyImmediate(texture);
            }

            sprite = null;
        }
    }
}
