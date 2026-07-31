using System;
using NineGrid.Core.Stats;
using QFramework;

namespace NineGrid.Core.Systems
{
    public interface IStatSystem : ISystem
    {
        RuleModifierRegistry RuleModifiers { get; }
        StatEvaluationContext CreateContext(CardInstance owner);
        float GetEffectiveValue(CardInstance card, StatId stat);
        float GetEffectiveValue(CardInstance card, StatId stat, StatEvaluationContext context);
        int GetEffectiveInt(CardInstance card, StatId stat);
        void AddModifier(CardInstance card, StatModifier modifier);
        bool RemoveModifier(CardInstance card, StatModifier modifier);
        int RemoveModifiersBySource(CardInstance card, ModifierSource source);
        int ClearModifiersByScope(CardInstance card, ModifierScope scope);
        float EvaluateRule(RuleId rule, float baseValue);
        float EvaluateRule(RuleId rule, float baseValue, StatEvaluationContext context);
    }

    public sealed class StatSystem : AbstractSystem, IStatSystem
    {
        private readonly StatPipeline mPipeline = new StatPipeline();

        public RuleModifierRegistry RuleModifiers { get; private set; }

        protected override void OnInit()
        {
            RuleModifiers = new RuleModifierRegistry();
        }

        public StatEvaluationContext CreateContext(CardInstance owner)
        {
            return new StatEvaluationContext(
                owner,
                this.GetModel<CardRegistry>(),
                this.GetModel<BoardModel>(),
                this.GetModel<PlayerModel>(),
                RuleModifiers);
        }

        public float GetEffectiveValue(CardInstance card, StatId stat)
        {
            return GetEffectiveValue(card, stat, CreateContext(card));
        }

        public float GetEffectiveValue(CardInstance card, StatId stat, StatEvaluationContext context)
        {
            if (card == null)
            {
                throw new ArgumentNullException("card");
            }

            return mPipeline.Evaluate(card.Stats, stat, context);
        }

        public int GetEffectiveInt(CardInstance card, StatId stat)
        {
            return (int)Math.Round(GetEffectiveValue(card, stat));
        }

        public void AddModifier(CardInstance card, StatModifier modifier)
        {
            if (card == null)
            {
                throw new ArgumentNullException("card");
            }

            if (modifier == null)
            {
                throw new ArgumentNullException("modifier");
            }

            card.Stats.AddModifier(modifier);
        }

        public bool RemoveModifier(CardInstance card, StatModifier modifier)
        {
            return card != null && modifier != null && card.Stats.RemoveModifier(modifier);
        }

        public int RemoveModifiersBySource(CardInstance card, ModifierSource source)
        {
            return card == null ? 0 : card.Stats.RemoveModifiersBySource(source);
        }

        public int ClearModifiersByScope(CardInstance card, ModifierScope scope)
        {
            return card == null ? 0 : card.Stats.ClearModifiersByScope(scope);
        }

        public float EvaluateRule(RuleId rule, float baseValue)
        {
            return EvaluateRule(rule, baseValue, CreateContext(null));
        }

        public float EvaluateRule(RuleId rule, float baseValue, StatEvaluationContext context)
        {
            return RuleModifiers.Evaluate(rule, baseValue, context);
        }
    }
}
