using System;
using System.Collections.Generic;
using NineGrid.Core.Stats;
using NineGrid.Core.Systems;
using QFramework;

namespace NineGrid.Core.Effects
{
    public interface IEffectSystem : ISystem
    {
        EffectAtomRegistry AtomRegistry { get; }
        EffectValidator Validator { get; }
        IReadOnlyList<EffectInstance> Instances { get; }
        EffectDefinition ParseJson(string json);
        EffectValidationResult Validate(EffectDefinition definition);
        EffectInstance Activate(EffectDefinition definition, EffectOwner owner);
        bool Deactivate(string instanceId);
        IReadOnlyList<string> GetInstanceIdsByOwner(int ownerUid);
        int DeactivateByOwner(int ownerUid);
        bool TryGetInstance(string instanceId, out EffectInstance instance);
        IReadOnlyList<GameAction> BuildTriggeredActions(string instanceId, TriggerContext triggerContext);
        /// <summary>
        /// ADR-0010 / #73：未触发探查——按效果实例 + 触发上下文返回失败类别（requires vs conditions 等）。
        /// 不改写正向 EffectTriggered / ChainId 管线。
        /// </summary>
        EffectNonTriggerProbeResult ProbeWhyNotTriggered(string instanceId, TriggerContext triggerContext);
        /// <summary>
        /// 按 owner 牌面朝向 suppress/restore 非 <c>ActiveWhileFaceDown</c> 的被动修饰。
        /// </summary>
        void SyncOwnerFaceSuppression(int ownerUid);
        void Clear();
    }

    public sealed class EffectInstance
    {
        private readonly List<IUnRegister> mUnregisters = new List<IUnRegister>();
        private readonly List<AppliedStatModifier> mStatModifiers = new List<AppliedStatModifier>();
        private readonly List<RuleModifier> mRuleModifiers = new List<RuleModifier>();
        private readonly List<ICondition> mConditions = new List<ICondition>();

        public EffectInstance(string instanceId, EffectDefinition definition, EffectOwner owner)
        {
            InstanceId = instanceId ?? string.Empty;
            Definition = definition;
            Owner = owner;
        }

        public string InstanceId { get; private set; }
        public EffectDefinition Definition { get; private set; }
        public EffectOwner Owner { get; private set; }
        public ITrigger Trigger { get; internal set; }
        public ITarget Target { get; internal set; }
        public IAction Action { get; internal set; }
        /// <summary>背面惰性：非豁免修饰已被临时卸下。</summary>
        public bool IsFaceSuppressed { get; internal set; }

        public IReadOnlyList<ICondition> Conditions
        {
            get { return mConditions; }
        }

        internal List<IUnRegister> Unregisters
        {
            get { return mUnregisters; }
        }

        internal List<AppliedStatModifier> StatModifiers
        {
            get { return mStatModifiers; }
        }

        internal List<RuleModifier> RuleModifiers
        {
            get { return mRuleModifiers; }
        }

        internal void AddCondition(ICondition condition)
        {
            if (condition != null)
            {
                mConditions.Add(condition);
            }
        }
    }

    internal sealed class AppliedStatModifier
    {
        public AppliedStatModifier(CardInstance card, StatModifier modifier)
        {
            Card = card;
            Modifier = modifier;
        }

        public CardInstance Card { get; private set; }
        public StatModifier Modifier { get; private set; }
    }

    public sealed class EffectSystem : AbstractSystem, IEffectSystem
    {
        private readonly Dictionary<string, EffectInstance> mInstances = new Dictionary<string, EffectInstance>();
        private int mNextInstanceId;

        public EffectAtomRegistry AtomRegistry { get; private set; }
        public EffectValidator Validator { get; private set; }

        public IReadOnlyList<EffectInstance> Instances
        {
            get { return new List<EffectInstance>(mInstances.Values); }
        }

        protected override void OnInit()
        {
            AtomRegistry = new EffectAtomRegistry();
            AtomRegistry.DiscoverLoadedAssemblies();
            Validator = new EffectValidator(AtomRegistry);
            Clear();
        }

        public EffectDefinition ParseJson(string json)
        {
            return EffectDefinitionParser.ParseJson(json);
        }

        public EffectValidationResult Validate(EffectDefinition definition)
        {
            return Validator.Validate(definition);
        }

