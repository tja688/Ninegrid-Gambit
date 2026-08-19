using System.Collections.Generic;
using NineGrid.Cards;
using NineGrid.Cards.Presentation;
using NineGrid.Content;
using NineGrid.Core;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests
{
    public sealed class CardInspectGlossaryAssemblerTests
    {
        private CardFaceDescriptionIconCatalogSO _catalog;
        private Sprite _icon;

        [SetUp]
        public void SetUp()
        {
            InspectEffectCopyCatalog.Invalidate();
            _catalog = ScriptableObject.CreateInstance<CardFaceDescriptionIconCatalogSO>();
            _icon = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 4f, 4f),
                new Vector2(0.5f, 0.5f),
                4f);
            Assert.IsTrue(_catalog.TryAddOrUpdate("HP", _icon, "血量", out _));
            Assert.IsTrue(_catalog.TryAddOrUpdate("MHP", _icon, "血量上限", out _));
            Assert.IsTrue(_catalog.TryAddOrUpdate("attack", _icon, "攻击", out _));
            Assert.IsTrue(_catalog.TryAddOrUpdate("ortho_attack", _icon, "正交攻击", out _));
            Assert.IsTrue(_catalog.TryAddOrUpdate("diag_attack", _icon, "斜向攻击", out _));
            Assert.IsTrue(_catalog.TryAddOrUpdate("omni_attack", _icon, "全向攻击", out _));
            Assert.IsTrue(_catalog.TryAddOrUpdate("action", _icon, "行动计数", out _));
        }

        [TearDown]
        public void TearDown()
        {
            if (_catalog != null)
            {
                Object.DestroyImmediate(_catalog);
            }

            if (_icon != null)
            {
                Object.DestroyImmediate(_icon);
            }

            InspectEffectCopyCatalog.Invalidate();
        }

        [Test]
        public void Monster_DevotionPlusOrthogonalRhythm_AssemblesSkillRangeThenCount()
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DefId = "monster.skull_head",
                DisplayName = "负伤莱姆",
                BasicDescription = "[[献身]]",
                AttackPattern = AttackPattern.OrthogonalMelee,
                HasActiveRhythm = true,
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.Monster,
                snapshot,
                extraNames: null,
                _catalog);

            AssertNames(terms, "献身", "正交攻击", "行动计数");
            Assert.AreEqual("ortho_attack", terms[1].InlineCode);
            Assert.AreEqual("action", terms[2].InlineCode);
        }

        [Test]
        public void Monster_NoRhythm_OmitsCountRow()
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                DisplayName = "决斗怪",
                BasicDescription = "[[献身]]",
                AttackPattern = AttackPattern.OrthogonalMelee,
                HasActiveRhythm = false,
                ShowActionCount = false,
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.Monster,
                snapshot,
                extraNames: null,
                _catalog);

            AssertNames(terms, "献身", "正交攻击");
        }

        [Test]
        public void Monster_OmniAliasInDescription_DedupesToCanonicalName()
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                BasicDescription = "[[全向近战]]",
                AttackPattern = AttackPattern.OmnidirectionalMelee,
                HasActiveRhythm = false,
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.Monster,
                snapshot,
                extraNames: null,
                _catalog);

            AssertNames(terms, "全向攻击");
            Assert.AreEqual("omni_attack", terms[0].InlineCode);
        }

        [Test]
        public void Monster_DiagonalAttackAlias_DedupesToCanonicalName()
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                BasicDescription = "[[斜角近战]]",
                AttackPattern = AttackPattern.DiagonalMelee,
                HasActiveRhythm = false,
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.Monster,
                snapshot,
                extraNames: null,
                _catalog);

            AssertNames(terms, "斜向攻击");
            Assert.AreEqual("diag_attack", terms[0].InlineCode);
        }

        [Test]
        public void Monster_OrthogonalAliasInDescription_DedupesToCanonicalName()
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Monster,
                BasicDescription = "[[普通近战]]",
                AttackPattern = AttackPattern.OrthogonalMelee,
                HasActiveRhythm = false,
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.Monster,
                snapshot,
                extraNames: null,
                _catalog);

            AssertNames(terms, "正交攻击");
            Assert.AreEqual("ortho_attack", terms[0].InlineCode);
        }

        [Test]
        public void HelpCard_Food_WithoutCatalog_IsDisplayNameOnly()
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.HelpCard,
                DefId = "help.food_card",
                DisplayName = "生日蛋糕",
                BasicDescription = "将[HP]恢复到[MHP]",
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.HelpCard,
                snapshot,
                extraNames: null,
                catalog: null);

            Assert.AreEqual(1, terms.Count);
            Assert.AreEqual("生日蛋糕", terms[0].DisplayName);
            Assert.IsFalse(string.IsNullOrWhiteSpace(terms[0].Explanation));
        }

        [Test]
        public void HelpCard_Food_WithCatalog_AddsHpAndMaxHpIconRows()
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.HelpCard,
                DefId = "help.food_card",
                DisplayName = "生日蛋糕",
                BasicDescription = "将[HP]恢复到[MHP]",
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.HelpCard,
                snapshot,
                extraNames: null,
                _catalog);

            AssertNames(terms, "生日蛋糕", "血量", "血量上限");
            Assert.AreEqual("HP", terms[1].InlineCode);
            Assert.AreEqual("MHP", terms[2].InlineCode);
        }

        [Test]
        public void HelpCard_Brutality_AddsAttackIcon_NotRelicRow()
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.HelpCard,
                DefId = "help.brutality_card",
                DisplayName = "暴力",
                BasicDescription = "[attack]翻倍，战斗后复原",
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.HelpCard,
                snapshot,
                extraNames: null,
                _catalog);

            AssertNames(terms, "暴力", "攻击");
            Assert.AreEqual("attack", terms[1].InlineCode);
            for (var i = 0; i < terms.Count; i++)
            {
                Assert.AreNotEqual("遗物", terms[i].DisplayName);
                Assert.AreNotEqual("遗物", terms[i].LookupName);
            }
        }

        [Test]
        public void Trap_ShowActionCount_InsertsCountAfterTitle()
        {
            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Trap,
                DefId = "trap.rolling_stone",
                DisplayName = "滚石",
                BasicDescription = "对玩家造成伤害",
                ShowActionCount = true,
                HasActiveRhythm = false,
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.Trap,
                snapshot,
                extraNames: null,
                _catalog);

            Assert.GreaterOrEqual(terms.Count, 2);
            Assert.AreEqual("滚石", terms[0].DisplayName);
            Assert.AreEqual("行动计数", terms[1].DisplayName);
        }

        [Test]
        public void Trap_ExtraGlossaryTerm_AppendsMechanismTerm()
        {
            var entry = _catalog.AddBlankEntry();
            entry.displayNameZh = "机关";
            entry.explanation = "中立单位，可以被攻击，可以被部分道具卡选作目标，可破坏";

            var snapshot = new CardPresentationSnapshot
            {
                Kind = CardPresentationKind.Trap,
                DefId = "trap.spike",
                DisplayName = "刺藤",
                BasicDescription = "对正交相邻造成伤害",
                ShowActionCount = false,
                HasActiveRhythm = false,
            };

            var terms = CardInspectGlossaryAssembler.Assemble(
                CardPresentationKind.Trap,
                snapshot,
                extraNames: new[] { "机关" },
                _catalog);

            Assert.AreEqual(2, terms.Count);
            Assert.AreEqual("刺藤", terms[0].DisplayName);
            Assert.AreEqual("机关", terms[1].DisplayName);
            Assert.AreEqual("中立单位，可以被攻击，可以被部分道具卡选作目标，可破坏", terms[1].Explanation);
        }

        [Test]
        public void StripMarkup_KeepsSemanticPrefix_ReplacesIconCodes()
        {
            var raw = "[使用时] 将[HP]恢复到[MHP]";
            var stripped = CardGlossaryTerms.StripMarkupForReadableFallback(raw, _catalog);
            Assert.AreEqual("[使用时] 将血量恢复到血量上限", stripped);
        }

        private static void AssertNames(
            IReadOnlyList<CardGlossaryTerms.ResolvedTerm> terms,
            params string[] expected)
        {
            Assert.AreEqual(expected.Length, terms.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], terms[i].DisplayName);
            }
        }
    }
}
