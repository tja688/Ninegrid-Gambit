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
    /// #72 / ADR-0010 Phase A+B：requires 校验与运行时；上下文开关拒旧形；未声明挂载审计归零。
    /// </summary>
    public sealed class EffectSelfDeclarationContractTests
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
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void EffectValidator_RejectsUnknownRequiresToken()
        {
            const string json =
                "{\"id\":\"test.requires.unknown\",\"containerType\":\"HelpCard\",\"kind\":\"Triggered\","
                + "\"requires\":[\"NotARealToken\"],"
                + "\"trigger\":{\"atom\":\"OnSelfUsed\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Heal\",\"amount\":1,\"actor\":\"Player\"}}";

            var result = mEffects.Validate(mEffects.ParseJson(json));
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(HasCode(result, "requires.unknown"), string.Join("; ", Format(result)));
        }

        [Test]
        public void EffectValidator_RejectsHasOwnerEntity_OnRelic()
        {
            const string json =
                "{\"id\":\"test.requires.mismatch\",\"containerType\":\"Relic\",\"kind\":\"Triggered\","
                + "\"requires\":[\"HasOwnerEntity\"],"
                + "\"trigger\":{\"atom\":\"OnSelfUsed\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Heal\",\"amount\":1,\"actor\":\"Player\"}}";

            var result = mEffects.Validate(mEffects.ParseJson(json));
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(HasCode(result, "requires.mount-mismatch"), string.Join("; ", Format(result)));
        }

        [Test]
        public void EffectValidator_RejectsMissingRequires_OnCardOwnedTriggered()
        {
            const string json =
                "{\"id\":\"test.requires.missing\",\"containerType\":\"HelpCard\",\"kind\":\"Triggered\","
                + "\"trigger\":{\"atom\":\"OnSelfUsed\"},"
                + "\"target\":{\"atom\":\"Player\"},"
                + "\"action\":{\"atom\":\"Heal\",\"amount\":1,\"actor\":\"Player\"}}";

            var result = mEffects.Validate(mEffects.ParseJson(json));
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(HasCode(result, "requires.missing"), string.Join("; ", Format(result)));
        }

        [Test]
        public void EffectValidator_RejectsOwnerOnlyContextSwitchParam()
        {
            const string json =
                "{\"id\":\"test.deprecated.ownerOnly\",\"containerType\":\"MonsterSkill\",\"kind\":\"Triggered\","
                + "\"requires\":[\"HasOwnerEntity\",\"CardZoneTriggerable\"],"
                + "\"trigger\":{\"atom\":\"OnRemove\",\"ownerOnly\":false},"
                + "\"target\":{\"atom\":\"Self\"},"
                + "\"action\":{\"atom\":\"ModifyBaseStat\",\"stat\":\"Attack\",\"delta\":1,\"reason\":\"x\"}}";

            var result = mEffects.Validate(mEffects.ParseJson(json));
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(HasCode(result, "schema.deprecated-param"), string.Join("; ", Format(result)));
        }

        [Test]
        public void BootstrapCatalog_CardOwnedMounts_HaveExplicitSceneDeclaration()
        {
            EffectTemplateCatalog.Invalidate();
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);

            var undeclared = new List<string>();
            var deprecated = new List<string>();
            foreach (var pair in catalog.Effects)
            {
                var effect = pair.Value;
                if (effect.State != ContentImplementationState.Implemented)
                {
                    continue;
                }

                if (effect.ContainerType != EffectContainerType.HelpCard
                    && effect.ContainerType != EffectContainerType.MonsterSkill)
                {
                    continue;
                }

                var definition = mEffects.ParseJson(effect.Json);
                if (!EffectRequiresValidator.HasExplicitSceneDeclaration(definition))
                {
                    undeclared.Add(effect.Id);
                }

                var validation = mEffects.Validate(definition);
                for (var i = 0; i < validation.Issues.Count; i++)
                {
                    if (validation.Issues[i].Code == "schema.deprecated-param")
                    {
                        deprecated.Add(effect.Id + ":" + validation.Issues[i].Message);
                    }
                }
            }

            Assert.AreEqual(0, undeclared.Count, "Undeclared mounts: " + string.Join(", ", undeclared));
            Assert.AreEqual(0, deprecated.Count, "Deprecated params: " + string.Join(", ", deprecated));
        }

        [Test]
        public void BootstrapCatalog_ValidateCatalog_IsValid_AfterSelfDeclaration()
        {
            EffectTemplateCatalog.Invalidate();
            var catalog = ContentCatalogBootstrap.Load();
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, catalog);
            var report = mArch.GetSystem<IContentSystem>().ValidateCatalog();
            Assert.IsTrue(report.IsValid, string.Join("; ", report.Issues));
        }

        private static bool HasCode(EffectValidationResult result, string code)
        {
            for (var i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].Code == code)
                {
                    return true;
                }
            }

            return false;
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
