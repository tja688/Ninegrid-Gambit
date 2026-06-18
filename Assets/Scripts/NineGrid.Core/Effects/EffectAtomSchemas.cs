using System;
using System.Collections.Generic;

namespace NineGrid.Core.Effects
{
    internal static class EffectAtomSchemas
    {
        public static void ValidateDefinition(EffectDefinition definition, EffectAtomRegistry registry, EffectValidationResult result)
        {
            if (definition == null || registry == null || result == null)
            {
                return;
            }

            if (definition.Kind == EffectKind.Triggered)
            {
                ValidateNode(definition.Trigger, EffectAtomKind.Trigger, "trigger", registry, result);
                ValidateNode(definition.Target, EffectAtomKind.Target, "target", registry, result);
                ValidateNode(definition.Action, EffectAtomKind.Action, "action", registry, result);
            }
            else if (definition.Kind == EffectKind.Modifier)
            {
                ValidateNode(definition.Target, EffectAtomKind.Target, "target", registry, result);
                ValidateModifierBlock(definition.Modifier, "modifier", result);
            }
            else if (definition.Kind == EffectKind.RuleModifier)
            {
                ValidateRuleModifierBlock(definition.RuleModifier, "ruleModifier", result);
            }

            for (var i = 0; i < definition.Conditions.Count; i++)
            {
                ValidateNode(definition.Conditions[i], EffectAtomKind.Condition, "conditions[" + i + "]", registry, result);
            }
        }

        private static void ValidateNode(
            EffectDslNode node,
            EffectAtomKind expectedKind,
            string path,
            EffectAtomRegistry registry,
            EffectValidationResult result)
        {
            if (node == null || node.IsNull)
            {
                return;
            }

            var atom = ReadAtomName(node);
            if (string.IsNullOrEmpty(atom))
            {
                result.Add("atom.missing", path + " requires atom.");
                return;
            }

            if (!IsKnownAtom(registry, expectedKind, atom))
            {
                result.Add("atom.unknown", path + " uses unknown " + expectedKind + " atom '" + atom + "'.");
                return;
            }

            ValidateRequiredFields(atom, expectedKind, node, path, result);
            ValidateRanges(atom, expectedKind, node, path, result);

            if (expectedKind == EffectAtomKind.Action)
            {
                ValidateNestedActionGraph(atom, node, path, registry, result);
            }
        }

        private static void ValidateNestedActionGraph(
            string atom,
            EffectDslNode node,
            string path,
            EffectAtomRegistry registry,
            EffectValidationResult result)
        {
            if (Same(atom, "Sequence"))
            {
                var actions = node.Get("actions").AsArray();
                if (actions.Count == 0)
                {
                    result.Add("schema.sequence.empty", path + ".actions must not be empty.");
                }

                for (var i = 0; i < actions.Count; i++)
                {
                    ValidateNode(actions[i], EffectAtomKind.Action, path + ".actions[" + i + "]", registry, result);
                }

                return;
            }

            if (Same(atom, "WeightedRandom"))
            {
                var choices = node.Get("choices").AsArray();
                if (choices.Count == 0)
                {
                    result.Add("schema.weightedRandom.empty", path + ".choices must not be empty.");
                }

                for (var i = 0; i < choices.Count; i++)
                {
                    var choice = choices[i];
                    if (choice.Get("weight").AsInt(-1) < 0)
                    {
                        result.Add("schema.weightedRandom.weight", path + ".choices[" + i + "].weight must be >= 0.");
                    }

                    ValidateNode(choice.Get("action"), EffectAtomKind.Action, path + ".choices[" + i + "].action", registry, result);
                }

                return;
            }

            if (Same(atom, "Repeat"))
            {
                if (!node.Has("action"))
                {
                    result.Add("schema.repeat.action", path + ".action is required.");
                }

                ValidateNode(node.Get("action"), EffectAtomKind.Action, path + ".action", registry, result);
                return;
            }

            if (Same(atom, "Conditional"))
            {
                ValidateNode(node.Get("condition"), EffectAtomKind.Condition, path + ".condition", registry, result);
                if (node.Has("action"))
                {
                    ValidateNode(node.Get("action"), EffectAtomKind.Action, path + ".action", registry, result);
                }

                if (node.Has("elseAction"))
                {
                    ValidateNode(node.Get("elseAction"), EffectAtomKind.Action, path + ".elseAction", registry, result);
                }
            }
        }

        private static void ValidateModifierBlock(EffectDslNode node, string path, EffectValidationResult result)
        {
            if (node == null || node.IsNull)
            {
                return;
            }

            if (!node.Has("stat"))
            {
                result.Add("schema.modifier.stat", path + ".stat is required.");
            }

            if (!node.Has("value"))
            {
                result.Add("schema.modifier.value", path + ".value is required.");
            }
        }

