using System.Collections.Generic;
using NineGrid.Cards.Presentation;
using NineGrid.Content.CardPresentation;
using NUnit.Framework;
using UnityEngine;

namespace NineGrid.Presentation.Tests.Cards
{
    public sealed class CardFaceDescriptionParamAndDetailTests
    {
        private CardFaceDescriptionIconCatalogSO _glossary;

        [SetUp]
        public void SetUp()
        {
            _glossary = ScriptableObject.CreateInstance<CardFaceDescriptionIconCatalogSO>();
            _glossary.ReplaceEntries(new[]
            {
                new CardFaceDescriptionIconCatalogSO.Entry
                {
                    code = "灼烧",
                    displayNameZh = "灼烧",
                    explanation = "每回合受到额外伤害。",
                    partition = "状态",
                    sprite = null,
                },
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_glossary != null)
            {
                Object.DestroyImmediate(_glossary);
                _glossary = null;
            }
        }

        [Test]
        public void ParamFiller_ReplacesPlaceholdersFromAssemblyArgs()
        {
            var assemblies = new[]
            {
                new EffectAssemblyDto
                {
                    id = "help.healing_potion.use",
                    templateId = "tpl.heal",
                    argsJson = "{\"amount\":10}",
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "[使用时] 恢复{amount}点血量",
                assemblies);

            Assert.AreEqual("[使用时] 恢复10点血量", filled);
        }

        [Test]
        public void DetailComposer_ExpandsGlossaryEntriesFromSameCodes()
        {
            var detail = CardDetailDescriptionComposer.Compose(
                "造成[灼烧]效果",
                _glossary);

            StringAssert.Contains("造成[灼烧]效果", detail);
            StringAssert.Contains("灼烧：每回合受到额外伤害。", detail);
        }

        [Test]
        public void FaceIntro_FillsParamsLikeBasicDescription()
        {
            var assemblies = new[]
            {
                new EffectAssemblyDto
                {
                    id = "help.fire.use",
                    templateId = "tpl.burn",
                    argsJson = "{\"amount\":3}",
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "造成{amount}层[灼烧]",
                assemblies);
            var detail = CardDetailDescriptionComposer.Compose(filled, _glossary);

            Assert.AreEqual("造成3层[灼烧]", filled);
            StringAssert.Contains("灼烧：每回合受到额外伤害。", detail);
        }
    }
}
