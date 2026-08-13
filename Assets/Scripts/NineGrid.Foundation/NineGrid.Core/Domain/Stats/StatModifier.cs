using System;

namespace NineGrid.Core.Stats
{
    public sealed class StatModifier
    {
        public StatModifier(
            StatId stat,
            ModifierOp op,
            float value,
            ModifierLayer layer,
            ModifierSource source,
            ModifierScope scope)
            : this(stat, op, value, layer, source, scope, null)
        {
        }

        public StatModifier(
            StatId stat,
            ModifierOp op,
            float value,
            ModifierLayer layer,
            ModifierSource source,
            ModifierScope scope,
            IStatCondition condition)
            : this(stat, op, value, layer, source, scope, condition, null)
        {
        }

        public StatModifier(
            StatId stat,
            ModifierOp op,
            float value,
            ModifierLayer layer,
            ModifierSource source,
            ModifierScope scope,
            IStatCondition condition,
            Func<StatEvaluationContext, float> valueProvider)
        {
            Stat = stat;
            Op = op;
            Value = value;
            Layer = layer;
            Source = source;
            Scope = scope;
            Condition = condition;
            ValueProvider = valueProvider;
        }

        public StatId Stat { get; private set; }
        public ModifierOp Op { get; private set; }
        public float Value { get; private set; }
        public ModifierLayer Layer { get; private set; }
        public ModifierSource Source { get; private set; }
        public ModifierScope Scope { get; private set; }
        public IStatCondition Condition { get; private set; }
        public Func<StatEvaluationContext, float> ValueProvider { get; private set; }

        /// <summary>
        /// 诊断专用（<see cref="ConditionalModifierAudit"/> 审计缝持有）：上次采样的激活态，
        /// null＝尚未采样。不参与规则结算，随修饰符实例生灭，无跨局残留。
        /// </summary>
        public bool? DiagLastActive { get; set; }

        public bool IsActive(StatEvaluationContext context)
        {
            return Condition == null || Condition.IsMet(context);
        }

        public float EvaluateValue(StatEvaluationContext context)
        {
            return ValueProvider == null ? Value : ValueProvider(context);
        }

        public override string ToString()
        {
            return Stat + " " + Op + " " + Value + " [" + Layer + "] from " + Source;
        }
    }

    public sealed class RuleModifier
    {
        public RuleModifier(
            RuleId rule,
            ModifierOp op,
            float value,
            ModifierLayer layer,
            ModifierSource source,
            ModifierScope scope)
            : this(rule, op, value, layer, source, scope, null)
        {
        }

        public RuleModifier(
            RuleId rule,
            ModifierOp op,
            float value,
            ModifierLayer layer,
            ModifierSource source,
            ModifierScope scope,
            IStatCondition condition)
        {
            Rule = rule;
            Op = op;
            Value = value;
            Layer = layer;
            Source = source;
            Scope = scope;
            Condition = condition;
        }

        public RuleId Rule { get; private set; }
        public ModifierOp Op { get; private set; }
        public float Value { get; private set; }
        public ModifierLayer Layer { get; private set; }
        public ModifierSource Source { get; private set; }
        public ModifierScope Scope { get; private set; }
        public IStatCondition Condition { get; private set; }

        public bool IsActive(StatEvaluationContext context)
        {
            return Condition == null || Condition.IsMet(context);
        }
    }
}
