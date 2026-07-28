using System;
using System.Collections.Generic;
using NineGrid.Content;
using NineGrid.Core;
using NineGrid.Core.Content;
using NineGrid.Core.Effects;
using NineGrid.Core.Systems;
using NineGrid.Core.Utilities;
using NUnit.Framework;
using QFramework;

namespace NineGrid.Core.Tests
{
    /// <summary>
    /// #70 / ADR-0009：参数化模板 + 装配引用；取消 typeTag/verb 字符串门禁；requires 字段就位。
    /// </summary>
    public sealed class EffectTemplateAssemblyContractTests
    {
        private IArchitecture mArch;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, TableNineContentCatalog.CreateDefault());
            mEffects = mArch.GetSystem<IEffectSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void EffectValidator_AcceptsDefinition_WithoutTypeTagOrVerb()
        {
            const string json =
                "{\"id\":\"test.no_typetag\",\"containerType\":\"HelpCard\",\"kind\":\"Triggered\","
                + "\"requires\":[\"ActivatedByUse\",\"HasOwnerEntity\"],"
                + "\"trigger\":{\"atom\":\"OnSelfUsed\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Heal\",\"amount\":3,\"actor\":\"Player\"}}";

            var definition = mEffects.ParseJson(json);
            var result = mEffects.Validate(definition);

            Assert.IsTrue(result.IsValid, string.Join("; ", Format(result)));
            Assert.IsTrue(string.IsNullOrEmpty(definition.TypeTag));
            Assert.IsTrue(string.IsNullOrEmpty(definition.Verb));
        }

        [Test]
        public void EffectValidator_DoesNotRejectRelic_WithVerbUse()
        {
            const string json =
                "{\"id\":\"test.relic.verb\",\"containerType\":\"Relic\",\"kind\":\"Triggered\",\"verb\":\"Use\","
                + "\"requires\":[\"NoOwnerEntity\"],"
                + "\"trigger\":{\"atom\":\"OnSelfUsed\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Heal\",\"amount\":1,\"actor\":\"Player\"}}";

            var definition = mEffects.ParseJson(json);
            var result = mEffects.Validate(definition);

            Assert.IsTrue(result.IsValid, string.Join("; ", Format(result)));
            foreach (var issue in result.Issues)
            {
                Assert.IsFalse(issue.Code.StartsWith("typeTag."), issue.ToString());
                Assert.IsFalse(issue.Code.StartsWith("verb."), issue.ToString());
            }
        }

        [Test]
        public void EffectDefinitionParser_ReadsRequiresField()
        {
            const string json =
                "{\"id\":\"test.requires\",\"containerType\":\"HelpCard\",\"kind\":\"Triggered\","
                + "\"requires\":[\"ActivatedByUse\",\"HasOwnerEntity\"],"
                + "\"trigger\":{\"atom\":\"OnSelfUsed\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Heal\",\"amount\":1,\"actor\":\"Player\"}}";

            var definition = mEffects.ParseJson(json);

            Assert.AreEqual(2, definition.Requires.Count);
            Assert.AreEqual("ActivatedByUse", definition.Requires[0]);
            Assert.AreEqual("HasOwnerEntity", definition.Requires[1]);
        }

        [Test]
        public void EffectAssemblyResolver_SubstitutesArgs_AndPreservesRequires()
        {
            var template = new EffectTemplateDefinition(
                "tpl.heal_player_on_use_help_card",
                new[] { "ActivatedByUse" },
                System.Array.Empty<string>(),
                "{\"kind\":\"Triggered\",\"trigger\":{\"atom\":\"OnSelfUsed\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Heal\",\"amount\":\"{{amount}}\",\"actor\":\"Player\"}}",
                ContentImplementationState.Implemented,
                "heal player");

            var args = new Dictionary<string, object> { { "amount", 10L } };
            var resolved = EffectAssemblyResolver.Resolve(
                template,
                "help.healing_potion.use",
                EffectContainerType.HelpCard,
                args);

            Assert.AreEqual("help.healing_potion.use", resolved.Id);
            Assert.AreEqual(EffectContainerType.HelpCard, resolved.ContainerType);

            var definition = mEffects.ParseJson(resolved.Json);
            Assert.AreEqual("help.healing_potion.use", definition.Id);
            Assert.AreEqual(EffectContainerType.HelpCard, definition.ContainerType);
            Assert.AreEqual(1, definition.Requires.Count);
            Assert.AreEqual("ActivatedByUse", definition.Requires[0]);
            Assert.AreEqual(10, definition.Action.Get("amount").AsInt(0));
            Assert.IsTrue(string.IsNullOrEmpty(definition.TypeTag));
            Assert.IsTrue(mEffects.Validate(definition).IsValid, resolved.Json);
        }

