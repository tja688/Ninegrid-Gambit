using System;

namespace NineGrid.Core.Effects
{
    /// <summary>
    /// 卡牌挂载效果（MonsterSkill/HelpCard）的统一本卡 scope 薄层门禁。
    /// 在区域门禁之后、Trigger.Matches 之前执行，对已修补 Atom 形成二验，对未声明 scope 的触发器兜底。
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
            // S1/S2：ownerOnly:false（吸骨、暴力营养等听他卡移除）
            if (trigger.Has("ownerOnly") && !trigger.Get("ownerOnly").AsBool(true))
            {
                return true;
            }

            if (trigger.Get("excludeSelf").AsBool(false))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(trigger.Get("targetNot").AsString(string.Empty)))
            {
                return true;
            }

            // S3：OnEvent + 观察型 EventFilter（石头爱好者/学习成长/灼热观察等）
            var atom = ReadAtom(trigger);
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
                if (condition == null || condition.IsNull || !Same(ReadAtom(condition), "EventFilter"))
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
            if (HasNonEmpty(filter, "targetNot"))
            {
                return true;
            }

            if (HasNonEmpty(filter, "targetIs"))
            {
                return false;
            }

            if (HasNonEmpty(filter, "targetKind")
                || HasNonEmpty(filter, "sourcePrefix")
                || HasNonEmpty(filter, "sourceDefId"))
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
            if (Same(atom, "OnArmorBreak") || Same(atom, "OnSelfMove") || Same(atom, "OnEnter"))
            {
                return true;
            }

            if (Same(atom, "OnRemove") && trigger.Get("ownerOnly").AsBool(true))
            {
                return true;
            }

            if (Same(atom, "OnUseHelpCard") && trigger.Get("ownerOnly").AsBool(true))
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
                return HasNonEmpty(trigger, "targetIs")
                    || HasNonEmpty(trigger, "targetNot")
                    || HasNonEmpty(trigger, "actorIs")
                    || HasNonEmpty(trigger, "sourceDefId")
                    || HasNonEmpty(trigger, "sourcePrefix");
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
                if (Same(atom, "Adjacent") || Same(atom, "AtSlot"))
                {
                    return true;
                }

                if (!Same(atom, "EventFilter"))
                {
                    continue;
                }

                if (HasNonEmpty(condition, "targetIs")
                    || HasNonEmpty(condition, "targetNot")
                    || HasNonEmpty(condition, "actorIs")
                    || HasNonEmpty(condition, "targetKind")
                    || HasNonEmpty(condition, "sourceDefId")
                    || HasNonEmpty(condition, "sourcePrefix"))
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

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
