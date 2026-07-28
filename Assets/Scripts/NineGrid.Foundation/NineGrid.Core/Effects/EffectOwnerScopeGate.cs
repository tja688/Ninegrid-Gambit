using System;

namespace NineGrid.Core.Effects
{
    /// <summary>
    /// 卡牌挂载效果（MonsterSkill/HelpCard）的统一本卡 scope 薄层门禁。
    /// 在区域门禁之后、Trigger.Matches 之前执行，对已修补 Atom 形成二验，对未声明 scope 的触发器兜底。
    /// #72：识别单形态原子名；上下文开关形参已退役。
    /// </summary>
    internal static class EffectOwnerScopeGate
    {
        public static bool Passes(EffectInstance instance, EffectRuntimeContext runtime)
        {
            if (instance?.Owner == null || instance.Owner.OwnerUid == 0)
            {
                return true;
            }

            var container = instance.Owner.ContainerType;
            if (container != EffectContainerType.MonsterSkill && container != EffectContainerType.HelpCard)
            {
                return true;
            }

            var trigger = instance.Definition?.Trigger;
            if (trigger == null || trigger.IsNull)
            {
                return true;
            }

            if (HasExplicitGlobalScope(trigger, instance.Definition))
            {
                return true;
            }

            if (TriggerHasBuiltInOwnerScope(trigger))
            {
                return true;
            }

            if (TriggerDeclaresEventScope(trigger, container))
            {
                return true;
            }

            if (ConditionsDeclareEventScope(instance.Definition))
            {
                return true;
            }

            return BatchConcernsOwner(instance.Owner.OwnerUid, runtime);
        }

        private static bool HasExplicitGlobalScope(EffectDslNode trigger, EffectDefinition definition)
        {
            var atom = ReadAtom(trigger);
            if (Same(atom, "OnAnyCardRemoved")
                || Same(atom, "OnOtherHelpCardUsed")
                || Same(atom, "OnAnyHelpCardUsed"))
            {
                return true;
            }

            // S3：OnEvent + 观察型 EventFilter（石头爱好者/学习成长/灼热观察等）
            if (Same(atom, "OnEvent") && HasObservationOrGlobalEventFilter(definition))
            {
                return true;
            }

            return false;
        }

        private static bool HasObservationOrGlobalEventFilter(EffectDefinition definition)
        {
            var conditions = definition.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null || condition.IsNull)
                {
                    continue;
                }

                var atom = ReadAtom(condition);
                if (Same(atom, "TargetNotSelf")
                    || Same(atom, "SourcePrefix")
                    || Same(atom, "EventFilterTargetNotSelf")
                    || Same(atom, "EventFilterActorIsPlayerTargetNotSelf")
                    || Same(atom, "EventFilterSourcePrefix"))
                {
                    return true;
                }

                if (!IsEventFilterFamily(atom))
                {
                    continue;
                }

                if (IsObservationOrGlobalEventFilter(condition))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsObservationOrGlobalEventFilter(EffectDslNode filter)
        {
            if (HasNonEmpty(filter, "targetKind")
                || HasNonEmpty(filter, "sourceDefId")
                || HasNonEmpty(filter, "excludeSourceDefId"))
            {
                return true;
            }

            if (filter.Has("maxDelta") || filter.Has("minDelta") || HasNonEmpty(filter, "stat"))
            {
                return true;
            }

            return false;
        }

        private static bool TriggerHasBuiltInOwnerScope(EffectDslNode trigger)
        {
            var atom = ReadAtom(trigger);
            if (Same(atom, "OnArmorBreak")
                || Same(atom, "OnSelfMove")
                || Same(atom, "OnEnter")
                || Same(atom, "OnSelfRemoved")
                || Same(atom, "OnSelfUsed")
                || Same(atom, "OnSelfArmorLostCumulative")
                || Same(atom, "OnSelfDamageDealtToPlayerCumulative"))
            {
                return true;
            }

            // 兼容期：无开关形参的旧名等价于本卡语义。
            if (Same(atom, "OnRemove") || Same(atom, "OnUseHelpCard"))
            {
                return true;
            }

            return false;
        }

        private static bool TriggerDeclaresEventScope(EffectDslNode trigger, EffectContainerType container)
        {
            var atom = ReadAtom(trigger);
            if (Same(atom, "OnCumulative"))
            {
                // 无上下文开关后，裸 OnCumulative 走 BatchConcernsOwner 兜底。
                return false;
            }

            if (Same(atom, "OnMoveToSlot"))
            {
                var target = trigger.Get("target").AsString("Any");
                if (TargetResolver.IsSelfRef(target))
                {
                    return true;
                }

                // U3：MonsterSkill 默认 target:Any 视同 Self，走 BatchConcernsOwner 兜底而非放行。
                return false;
            }

            return false;
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

                var atom = ReadAtom(condition);
                if (Same(atom, "Adjacent")
                    || Same(atom, "AtSlot")
                    || Same(atom, "ActorIsPlayer")
                    || Same(atom, "ActorIsSelf")
                    || Same(atom, "TargetIsSelf")
                    || Same(atom, "TargetNotSelf")
                    || Same(atom, "SourcePrefix")
                    || Same(atom, "ExcludeCause")
                    || Same(atom, "EventFilterActorIsPlayer")
                    || Same(atom, "EventFilterTargetIsSelf")
                    || Same(atom, "EventFilterTargetNotSelf")
                    || Same(atom, "EventFilterActorIsPlayerTargetIsSelf")
                    || Same(atom, "EventFilterActorIsPlayerTargetNotSelf")
                    || Same(atom, "EventFilterSourcePrefix")
                    || Same(atom, "EventFilterExcludeCause"))
                {
                    return true;
                }

                if (!IsEventFilterFamily(atom))
                {
                    continue;
                }

                if (HasNonEmpty(condition, "targetKind")
                    || HasNonEmpty(condition, "sourceDefId")
                    || HasNonEmpty(condition, "excludeSourceDefId"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool BatchConcernsOwner(int ownerUid, EffectRuntimeContext runtime)
        {
            var events = runtime.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (EventConcernsOwner(events[i], ownerUid))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool EventConcernsOwner(CoreGameEvent gameEvent, int ownerUid)
        {
            return gameEvent.CardUid == ownerUid
                || gameEvent.TargetUid == ownerUid
                || gameEvent.ActorUid == ownerUid;
        }

        private static bool HasNonEmpty(EffectDslNode node, string key)
        {
            return !string.IsNullOrEmpty(node.Get(key).AsString(string.Empty));
        }

        private static string ReadAtom(EffectDslNode node)
        {
            return node.Get("atom").AsString(node.Get("type").AsString(string.Empty));
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
