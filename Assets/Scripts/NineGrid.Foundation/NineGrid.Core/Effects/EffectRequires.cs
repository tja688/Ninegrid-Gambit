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
        /// <summary>自管翻面：背面时仍可触发/生效，不受背面惰性门闩。</summary>
        public const string ActiveWhileFaceDown = "ActiveWhileFaceDown";

        public static bool HasActiveWhileFaceDown(EffectDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            for (var i = 0; i < definition.Requires.Count; i++)
            {
                if (string.Equals(
                        definition.Requires[i],
                        ActiveWhileFaceDown,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
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
                || Same(token, EffectRequireTokens.CardZoneItemSlots)
                || Same(token, EffectRequireTokens.ActiveWhileFaceDown);
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
                || definition.ContainerType == EffectContainerType.MonsterSkill
                || definition.ContainerType == EffectContainerType.Trap;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 运行时 requires：区域/实体适用声明（ADR-0010）。静态 mount 错配应已在 Validate 拦截。
    /// #73 起外部门禁已拆除；本卡移除帧与道具格被动限制由本类承接。
    /// </summary>
    public static class EffectRequiresRuntime
    {
        public static bool Passes(EffectInstance instance, EffectRuntimeContext runtime)
        {
            string unused;
            return TryExplainFailure(instance, runtime, out unused);
        }

        /// <summary>
        /// 全部 requires 成立时返回 true（failedToken 为空）；否则返回 false 并写出失败 token。
        /// </summary>
        /// <summary>
        /// 一次性 OnActivate 激活路径的 requires 求值：跳过区域 token
        /// （CardZoneTriggerable / CardZoneBoard / CardZoneItemSlots）。
        /// 卡在造卡即激活（SetupNodeDeck 阶段还停在 EnemyCardPool/PlayerCardPool），
        /// 区域门闩本为响应式触发「触发时」设计，在一次性挂载时刻求值会把
        /// 挂载即生效的技能（神圣决斗 HolyDuel / 远程武器 CounterAttackBanned 等）静默丢弃；
        /// 区域约束由效果自身语义（如 Persistent 规则修饰器在场上才被查询）承担。
        /// </summary>
        public static bool PassesIgnoringZoneRequires(
            EffectInstance instance,
            EffectRuntimeContext runtime)
        {
            if (instance?.Definition == null || runtime == null)
            {
                return true;
            }

            var requires = instance.Definition.Requires;
            for (var i = 0; i < requires.Count; i++)
            {
                var token = requires[i];
                if (IsZoneRequireToken(token))
                {
                    continue;
                }

                if (!PassesToken(token, instance, runtime))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsZoneRequireToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            return Same(token, EffectRequireTokens.CardZoneTriggerable)
                || Same(token, EffectRequireTokens.CardZoneBoard)
                || Same(token, EffectRequireTokens.CardZoneItemSlots);
        }

        public static bool TryExplainFailure(
            EffectInstance instance,
            EffectRuntimeContext runtime,
            out string failedToken)
        {
            failedToken = string.Empty;
            if (instance?.Definition == null || runtime == null)
            {
                return true;
            }

            var requires = instance.Definition.Requires;
            for (var i = 0; i < requires.Count; i++)
            {
                var token = requires[i];
                if (!PassesToken(token, instance, runtime))
                {
                    failedToken = string.IsNullOrEmpty(token) ? "(empty)" : token;
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

            // 能力声明：不作为运行时 requires 门闩，仅供背面惰性豁免读取。
            if (Same(token, EffectRequireTokens.ActiveWhileFaceDown))
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

            // CardZoneTriggerable：Board 可；ItemSlots 仅帮助卡「使用时」或显式 ItemSlots 条件；
            // 卡组/坟场/移除区否（本卡移除帧特例放行）。
            if (zone == ZoneId.Board)
            {
                return true;
            }

            if (zone == ZoneId.ItemSlots && owner.ContainerType == EffectContainerType.HelpCard)
            {
                return IsHelpCardItemSlotTriggerable(instance);
            }

            if (zone == ZoneId.Graveyard || zone == ZoneId.Removed || zone == ZoneId.DrawPile)
            {
                return IsOwnerSelfRemoveBatch(instance, runtime);
            }

            return false;
        }

        /// <summary>
        /// 道具格帮助卡：仅「使用时」族触发，或声明 CardZone:ItemSlots 条件的被动。
        /// </summary>
        private static bool IsHelpCardItemSlotTriggerable(EffectInstance instance)
        {
            var trigger = instance.Definition?.Trigger;
            if (trigger != null && !trigger.IsNull)
            {
                var atom = trigger.Get("atom").AsString(string.Empty);
                if (Same(atom, "OnUseHelpCard")
                    || Same(atom, "OnSelfUsed")
                    || Same(atom, "OnOtherHelpCardUsed")
                    || Same(atom, "OnAnyHelpCardUsed"))
                {
                    return true;
                }
            }

            return HasCardZoneCondition(instance, ZoneId.ItemSlots);
        }

        private static bool HasCardZoneCondition(EffectInstance instance, ZoneId zone)
        {
            var conditions = instance.Definition?.Conditions;
            if (conditions == null)
            {
                return false;
            }

            for (var i = 0; i < conditions.Count; i++)
            {
                var node = conditions[i];
                if (node == null || node.IsNull)
                {
                    continue;
                }

                if (!Same(node.Get("atom").AsString(string.Empty), "CardZone"))
                {
                    continue;
                }

                if (node.Get("zone").AsEnum(ZoneId.None) == zone)
                {
                    return true;
                }
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
