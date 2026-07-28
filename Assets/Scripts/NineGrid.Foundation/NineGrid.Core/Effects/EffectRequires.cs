using System;

namespace NineGrid.Core.Effects
{
    /// <summary>
    /// ADR-0010 / #72：适用声明 token 词汇与校验 / 运行时求值。
    /// requires 失败属装配错误（校验）或运行时不适用（动态 zone）；conditions 失败属正常玩法。
    /// </summary>
    public static class EffectRequireTokens
    {
        public const string HasOwnerEntity = "HasOwnerEntity";
        public const string NoOwnerEntity = "NoOwnerEntity";
        public const string ActivatedByUse = "ActivatedByUse";
        public const string CardZoneTriggerable = "CardZoneTriggerable";
        public const string CardZoneBoard = "CardZone:Board";
        public const string CardZoneItemSlots = "CardZone:ItemSlots";
    }

    public static class EffectRequiresValidator
    {
        public static void Validate(EffectDefinition definition, EffectValidationResult result)
        {
            if (definition == null || result == null)
            {
                return;
            }

            for (var i = 0; i < definition.Requires.Count; i++)
            {
                var token = definition.Requires[i];
                if (!IsKnownToken(token))
                {
                    result.Add("requires.unknown", "Unknown requires token '" + token + "'.");
                    continue;
                }

                if (Same(token, EffectRequireTokens.HasOwnerEntity)
                    && definition.ContainerType == EffectContainerType.Relic)
                {
                    result.Add(
                        "requires.mount-mismatch",
                        "Requires HasOwnerEntity cannot mount on Relic (no owner entity).");
                }

                if (Same(token, EffectRequireTokens.NoOwnerEntity)
                    && (definition.ContainerType == EffectContainerType.HelpCard
                        || definition.ContainerType == EffectContainerType.MonsterSkill))
                {
                    result.Add(
                        "requires.mount-mismatch",
                        "Requires NoOwnerEntity cannot mount on " + definition.ContainerType + ".");
                }
            }

            if (NeedsExplicitRequires(definition) && definition.Requires.Count == 0)
            {
                result.Add(
                    "requires.missing",
                    "Card-owned Triggered effects must declare non-empty requires (ADR-0010).");
            }
        }

        public static bool IsKnownToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            return Same(token, EffectRequireTokens.HasOwnerEntity)
                || Same(token, EffectRequireTokens.NoOwnerEntity)
                || Same(token, EffectRequireTokens.ActivatedByUse)
                || Same(token, EffectRequireTokens.CardZoneTriggerable)
                || Same(token, EffectRequireTokens.CardZoneBoard)
                || Same(token, EffectRequireTokens.CardZoneItemSlots);
        }

