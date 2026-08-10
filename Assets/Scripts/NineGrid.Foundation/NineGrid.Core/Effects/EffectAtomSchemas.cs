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

            if (definition.Kind == EffectKind.Triggered)
            {
                ValidateCardOwnedTriggerScope(definition, result);
            }
        }

        private static void ValidateCardOwnedTriggerScope(EffectDefinition definition, EffectValidationResult result)
        {
            if (definition.ContainerType != EffectContainerType.MonsterSkill)
            {
                return;
            }

            var trigger = definition.Trigger;
            if (trigger == null || trigger.IsNull)
            {
                return;
            }

            var atom = ReadAtomName(trigger);
            if (Same(atom, "OnBattle") && !ConditionsDeclareEventScope(definition))
            {
                result.Add(
                    "scope.monster-on-battle",
                    "MonsterSkill OnBattle requires EventFilter or AtSlot scope in conditions.");
            }

            // ADR-0010 / #72：全局移除监听改由 OnAnyCardRemoved 自陈，不再用 ownerOnly:false。
            if (Same(atom, "OnAnyCardRemoved") && !ConditionsDeclareAdjacentOrEventFilter(definition))
            {
                result.Add(
                    "scope.remove-global",
                    "OnAnyCardRemoved requires EventFilter or Adjacent in conditions.");
            }
        }

        private static bool ConditionsDeclareEventScope(EffectDefinition definition)
        {
            var conditions = definition.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null || condition.IsNull)
                {
                    continue;
                }

                var atom = ReadAtomName(condition);
                if (Same(atom, "AtSlot")
                    || IsEventFilterFamily(atom)
                    || Same(atom, "ActorIsPlayer")
                    || Same(atom, "ActorIsSelf")
                    || Same(atom, "TargetIsSelf")
                    || Same(atom, "TargetNotSelf")
                    || Same(atom, "SourcePrefix")
                    || Same(atom, "ExcludeCause"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ConditionsDeclareAdjacentOrEventFilter(EffectDefinition definition)
        {
            var conditions = definition.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null || condition.IsNull)
                {
                    continue;
                }

                var atom = ReadAtomName(condition);
                if (Same(atom, "Adjacent") || IsEventFilterFamily(atom))
                {
                    return true;
                }
            }

            return false;
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

            EffectContextSwitchParams.RejectBannedParams(node, path, result);
            ValidateRequiredFields(atom, expectedKind, node, path, result);
            ValidateRanges(atom, expectedKind, node, path, result);

            if (expectedKind == EffectAtomKind.Action)
            {
                ValidateNestedActionGraph(atom, node, path, registry, result);
            }

            if (expectedKind == EffectAtomKind.Condition && Same(atom, "TargetCount"))
            {
                ValidateNode(node.Get("target"), EffectAtomKind.Target, path + ".target", registry, result);
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

            if (node.Has("value") && node.Get("value").IsObject)
            {
                EffectValueExpression.Validate(node.Get("value"), path + ".value", result);
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
                if (Same(atom, "OnMoveToSlot") && !node.Has("slot") && node.Get("slots").AsArray().Count == 0)
                {
                    result.Add("schema.trigger.slot", path + ".slot or .slots is required for OnMoveToSlot.");
                }

                if (Same(atom, "OnMoveToBoardMark") && !node.Has("mark"))
                {
                    result.Add("schema.trigger.mark", path + ".mark is required for OnMoveToBoardMark.");
                }

                if (Same(atom, "OnCumulative")
                    || Same(atom, "OnSelfArmorLostCumulative")
                    || Same(atom, "OnSelfDamageDealtToPlayerCumulative"))
                {
                    if (Same(atom, "OnCumulative") && !node.Has("metric"))
                    {
                        result.Add("schema.trigger.metric", path + ".metric is required for OnCumulative.");
                    }

                    if (!node.Has("threshold"))
                    {
                        result.Add("schema.trigger.threshold", path + ".threshold is required for " + atom + ".");
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

                if (Same(atom, "StatAtLeast"))
                {
                    if (!node.Has("stat"))
                    {
                        result.Add("schema.condition.stat", path + ".stat is required for StatAtLeast.");
                    }

                    if (!node.Has("value"))
                    {
                        result.Add("schema.condition.value", path + ".value is required for StatAtLeast.");
                    }
                }

                if (Same(atom, "HasCard") && !node.Has("defId") && !node.Has("kind") && !node.Has("zone"))
                {
                    result.Add("schema.condition.hasCard", path + " requires defId, kind, or zone for HasCard.");
                }

                if (Same(atom, "CardCounter") && !node.Has("key"))
                {
                    result.Add("schema.condition.key", path + ".key is required for CardCounter.");
                }

                if (Same(atom, "TargetCount") && !node.Has("target"))
                {
                    result.Add("schema.condition.target", path + ".target is required for TargetCount.");
                }

                if (Same(atom, "BoardMarkCount") && !node.Has("mark"))
                {
                    result.Add("schema.condition.mark", path + ".mark is required for BoardMarkCount.");
                }

                if (Same(atom, "CardZone") && !node.Has("zone"))
                {
                    result.Add("schema.condition.zone", path + ".zone is required for CardZone.");
                }

                if (Same(atom, "SourcePrefix") && string.IsNullOrEmpty(node.Get("prefix").AsString(string.Empty)))
                {
                    result.Add("schema.condition.prefix", path + ".prefix is required for SourcePrefix.");
                }

                if (Same(atom, "EventFilterSourcePrefix")
                    && string.IsNullOrEmpty(node.Get("prefix").AsString(string.Empty)))
                {
                    result.Add("schema.condition.prefix", path + ".prefix is required for EventFilterSourcePrefix.");
                }

                if (Same(atom, "ExcludeCause") && string.IsNullOrEmpty(node.Get("cause").AsString(string.Empty)))
                {
                    result.Add("schema.condition.cause", path + ".cause is required for ExcludeCause.");
                }

                if (Same(atom, "EventFilterExcludeCause")
                    && string.IsNullOrEmpty(node.Get("cause").AsString(string.Empty)))
                {
                    result.Add("schema.condition.cause", path + ".cause is required for EventFilterExcludeCause.");
                }

                if (Same(atom, "SelectedOption") && !node.Has("option"))
                {
                    result.Add("schema.condition.option", path + ".option is required for SelectedOption.");
                }

                if (Same(atom, "AdjacentHasCard")
                    && string.IsNullOrEmpty(node.Get("defId").AsString(string.Empty))
                    && !node.Has("kind"))
                {
                    result.Add("schema.condition.defId", path + ".defId or .kind is required for AdjacentHasCard.");
                }

                if (Same(atom, "AdjacentHasCard")
                    && node.Has("kind")
                    && !IsSupportedCardKind(node.Get("kind").AsString(string.Empty)))
                {
                    result.Add("schema.condition.kind", path + ".kind is not supported.");
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

                if (Same(atom, "BoardMarkEventCard") && !node.Has("mark"))
                {
                    result.Add("schema.target.mark", path + ".mark is required for BoardMarkEventCard.");
                }

                return;
            }

            if (kind != EffectAtomKind.Action)
            {
                return;
            }

            if (Same(atom, "DealDamage") || Same(atom, "Heal") || Same(atom, "GainArmor") || Same(atom, "SyncAdjacentBorrowedArmor"))
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
            else if (Same(atom, "TransferArmor"))
            {
                if (!node.Get("all").AsBool(false) && !node.Has("amount"))
                {
                    result.Add("schema.action.amount", path + ".amount is required for TransferArmor unless .all is true.");
                }
            }
            else if (Same(atom, "ModifyGold") && !node.Has("delta") && !node.Has("value"))
            {
                result.Add("schema.action.delta", path + ".delta or value is required for ModifyGold.");
            }
            else if (Same(atom, "ModifyBaseStat"))
            {
                if (!node.Has("stat"))
                {
                    result.Add("schema.action.stat", path + ".stat is required for ModifyBaseStat.");
                }

                if (!node.Has("delta") && !node.Has("value"))
                {
                    result.Add("schema.action.delta", path + ".delta or .value is required for ModifyBaseStat.");
                }

                if (node.Has("value"))
                {
                    EffectValueExpression.Validate(node.Get("value"), path + ".value", result);
                }
            }
            else if (Same(atom, "SetCounter") && !node.Has("key"))
            {
                result.Add("schema.action.key", path + ".key is required for SetCounter.");
            }
            else if (Same(atom, "RemoveRuleModifiersBySource") && !node.Has("source"))
            {
                result.Add("schema.action.source", path + ".source is required for RemoveRuleModifiersBySource.");
            }
            else if (Same(atom, "ModifyRelicRunContribution"))
            {
                if (!node.Has("stat"))
                {
                    result.Add("schema.action.stat", path + ".stat is required for ModifyRelicRunContribution.");
                }

                if (!node.Has("delta") && !node.Has("value"))
                {
                    result.Add(
                        "schema.action.delta",
                        path + ".delta or .value is required for ModifyRelicRunContribution.");
                }
            }
            else if (Same(atom, "OfferRewardChoice") && !node.Has("poolId"))
            {
                result.Add("schema.action.poolId", path + ".poolId is required for OfferRewardChoice.");
            }
            else if (Same(atom, "GrantRewardFromPool") && !node.Has("poolId"))
            {
                result.Add("schema.action.poolId", path + ".poolId is required for GrantRewardFromPool.");
            }
            else if (Same(atom, "GrantRelic") && !node.Has("relicDefId"))
            {
                result.Add("schema.action.relicDefId", path + ".relicDefId is required for GrantRelic.");
            }
            else if (Same(atom, "ShuffleInto") || Same(atom, "Spawn"))
            {
                if (!node.Has("defId"))
                {
                    result.Add("schema.action.defId", path + ".defId is required for " + atom + ".");
                }
            }
            else if ((Same(atom, "ShuffleRandomContent") || Same(atom, "ExchangeWithDrawPile"))
                && node.Has("kind")
                && !IsSupportedCardKind(node.Get("kind").AsString(string.Empty)))
            {
                result.Add("schema.action.kind", path + ".kind is not supported.");
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

                if (node.Has("conditionTargetKind") && !IsSupportedCardKind(node.Get("conditionTargetKind").AsString(string.Empty)))
                {
                    result.Add("schema.action.conditionTargetKind", path + ".conditionTargetKind is not supported.");
                }
            }
            else if (Same(atom, "AddModifier"))
            {
                if (!node.Has("stat"))
                {
                    result.Add("schema.action.stat", path + ".stat is required for AddModifier.");
                }

                if (!node.Has("value"))
                {
                    result.Add("schema.action.value", path + ".value is required for AddModifier.");
                }

                if (node.Has("value") && node.Get("value").IsObject)
                {
                    EffectValueExpression.Validate(node.Get("value"), path + ".value", result);
                }
            }
            else if (Same(atom, "SetBoardMark"))
            {
                if (!node.Has("mark"))
                {
                    result.Add("schema.action.mark", path + ".mark is required for SetBoardMark.");
                }

                if (!node.Get("random").AsBool(false) && !node.Has("slot"))
                {
                    result.Add("schema.action.slot", path + ".slot is required for fixed SetBoardMark.");
                }
            }
            else if (Same(atom, "ReplayHelpCardEffects") && node.Has("targetKind") && !IsSupportedCardKind(node.Get("targetKind").AsString(string.Empty)))
            {
                result.Add("schema.action.targetKind", path + ".targetKind is not supported.");
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
                if (Same(atom, "OnBattle") && node.Has("targetKind") && !IsSupportedCardKind(node.Get("targetKind").AsString(string.Empty)))
                {
                    result.Add("schema.trigger.targetKind", path + ".targetKind is not supported.");
                }

                if (Same(atom, "OnEvent"))
                {
                    if (node.Has("eventType") && !IsSupportedEventType(node.Get("eventType").AsString(string.Empty)))
                    {
                        result.Add("schema.trigger.eventType", path + ".eventType is not supported.");
                    }

                    var eventTypes = node.Get("eventTypes").AsArray();
                    for (var i = 0; i < eventTypes.Count; i++)
                    {
                        if (!IsSupportedEventType(eventTypes[i].AsString(string.Empty)))
                        {
                            result.Add("schema.trigger.eventType", path + ".eventTypes[" + i + "] is not supported.");
                        }
                    }
                }

                if (Same(atom, "OnMoveToBoardMark"))
                {
                    if (node.Has("mark") && !IsSupportedBoardMark(node.Get("mark").AsString(string.Empty)))
                    {
                        result.Add("schema.trigger.mark", path + ".mark is not supported.");
                    }

                    if (node.Has("targetKind") && !IsSupportedCardKind(node.Get("targetKind").AsString(string.Empty)))
                    {
                        result.Add("schema.trigger.targetKind", path + ".targetKind is not supported.");
                    }
                }

                if ((Same(atom, "OnSelfMove") || Same(atom, "OnInteract"))
                    && node.Has("every")
                    && node.Get("every").AsInt(1) < 1)
                {
                    result.Add("schema.range.every", path + ".every must be >= 1.");
                }

                if (Same(atom, "OnCardRhythmFire") && node.Has("every"))
                {
                    result.Add(
                        "schema.trigger.every",
                        path + ".OnCardRhythmFire must not declare every (ADR-0038).");
                }

                if (Same(atom, "OnSelfMove") && node.Has("requireAdjacentTo") && string.IsNullOrEmpty(node.Get("requireAdjacentTo").AsString(string.Empty)))
                {
                    result.Add("schema.trigger.requireAdjacentTo", path + ".requireAdjacentTo must not be empty.");
                }

                if (Same(atom, "OnBattle") && node.Has("maxActionDepth") && node.Get("maxActionDepth").AsInt(-1) < 0)
                {
                    result.Add("schema.range.maxActionDepth", path + ".maxActionDepth must be >= 0.");
                }

                if (Same(atom, "OnMoveToSlot") && node.Has("slot") && !IsBoardSlot(node.Get("slot").AsInt(0)))
                {
                    result.Add("schema.range.slot", path + ".slot must be between 1 and 9.");
                }

                if (Same(atom, "OnMoveToSlot"))
                {
                    var slots = node.Get("slots").AsArray();
                    for (var i = 0; i < slots.Count; i++)
                    {
                        if (!IsBoardSlot(slots[i].AsInt(0)))
                        {
                            result.Add("schema.range.slots", path + ".slots[" + i + "] must be between 1 and 9.");
                        }
                    }
                }

                if (Same(atom, "OnCumulative") && node.Has("threshold") && node.Get("threshold").AsInt(1) < 1)
                {
                    result.Add("schema.range.threshold", path + ".threshold must be >= 1.");
                }

                if (Same(atom, "OnCumulative") && node.Has("metric") && !IsSupportedCumulativeMetric(node.Get("metric").AsString(string.Empty)))
                {
                    result.Add("schema.trigger.metric", path + ".metric is not supported.");
                }

                if (Same(atom, "OnCumulative") && node.Has("eventType") && !IsSupportedEventType(node.Get("eventType").AsString(string.Empty)))
                {
                    result.Add("schema.trigger.eventType", path + ".eventType is not supported.");
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

                if (Same(atom, "StatAtLeast"))
                {
                    if (node.Has("value") && node.Get("value").AsFloat(0f) < 0f)
                    {
                        result.Add("schema.range.value", path + ".value must be >= 0.");
                    }

                    if (node.Has("stat") && !IsSupportedStat(node.Get("stat").AsString(string.Empty)))
                    {
                        result.Add("schema.condition.stat", path + ".stat is not supported.");
                    }
                }

                if (Same(atom, "CardCounter") && node.Has("min") && node.Get("min").AsInt(1) < 1)
                {
                    result.Add("schema.range.min", path + ".min must be >= 1.");
                }

                if (Same(atom, "TargetCount") && node.Has("min") && node.Get("min").AsInt(1) < 0)
                {
                    result.Add("schema.range.min", path + ".min must be >= 0.");
                }

                if (Same(atom, "BoardMarkCount"))
                {
                    if (node.Has("min") && node.Get("min").AsInt(1) < 0)
                    {
                        result.Add("schema.range.min", path + ".min must be >= 0.");
                    }

                    if (node.Has("mark") && !IsSupportedBoardMark(node.Get("mark").AsString(string.Empty)))
                    {
                        result.Add("schema.condition.mark", path + ".mark is not supported.");
                    }
                }

                if (Same(atom, "EventFilter"))
                {
                    if (node.Has("eventType") && !IsSupportedEventType(node.Get("eventType").AsString(string.Empty)))
                    {
                        result.Add("schema.condition.eventType", path + ".eventType is not supported.");
                    }

                    var eventTypes = node.Get("eventTypes").AsArray();
                    for (var i = 0; i < eventTypes.Count; i++)
                    {
                        if (!IsSupportedEventType(eventTypes[i].AsString(string.Empty)))
                        {
                            result.Add("schema.condition.eventType", path + ".eventTypes[" + i + "] is not supported.");
                        }
                    }

                    if (node.Has("targetKind") && !IsSupportedCardKind(node.Get("targetKind").AsString(string.Empty)))
                    {
                        result.Add("schema.condition.targetKind", path + ".targetKind is not supported.");
                    }

                    if (node.Has("stat") && !IsSupportedStat(node.Get("stat").AsString(string.Empty)))
                    {
                        result.Add("schema.condition.stat", path + ".stat is not supported.");
                    }
                }

                if (Same(atom, "ActionSource") && node.Has("action"))
                {
                    var action = node.Get("action").AsString(string.Empty);
                    if (string.IsNullOrEmpty(action))
                    {
                        result.Add("schema.condition.action", path + ".action must not be empty.");
                    }
                }

                if (Same(atom, "CardZone") && node.Has("zone") && !IsSupportedZone(node.Get("zone").AsString(string.Empty)))
                {
                    result.Add("schema.condition.zone", path + ".zone is not supported.");
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

                if (Same(atom, "FilteredCards"))
                {
                    ValidateLevelRange(node, path, result);

                    if (node.Has("kind") && !IsSupportedCardKind(node.Get("kind").AsString(string.Empty)))
                    {
                        result.Add("schema.target.kind", path + ".kind is not supported.");
                    }

                    if (node.Has("zone") && !IsSupportedZone(node.Get("zone").AsString(string.Empty)))
                    {
                        result.Add("schema.target.zone", path + ".zone is not supported.");
                    }

                    var zones = node.Get("zones").AsArray();
                    for (var i = 0; i < zones.Count; i++)
                    {
                        if (!IsSupportedZone(zones[i].AsString(string.Empty)))
                        {
                            result.Add("schema.target.zone", path + ".zones[" + i + "] is not supported.");
                        }
                    }

                    var slots = node.Get("slots").AsArray();
                    for (var i = 0; i < slots.Count; i++)
                    {
                        if (!IsBoardSlot(slots[i].AsInt(0)))
                        {
                            result.Add("schema.range.slots", path + ".slots[" + i + "] must be between 1 and 9.");
                        }
                    }
                }

                if (Same(atom, "SelectedCards"))
                {
                    ValidateLevelRange(node, path, result);

                    if (node.Has("count") && node.Get("count").AsInt(0) < 0)
                    {
                        result.Add("schema.range.count", path + ".count must be >= 0.");
                    }

                    if (node.Has("kind") && !IsSupportedCardKind(node.Get("kind").AsString(string.Empty)))
                    {
                        result.Add("schema.target.kind", path + ".kind is not supported.");
                    }

                    if (node.Has("zone") && !IsSupportedZone(node.Get("zone").AsString(string.Empty)))
                    {
                        result.Add("schema.target.zone", path + ".zone is not supported.");
                    }
                }

                if (Same(atom, "BoardMarkEventCard"))
                {
                    if (node.Has("mark") && !IsSupportedBoardMark(node.Get("mark").AsString(string.Empty)))
                    {
                        result.Add("schema.target.mark", path + ".mark is not supported.");
                    }

                    if (node.Has("targetKind") && !IsSupportedCardKind(node.Get("targetKind").AsString(string.Empty)))
                    {
                        result.Add("schema.target.targetKind", path + ".targetKind is not supported.");
                    }
                }

                if (Same(atom, "SlotCard") && node.Has("kind") && !IsSupportedCardKind(node.Get("kind").AsString(string.Empty)))
                {
                    result.Add("schema.target.kind", path + ".kind is not supported.");
                }

                if (Same(atom, "SlotCard"))
                {
                    ValidateLevelRange(node, path, result);
                }

                return;
            }

            if (kind != EffectAtomKind.Action)
            {
                return;
            }

            if ((Same(atom, "DealDamage") || Same(atom, "Heal") || Same(atom, "GainArmor") || Same(atom, "SyncAdjacentBorrowedArmor"))
                && node.Has("amount")
                && node.Get("amount").AsInt(0) < 0)
            {
                result.Add("schema.range.amount", path + ".amount must be >= 0.");
            }

            if (Same(atom, "AddModifier"))
            {
                if (node.Has("stat") && !IsSupportedStat(node.Get("stat").AsString(string.Empty)))
                {
                    result.Add("schema.action.stat", path + ".stat is not supported.");
                }

                if (node.Has("value") && node.Get("value").AsFloat(0f) < 0f && !node.Get("value").IsObject)
                {
                    result.Add("schema.range.value", path + ".value must be >= 0.");
                }

                if (node.Has("activeWhileAdjacentTo") && string.IsNullOrEmpty(node.Get("activeWhileAdjacentTo").AsString(string.Empty)))
                {
                    result.Add("schema.action.activeWhileAdjacentTo", path + ".activeWhileAdjacentTo must not be empty.");
                }
            }

            if (Same(atom, "TransferArmor") && node.Has("amount") && node.Get("amount").AsInt(0) < 0)
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

            if ((Same(atom, "ShuffleInto") || Same(atom, "Spawn") || Same(atom, "ShuffleRandomContent"))
                && node.Has("count")
                && node.Get("count").AsInt(0) < 0)
            {
                result.Add("schema.range.count", path + ".count must be >= 0.");
            }

            if (Same(atom, "ShuffleRandomContent") || Same(atom, "ExchangeWithDrawPile"))
            {
                ValidateLevelRange(node, path, result);
            }

            if (Same(atom, "SetBoardMark"))
            {
                if (node.Has("mark") && !IsSupportedBoardMark(node.Get("mark").AsString(string.Empty)))
                {
                    result.Add("schema.action.mark", path + ".mark is not supported.");
                }

                if (node.Has("slot") && !IsBoardSlot(node.Get("slot").AsInt(0)))
                {
                    result.Add("schema.range.slot", path + ".slot must be between 1 and 9.");
                }

                if (node.Has("count") && node.Get("count").AsInt(0) < 0)
                {
                    result.Add("schema.range.count", path + ".count must be >= 0.");
                }

                var excludeSlots = node.Get("excludeSlots").AsArray();
                for (var i = 0; i < excludeSlots.Count; i++)
                {
                    if (!IsBoardSlot(excludeSlots[i].AsInt(0)))
                    {
                        result.Add("schema.range.excludeSlots", path + ".excludeSlots[" + i + "] must be between 1 and 9.");
                    }
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

        private static bool IsSupportedCardKind(string kind)
        {
            if (string.IsNullOrEmpty(kind))
            {
                return false;
            }

            CardKind ignored;
            return Enum.TryParse(kind, true, out ignored);
        }

        private static bool IsSupportedEventType(string eventType)
        {
            if (string.IsNullOrEmpty(eventType))
            {
                return false;
            }

            CoreEventType ignored;
            return Enum.TryParse(eventType, true, out ignored);
        }

        private static bool IsSupportedCumulativeMetric(string metric)
        {
            return Same(metric, "armorLost")
                || Same(metric, "hpLost")
                || Same(metric, "damageDealt")
                || Same(metric, "damageTaken")
                || Same(metric, "totalDamageTaken")
                || Same(metric, "monsterRemoved")
                || Same(metric, "helpCardUsed");
        }

        private static bool IsSupportedZone(string zone)
        {
            if (string.IsNullOrEmpty(zone))
            {
                return false;
            }

            ZoneId ignored;
            return Enum.TryParse(zone, true, out ignored);
        }

        private static void ValidateLevelRange(EffectDslNode node, string path, EffectValidationResult result)
        {
            var min = node.Get("minLevel").AsInt(0);
            var max = node.Get("maxLevel").AsInt(0);
            if (node.Has("minLevel") && min < 0)
            {
                result.Add("schema.range.minLevel", path + ".minLevel must be >= 0.");
            }

            if (node.Has("maxLevel") && max < 0)
            {
                result.Add("schema.range.maxLevel", path + ".maxLevel must be >= 0.");
            }

            if (min > 0 && max > 0 && min > max)
            {
                result.Add("schema.range.level", path + ".minLevel must be <= maxLevel.");
            }
        }

        private static bool IsSupportedBoardMark(string mark)
        {
            if (string.IsNullOrEmpty(mark))
            {
                return false;
            }

            BoardMarkId parsed;
            return Enum.TryParse(mark, true, out parsed) && parsed != BoardMarkId.None;
        }

        private static bool IsSupportedStat(string stat)
        {
            if (string.IsNullOrEmpty(stat))
            {
                return false;
            }

            StatId ignored;
            return Enum.TryParse(stat, true, out ignored);
        }

        private static bool IsEventFilterFamily(string atom)
        {
            return !string.IsNullOrEmpty(atom)
                && atom.StartsWith("EventFilter", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