        [Test]
        public void EffectAssemblyResolver_SharedTemplate_DifferentArgsAcrossContainers()
        {
            var template = new EffectTemplateDefinition(
                "tpl.heal_player_on_use_help_card",
                new[] { "ActivatedByUse" },
                System.Array.Empty<string>(),
                "{\"kind\":\"Triggered\",\"trigger\":{\"atom\":\"OnSelfUsed\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Heal\",\"amount\":\"{{amount}}\",\"actor\":\"Player\"}}",
                ContentImplementationState.Implemented,
                "heal player");

            var help = EffectAssemblyResolver.Resolve(
                template,
                "help.healing_potion.use",
                EffectContainerType.HelpCard,
                new Dictionary<string, object> { { "amount", 10L } });
            var relic = EffectAssemblyResolver.Resolve(
                template,
                "relic.junk_recycler.use",
                EffectContainerType.Relic,
                new Dictionary<string, object> { { "amount", 2L } });

            var helpDef = mEffects.ParseJson(help.Json);
            var relicDef = mEffects.ParseJson(relic.Json);

            Assert.AreEqual(10, helpDef.Action.Get("amount").AsInt(0));
            Assert.AreEqual(2, relicDef.Action.Get("amount").AsInt(0));
            Assert.AreEqual(EffectContainerType.HelpCard, helpDef.ContainerType);
            Assert.AreEqual(EffectContainerType.Relic, relicDef.ContainerType);
            Assert.IsTrue(mEffects.Validate(helpDef).IsValid);
            Assert.IsTrue(mEffects.Validate(relicDef).IsValid);
        }

        [Test]
        public void EffectAssemblyResolver_EmptyArgs_WithPlaceholders_Throws()
        {
            var template = new EffectTemplateDefinition(
                "tpl.skill.stray_cub.slot6",
                new[] { "HasOwnerEntity", "CardZoneTriggerable" },
                new[] { "AtSlot" },
                "{\"kind\":\"Modifier\",\"target\":{\"atom\":\"Self\"},"
                + "\"conditions\":[{\"atom\":\"AtSlot\",\"target\":\"Self\",\"slot\":6}],"
                + "\"modifier\":{\"stat\":\"Attack\",\"op\":\"Add\",\"value\":\"{{value}}\","
                + "\"layer\":\"Conditional\",\"scope\":\"Permanent\"}}",
                ContentImplementationState.Implemented,
                "[场上] 处于格6时，本卡攻击力+2");

            Assert.Throws<FormatException>(() => EffectAssemblyResolver.Resolve(
                template,
                "fx.empty_args",
                EffectContainerType.MonsterSkill,
                new Dictionary<string, object>()));
        }

        [Test]
        public void EffectAssemblyResolver_SuggestArgsJson_PrefersPeerThenPlaceholderDefaults()
        {
            var body = "{\"modifier\":{\"value\":\"{{value}}\",\"reason\":\"{{reason}}\"}}";
            Assert.AreEqual("{\"value\":2}", EffectAssemblyResolver.SuggestArgsJson(body, "{\"value\":2}"));
            Assert.AreEqual(
                "{\"value\":0,\"reason\":\"\"}",
                EffectAssemblyResolver.SuggestArgsJson(body, "{}"));
        }

        private static IEnumerable<string> Format(EffectValidationResult result)
        {
            for (var i = 0; i < result.Issues.Count; i++)
            {
                yield return result.Issues[i].ToString();
            }
        }
    }
}
