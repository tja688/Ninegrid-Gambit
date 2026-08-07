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

        [Test]
        public void ParamFiller_PlainToken_TakesFirstAssemblyContainingKey()
        {
            var assemblies = new[]
            {
                new EffectAssemblyDto
                {
                    id = "relic.wood_armor.base",
                    templateId = "tpl.relic.wood_armor.base",
                    argsJson = "{\"value\":2}",
                },
                new EffectAssemblyDto
                {
                    id = "relic.wood_armor.set",
                    templateId = "tpl.relic.wood_armor.set",
                    argsJson = "{\"value\":8}",
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "生命上限+{value}；套装生命上限+{value}",
                assemblies);

            // 简单式语义不唯一：两处都取第一个含键装配（旧行为保留）。
            Assert.AreEqual("生命上限+2；套装生命上限+2", filled);
        }

        [Test]
        public void ParamFiller_QualifiedDefIdPrefix_UnanimousValues_Fills()
        {
            var assemblies = new[]
            {
                new EffectAssemblyDto
                {
                    id = "trap.attack_totem.aura",
                    templateId = "tpl.trap.attack_totem.aura",
                    argsJson = "{\"value\":1}",
                },
                new EffectAssemblyDto
                {
                    id = "trap.attack_totem.refresh",
                    templateId = "tpl.trap.attack_totem.refresh",
                    argsJson = "{\"value\":1}",
                },
                new EffectAssemblyDto
                {
                    id = "trap.attack_totem.aura_player",
                    templateId = "tpl.trap.attack_totem.aura_player",
                    argsJson = "{\"value\":1}",
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "相邻格对象攻击+{trap.attack_totem.value}",
                assemblies);

            Assert.AreEqual("相邻格对象攻击+1", filled);
        }

        [Test]
        public void ParamFiller_QualifiedDefIdPrefix_AmbiguousValues_KeepsLiteral()
        {
            var assemblies = new[]
            {
                new EffectAssemblyDto
                {
                    id = "relic.wood_armor.base",
                    templateId = "tpl.relic.wood_armor.base",
                    argsJson = "{\"value\":2}",
                },
                new EffectAssemblyDto
                {
                    id = "relic.wood_armor.set",
                    templateId = "tpl.relic.wood_armor.set",
                    argsJson = "{\"value\":8}",
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "生命上限+{relic.wood_armor.value}",
                assemblies);

            // 前缀命中两装配取值不一致 → 歧义，保留字面量提示作者改精确装配。
            Assert.AreEqual("生命上限+{relic.wood_armor.value}", filled);
        }

        [Test]
        public void ParamFiller_QualifiedExactAssemblyId_Fills()
        {
            var assemblies = new[]
            {
                new EffectAssemblyDto
                {
                    id = "help.watchtower.board_corner",
                    templateId = "tpl.help.watchtower.board_corner",
                    argsJson = "{\"amount\":3}",
                },
                new EffectAssemblyDto
                {
                    id = "help.watchtower.item_battle",
                    templateId = "tpl.help.watchtower.item_battle",
                    argsJson = "{\"amount\":2}",
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "部署造成{help.watchtower.board_corner.amount}点伤害；入战造成{help.watchtower.item_battle.amount}点伤害",
                assemblies);

            Assert.AreEqual("部署造成3点伤害；入战造成2点伤害", filled);
        }

        [Test]
        public void ParamFiller_QualifiedExactTemplateId_Fills()
        {
            var assemblies = new[]
            {
                new EffectAssemblyDto
                {
                    id = "relic.wood_armor.base",
                    templateId = "tpl.relic.wood_armor.base",
                    argsJson = "{\"value\":2}",
                },
                new EffectAssemblyDto
                {
                    id = "relic.wood_armor.set",
                    templateId = "tpl.relic.wood_armor.set",
                    argsJson = "{\"value\":8}",
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "套装生命上限+{tpl.relic.wood_armor.set.value}",
                assemblies);

            Assert.AreEqual("套装生命上限+8", filled);
        }

        [Test]
        public void ParamFiller_QualifiedPrefix_KeyMissingInSomeAssemblies_FillsFromContainingOnes()
        {
            var assemblies = new[]
            {
                new EffectAssemblyDto
                {
                    id = "trap.flame.move",
                    templateId = "tpl.trap.flame.move",
                    argsJson = "{\"amount\":1}",
                },
                new EffectAssemblyDto
                {
                    id = "trap.flame.remove",
                    templateId = "tpl.trap.flame.remove",
                    argsJson = "{\"reason\":\"trap.flame\"}",
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "踩中每移动{trap.flame.amount}次",
                assemblies);

            Assert.AreEqual("踩中每移动1次", filled);
        }

        [Test]
        public void ParamFiller_UnknownQualifier_KeepsLiteral()
        {
            var assemblies = new[]
            {
                new EffectAssemblyDto
                {
                    id = "trap.attack_totem.aura",
                    templateId = "tpl.trap.attack_totem.aura",
                    argsJson = "{\"value\":1}",
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "攻击+{monster.skeleton.value}",
                assemblies);

            Assert.AreEqual("攻击+{monster.skeleton.value}", filled);
        }

        [Test]
        public void ParamFiller_EmptyAssemblies_KeepsTokensLiteral()
        {
            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                "攻击+{trap.attack_totem.value}",
                System.Array.Empty<EffectAssemblyDto>());

            Assert.AreEqual("攻击+{trap.attack_totem.value}", filled);
        }

        [Test]
        public void ParamFiller_PlainArgsFill_KeepsDottedTokenLiteral()
        {
            var args = new Dictionary<string, object> { { "value", 5 } };
            Assert.AreEqual(
                "攻击+{trap.attack_totem.value}",
                CardFaceDescriptionParamFiller.Fill("攻击+{trap.attack_totem.value}", args));
            Assert.AreEqual(
                "攻击+5",
                CardFaceDescriptionParamFiller.Fill("攻击+{value}", args));
        }

        [Test]
        public void Seam_ExactAssemblyIdToken_FillsAndPassesTokenContract()
        {
            // ADR-0035 / #154：投影缝（初始装配实参填充）与令牌契约在边界上一致——
            // 缝能填的 {装配id.键} 令牌，契约校验必须干净。
            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "help.healing_potion",
                kind = "HelpCard",
                deckId = "deck.help",
                description = "恢复{help.healing_potion.use.amount}点[HP]",
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto { id = "help.healing_potion.use", argsJson = "{\"amount\":10}" },
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                dto.description,
                dto.effectAssemblies);

            Assert.AreEqual("恢复10点[HP]", filled);
            Assert.AreEqual(0, CardDescriptionTokenRules.ValidateCard(dto).Count);
        }

        [Test]
        public void Seam_DefIdPrefixToken_KeepsLiteralAndFailsTokenContract()
        {
            // defId 前缀式在范围内机关卡上退役：缝对歧义前缀保持字面量（防错线），契约校验必须报错。
            var dto = new CardPresentationConfigDto
            {
                schemaVersion = 2,
                contentId = "trap.attack_totem",
                kind = "Trap",
                deckId = "deck.trap",
                description = "攻击+{trap.attack_totem.value}",
                effectAssemblies = new[]
                {
                    new EffectAssemblyDto { id = "trap.attack_totem.aura", argsJson = "{\"value\":1}" },
                    new EffectAssemblyDto { id = "trap.attack_totem.refresh", argsJson = "{\"value\":8}" },
                },
            };

            var filled = CardFaceDescriptionParamFiller.FillFromAssemblies(
                dto.description,
                dto.effectAssemblies);

            Assert.AreEqual("攻击+{trap.attack_totem.value}", filled);
            var errors = CardDescriptionTokenRules.ValidateCard(dto);
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("{trap.attack_totem.value}", errors[0]);
        }
    }
}
