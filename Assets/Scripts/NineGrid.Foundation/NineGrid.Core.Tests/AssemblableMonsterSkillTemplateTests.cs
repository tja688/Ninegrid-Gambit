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
    /// 本轮可拼装怪物技能模板：装配解析 + Validate 全绿。
    /// </summary>
    public sealed class AssemblableMonsterSkillTemplateTests
    {
        private static readonly string[] TemplateIds =
        {
            "tpl.skill.stack_armor.interact",
            "tpl.skill.sacrifice.remove",
            "tpl.skill.absorb.remove",
            "tpl.skill.chant.interact",
            "tpl.skill.call_melee6.interact",
            "tpl.skill.link_armor.interact",
            "tpl.skill.endless_flame.remove",
            "tpl.skill.attrition.remove",
            "tpl.skill.cloud_breath.interact",
            "tpl.skill.link_attack_bonus.rule",
            "tpl.skill.link_prep.move",
        };

        private IArchitecture mArch;
        private IEffectSystem mEffects;

        [SetUp]
        public void SetUp()
        {
            NineGridArchitecture.ResetForTests();
            EffectTemplateCatalog.Invalidate();
            mArch = NineGridArchitecture.Current;
            mArch.GetUtility<IConfigUtility>().Set(ContentConfigKeys.DefaultCatalog, ContentCatalogBootstrap.Load());
            mEffects = mArch.GetSystem<IEffectSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EffectTemplateCatalog.Invalidate();
            NineGridArchitecture.ResetForTests();
        }

        [Test]
        public void AssemblableSkillTemplates_ResolveAndValidate()
        {
            var argsByTemplate = new Dictionary<string, string>
            {
                ["tpl.skill.stack_armor.interact"] = "{\"amount\":2}",
                ["tpl.skill.sacrifice.remove"] = "{\"delta\":1,\"reason\":\"skill.sacrifice\"}",
                ["tpl.skill.absorb.remove"] =
                    "{\"weight\":1,\"delta\":1,\"reason\":\"skill.absorb\",\"action_choices_1_action_delta\":2,\"action_choices_2_action_delta\":4}",
                ["tpl.skill.chant.interact"] = "{\"count\":1}",
                ["tpl.skill.call_melee6.interact"] = "{\"count\":1}",
                ["tpl.skill.link_armor.interact"] = "{\"amount\":1}",
                ["tpl.skill.endless_flame.remove"] = "{\"count\":1}",
                ["tpl.skill.attrition.remove"] =
                    "{\"delta\":-1,\"reason\":\"skill.attrition\",\"action_actions_1_delta\":-10}",
                ["tpl.skill.cloud_breath.interact"] = "{\"reason\":\"skill.cloud_breath\"}",
                ["tpl.skill.link_attack_bonus.rule"] =
                    "{\"value\":1,\"source\":\"skill.link_attack_bonus\"}",
                ["tpl.skill.link_prep.move"] = "{\"reason\":\"skill.link_prep\"}",
            };

            foreach (var templateId in TemplateIds)
            {
                Assert.IsTrue(
                    EffectTemplateCatalog.TryGet(templateId, out var template),
                    "missing template " + templateId);
                Assert.IsFalse(string.IsNullOrEmpty(template.DesignText), templateId);

                var mountId = templateId.Replace("tpl.", string.Empty);
                var resolved = EffectAssemblyResolver.Resolve(
                    template,
                    mountId,
                    EffectContainerType.MonsterSkill,
                    EffectAssemblyResolver.ParseArgsJson(argsByTemplate[templateId]));
                Assert.IsNotNull(resolved, templateId);

                var definition = mEffects.ParseJson(resolved.Json);
                var validation = mEffects.Validate(definition);
                Assert.IsTrue(
                    validation.IsValid,
                    templateId + " => " + string.Join("; ", Format(validation)));
            }
        }

        private static List<string> Format(EffectValidationResult result)
        {
            var lines = new List<string>();
            if (result == null || result.Issues == null)
            {
                return lines;
            }

            for (var i = 0; i < result.Issues.Count; i++)
            {
                lines.Add(result.Issues[i].Code + ": " + result.Issues[i].Message);
            }

            return lines;
        }
    }
}
