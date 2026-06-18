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
        EffectDefinition ParseJson(string json);
        EffectValidationResult Validate(EffectDefinition definition);
        EffectInstance Activate(EffectDefinition definition, EffectOwner owner);
        bool Deactivate(string instanceId);
        bool TryGetInstance(string instanceId, out EffectInstance instance);
        IReadOnlyList<GameAction> BuildTriggeredActions(string instanceId, TriggerContext triggerContext);
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

        private void ActivateTriggered(EffectInstance instance)
        {
            instance.Trigger = AtomRegistry.CreateTrigger(instance.Definition.Trigger);
            instance.Target = AtomRegistry.CreateTarget(instance.Definition.Target);
            instance.Action = AtomRegistry.CreateAction(instance.Definition.Action);

            var reaction = new EffectTriggerReaction(instance.InstanceId, this);
            var unRegister = this.GetSystem<ITriggerSystem>().Register(instance.Trigger.Point, instance.Trigger.Timing, reaction);
            instance.Unregisters.Add(unRegister);
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
            if (instance.Trigger == null || instance.Action == null || !instance.Trigger.Matches(runtime))
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

            return true;
        }

        private StatModifier CreateStatModifier(EffectInstance instance)
        {
            var node = instance.Definition.Modifier;
            var condition = CreateCompositeStatCondition(instance);
            return new StatModifier(
                node.Get("stat").AsEnum(StatId.Attack),
                node.Get("op").AsEnum(ModifierOp.Add),
                node.Get("value").AsFloat(0f),
                node.Get("layer").AsEnum(ModifierLayer.Persistent),
                new ModifierSource(node.Get("source").AsString("effect:" + instance.InstanceId)),
                node.Get("scope").AsEnum(ModifierScope.Permanent),
                condition);
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
