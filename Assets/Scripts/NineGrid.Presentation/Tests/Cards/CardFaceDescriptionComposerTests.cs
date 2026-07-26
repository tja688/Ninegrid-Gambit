using System.Collections.Generic;
using NUnit.Framework;
using NineGrid.Cards.Presentation;
using NineGrid.Cards.Slots;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    /// <summary>
    /// 旁路 Seam：基础描述 `[SlotCode]` 解析为装配图标引用（纯数据）。
    /// 对齐 #16 / ADR-0002；不测 Inspector / hover UI。
    /// </summary>
    public sealed class CardFaceDescriptionComposerTests
    {
        private CardFaceSlotRegistrySO _registry;
        private CardFaceDescriptionIconCatalogSO _catalog;
        private Sprite _actionIcon;
        private Sprite _mainIcon;
        private Sprite _poisonIcon;

        [SetUp]
        public void SetUp()
        {
            _registry = ScriptableObject.CreateInstance<CardFaceSlotRegistrySO>();
            _registry.ApplyDefaultCatalog();
            _catalog = ScriptableObject.CreateInstance<CardFaceDescriptionIconCatalogSO>();
            _actionIcon = CreateSprite("assembled-action-icon");
            _mainIcon = CreateSprite("assembled-main-icon");
            _poisonIcon = CreateSprite("catalog-poison-icon");
            Assert.IsTrue(_catalog.TryAddOrUpdate("Poison", _poisonIcon, "毒素", out _));
        }

        [TearDown]
        public void TearDown()
        {
            if (_registry != null)
            {
                Object.DestroyImmediate(_registry);
                _registry = null;
            }

            if (_catalog != null)
            {
                Object.DestroyImmediate(_catalog);
                _catalog = null;
            }

            DestroySprite(ref _actionIcon);
            DestroySprite(ref _mainIcon);
            DestroySprite(ref _poisonIcon);
        }

        [Test]
        public void Compose_InsertableSlotCode_ReplacesWithTmpSpriteTag_AndKeepsAssembledSprite()
        {
            var assembled = new Dictionary<string, Sprite>
            {
                { CardFaceSlotCodes.ActionIcon, _actionIcon }
            };

            var result = CardFaceDescriptionComposer.Compose(
                "在[Action_Icon]回合后攻击玩家",
                assembled,
                _registry);

            Assert.AreEqual(
                "在<sprite name=\"Action_Icon\">回合后攻击玩家",
                result.TmpRichText,
                "应插入 TMP sprite 标签，而非表情字符或资源路径");
            Assert.AreEqual(1, result.Icons.Count);
            Assert.AreEqual(CardFaceSlotCodes.ActionIcon, result.Icons[0].SlotCode);
            Assert.AreSame(_actionIcon, result.Icons[0].Sprite);
        }

        [Test]
        public void Compose_NonSlotBracketText_LeftUnchanged()
        {
            var assembled = new Dictionary<string, Sprite>
            {
                { CardFaceSlotCodes.ActionIcon, _actionIcon }
            };

            var result = CardFaceDescriptionComposer.Compose(
                "[使用时]对敌人造成伤害",
                assembled,
                _registry);

            Assert.AreEqual("[使用时]对敌人造成伤害", result.TmpRichText);
            Assert.AreEqual(0, result.Icons.Count);
        }

        [Test]
        public void Compose_InsertableButMissingSprite_LeavesBracketLiteral()
        {
            var result = CardFaceDescriptionComposer.Compose(
                "在[Action_Icon]后攻击",
                new Dictionary<string, Sprite>(),
                _registry);

            Assert.AreEqual("在[Action_Icon]后攻击", result.TmpRichText);
            Assert.AreEqual(0, result.Icons.Count);
        }

        [Test]
        public void Compose_NonInsertableSlotCode_LeavesBracketLiteral()
        {
            // Attack 是数值槽，不可插入描述。
            var assembled = new Dictionary<string, Sprite>
            {
                { CardFaceSlotCodes.Attack, _mainIcon }
            };

            var result = CardFaceDescriptionComposer.Compose(
                "造成[Attack]点伤害",
                assembled,
                _registry);

            Assert.AreEqual("造成[Attack]点伤害", result.TmpRichText);
            Assert.AreEqual(0, result.Icons.Count);
        }

        [Test]
        public void Compose_MainIconInsertable_AlsoResolves()
        {
            var assembled = new Dictionary<string, Sprite>
            {
                { CardFaceSlotCodes.MainIcon, _mainIcon }
            };

            var result = CardFaceDescriptionComposer.Compose(
                "获得[Main_Icon]",
                assembled,
                _registry);

            Assert.AreEqual("获得<sprite name=\"Main_Icon\">", result.TmpRichText);
            Assert.AreSame(_mainIcon, result.Icons[0].Sprite);
        }

        [Test]
        public void Compose_NullOrEmpty_ReturnsEmpty()
        {
            var empty = CardFaceDescriptionComposer.Compose(null, null, _registry);
            Assert.AreEqual(string.Empty, empty.TmpRichText);
            Assert.AreEqual(0, empty.Icons.Count);

            var blank = CardFaceDescriptionComposer.Compose("   ", null, _registry);
            Assert.AreEqual(string.Empty, blank.TmpRichText);
        }

        [Test]
        public void Compose_CatalogCode_ResolvesFromDescriptionIconCatalog()
        {
            var result = CardFaceDescriptionComposer.Compose(
                "施加[Poison]",
                null,
                _registry,
                _catalog);

            Assert.AreEqual("施加<sprite name=\"Poison\">", result.TmpRichText);
            Assert.AreEqual(1, result.Icons.Count);
            Assert.AreEqual("Poison", result.Icons[0].SlotCode);
            Assert.AreSame(_poisonIcon, result.Icons[0].Sprite);
        }

        [Test]
        public void Compose_CatalogCannotOverrideInsertableAssemblySlot()
        {
            Assert.IsFalse(
                _catalog.TryAddOrUpdate(CardFaceSlotCodes.ActionIcon, _poisonIcon, null, out _),
                "catalog API 应拒绝保留装配槽代号");

            // 即便强行塞入条目，Compose 也只走 assembled。
            _catalog.ReplaceEntries(new[]
            {
                new CardFaceDescriptionIconCatalogSO.Entry
                {
                    code = CardFaceSlotCodes.ActionIcon,
                    sprite = _poisonIcon,
                },
                new CardFaceDescriptionIconCatalogSO.Entry
                {
                    code = "Poison",
                    sprite = _poisonIcon,
                },
            });

            var assembled = new Dictionary<string, Sprite>
            {
                { CardFaceSlotCodes.ActionIcon, _actionIcon }
            };
            var result = CardFaceDescriptionComposer.Compose(
                "在[Action_Icon]后",
                assembled,
                _registry,
                _catalog);

            Assert.AreEqual("在<sprite name=\"Action_Icon\">后", result.TmpRichText);
            Assert.AreSame(_actionIcon, result.Icons[0].Sprite);
        }

        [Test]
        public void Compose_CatalogEntryForMissingInsertable_DoesNotFillGap()
        {
            _catalog.ReplaceEntries(new[]
            {
                new CardFaceDescriptionIconCatalogSO.Entry
                {
                    code = CardFaceSlotCodes.ActionIcon,
                    sprite = _poisonIcon,
                },
            });

            var result = CardFaceDescriptionComposer.Compose(
                "在[Action_Icon]后",
                new Dictionary<string, Sprite>(),
                _registry,
                _catalog);

            Assert.AreEqual("在[Action_Icon]后", result.TmpRichText);
            Assert.AreEqual(0, result.Icons.Count);
        }

        [Test]
        public void Compose_MixedAssemblyAndCatalog_ResolvesBoth()
        {
            var assembled = new Dictionary<string, Sprite>
            {
                { CardFaceSlotCodes.ActionIcon, _actionIcon }
            };

            var result = CardFaceDescriptionComposer.Compose(
                "在[Action_Icon]后施加[Poison]",
                assembled,
                _registry,
                _catalog);

            Assert.AreEqual(
                "在<sprite name=\"Action_Icon\">后施加<sprite name=\"Poison\">",
                result.TmpRichText);
            Assert.AreEqual(2, result.Icons.Count);
        }

        [Test]
        public void Compose_CatalogEmptySprite_LeavesBracketLiteral()
        {
            Assert.IsTrue(_catalog.TryAddOrUpdate("EmptyIcon", null, null, out _));
            var result = CardFaceDescriptionComposer.Compose(
                "看[EmptyIcon]",
                null,
                _registry,
                _catalog);

            Assert.AreEqual("看[EmptyIcon]", result.TmpRichText);
            Assert.AreEqual(0, result.Icons.Count);
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
