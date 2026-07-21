using System;
using System.Collections.Generic;
using System.Text;

namespace NineGrid.Core.Effects
{
    public sealed class EffectDefinition
    {
        private readonly List<EffectDslNode> mConditions = new List<EffectDslNode>();

        public string Id { get; internal set; }
        public string TypeTag { get; internal set; }
        public string Verb { get; internal set; }
        public EffectKind Kind { get; internal set; }
        public EffectContainerType ContainerType { get; internal set; }
        public EffectDslNode Root { get; internal set; }
        public EffectDslNode Trigger { get; internal set; }
        public EffectDslNode Target { get; internal set; }
        public EffectDslNode Action { get; internal set; }
        public EffectDslNode Modifier { get; internal set; }
        public EffectDslNode RuleModifier { get; internal set; }

        public IReadOnlyList<EffectDslNode> Conditions
        {
            get { return mConditions; }
        }

        internal void AddCondition(EffectDslNode node)
        {
            if (node != null && !node.IsNull)
            {
                mConditions.Add(node);
            }
        }
    }

    public sealed class EffectOwner
    {
        public EffectOwner(EffectContainerType containerType, string sourceDefId, int ownerUid)
        {
            ContainerType = containerType;
            SourceDefId = sourceDefId ?? string.Empty;
            OwnerUid = ownerUid;
        }

        public EffectContainerType ContainerType { get; private set; }
        public string SourceDefId { get; private set; }
        public int OwnerUid { get; private set; }
    }

    public static class EffectDefinitionParser
    {
        public static EffectDefinition ParseJson(string json)
        {
            return Parse(EffectJson.Parse(json));
        }

        public static EffectDefinition Parse(EffectDslNode root)
        {
            if (root == null || !root.IsObject)
            {
                throw new ArgumentException("Effect definition root must be a JSON object.", "root");
            }

            var definition = new EffectDefinition
            {
                Root = root,
                Id = root.Get("id").AsString(string.Empty),
                TypeTag = root.Get("typeTag").AsString(string.Empty),
                Verb = root.Get("verb").AsString(string.Empty),
                Kind = root.Get("kind").AsEnum(EffectKind.Unknown),
                ContainerType = root.Get("containerType").AsEnum(EffectContainerType.Unknown),
                Trigger = root.Get("trigger"),
                Target = root.Get("target"),
                Action = root.Get("action"),
                Modifier = root.Get("modifier"),
                RuleModifier = root.Get("ruleModifier")
            };

            var conditions = root.Get("conditions").AsArray();
            for (var i = 0; i < conditions.Count; i++)
            {
                definition.AddCondition(conditions[i]);
            }

            var singleCondition = root.Get("condition");
            if (!singleCondition.IsNull)
            {
                definition.AddCondition(singleCondition);
            }

            return definition;
        }
    }

    public sealed class EffectValidationIssue
    {
        public EffectValidationIssue(string code, string message)
        {
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public string Code { get; private set; }
        public string Message { get; private set; }

        public override string ToString()
        {
            return Code + ": " + Message;
        }
    }

    public sealed class EffectValidationResult
    {
        private readonly List<EffectValidationIssue> mIssues = new List<EffectValidationIssue>();

        public IReadOnlyList<EffectValidationIssue> Issues
        {
            get { return mIssues; }
        }

        public bool IsValid
        {
            get { return mIssues.Count == 0; }
        }

        public string GoldenSnapshot { get; internal set; }

        internal void Add(string code, string message)
        {
            mIssues.Add(new EffectValidationIssue(code, message));
        }
    }

    public sealed class EffectValidator
    {
        private readonly EffectAtomRegistry mRegistry;

        public EffectValidator()
            : this(null)
        {
        }

        public EffectValidator(EffectAtomRegistry registry)
        {
            mRegistry = registry;
        }

        public EffectValidationResult Validate(EffectDefinition definition)
        {
            var result = new EffectValidationResult();
            if (definition == null)
            {
                result.Add("missing.definition", "Effect definition is null.");
                return result;
            }

            ValidateTypeTag(definition, result);
            ValidateRequiredFields(definition, result);
            ValidateMutualExclusion(definition, result);
            ValidateVerb(definition, result);
            if (mRegistry != null)
            {
                EffectAtomSchemas.ValidateDefinition(definition, mRegistry, result);
            }

            result.GoldenSnapshot = BuildGoldenSnapshot(definition);
            return result;
        }