        public EffectInstance Activate(EffectDefinition definition, EffectOwner owner)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }

            owner = owner ?? new EffectOwner(definition.ContainerType, definition.Id, 0);
            var validation = Validate(definition);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException("Invalid effect definition: " + validation.Issues[0]);
            }

            var instance = new EffectInstance(CreateInstanceId(definition), definition, owner);
            BuildConditions(instance);

            if (definition.Kind == EffectKind.Triggered)
            {
                ActivateTriggered(instance);
            }
            else if (definition.Kind == EffectKind.Modifier)
            {
                ActivateModifier(instance);
            }
            else if (definition.Kind == EffectKind.RuleModifier)
            {
                ActivateRuleModifier(instance);
            }

            mInstances.Add(instance.InstanceId, instance);
            var ownerCard = GetOwnerCard(owner);
            if (ownerCard != null)
            {
                ownerCard.AddEffect(definition.Id);
                if (!ownerCard.FaceUp)
                {
                    SyncOwnerFaceSuppression(ownerCard.Uid);
                }
            }

            return instance;
        }

        public bool Deactivate(string instanceId)
        {
            EffectInstance instance;
            if (string.IsNullOrEmpty(instanceId) || !mInstances.TryGetValue(instanceId, out instance))
            {
                return false;
            }

            for (var i = 0; i < instance.Unregisters.Count; i++)
            {
                instance.Unregisters[i].UnRegister();
            }

            var statSystem = this.GetSystem<IStatSystem>();
            for (var i = 0; i < instance.StatModifiers.Count; i++)
            {
                statSystem.RemoveModifier(instance.StatModifiers[i].Card, instance.StatModifiers[i].Modifier);
            }

            for (var i = 0; i < instance.RuleModifiers.Count; i++)
            {
                statSystem.RuleModifiers.Remove(instance.RuleModifiers[i]);
            }

            var ownerCard = GetOwnerCard(instance.Owner);
            if (ownerCard != null)
            {
                ownerCard.RemoveEffect(instance.Definition.Id);
            }

            mInstances.Remove(instanceId);
            return true;
        }

        public IReadOnlyList<string> GetInstanceIdsByOwner(int ownerUid)
        {
            var ids = new List<string>();
            if (ownerUid == 0)
            {
                return ids;
            }

            foreach (var pair in mInstances)
            {
                if (pair.Value.Owner != null && pair.Value.Owner.OwnerUid == ownerUid)
                {
                    ids.Add(pair.Key);
                }
            }

            return ids;
        }

        public int DeactivateByOwner(int ownerUid)
        {
            var ids = GetInstanceIdsByOwner(ownerUid);
            var count = 0;
            for (var i = 0; i < ids.Count; i++)
            {
                if (Deactivate(ids[i]))
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryGetInstance(string instanceId, out EffectInstance instance)
        {
            return mInstances.TryGetValue(instanceId ?? string.Empty, out instance);
        }

        public IReadOnlyList<GameAction> BuildTriggeredActions(string instanceId, TriggerContext triggerContext)
        {
            EffectInstance instance;
            if (!TryGetInstance(instanceId, out instance))
            {
                return new GameAction[0];
            }

            var runtime = new EffectRuntimeContext(((IBelongToArchitecture)this).GetArchitecture(), instance, triggerContext);
            if (!CanTrigger(instance, runtime))
            {
                return new GameAction[0];
            }

            return BuildActionsForMatchedInstance(instance, runtime);
        }

        public EffectNonTriggerProbeResult ProbeWhyNotTriggered(string instanceId, TriggerContext triggerContext)
        {
            EffectInstance instance;
            if (!TryGetInstance(instanceId, out instance))
            {
                return EffectNonTriggerProbeResult.Failed(
                    EffectNonTriggerFailureKind.Other,
                    "instance.missing",
                    "Effect instance '" + (instanceId ?? string.Empty) + "' was not found.");
            }

            var runtime = new EffectRuntimeContext(
                ((IBelongToArchitecture)this).GetArchitecture(),
                instance,
                triggerContext);
            return EffectNonTriggerProbe.Evaluate(instance, runtime);
        }

        internal IReadOnlyList<GameAction> BuildActionsForMatchedInstance(EffectInstance instance, EffectRuntimeContext runtime)
        {
            var targets = instance.Target.Resolve(runtime);
            return instance.Action.BuildActions(runtime, targets);
        }

        public void Clear()
        {
            var ids = new List<string>(mInstances.Keys);
            for (var i = 0; i < ids.Count; i++)
            {
                Deactivate(ids[i]);
            }

            mInstances.Clear();
            mNextInstanceId = 1;
        }

        public void SyncOwnerFaceSuppression(int ownerUid)
        {
            if (ownerUid == 0)
            {
                return;
            }

            CardInstance ownerCard;
            if (!this.GetModel<CardRegistry>().TryGet(ownerUid, out ownerCard) || ownerCard == null)
            {
                return;
            }

            var shouldSuppress = !ownerCard.FaceUp;
            var statSystem = this.GetSystem<IStatSystem>();
            foreach (var pair in mInstances)
            {
                var instance = pair.Value;
                if (instance.Owner == null || instance.Owner.OwnerUid != ownerUid)
                {
                    continue;
                }

                if (EffectRequireTokens.HasActiveWhileFaceDown(instance.Definition))
                {
                    if (instance.IsFaceSuppressed)
                    {
                        RestoreFaceSuppressedModifiers(instance, statSystem);
                        instance.IsFaceSuppressed = false;
                    }

                    continue;
                }

                if (shouldSuppress && !instance.IsFaceSuppressed)
                {
                    SuppressFaceModifiers(instance, statSystem);
                    instance.IsFaceSuppressed = true;
                }
                else if (!shouldSuppress && instance.IsFaceSuppressed)
                {
                    RestoreFaceSuppressedModifiers(instance, statSystem);
                    instance.IsFaceSuppressed = false;
                }
            }
        }

        private static void SuppressFaceModifiers(EffectInstance instance, IStatSystem statSystem)
        {
            if (statSystem == null || instance == null)
            {
                return;
            }

            for (var i = 0; i < instance.StatModifiers.Count; i++)
            {
                var applied = instance.StatModifiers[i];
                if (applied?.Card != null && applied.Modifier != null)
                {
                    statSystem.RemoveModifier(applied.Card, applied.Modifier);
                }
            }

            for (var i = 0; i < instance.RuleModifiers.Count; i++)
            {
                var rule = instance.RuleModifiers[i];
                if (rule != null)
                {
                    statSystem.RuleModifiers.Remove(rule);
                }
            }
        }

        private static void RestoreFaceSuppressedModifiers(EffectInstance instance, IStatSystem statSystem)
        {
            if (statSystem == null || instance == null)
            {
                return;
            }

            for (var i = 0; i < instance.StatModifiers.Count; i++)
            {
                var applied = instance.StatModifiers[i];
                if (applied?.Card != null && applied.Modifier != null)
                {
                    statSystem.AddModifier(applied.Card, applied.Modifier);
                }
            }

            for (var i = 0; i < instance.RuleModifiers.Count; i++)
            {
                var rule = instance.RuleModifiers[i];
                if (rule != null)
                {
                    statSystem.RuleModifiers.Add(rule);
                }
            }
        }

        private void ActivateTriggered(EffectInstance instance)
        {
            instance.Trigger = AtomRegistry.CreateTrigger(instance.Definition.Trigger);
            instance.Target = AtomRegistry.CreateTarget(instance.Definition.Target);
            instance.Action = AtomRegistry.CreateAction(instance.Definition.Action);

            if (instance.Trigger.Point == TriggerPoint.OnActivate)
            {
                EnqueueActivateActions(instance);
                return;
            }

            var reaction = new EffectTriggerReaction(instance.InstanceId, this);
            var unRegister = this.GetSystem<ITriggerSystem>().Register(instance.Trigger.Point, instance.Trigger.Timing, reaction);
            instance.Unregisters.Add(unRegister);
        }

        private void EnqueueActivateActions(EffectInstance instance)
        {
            var runtime = new EffectRuntimeContext(((IBelongToArchitecture)this).GetArchitecture(), instance, null);
            if (!CanTrigger(instance, runtime))
            {
                return;
            }

            var actions = BuildActionsForMatchedInstance(instance, runtime);
            if (actions == null || actions.Count == 0)
            {
                return;
            }

            var pipeline = this.GetSystem<IActionPipelineSystem>();
            for (var i = 0; i < actions.Count; i++)
            {
                pipeline.Enqueue(actions[i]);
            }
        }

        private void ActivateModifier(EffectInstance instance)
        {
            instance.Target = AtomRegistry.CreateTarget(instance.Definition.Target);
            var runtime = new EffectRuntimeContext(((IBelongToArchitecture)this).GetArchitecture(), instance, null);
            var targets = instance.Target.Resolve(runtime);
            var statSystem = this.GetSystem<IStatSystem>();

            for (var i = 0; i < targets.Count; i++)
            {
                CardInstance card;
                if (!runtime.TryGetCard(targets[i], out card))
                {
                    continue;
                }

                var modifier = CreateStatModifier(instance);
                statSystem.AddModifier(card, modifier);
                instance.StatModifiers.Add(new AppliedStatModifier(card, modifier));
            }
        }

        private void ActivateRuleModifier(EffectInstance instance)
        {
            var modifier = CreateRuleModifier(instance);
            this.GetSystem<IStatSystem>().RuleModifiers.Add(modifier);
            instance.RuleModifiers.Add(modifier);
        }

        private void BuildConditions(EffectInstance instance)
        {
            var conditions = instance.Definition.Conditions;
            for (var i = 0; i < conditions.Count; i++)
            {
                instance.AddCondition(AtomRegistry.CreateCondition(conditions[i]));
            }
        }

        private bool CanTrigger(EffectInstance instance, EffectRuntimeContext runtime)
        {
            if (instance.Trigger == null || instance.Action == null)
            {
                return false;
            }

            // 必须在 Trigger.Matches 之前检查：OnCumulative 等在 Matches 内会改写计数器。
            // ADR-0010 / #73：外部门禁已拆除；区域/实体适用由 requires 自陈。
            if (!EffectRequiresRuntime.Passes(instance, runtime))
            {
                return false;
            }

            if (IsBlockedByFaceDown(instance))
            {
                return false;
            }

            if (!instance.Trigger.Matches(runtime))
            {
                return false;
            }

            for (var i = 0; i < instance.Conditions.Count; i++)
            {
                if (!instance.Conditions[i].IsMet(runtime))
                {
                    return false;
                }
            }

            if (!FragmentRecombineDedup.Passes(instance, runtime))
            {
                return false;
            }

            return true;
        }

        private bool IsBlockedByFaceDown(EffectInstance instance)
        {
            if (instance?.Owner == null || instance.Owner.OwnerUid == 0)
            {
                return false;
            }

            if (EffectRequireTokens.HasActiveWhileFaceDown(instance.Definition))
            {
                return false;
            }

            CardInstance owner;
            if (!this.GetModel<CardRegistry>().TryGet(instance.Owner.OwnerUid, out owner) || owner == null)
            {
                return false;
            }

            return !owner.FaceUp;
        }

        private StatModifier CreateStatModifier(EffectInstance instance)
        {
            var node = instance.Definition.Modifier;
            var condition = CreateCompositeStatCondition(instance);
            var value = EffectValueExpression.FromModifierValue(node);
            Func<StatEvaluationContext, float> valueProvider = node.Get("value").IsObject
                ? (Func<StatEvaluationContext, float>)(context => value.Evaluate(context))
                : null;
            return new StatModifier(
                node.Get("stat").AsEnum(StatId.Attack),
                node.Get("op").AsEnum(ModifierOp.Add),
                node.Get("value").AsFloat(0f),
                node.Get("layer").AsEnum(ModifierLayer.Persistent),
                new ModifierSource(node.Get("source").AsString("effect:" + instance.InstanceId)),
                node.Get("scope").AsEnum(ModifierScope.Permanent),
                condition,
                valueProvider);
        }

        private RuleModifier CreateRuleModifier(EffectInstance instance)
        {
            var node = instance.Definition.RuleModifier;
            var condition = CreateCompositeStatCondition(instance, ResolveRuleModifierTargetUid(instance, node));
            return new RuleModifier(
                node.Get("rule").AsEnum(RuleId.RecoveryMultiplier),
                node.Get("op").AsEnum(ModifierOp.Add),
                ResolveRuleModifierValue(instance, node),
                node.Get("layer").AsEnum(ModifierLayer.Persistent),
                new ModifierSource(node.Get("source").AsString("effect:" + instance.InstanceId)),
                node.Get("scope").AsEnum(ModifierScope.Permanent),
                condition);
        }

        private static float ResolveRuleModifierValue(EffectInstance instance, EffectDslNode node)
        {
            var rule = node.Get("rule").AsEnum(RuleId.RecoveryMultiplier);
            if (rule == RuleId.AttackTargetRestriction && instance != null && instance.Owner != null && instance.Owner.OwnerUid != 0)
            {
                return instance.Owner.OwnerUid;
            }

            return node.Get("value").AsFloat(0f);
        }

        private IStatCondition CreateCompositeStatCondition(EffectInstance instance)
        {
            return CreateCompositeStatCondition(instance, 0);
        }

        private IStatCondition CreateCompositeStatCondition(EffectInstance instance, int targetUid)
        {
            if (instance.Conditions.Count == 0 && targetUid == 0)
            {
                return null;
            }

            var buildContext = new EffectBuildContext(((IBelongToArchitecture)this).GetArchitecture(), instance);
            var conditions = new List<IStatCondition>();
            if (targetUid != 0 && !IsAttackTargetRestrictionRule(instance))
            {
                conditions.Add(new TargetUidCondition(targetUid));
            }

            for (var i = 0; i < instance.Conditions.Count; i++)
            {
                var condition = instance.Conditions[i].CreateStatCondition(buildContext);
                if (condition != null)
                {
                    conditions.Add(condition);
                }
            }

            return conditions.Count == 0 ? null : new AllStatCondition(conditions);
        }

        private static bool IsAttackTargetRestrictionRule(EffectInstance instance)
        {
            return instance != null
                && instance.Definition != null
                && instance.Definition.Kind == EffectKind.RuleModifier
                && instance.Definition.RuleModifier != null
                && instance.Definition.RuleModifier.Get("rule").AsEnum(RuleId.RecoveryMultiplier) == RuleId.AttackTargetRestriction;
        }

        private int ResolveRuleModifierTargetUid(EffectInstance instance, EffectDslNode node)
        {
            if (node == null || node.IsNull || !node.Has("target"))
            {
                return 0;
            }

            var target = node.Get("target").AsString(string.Empty);
            if (string.Equals(target, "Self", StringComparison.OrdinalIgnoreCase))
            {
                return instance.Owner == null ? 0 : instance.Owner.OwnerUid;
            }

            if (string.Equals(target, "Player", StringComparison.OrdinalIgnoreCase))
            {
                return this.GetModel<BoardModel>().AvatarUid.Value;
            }

            return 0;
        }

        private CardInstance GetOwnerCard(EffectOwner owner)
        {
            if (owner == null || owner.OwnerUid == 0)
            {
                return null;
            }

            CardInstance card;
            return this.GetModel<CardRegistry>().TryGet(owner.OwnerUid, out card) ? card : null;
        }

        private string CreateInstanceId(EffectDefinition definition)
        {
            return definition.Id + "#" + mNextInstanceId++;
        }

        private sealed class EffectTriggerReaction : ITriggerReaction
        {
            private readonly string mInstanceId;
            private readonly EffectSystem mSystem;

            public EffectTriggerReaction(string instanceId, EffectSystem system)
            {
                mInstanceId = instanceId;
                mSystem = system;
            }

            public string Id
            {
                get { return "effect:" + mInstanceId; }
            }

            public IEnumerable<GameAction> React(TriggerContext context)
            {
                EffectInstance instance;
                if (!mSystem.TryGetInstance(mInstanceId, out instance))
                {
                    return null;
                }

                var runtime = new EffectRuntimeContext(((IBelongToArchitecture)mSystem).GetArchitecture(), instance, context);
                if (!mSystem.CanTrigger(instance, runtime))
                {
                    return null;
                }

                return new[] { new ExecuteEffectAction(mInstanceId, context, mSystem.BuildActionsForMatchedInstance(instance, runtime)) };
            }
        }
    }

    internal sealed class AllStatCondition : IStatCondition
    {
        private readonly IReadOnlyList<IStatCondition> mConditions;

        public AllStatCondition(IReadOnlyList<IStatCondition> conditions)
        {
            mConditions = conditions;
        }

        public bool IsMet(StatEvaluationContext context)
        {
            for (var i = 0; i < mConditions.Count; i++)
            {
                if (!mConditions[i].IsMet(context))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