        private static void ValidateRuleModifierBlock(EffectDslNode node, string path, EffectValidationResult result)
        {
            if (node == null || node.IsNull)
            {
                return;
            }

            if (!node.Has("rule"))
            {
                result.Add("schema.ruleModifier.rule", path + ".rule is required.");
            }

            if (!node.Has("value"))
            {
                result.Add("schema.ruleModifier.value", path + ".value is required.");
            }
        }

        private static void ValidateRequiredFields(
            string atom,
            EffectAtomKind kind,
            EffectDslNode node,
            string path,
            EffectValidationResult result)
        {
            if (kind == EffectAtomKind.Trigger)
            {
                if (Same(atom, "OnMoveToSlot") && !node.Has("slot"))
                {
                    result.Add("schema.trigger.slot", path + ".slot is required for OnMoveToSlot.");
                }

                if (Same(atom, "OnCumulative"))
                {
                    if (!node.Has("metric"))
                    {
                        result.Add("schema.trigger.metric", path + ".metric is required for OnCumulative.");
                    }

                    if (!node.Has("threshold"))
                    {
                        result.Add("schema.trigger.threshold", path + ".threshold is required for OnCumulative.");
                    }
                }

                return;
            }

            if (kind == EffectAtomKind.Condition)
            {
                if (Same(atom, "AtSlot") && !node.Has("slot"))
                {
                    result.Add("schema.condition.slot", path + ".slot is required for AtSlot.");
                }

                if (Same(atom, "HasCard") && !node.Has("defId") && !node.Has("kind") && !node.Has("zone"))
                {
                    result.Add("schema.condition.hasCard", path + " requires defId, kind, or zone for HasCard.");
                }

                if (Same(atom, "CardCounter") && !node.Has("key"))
                {
                    result.Add("schema.condition.key", path + ".key is required for CardCounter.");
                }

                if (Same(atom, "AdjacentHasCard") && !node.Has("defId"))
                {
                    result.Add("schema.condition.defId", path + ".defId is required for AdjacentHasCard.");
                }

                if (Same(atom, "OwnsRelicSet"))
                {
                    var defIds = node.Get("defIds").AsArray();
                    var single = node.Get("defId").AsString(string.Empty);
                    if (defIds.Count == 0 && string.IsNullOrEmpty(single))
                    {
                        result.Add("schema.condition.defIds", path + ".defIds must not be empty for OwnsRelicSet.");
                    }
                }

                return;
            }

            if (kind == EffectAtomKind.Target)
            {
                if (Same(atom, "SlotCard") && !node.Has("slot"))
                {
                    result.Add("schema.target.slot", path + ".slot is required for SlotCard.");
                }

                if (Same(atom, "Column") && !node.Has("column"))
                {
                    result.Add("schema.target.column", path + ".column is required for Column.");
                }

                if (Same(atom, "AdjacentCard") && !node.Has("defId"))
                {
                    result.Add("schema.target.defId", path + ".defId is required for AdjacentCard.");
                }

                return;
            }

            if (kind != EffectAtomKind.Action)
            {
                return;
            }

            if (Same(atom, "DealDamage") || Same(atom, "Heal") || Same(atom, "GainArmor"))
            {
                if (!node.Has("amount") && !node.Has("value"))
                {
                    result.Add("schema.action.amount", path + ".amount or .value is required for " + atom + ".");
                }

                if (node.Has("value"))
                {
                    EffectValueExpression.Validate(node.Get("value"), path + ".value", result);
                }
            }
            else if (Same(atom, "ModifyGold") && !node.Has("delta"))
            {
                result.Add("schema.action.delta", path + ".delta is required for ModifyGold.");
            }
            else if (Same(atom, "OfferRewardChoice") && !node.Has("poolId"))
            {
                result.Add("schema.action.poolId", path + ".poolId is required for OfferRewardChoice.");
            }
            else if (Same(atom, "ShuffleInto") || Same(atom, "Spawn"))
            {
                if (!node.Has("defId"))
                {
                    result.Add("schema.action.defId", path + ".defId is required for " + atom + ".");
                }
            }
            else if (Same(atom, "GrantSkill") && !node.Has("skillDefId"))
            {
                result.Add("schema.action.skillDefId", path + ".skillDefId is required for GrantSkill.");
            }
            else if (Same(atom, "AddRuleModifier"))
            {
                if (!node.Has("rule"))
                {
                    result.Add("schema.action.rule", path + ".rule is required for AddRuleModifier.");
                }

                if (!node.Has("value"))
                {
                    result.Add("schema.action.value", path + ".value is required for AddRuleModifier.");
                }
            }
            else if (Same(atom, "Move") && !node.Has("toSlot"))
            {
                result.Add("schema.action.toSlot", path + ".toSlot is required for Move.");
            }
            else if (Same(atom, "Repeat") && !node.Has("action"))
            {
                result.Add("schema.action.nested", path + ".action is required for Repeat.");
            }
            else if (Same(atom, "WeightedRandom") && node.Get("choices").AsArray().Count == 0)
            {
                result.Add("schema.action.choices", path + ".choices must not be empty for WeightedRandom.");
            }
            else if (Same(atom, "Sequence") && node.Get("actions").AsArray().Count == 0)
            {
                result.Add("schema.action.actions", path + ".actions must not be empty for Sequence.");
            }
        }

