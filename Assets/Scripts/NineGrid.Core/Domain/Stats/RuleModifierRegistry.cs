using System.Collections.Generic;

namespace NineGrid.Core.Stats
{
    public sealed class RuleModifierRegistry
    {
        private readonly List<RuleModifier> mModifiers = new List<RuleModifier>();

        private static readonly ModifierLayer[] sLayerOrder =
        {
            ModifierLayer.Persistent,
            ModifierLayer.Conditional,
            ModifierLayer.Temporary
        };

        public IReadOnlyList<RuleModifier> Modifiers
        {
            get { return mModifiers; }
        }

        public void Add(RuleModifier modifier)
        {
            mModifiers.Add(modifier);
        }

        public bool Remove(RuleModifier modifier)
        {
            return mModifiers.Remove(modifier);
        }

        public int RemoveBySource(ModifierSource source)
        {
            return mModifiers.RemoveAll(modifier => modifier.Source == source);
        }

        public int ClearByScope(ModifierScope scope)
        {
            return mModifiers.RemoveAll(modifier => modifier.Scope == scope);
        }

        public float Evaluate(RuleId rule, float baseValue, StatEvaluationContext context)
        {
            var value = baseValue;
            for (var layerIndex = 0; layerIndex < sLayerOrder.Length; layerIndex++)
            {
                var layer = sLayerOrder[layerIndex];
                for (var i = 0; i < mModifiers.Count; i++)
                {
                    var modifier = mModifiers[i];
                    if (modifier.Rule != rule || modifier.Layer != layer || !modifier.IsActive(context))
                    {
                        continue;
                    }

                    value = StatPipeline.ApplyOp(value, modifier.Op, modifier.Value);
                }
            }

            return value;
        }

        public void Clear()
        {
            mModifiers.Clear();
        }
    }
}