        public static bool HasExplicitSceneDeclaration(EffectDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            if (definition.Requires.Count > 0)
            {
                return true;
            }

            for (var i = 0; i < definition.Conditions.Count; i++)
            {
                var condition = definition.Conditions[i];
                if (condition == null || condition.IsNull)
                {
                    continue;
                }

                var atom = condition.Get("atom").AsString(condition.Get("type").AsString(string.Empty));
                if (Same(atom, "CardZone")
                    || Same(atom, "EventFilter")
                    || Same(atom, "AtSlot")
                    || Same(atom, "Adjacent")
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

        private static bool NeedsExplicitRequires(EffectDefinition definition)
        {
            if (definition.Kind != EffectKind.Triggered)
            {
                return false;
            }

            return definition.ContainerType == EffectContainerType.HelpCard
                || definition.ContainerType == EffectContainerType.MonsterSkill;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 运行时 requires：与区域门禁并行、只紧不松。静态 mount 错配应已在 Validate 拦截。
    /// </summary>
    public static class EffectRequiresRuntime
    {
        public static bool Passes(EffectInstance instance, EffectRuntimeContext runtime)
        {
            if (instance?.Definition == null || runtime == null)
            {
                return true;
            }

            var requires = instance.Definition.Requires;
            for (var i = 0; i < requires.Count; i++)
            {
                if (!PassesToken(requires[i], instance, runtime))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PassesToken(string token, EffectInstance instance, EffectRuntimeContext runtime)
        {
            if (string.IsNullOrEmpty(token))
            {
                return true;
            }

            var owner = instance.Owner;
            var ownerUid = owner == null ? 0 : owner.OwnerUid;

            if (Same(token, EffectRequireTokens.HasOwnerEntity))
            {
                return ownerUid != 0;
            }

            if (Same(token, EffectRequireTokens.NoOwnerEntity))
            {
                return ownerUid == 0;
            }

            if (Same(token, EffectRequireTokens.ActivatedByUse))
            {
                return true;
            }

            if (Same(token, EffectRequireTokens.CardZoneTriggerable)
                || Same(token, EffectRequireTokens.CardZoneBoard)
                || Same(token, EffectRequireTokens.CardZoneItemSlots))
            {
                return PassesZoneRequire(token, instance, runtime);
            }

            return true;
        }

        private static bool PassesZoneRequire(string token, EffectInstance instance, EffectRuntimeContext runtime)
        {
            var owner = instance.Owner;
            if (owner == null || owner.OwnerUid == 0)
            {
                return true;
            }

            CardInstance ownerCard;
            if (!runtime.TryGetCard(owner.OwnerUid, out ownerCard))
            {
                return false;
            }

            var zone = ownerCard.Zone.Value;
            if (Same(token, EffectRequireTokens.CardZoneBoard))
            {
                return zone == ZoneId.Board;
            }

            if (Same(token, EffectRequireTokens.CardZoneItemSlots))
            {
                return zone == ZoneId.ItemSlots;
            }

            // CardZoneTriggerable：与区域门禁同向——Board 可；ItemSlots 仅 HelpCard；其余否（本卡移除帧由门禁特例放行）。
            if (zone == ZoneId.Board)
            {
                return true;
            }

            if (zone == ZoneId.ItemSlots && owner.ContainerType == EffectContainerType.HelpCard)
            {
                return true;
            }

            if (zone == ZoneId.Graveyard || zone == ZoneId.Removed || zone == ZoneId.DrawPile)
            {
                return IsOwnerSelfRemoveBatch(instance, runtime);
            }

            return false;
        }

        private static bool IsOwnerSelfRemoveBatch(EffectInstance instance, EffectRuntimeContext runtime)
        {
            if (instance?.Trigger == null || instance.Trigger.Point != TriggerPoint.OnRemove)
            {
                return false;
            }

            var ownerUid = instance.Owner == null ? 0 : instance.Owner.OwnerUid;
            if (ownerUid == 0)
            {
                return false;
            }

            var events = runtime.Events;
            for (var i = 0; i < events.Count; i++)
            {
                if (events[i].Type == CoreEventType.CardRemoved && events[i].CardUid == ownerUid)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// ADR-0010：禁止的上下文开关形参；原子名须单一语义。
    /// </summary>
    public static class EffectContextSwitchParams
    {
        public static readonly string[] BannedKeys =
        {
            "ownerOnly",
            "excludeSelf",
            "actorIs",
            "targetIs",
            "targetNot",
            "sourcePrefix",
            "excludeCause"
        };

        public static void RejectBannedParams(EffectDslNode node, string path, EffectValidationResult result)
        {
            if (node == null || node.IsNull || result == null)
            {
                return;
            }

            for (var i = 0; i < BannedKeys.Length; i++)
            {
                var key = BannedKeys[i];
                if (node.Has(key))
                {
                    result.Add(
                        "schema.deprecated-param",
                        path + " must not declare context-switch param '" + key
                        + "' (ADR-0010); use a single-morphology atom.");
                }
            }
        }
    }
}