        private static void ValidateRanges(
            string atom,
            EffectAtomKind kind,
            EffectDslNode node,
            string path,
            EffectValidationResult result)
        {
            if (kind == EffectAtomKind.Trigger)
            {
                if (Same(atom, "OnSelfMove") && node.Has("every") && node.Get("every").AsInt(1) < 1)
                {
                    result.Add("schema.range.every", path + ".every must be >= 1.");
                }

                if (Same(atom, "OnMoveToSlot") && node.Has("slot") && !IsBoardSlot(node.Get("slot").AsInt(0)))
                {
                    result.Add("schema.range.slot", path + ".slot must be between 1 and 9.");
                }

                if (Same(atom, "OnCumulative") && node.Has("threshold") && node.Get("threshold").AsInt(1) < 1)
                {
                    result.Add("schema.range.threshold", path + ".threshold must be >= 1.");
                }

                return;
            }

            if (kind == EffectAtomKind.Condition)
            {
                if (Same(atom, "AtSlot") && node.Has("slot") && !IsBoardSlot(node.Get("slot").AsInt(0)))
                {
                    result.Add("schema.range.slot", path + ".slot must be between 1 and 9.");
                }

                if (Same(atom, "HpBelow") && node.Has("pct"))
                {
                    var pct = node.Get("pct").AsFloat(0.5f);
                    if (pct <= 0f || pct > 1f)
                    {
                        result.Add("schema.range.pct", path + ".pct must be in (0, 1].");
                    }
                }

                if (Same(atom, "CardCounter") && node.Has("min") && node.Get("min").AsInt(1) < 1)
                {
                    result.Add("schema.range.min", path + ".min must be >= 1.");
                }

                return;
            }

            if (kind == EffectAtomKind.Target)
            {
                if (Same(atom, "SlotCard") && node.Has("slot") && !IsBoardSlot(node.Get("slot").AsInt(0)))
                {
                    result.Add("schema.range.slot", path + ".slot must be between 1 and 9.");
                }

                if (Same(atom, "Column") && node.Has("column"))
                {
                    var column = node.Get("column").AsInt(0);
                    if (column < 1 || column > 3)
                    {
                        result.Add("schema.range.column", path + ".column must be between 1 and 3.");
                    }
                }

                if (Same(atom, "FilteredCards") && node.Has("count") && node.Get("count").AsInt(0) < 0)
                {
                    result.Add("schema.range.count", path + ".count must be >= 0.");
                }

                return;
            }

            if (kind != EffectAtomKind.Action)
            {
                return;
            }

            if ((Same(atom, "DealDamage") || Same(atom, "Heal") || Same(atom, "GainArmor"))
                && node.Has("amount")
                && node.Get("amount").AsInt(0) < 0)
            {
                result.Add("schema.range.amount", path + ".amount must be >= 0.");
            }

            if (Same(atom, "Repeat") && node.Has("count") && node.Get("count").AsInt(0) < 0)
            {
                result.Add("schema.range.count", path + ".count must be >= 0.");
            }

            if (Same(atom, "Rotate") && node.Has("count") && node.Get("count").AsInt(0) < 0)
            {
                result.Add("schema.range.count", path + ".count must be >= 0.");
            }

            if (Same(atom, "Rotate") && node.Has("direction"))
            {
                var direction = node.Get("direction").AsString(string.Empty);
                if (!Same(direction, "Clockwise") && !Same(direction, "CounterClockwise"))
                {
                    result.Add("schema.rotate.direction", path + ".direction must be Clockwise or CounterClockwise.");
                }
            }
        }

        private static bool IsKnownAtom(EffectAtomRegistry registry, EffectAtomKind kind, string atom)
        {
            switch (kind)
            {
                case EffectAtomKind.Trigger:
                    return registry.Triggers.ContainsKey(atom);
                case EffectAtomKind.Condition:
                    return registry.Conditions.ContainsKey(atom);
                case EffectAtomKind.Target:
                    return registry.Targets.ContainsKey(atom);
                case EffectAtomKind.Action:
                    return registry.Actions.ContainsKey(atom);
                default:
                    return false;
            }
        }

        private static string ReadAtomName(EffectDslNode node)
        {
            return node.Get("atom").AsString(node.Get("type").AsString(string.Empty));
        }

        private static bool IsBoardSlot(int slot)
        {
            return slot >= SlotId.MinBoardIndex && slot <= SlotId.MaxBoardIndex;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