        private static void ValidateTypeTag(EffectDefinition definition, EffectValidationResult result)
        {
            if (string.IsNullOrEmpty(definition.Id))
            {
                result.Add("required.id", "Effect id is required.");
            }

            if (definition.Kind == EffectKind.Unknown)
            {
                result.Add("required.kind", "Effect kind must be Triggered, Modifier, or RuleModifier.");
            }

            if (definition.ContainerType == EffectContainerType.Unknown)
            {
                result.Add("typeTag.container", "containerType is required.");
            }

            if (string.IsNullOrEmpty(definition.TypeTag))
            {
                result.Add("typeTag.missing", "typeTag is required as the first defense line.");
                return;
            }

            var expected = GetExpectedTypeTag(definition.ContainerType);
            if (!string.IsNullOrEmpty(expected) && !string.Equals(definition.TypeTag, expected, StringComparison.Ordinal))
            {
                result.Add("typeTag.mismatch", "typeTag " + definition.TypeTag + " does not match containerType " + definition.ContainerType + ".");
            }
        }

        private static void ValidateRequiredFields(EffectDefinition definition, EffectValidationResult result)
        {
            if (definition.Kind == EffectKind.Triggered)
            {
                if (definition.Trigger == null || definition.Trigger.IsNull)
                {
                    result.Add("required.trigger", "Triggered effects require trigger.");
                }

                if (definition.Action == null || definition.Action.IsNull)
                {
                    result.Add("required.action", "Triggered effects require action.");
                }
            }
            else if (definition.Kind == EffectKind.Modifier)
            {
                if (definition.Target == null || definition.Target.IsNull)
                {
                    result.Add("required.target", "Modifier effects require target.");
                }

                if (definition.Modifier == null || definition.Modifier.IsNull)
                {
                    result.Add("required.modifier", "Modifier effects require modifier.");
                }
            }
            else if (definition.Kind == EffectKind.RuleModifier)
            {
                if (definition.RuleModifier == null || definition.RuleModifier.IsNull)
                {
                    result.Add("required.ruleModifier", "RuleModifier effects require ruleModifier.");
                }
            }
        }

        private static void ValidateMutualExclusion(EffectDefinition definition, EffectValidationResult result)
        {
            if (definition.Kind == EffectKind.Triggered && Has(definition.Modifier))
            {
                result.Add("exclusive.modifier", "Triggered effects cannot also declare modifier.");
            }

            if (definition.Kind == EffectKind.Triggered && Has(definition.RuleModifier))
            {
                result.Add("exclusive.ruleModifier", "Triggered effects cannot also declare ruleModifier.");
            }

            if (definition.Kind == EffectKind.Modifier && (Has(definition.Action) || Has(definition.RuleModifier)))
            {
                result.Add("exclusive.modifierKind", "Modifier effects cannot declare action or ruleModifier.");
            }

            if (definition.Kind == EffectKind.RuleModifier && (Has(definition.Action) || Has(definition.Modifier)))
            {
                result.Add("exclusive.ruleModifierKind", "RuleModifier effects cannot declare action or modifier.");
            }
        }

        private static void ValidateVerb(EffectDefinition definition, EffectValidationResult result)
        {
            if (string.IsNullOrEmpty(definition.Verb))
            {
                return;
            }

            if (definition.ContainerType == EffectContainerType.Relic && Same(definition.Verb, "Use"))
            {
                result.Add("verb.relic", "Relic effects must trigger or stay active; they are not used.");
            }

            if (definition.ContainerType == EffectContainerType.HelpCard && Same(definition.Verb, "Equip"))
            {
                result.Add("verb.helpCard", "Help cards are not passive equipment.");
            }
        }

        public string BuildGoldenSnapshot(EffectDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(definition.Id);
            builder.Append("|");
            builder.Append(definition.Kind);
            builder.Append("|");
            builder.Append(definition.ContainerType);
            builder.Append("|trigger=");
            builder.Append(AtomName(definition.Trigger));
            builder.Append("|conditions=");
            builder.Append(definition.Conditions.Count);
            builder.Append("|target=");
            builder.Append(AtomName(definition.Target));
            builder.Append("|action=");
            builder.Append(AtomName(definition.Action));
            builder.Append("|modifier=");
            builder.Append(definition.Modifier == null || definition.Modifier.IsNull ? string.Empty : definition.Modifier.Get("stat").AsString(definition.Modifier.Get("rule").AsString(string.Empty)));
            builder.Append("|rule=");
            builder.Append(definition.RuleModifier == null || definition.RuleModifier.IsNull ? string.Empty : definition.RuleModifier.Get("rule").AsString(string.Empty));
            return builder.ToString();
        }

        private static bool Has(EffectDslNode node)
        {
            return node != null && !node.IsNull;
        }

        private static string AtomName(EffectDslNode node)
        {
            return node == null || node.IsNull ? string.Empty : node.Get("atom").AsString(node.Get("type").AsString(string.Empty));
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetExpectedTypeTag(EffectContainerType containerType)
        {
            switch (containerType)
            {
                case EffectContainerType.Relic:
                    return "【类型遗物】";
                case EffectContainerType.MonsterSkill:
                    return "【类型怪物技能】";
                case EffectContainerType.HelpCard:
                    return "【类型帮助卡】";
                default:
                    return string.Empty;
            }
        }
    }
}
